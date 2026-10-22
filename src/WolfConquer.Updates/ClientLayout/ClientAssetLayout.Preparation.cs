using WolfConquer.Updates.Install;
using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.ClientLayout;

public static partial class ClientAssetLayout
{
    public static async Task PrepareAsync(VerifiedUpdateManifest verified, UpdateInstallOptions options, string preset,
        IUpdateProcessGuard? guard = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateManifest(verified, preset);
        cancellation.ThrowIfCancellationRequested();
        RequireUnexpired(verified);
        string root = Preflight(options.InstallRoot, requireDirectories: true);
        using var session = new InstallSession(options, guard ?? new ConfiguredExecutableProcessGuard());
        await session.Acquire(cancellation).ConfigureAwait(false);
        InstallRecovery.AssertNoPending(session, cancellation);
        RequireCommittedRelease(session, verified);
        var created = new List<string>();
        try
        {
            foreach (var name in Directories)
            {
                var alias = Path.Combine(root, EngineDirectory, name);
                var target = Path.Combine(root, name);
                if (ClientDirectoryJunction.IsExact(alias, target)) continue;
                await session.Stopped(cancellation).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                Preflight(root, requireDirectories: true);
                RequireStateUnchanged(session);
                RequireUnexpired(verified);
                // The lease coordinates updater instances. CreateNew still refuses
                // another writer's newly appeared leaf rather than adopting it.
                ClientDirectoryJunction.CreateNew(alias, target);
                created.Add(name);
                if (!ClientDirectoryJunction.IsExact(alias, target))
                    throw new IOException("A newly created DX8 alias disappeared before validation.");
            }
            await session.Stopped(cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            Preflight(root, requireDirectories: true);
            RequireStateUnchanged(session);
            RequireUnexpired(verified);
        }
        catch (Exception failure)
        {
            var rollbackErrors = new List<Exception>();
            for (int index = created.Count - 1; index >= 0; index--)
            {
                try
                {
                    // Cancellation cannot abandon ownership cleanup. Never remove
                    // pre-existing links, payload directories, or a changed identity.
                    await session.Stopped(CancellationToken.None).ConfigureAwait(false);
                    RequireStateUnchanged(session);
                    string name = created[index];
                    CheckOwnedPaths(root, name);
                    ClientDirectoryJunction.DeleteExact(Path.Combine(root, EngineDirectory, name), Path.Combine(root, name));
                }
                catch (Exception rollbackFailure) { rollbackErrors.Add(rollbackFailure); }
            }
            if (rollbackErrors.Count != 0)
                throw new IOException("DX8 layout failed; some current-call aliases or ordinary remnants may remain. Inspect before retrying.",
                    new AggregateException(new[] { failure }.Concat(rollbackErrors)));
            throw;
        }
    }

    private static void RequireUnexpired(VerifiedUpdateManifest verified)
    {
        if (verified.Manifest.ExpiresUtc <= DateTimeOffset.UtcNow)
            throw new InvalidDataException("The signed release expired before DX8 layout preparation completed.");
    }
}
