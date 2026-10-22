using WolfConquer.Updates.Manifest;
using WolfConquer.Updates.Discovery;
using WolfConquer.Updates.Progress;

namespace WolfConquer.Updates.Install;

// Supplied HttpClient must disable redirects, cookies and ambient credentials. Responses
// are additionally checked for an unchanged effective URI before any bytes are staged.
public sealed class TransactionalUpdateInstaller(HttpClient client, IUpdateProcessGuard? processGuard = null)
{
    private readonly HttpClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly IUpdateProcessGuard _guard = processGuard ?? new ConfiguredExecutableProcessGuard();

    public Task<UpdateInstallResult> InstallAsync(VerifiedUpdateManifest verified, UpdateInstallOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verified);
        return InstallCore(verified, verified.Manifest.ContentBaseUrl, null, options, cancellationToken);
    }

    public Task<UpdateInstallResult> InstallAsync(VerifiedUpdateRelease release, UpdateInstallOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        return InstallCore(release.FullManifest, release.ContentBaseUrl, release.IndexManifest, options, cancellationToken);
    }

    private async Task<UpdateInstallResult> InstallCore(VerifiedUpdateManifest verified, Uri contentBase,
        VerifiedUpdateManifest? index, UpdateInstallOptions options, CancellationToken cancellationToken)
    {
        var progress = new UpdateProgressReporter(options?.Progress);
        try { return await InstallTracked(verified, contentBase, index, options!, cancellationToken, progress).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            progress.Report(UpdateOperationPhase.Skipped, "Installation was canceled; inspect the result before retrying.");
            throw;
        }
        catch (Exception)
        {
            progress.Report(UpdateOperationPhase.Failed, "Installation did not complete; inspect the result before retrying.");
            throw;
        }
    }

    private async Task<UpdateInstallResult> InstallTracked(VerifiedUpdateManifest verified, Uri contentBase,
        VerifiedUpdateManifest? index, UpdateInstallOptions options, CancellationToken cancellationToken, UpdateProgressReporter progress)
    {
        progress.Report(UpdateOperationPhase.Checking, "Checking release and installation prerequisites.");
        CheckExpiry(verified, index);
        using var session = new InstallSession(options, _guard, progress);
        await session.Acquire(cancellationToken).ConfigureAwait(false);
        await InstallRecovery.Recover(session, cancellationToken).ConfigureAwait(false);
        var state = session.State();
        var changes = session.Plan(verified, state);
        var manifest = verified.Manifest;
        if (changes.Count == 0 && state is not null && state.Sequence == manifest.Sequence)
        {
            progress.Report(UpdateOperationPhase.Current, "Installed files already match the signed release.",
                manifest.Files.Count, manifest.Files.Count, UpdateProgressUnit.Files);
            return new(manifest.ProductId, manifest.Channel, manifest.Sequence, 0, manifest.Files.Count, null);
        }
        await session.Stopped(cancellationToken).ConfigureAwait(false);
        var transaction = Path.Combine(session.Fs.Control, "transactions", Guid.NewGuid().ToString("N"));
        session.Fs.DirectoryFor(Path.Combine(transaction, "staged"));
        session.Fs.DirectoryFor(Path.Combine(transaction, "backups"));
        await InstallDownloads.Stage(_client, session, verified, changes, transaction, cancellationToken, contentBase).ConfigureAwait(false);
        CheckExpiry(verified, index);
        await session.Stopped(cancellationToken).ConfigureAwait(false);
        var journal = InstallPreparation.Prepare(session, verified, changes, transaction);
        try
        {
            await InstallApplication.Apply(session, transaction, journal, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception installError)
        {
            try { await InstallRecovery.Rollback(session, transaction, journal, CancellationToken.None).ConfigureAwait(false); }
            catch (Exception rollbackError) { throw new UpdateRollbackException(transaction, installError, rollbackError); }
            throw;
        }
        progress.Report(UpdateOperationPhase.Completed, "Installation committed successfully.", changes.Count, changes.Count, UpdateProgressUnit.Files);
        return new(manifest.ProductId, manifest.Channel, manifest.Sequence, changes.Count,
            manifest.Files.Count - changes.Count, transaction);
    }

    private static void CheckExpiry(VerifiedUpdateManifest full, VerifiedUpdateManifest? index)
    {
        if (full.Manifest.ExpiresUtc <= DateTimeOffset.UtcNow || (index is not null && index.Manifest.ExpiresUtc <= DateTimeOffset.UtcNow))
            throw new InvalidDataException("The full release or discovery index has expired; no new payload will be applied.");
    }

    public async Task<int> RecoverAsync(UpdateInstallOptions options, CancellationToken cancellationToken = default)
    {
        using var session = new InstallSession(options, _guard);
        await session.Acquire(cancellationToken).ConfigureAwait(false);
        return await InstallRecovery.Recover(session, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads only committed cached release identity, not the payload's current integrity.</summary>
    public async Task<InstalledReleaseMetadata?> GetInstalledReleaseAsync(UpdateInstallOptions options,
        CancellationToken cancellationToken = default)
    {
        using var session = new InstallSession(options, _guard);
        await session.Acquire(cancellationToken).ConfigureAwait(false);
        InstallRecovery.AssertNoPending(session, cancellationToken);
        var state = session.State();
        return state is null ? null : new(state.ProductId, state.Channel, state.Sequence, state.EnvelopeSha256, state.EnvelopeLength);
    }
}
