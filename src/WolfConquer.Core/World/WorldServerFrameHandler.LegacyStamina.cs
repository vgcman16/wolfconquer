using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private readonly ConditionalWeakTable<ConquerTcpConnection, LegacyOwnedStaminaRuntime.Readiness> staminaReady = new();

    private void RegisterLegacyStaminaReadiness(ConquerTcpConnection connection, WorldSession expected)
    {
        if (_ownedAdmissionRuntime is null || expected.NativeGameClient?.OrdinarySpellBinding is null) return;
        lock (_tradeStates.SyncRoot)
        {
            if (staminaReady.TryGetValue(connection, out _)) return; // Never reset a live player's stamps.
            if (_sessions.TryPrepareStaminaReadiness(this, connection, expected,
                    _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System, out var prepared) && prepared is not null)
                staminaReady.Add(connection, prepared);
        }
    }

    internal bool IsStaminaReadinessCurrent(LegacyOwnedStaminaRuntime.Readiness readiness) =>
        ReferenceEquals(readiness.Frame, this) && ReferenceEquals(readiness.Store, _sessions) &&
        ReferenceEquals(readiness.Runtime, _ownedAdmissionRuntime) && !_tradeRecovery.IsBlocked &&
        !readiness.Runtime.IsStopping && ReferenceEquals(readiness.Clock, _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System) &&
        ReferenceEquals(readiness.LifecycleGate, _tradeStates.SyncRoot) &&
        !_tradeStates.HasConnectionEnded(readiness.Connection) &&
        _creationLifetimes.TryGetValue(readiness.Connection, out var lifetime) && lifetime.IsCurrent(readiness.Connection) &&
        staminaReady.TryGetValue(readiness.Connection, out var current) && ReferenceEquals(current, readiness);

    internal LegacyOwnedAdmissionRuntime? CurrentStaminaRuntime(WorldSessionStore store) =>
        ReferenceEquals(store, _sessions) && _protocolProfile == ConquerProtocolProfile.VisibleDx8D13 &&
        _ownedAdmissionRuntime is { IsStopping: false } ? _ownedAdmissionRuntime : null;

    // Actual runtime/test entry: no caller-supplied UID, timestamp, gain, stamp,
    // registration or successful result. Disabled scheduling changes no owner.
    internal async ValueTask SendDueLegacyStaminaUpdatesAsync(CancellationToken cancellationToken)
    {
        if (_ownedAdmissionRuntime is not { IsStopping: false } runtime) return;
        using var worker = runtime.EnterWorker();
        using var operation = await _worldOperations.EnterSharedAsync(cancellationToken).ConfigureAwait(false);
        if (_tradeRecovery.IsBlocked) return;
        foreach (var active in _sessions.Snapshot())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SendDueLegacyStaminaUpdateAsync(active.Connection, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask SendDueLegacyStaminaUpdateAsync(ConquerTcpConnection connection, CancellationToken cancellationToken)
    {
        if (!staminaReady.TryGetValue(connection, out var readiness) || !IsStaminaReadinessCurrent(readiness) ||
            !_sessions.TryGet(connection, out var expected) || expected is null) return;
        var context = CreateMonsterRuntimeContext(new(connection, expected));
        if (!_sessions.TryCommitOwnedStaminaTick(readiness, context, expected, out var publication)) return;
        if (publication is not null) await publication.PublishAsync(cancellationToken).ConfigureAwait(false);
    }
}
