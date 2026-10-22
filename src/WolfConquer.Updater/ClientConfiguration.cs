using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updater;

internal sealed record ClientConfiguration(int SchemaVersion, string ProductId, string Channel, string FeedUrl,
    string KeyId, string PublicKeySpkiPem, string InstallDirectory, string LaunchRelativePath,
    string[] LaunchArguments, string[] ProtectedExecutables, bool AllowLoopbackHttp = false,
    string? LaunchWorkingDirectoryRelativePath = null)
{
    internal static (ClientConfiguration Settings, string Root, string Cache) Load(string path, string? installRootOverride = null)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal))
            throw new InvalidDataException("Use an absolute local configuration path.");
        if (installRootOverride is not null && (!Path.IsPathFullyQualified(installRootOverride) ||
            installRootOverride.StartsWith("\\\\", StringComparison.Ordinal)))
            throw new InvalidDataException("An install folder override must be an explicit local absolute path.");
        var settings = LocalFiles.Read<ClientConfiguration>(path, 65536);
        if (settings.SchemaVersion != 1 || string.IsNullOrWhiteSpace(settings.ProductId) ||
            string.IsNullOrWhiteSpace(settings.Channel) || string.IsNullOrWhiteSpace(settings.KeyId) ||
            string.IsNullOrWhiteSpace(settings.PublicKeySpkiPem) || string.IsNullOrWhiteSpace(settings.InstallDirectory) ||
            settings.ProtectedExecutables is null || settings.ProtectedExecutables.Length is < 1 or > 16 ||
            settings.LaunchArguments is null || settings.LaunchArguments.Length > 32 ||
            settings.LaunchArguments.Any(value => value is null || value.Length > 4096))
            throw new InvalidDataException("Incomplete or unsupported local updater configuration.");
        UpdatePathPolicy.ValidateRelativePath(settings.LaunchRelativePath);
        if (settings.LaunchWorkingDirectoryRelativePath is not null)
            UpdatePathPolicy.ValidateRelativePath(settings.LaunchWorkingDirectoryRelativePath);
        foreach (var executable in settings.ProtectedExecutables) UpdatePathPolicy.ValidateRelativePath(executable);
        if (!settings.ProtectedExecutables.Contains(settings.LaunchRelativePath, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Launch executable must be in the protected process list.");
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installRootOverride ?? settings.InstallDirectory, directory));
        if (Path.GetFullPath(path).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(root, directory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Payload installation must be separate from bootstrap trust/configuration.");
        var cache = Path.Combine(directory, ".wolfconquer-cache");
        if (cache.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Payload cannot contain bootstrap cache.");
        LocalFiles.Check(root); LocalFiles.Check(cache);
        ResolveLaunchWorkingDirectory(settings, root, requireExists: false);
        return (settings, root, cache);
    }

    // Local bootstrap configuration alone selects the working directory. A signed
    // remote manifest never supplies launch arguments or a working-directory override.
    internal static string ResolveLaunchWorkingDirectory(ClientConfiguration settings, string root, bool requireExists)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string target = root;
        if (settings.LaunchWorkingDirectoryRelativePath is { } relative)
        {
            UpdatePathPolicy.ValidateRelativePath(relative);
            target = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Launch working directory must stay inside the payload root.");
        }
        LocalFiles.Check(target);
        if (File.Exists(target)) throw new InvalidDataException("Launch working directory must be an ordinary directory.");
        if (requireExists && !Directory.Exists(target))
            throw new DirectoryNotFoundException("Configured launch working directory does not exist; no process started.");
        return target;
    }
}
