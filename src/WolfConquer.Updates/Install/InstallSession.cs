using System.Security.Cryptography;
using WolfConquer.Updates.Manifest;
using WolfConquer.Updates.Progress;

namespace WolfConquer.Updates.Install;

internal sealed class InstallSession : IDisposable
{
    internal InstallFileSystem Fs { get; }
    internal UpdateInstallOptions Options { get; }
    internal IUpdateProcessGuard Guard { get; }
    internal IReadOnlyList<string> Executables { get; }
    internal UpdateProgressReporter Progress { get; }
    internal string? InitialStateHash { get; private set; }
    private FileStream? _lease;

    internal InstallSession(UpdateInstallOptions options, IUpdateProcessGuard guard, UpdateProgressReporter? progress = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaxFiles is < 1 or > InstallMetadata.HardMaxFiles ||
            options.MaxFileBytes is < 0 or > (64L << 30) || options.MaxTotalBytes is < 0 or > (1L << 40) ||
            options.DownloadTimeout <= TimeSpan.Zero || options.DownloadTimeout > TimeSpan.FromHours(24))
            throw new ArgumentOutOfRangeException(nameof(options), "Invalid bounded installer limits.");
        Fs = new InstallFileSystem(options.InstallRoot);
        if (options.ProtectedExecutablePaths is null || options.ProtectedExecutablePaths.Count == 0)
            throw new InvalidDataException("Protected executable paths are required.");
        Executables = Array.AsReadOnly(options.ProtectedExecutablePaths.Select(Fs.Target).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        Options = options;
        Guard = guard;
        Progress = progress ?? new UpdateProgressReporter(options.Progress);
    }

    internal Task Stopped(CancellationToken cancellation) => Guard.EnsureStoppedAsync(Executables, cancellation);
    internal async Task Acquire(CancellationToken cancellation)
    {
        await Stopped(cancellation).ConfigureAwait(false);
        Fs.DirectoryFor(Fs.Control);
        var leasePath = Path.Combine(Fs.Control, "install.lock");
        Fs.Check(leasePath);
        _lease = new FileStream(leasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Fs.Check(leasePath);
    }

    internal InstalledState? State()
    {
        var bytes = Fs.Read(Fs.StatePath, InstallMetadata.MaximumBytes);
        InitialStateHash = bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes));
        if (bytes is null) return null;
        var state = InstallMetadata.Parse<InstalledState>(bytes);
        InstallMetadata.Validate(state);
        return state;
    }

    internal List<JournalFile> Plan(VerifiedUpdateManifest verified, InstalledState? state)
    {
        var manifest = verified.Manifest;
        if (manifest.ExpiresUtc <= DateTimeOffset.UtcNow) throw new InvalidDataException("The verified release has expired before installation.");
        if (manifest.Files.Count > Options.MaxFiles) throw new InvalidDataException("Update exceeds the configured file bound.");
        if (state is not null && (state.ProductId != manifest.ProductId || state.Channel != manifest.Channel ||
            state.Sequence > manifest.Sequence || (state.Sequence == manifest.Sequence &&
            !InstallFileSystem.Same(state.PayloadSha256, verified.PayloadSha256))))
            throw new InvalidDataException("Installed state rejects this product, channel, sequence or conflicting release.");
        var fresh = state is null && Options.AllowFreshInstall;
        if (fresh) CheckFreshRoot(); // Establish empty/updater-only ownership before relaxing any missing-base precondition.
        var previous = state?.Files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        var changes = new List<JournalFile>();
        var existing = 0;
        var checkedFiles = 0;
        Progress.Report(UpdateOperationPhase.Preflight, "Checking installed files against the signed release.", 0, manifest.Files.Count, UpdateProgressUnit.Files);
        foreach (var entry in manifest.Files)
        {
            var target = Fs.Target(entry.Path);
            if (!names.Add(entry.Path) || entry.Length < 0 || entry.Length > Options.MaxFileBytes ||
                !InstallFileSystem.IsHash(entry.Sha256) || (entry.BaseSha256 is not null && !InstallFileSystem.IsHash(entry.BaseSha256)))
                throw new InvalidDataException("Invalid update file or case collision.");
            total = checked(total + entry.Length);
            if (total > Options.MaxTotalBytes) throw new InvalidDataException("Update exceeds the configured total bound.");
            var current = Fs.Hash(target);
            if (current is not null) existing++;
            if (InstallFileSystem.Same(current, entry.Sha256))
            {
                if (new FileInfo(target).Length != entry.Length) throw new InvalidDataException("Target length conflicts with signed hash metadata.");
                Progress.Report(UpdateOperationPhase.Preflight, "Checking installed files against the signed release.",
                    ++checkedFiles, manifest.Files.Count, UpdateProgressUnit.Files, entry.Path);
                continue;
            }
            var recognized = previous is not null && previous.TryGetValue(entry.Path, out var old) &&
                InstallFileSystem.Same(current, old.Sha256) && new FileInfo(target).Length == old.Length;
            if (!recognized && !(fresh && current is null) && !InstallFileSystem.Same(current, entry.BaseSha256))
                throw new InvalidDataException("Existing custom or modified file does not match an authorized base: " + entry.Path);
            changes.Add(new(entry.Path, entry.Length, entry.Sha256, current,
                current is null ? 0 : (int)File.GetAttributes(target),
                current is null ? 0 : File.GetLastWriteTimeUtc(target).Ticks));
            Progress.Report(UpdateOperationPhase.Preflight, "Checking installed files against the signed release.",
                ++checkedFiles, manifest.Files.Count, UpdateProgressUnit.Files, entry.Path);
        }
        foreach (var name in names)
        {
            for (var slash = name.LastIndexOf('/'); slash >= 0; slash = name.LastIndexOf('/', slash - 1))
                if (names.Contains(name[..slash])) throw new InvalidDataException("A payload file is also a payload directory.");
        }
        if (state is null && !fresh && existing == 0) throw new InvalidDataException("An empty install requires AllowFreshInstall.");
        return changes;
    }

    private void CheckFreshRoot()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".wolfconquer-updates", "wolfconquer-updates.json", "trusted-release-keys.json",
            "WolfConquer.Updater.exe", "WolfConquer.Updater.dll", "WolfConquer.Updater.deps.json", "WolfConquer.Updater.runtimeconfig.json"
        };
        foreach (var path in Directory.EnumerateFileSystemEntries(Fs.Root))
        {
            Fs.Check(path);
            if (!allowed.Contains(Path.GetFileName(path)) ||
                (Directory.Exists(path) && !Path.GetFileName(path).Equals(".wolfconquer-updates", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Fresh install root contains non-updater files.");
        }
    }

    public void Dispose() => _lease?.Dispose();
}
