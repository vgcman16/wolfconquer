using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private readonly LegacyOwnedAdmissionRuntime? _ownedAdmissionRuntime;

    private void StartOwnedAdmissionRuntimeWorkers(ConquerTcpConnection connection, CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems, PlayerSpellSnapshot playerSpells, CancellationToken cancellationToken)
    {
        var ownedLifetime = LinkOwnedAdmissionWorker(cancellationToken);
        TrackOwnedAdmissionWorker(SendLegacyPlayerRuntimeUpdatesAsync(connection, snapshot, playerItems, playerSpells,
            ownedLifetime?.Token ?? cancellationToken), ownedLifetime);
        TryStartLegacyGlobalRuntimeUpdateLoop(cancellationToken);
    }

    private CancellationTokenSource? LinkOwnedAdmissionWorker(CancellationToken connectionLifetime) =>
        _ownedAdmissionRuntime is { } runtime
            ? CancellationTokenSource.CreateLinkedTokenSource(connectionLifetime, runtime.ShutdownToken) : null;

    private void TrackOwnedAdmissionWorker(Task actual, CancellationTokenSource? lifetime)
    {
        TrackLegacyRuntimeWorker(actual);
        if (lifetime is not null)
            _ = actual.ContinueWith(static (_, state) => ((CancellationTokenSource)state!).Dispose(), lifetime,
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void QueueOwnedAdmissionDisconnectCleanup(WorldPacketHandlerContext context)
    {
        if (_ownedAdmissionRuntime is not { } owner)
        { _ = Task.Run(() => CancelTradeAfterDisconnectAsync(context)); return; }
        lock (owner.LifetimeGate)
        {
            // Shutdown blocks ordinary writes; late cancellation retains real
            // escrow for recovery rather than scheduling an untracked writer.
            if (owner.IsStopping) return;
            TrackLegacyRuntimeWorker(Task.Run(async () =>
            {
                using var actor = owner.EnterWorker();
                await CancelTradeAfterDisconnectAsync(context).ConfigureAwait(false);
            }));
        }
    }

    internal bool TryEnterOwnedAdmissionShutdown(LegacyOwnedAdmissionRuntime owner, out IDisposable? exclusive)
    {
        exclusive = null;
        return ReferenceEquals(_ownedAdmissionRuntime, owner) &&
            CaptureLegacyRuntimeWorkerTasks().All(task => task.IsCompleted) &&
            _worldOperations.TryEnterIdleExclusive(out exclusive);
    }

    internal async ValueTask<IDisposable> EnterOwnedAdmissionShutdownAsync(LegacyOwnedAdmissionRuntime owner)
    {
        if (!ReferenceEquals(_ownedAdmissionRuntime, owner)) throw new InvalidOperationException("Shutdown owner changed.");
        // Owner cancellation stops both actual worker producers. No timeout
        // releases a lease while an unfinished worker may still write.
        await Task.WhenAll(CaptureLegacyRuntimeWorkerTasks()).ConfigureAwait(false);
        return await _worldOperations.EnterExclusiveAsync().ConfigureAwait(false);
    }
}
