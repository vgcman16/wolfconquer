using System.Security.Cryptography;

namespace WolfConquer.Core.Client;

public sealed record ClientFileDriftOptions
{
    public bool ExcludeLogAndDebugFiles { get; init; }

    public IReadOnlyCollection<string>? IncludeRelativePaths { get; init; }

    public IReadOnlyCollection<string>? RequiredPatchedRelativePaths { get; init; }

    public Func<string, bool>? ExcludeRelativePath { get; init; }
}

public sealed record ClientFileDriftFile(string RelativePath, long Length, string Sha256);

public sealed record ClientFileDriftChangedFile(
    string RelativePath,
    ClientFileDriftFile OriginalFile,
    ClientFileDriftFile PatchedFile);

public sealed record ClientFileDriftMissingRequiredFile(string RelativePath, string PatchedPath);

public sealed record ClientFileDriftReport(
    string OriginalClientRoot,
    string PatchedClientRoot,
    IReadOnlyList<ClientFileDriftChangedFile> ChangedFiles,
    IReadOnlyList<ClientFileDriftFile> MissingFiles,
    IReadOnlyList<ClientFileDriftFile> ExtraFiles)
{
    public IReadOnlyList<ClientFileDriftMissingRequiredFile> MissingRequiredPatchedFiles { get; init; } = [];

    public bool HasDrift =>
        ChangedFiles.Count != 0 ||
        MissingFiles.Count != 0 ||
        ExtraFiles.Count != 0 ||
        MissingRequiredPatchedFiles.Count != 0;
}

public static class ClientFileDriftReporter
{
    private static readonly string[] LogAndDebugDirectoryNames =
    [
        "debug",
        "log",
        "logs",
    ];

    private static readonly string[] LogAndDebugExtensions =
    [
        ".dmp",
        ".dump",
        ".log",
        ".tmp",
        ".trace",
    ];

    public static ClientFileDriftReport Compare(
        string originalClientRoot,
        string patchedClientRoot,
        ClientFileDriftOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalClientRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(patchedClientRoot);

        options ??= new ClientFileDriftOptions();
        var fullOriginalClientRoot = RequireDirectory(originalClientRoot);
        var fullPatchedClientRoot = RequireDirectory(patchedClientRoot);
        var originalFiles = SnapshotFiles(fullOriginalClientRoot, options);
        var patchedFiles = SnapshotFiles(fullPatchedClientRoot, options);
        var changedFiles = new List<ClientFileDriftChangedFile>();
        var missingFiles = new List<ClientFileDriftFile>();
        var extraFiles = new List<ClientFileDriftFile>();
        var missingRequiredPatchedFiles = FindMissingRequiredPatchedFiles(fullPatchedClientRoot, options);

        foreach (var (relativePath, originalFile) in originalFiles)
        {
            if (!patchedFiles.TryGetValue(relativePath, out var patchedFile))
            {
                missingFiles.Add(originalFile);
                continue;
            }

            if (originalFile.Length != patchedFile.Length || originalFile.Sha256 != patchedFile.Sha256)
            {
                changedFiles.Add(new ClientFileDriftChangedFile(relativePath, originalFile, patchedFile));
            }
        }

        foreach (var (relativePath, patchedFile) in patchedFiles)
        {
            if (!originalFiles.ContainsKey(relativePath))
            {
                extraFiles.Add(patchedFile);
            }
        }

        return new ClientFileDriftReport(
            fullOriginalClientRoot,
            fullPatchedClientRoot,
            changedFiles,
            missingFiles,
            extraFiles)
        {
            MissingRequiredPatchedFiles = missingRequiredPatchedFiles,
        };
    }

    public static bool IsLogOrDebugFile(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        var extension = Path.GetExtension(normalizedRelativePath);
        if (LogAndDebugExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var segment in normalizedRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (LogAndDebugDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string RequireDirectory(string clientRoot)
    {
        var fullClientRoot = Path.GetFullPath(clientRoot);
        if (!Directory.Exists(fullClientRoot))
        {
            throw new DirectoryNotFoundException($"Client root does not exist: {fullClientRoot}");
        }

        return fullClientRoot;
    }

    private static SortedDictionary<string, ClientFileDriftFile> SnapshotFiles(
        string clientRoot,
        ClientFileDriftOptions options)
    {
        var files = new SortedDictionary<string, ClientFileDriftFile>(StringComparer.OrdinalIgnoreCase);
        if (options.IncludeRelativePaths is { Count: > 0 } includeRelativePaths)
        {
            foreach (var rawRelativePath in includeRelativePaths)
            {
                if (string.IsNullOrWhiteSpace(rawRelativePath))
                {
                    continue;
                }

                var relativePath = NormalizeRelativePath(rawRelativePath);
                if (ShouldExclude(relativePath, options))
                {
                    continue;
                }

                var filePath = ResolveClientFilePath(clientRoot, relativePath);
                if (File.Exists(filePath))
                {
                    files[relativePath] = CreateFileSnapshot(relativePath, filePath);
                }
            }

            return files;
        }

        var enumerationOptions = new EnumerationOptions
        {
            AttributesToSkip = 0,
            IgnoreInaccessible = false,
            RecurseSubdirectories = true,
        };

        foreach (var filePath in Directory.EnumerateFiles(clientRoot, "*", enumerationOptions))
        {
            var relativePath = NormalizeRelativePath(Path.GetRelativePath(clientRoot, filePath));
            if (ShouldExclude(relativePath, options))
            {
                continue;
            }

            files.Add(relativePath, CreateFileSnapshot(relativePath, filePath));
        }

        return files;
    }

    private static string ResolveClientFilePath(string clientRoot, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException($"Client relative path must not be rooted: {relativePath}");
        }

        var filePath = Path.GetFullPath(Path.Combine(
            clientRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = clientRoot.EndsWith(Path.DirectorySeparatorChar)
            ? clientRoot
            : clientRoot + Path.DirectorySeparatorChar;
        if (!filePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Client relative path escapes the client root: {relativePath}");
        }

        return filePath;
    }

    private static bool ShouldExclude(string relativePath, ClientFileDriftOptions options)
    {
        if (options.ExcludeLogAndDebugFiles && IsLogOrDebugFile(relativePath))
        {
            return true;
        }

        return options.ExcludeRelativePath?.Invoke(relativePath) == true;
    }

    private static IReadOnlyList<ClientFileDriftMissingRequiredFile> FindMissingRequiredPatchedFiles(
        string patchedClientRoot,
        ClientFileDriftOptions options)
    {
        if (options.RequiredPatchedRelativePaths is not { Count: > 0 } requiredRelativePaths)
        {
            return [];
        }

        var missingFiles = new SortedDictionary<string, ClientFileDriftMissingRequiredFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawRelativePath in requiredRelativePaths)
        {
            if (string.IsNullOrWhiteSpace(rawRelativePath))
            {
                continue;
            }

            var relativePath = NormalizeRelativePath(rawRelativePath);
            if (ShouldExclude(relativePath, options))
            {
                continue;
            }

            var patchedPath = ResolveClientFilePath(patchedClientRoot, relativePath);
            if (!File.Exists(patchedPath))
            {
                missingFiles[relativePath] = new ClientFileDriftMissingRequiredFile(relativePath, patchedPath);
            }
        }

        return missingFiles.Values.ToArray();
    }

    private static ClientFileDriftFile CreateFileSnapshot(string relativePath, string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        using var stream = File.OpenRead(filePath);
        return new ClientFileDriftFile(
            relativePath,
            fileInfo.Length,
            Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant());
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
