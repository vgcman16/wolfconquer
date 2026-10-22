using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace WolfConquer.Updates.Hosting;

internal sealed class FeedFileStore
{
    private readonly string _root;
    private readonly HashSet<string> _bootstrap;

    internal FeedFileStore(FeedHostOptions options)
    {
        try
        {
            if (!Path.IsPathFullyQualified(options.PublishRoot) || options.PublishRoot.StartsWith("\\\\", StringComparison.Ordinal) ||
                options.PublishRoot.StartsWith("//", StringComparison.Ordinal) || IsVolumeRoot(options.PublishRoot)) throw new IOException();
            _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.PublishRoot));
            if (IsVolumeRoot(_root) || !Directory.Exists(_root)) throw new IOException();
            NoReparse(_root);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            throw new ArgumentException("Publish root must be an ordinary, readable directory.", nameof(options));
        }
        _bootstrap = new HashSet<string>(options.BootstrapFiles ?? [], StringComparer.Ordinal);
        if (_bootstrap.Any(name => !SafeBootstrapName(name)))
            throw new ArgumentException("Bootstrap allowlist contains an invalid basename.", nameof(options));
    }

    internal FeedFile? Open(string target)
    {
        // Work on the raw request target once; never URL-decode or normalize it.
        if (target.Length > 512 || target.IndexOfAny(['%', '\\', '?', '#', '\0']) >= 0)
            return null;
        var segments = target.Split('/');
        if (segments.Length != 3 || segments[0] != "") return null;
        var name = segments[2];
        var blob = segments[1] == "blobs" && Regex.IsMatch(name, "\\A[0-9a-f]{64}\\z");
        var feed = segments[1] == "feeds" && Regex.IsMatch(name, "\\A[A-Za-z0-9._-]{1,257}\\.json\\z");
        var bootstrap = segments[1] == "bootstrap" && _bootstrap.Contains(name) && SafeBootstrapName(name);
        if (!blob && !feed && !bootstrap) return null;
        var path = Path.Combine(_root, segments[1], name);
        FileStream? stream = null;
        try
        {
            NoReparse(path);
            if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.Device)) != 0) return null;
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            NoReparse(path);
            if (OperatingSystem.IsWindows() && !SameWindowsHandlePath(stream.SafeFileHandle, path)) throw new IOException();
            return new FeedFile(stream, blob ? name : null, feed, bootstrap && name.EndsWith(".zip", StringComparison.Ordinal));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            stream?.Dispose();
            return null;
        }
    }

    private static bool IsVolumeRoot(string path) => string.Equals(
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
        (Path.GetPathRoot(path) ?? "").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool SafeBootstrapName(string name) =>
        name is not null && !name.Contains("..", StringComparison.Ordinal) &&
        Regex.IsMatch(name, "\\A[A-Za-z0-9][A-Za-z0-9._-]{0,95}\\.(?:zip|exe)\\z") &&
        !Regex.IsMatch(name, "\\A(?:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])\\.", RegexOptions.IgnoreCase);

    private static void NoReparse(string path)
    {
        var root = Path.GetPathRoot(path) ?? throw new IOException();
        var current = root;
        if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException();
        foreach (var segment in Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar))
        {
            if (segment == ".") continue;
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException();
        }
    }

    private static bool SameWindowsHandlePath(SafeFileHandle handle, string expected)
    {
        var buffer = new StringBuilder(32768);
        var count = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        if (count == 0 || count >= buffer.Capacity) return false;
        var actual = buffer.ToString();
        if (actual.StartsWith("\\\\?\\UNC\\", StringComparison.OrdinalIgnoreCase)) actual = "\\\\" + actual[8..];
        else if (actual.StartsWith("\\\\?\\", StringComparison.Ordinal)) actual = actual[4..];
        return string.Equals(Path.GetFullPath(actual), Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
}

internal sealed record FeedFile(FileStream Stream, string? Hash, bool IsFeed, bool IsZip);
