using System.Diagnostics;
using System.Security.Cryptography;
using WolfConquer.Updates.Discovery;
using WolfConquer.Updates.Install;
using WolfConquer.Updates.Manifest;
using WolfConquer.Updates.Progress;

namespace WolfConquer.Updater;

// Shared typed orchestration for the CLI and GUI. Never launches another updater/CLI process.
public static class LauncherWorkflow
{
    public static ClientSettingsSummary ReadClientSettings(string configAbsolutePath)
    {
        var (settings, root, _) = ClientConfiguration.Load(configAbsolutePath);
        return new(Path.GetFullPath(configAbsolutePath), settings.ProductId, settings.Channel, root,
            settings.LaunchRelativePath, settings.AllowLoopbackHttp, settings.LaunchWorkingDirectoryRelativePath);
    }

    public static Task<ClientWorkflowResult> RunClientAsync(string configAbsolutePath, bool fresh = false,
        bool repair = false, bool launch = false, IProgress<UpdateOperationProgress>? progress = null,
        CancellationToken cancellationToken = default, string? installRootOverride = null) =>
        Execute(configAbsolutePath, fresh, repair, launch, recoverOnly: false, progress, cancellationToken, installRootOverride);

    public static Task<ClientWorkflowResult> RecoverClientAsync(string configAbsolutePath,
        IProgress<UpdateOperationProgress>? progress = null, CancellationToken cancellationToken = default,
        string? installRootOverride = null) =>
        Execute(configAbsolutePath, fresh: false, repair: false, launch: false, recoverOnly: true,
            progress, cancellationToken, installRootOverride);

    private static async Task<ClientWorkflowResult> Execute(string configAbsolutePath, bool fresh, bool repair,
        bool launch, bool recoverOnly, IProgress<UpdateOperationProgress>? progress,
        CancellationToken cancellation, string? installRootOverride)
    {
        cancellation.ThrowIfCancellationRequested();
        Report(progress, UpdateOperationPhase.Checking, "Validating local updater configuration");
        var (settings, root, cache) = ClientConfiguration.Load(configAbsolutePath, installRootOverride);
        if (!Directory.Exists(root))
        {
            if (!fresh) throw new DirectoryNotFoundException("Install directory is absent; use --install for a fresh destination.");
            Directory.CreateDirectory(root);
            LocalFiles.Check(root);
        }
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false, Credentials = null,
            AutomaticDecompression = System.Net.DecompressionMethods.None
        };
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var installer = new TransactionalUpdateInstaller(http);
        var options = new UpdateInstallOptions(root, settings.ProtectedExecutables) { AllowFreshInstall = fresh, Progress = progress };
        Report(progress, UpdateOperationPhase.Checking, "Checking for unfinished installation recovery");
        int recovered = await installer.RecoverAsync(options, cancellation).ConfigureAwait(false);
        if (recoverOnly)
        {
            Report(progress, UpdateOperationPhase.Completed, "Installation recovery completed", 1, 1);
            return new("recovered", settings.ProductId, settings.Channel, null, recovered, false, null, null);
        }
        var installed = await installer.GetInstalledReleaseAsync(options, cancellation).ConfigureAwait(false);
        if (installed is not null && (installed.ProductId != settings.ProductId || installed.Channel != settings.Channel))
            throw new InvalidDataException("Install directory belongs to another product or channel.");
        var trust = new UpdateTrust(settings.ProductId, settings.Channel, settings.KeyId, settings.PublicKeySpkiPem, settings.AllowLoopbackHttp);
        var discovery = new UpdateDiscoveryClient(http);
        var minimum = installed?.Sequence ?? 1;
        Report(progress, UpdateOperationPhase.Checking, "Checking signed release discovery");
        var index = await discovery.FetchIndexAsync(new Uri(settings.FeedUrl, UriKind.Absolute), trust, minimum, cancellation).ConfigureAwait(false);
        var identity = index.Manifest.Files[0];
        bool unchanged = installed is not null && installed.Sequence == index.Manifest.Sequence &&
            installed.EnvelopeLength == identity.Length && installed.EnvelopeSha256.Equals(identity.Sha256, StringComparison.OrdinalIgnoreCase);
        if (unchanged && !repair && !launch)
        {
            Report(progress, UpdateOperationPhase.Current, "Selected release unchanged (metadata-only; not a payload integrity check)", 1, 1);
            return new("unchanged", settings.ProductId, settings.Channel, installed!.Sequence, recovered, true, null, null);
        }
        var cacheFile = Path.Combine(cache, identity.Sha256.ToLowerInvariant() + ".json");
        LocalFiles.Check(cacheFile);
        byte[]? cached = File.Exists(cacheFile) ? LocalFiles.Bytes(cacheFile, SignedUpdateManifest.MaximumEnvelopeBytes) : null;
        Report(progress, UpdateOperationPhase.Verifying, "Verifying the exact signed release manifest");
        var found = await discovery.FetchReleaseAsync(index, trust, minimum, cached, cancellation).ConfigureAwait(false);
        bool launchCandidateAvailable = found.Release.FullManifest.Manifest.Files.Any(file =>
            string.Equals(file.Path, settings.LaunchRelativePath, StringComparison.Ordinal));
        UpdateInstallResult? installation = null;
        if (!unchanged || repair)
            installation = await installer.InstallAsync(found.Release, options, cancellation).ConfigureAwait(false);
        if (launch) ClientConfiguration.ResolveLaunchWorkingDirectory(settings, root, requireExists: true);
        LocalFiles.AtomicWrite(cacheFile, found.FullEnvelope);
        int? processId = null;
        if (launch)
        {
            Report(progress, UpdateOperationPhase.Checking, "Verifying configured launch executable");
            processId = await Launch(settings, root, found.Release, cancellation).ConfigureAwait(false);
        }
        Report(progress, UpdateOperationPhase.Completed, processId is null ? "Client update checks completed" :
            "Configured client started; login remains manual", 1, 1);
        return new(processId is not null ? "launched" : unchanged ? "verified" : "updated", settings.ProductId,
            settings.Channel, found.Release.FullManifest.Manifest.Sequence, recovered, false, installation, processId,
            launchCandidateAvailable);
    }

    private static async Task<int> Launch(ClientConfiguration settings, string root, VerifiedUpdateRelease release,
        CancellationToken cancellation)
    {
        var entry = release.FullManifest.Manifest.Files.SingleOrDefault(file => file.Path == settings.LaunchRelativePath)
            ?? throw new InvalidDataException("Configured executable is absent from the signed release.");
        var target = Path.GetFullPath(Path.Combine(root, settings.LaunchRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        LocalFiles.Check(target);
        await new ConfiguredExecutableProcessGuard().EnsureStoppedAsync(settings.ProtectedExecutables
            .Select(path => Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)))).ToArray(), cancellation).ConfigureAwait(false);
        using var stream = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length != entry.Length || !Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellation).ConfigureAwait(false))
            .Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Launch executable differs from the signed release; no process started.");
        cancellation.ThrowIfCancellationRequested();
        string workingDirectory = ClientConfiguration.ResolveLaunchWorkingDirectory(settings, root, requireExists: true);
        LocalFiles.Check(target);
        // Avoid a separate console-host lifetime for configured console helpers.
        // CREATE_NO_WINDOW is ignored for GUI executables; their normal game window is not hidden.
        var start = new ProcessStartInfo(target)
        { WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in settings.LaunchArguments) start.ArgumentList.Add(argument);
        using var child = Process.Start(start) ?? throw new IOException("Configured client could not start.");
        return child.Id;
    }

    private static void Report(IProgress<UpdateOperationProgress>? progress, UpdateOperationPhase phase,
        string message, long? completed = null, long? total = null)
    {
        // Optional UI/telemetry observers are not transaction authority and cannot interrupt recovery.
        try { progress?.Report(new UpdateOperationProgress(phase, message, completed, total)); }
        catch (Exception) { }
    }
}
