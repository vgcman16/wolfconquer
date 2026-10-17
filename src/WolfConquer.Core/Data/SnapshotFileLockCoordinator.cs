using System.Collections.Concurrent;

namespace WolfConquer.Core.Data;

internal static class SnapshotFileLockCoordinator
{
    private static readonly StringComparer FilePathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static readonly ConcurrentDictionary<string, object> FileLocks =
        new(FilePathComparer);

    internal static TResult ExecuteLocked<TResult>(string path, Func<TResult> action)
    {
        return ExecuteLocked([path], action);
    }

    internal static TResult ExecuteLocked<TResult>(
        IReadOnlyCollection<string> paths,
        Func<TResult> action)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(action);
        if (paths.Count == 0)
        {
            throw new ArgumentException("At least one path is required.", nameof(paths));
        }

        var canonicalPaths = new HashSet<string>(FilePathComparer);
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(
                    "Paths cannot contain null, empty, or whitespace values.",
                    nameof(paths));
            }

            canonicalPaths.Add(Path.GetFullPath(path));
        }

        var orderedLocks = canonicalPaths
            .OrderBy(static path => path, FilePathComparer)
            .Select(GetCanonicalFileLock)
            .ToArray();
        var acquiredLockCount = 0;

        try
        {
            foreach (var fileLock in orderedLocks)
            {
                Monitor.Enter(fileLock);
                acquiredLockCount++;
            }

            return action();
        }
        finally
        {
            while (acquiredLockCount > 0)
            {
                Monitor.Exit(orderedLocks[--acquiredLockCount]);
            }
        }
    }

    private static object GetCanonicalFileLock(string canonicalPath)
    {
        return FileLocks.GetOrAdd(canonicalPath, static _ => new object());
    }
}
