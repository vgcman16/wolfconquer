using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Issued only by the real active global iteration. A run owns no mutation;
    // every capture/commit must mint its own tick bound to an active Shared lease.
    private sealed class RootExpiryRun { }
    private sealed record RootExpiryRunData(WorldServerFrameHandler Owner, TimeProvider Provider,
        DateTimeOffset LoopNow, CancellationToken Cancellation);
    private static readonly ConditionalWeakTable<RootExpiryRun, RootExpiryRunData> RootExpiryRuns = new();

    private bool RootExpiryRunIsCurrent(RootExpiryRun run, out RootExpiryRunData data)
    {
        data = null!;
        if (!RootExpiryRuns.TryGetValue(run, out var value) || !ReferenceEquals(value.Owner, this) ||
            !_runtimeUpdateLoop.Enabled || Volatile.Read(ref _legacyRuntimeGlobalLoopStarted) != 1 ||
            value.Cancellation.IsCancellationRequested || _tradeRecovery.IsBlocked ||
            !ReferenceEquals(value.Provider, _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System)) return false;
        data = value; return true;
    }

    private RootExpiryTick? IssueRootExpiryTick(RootExpiryRun run, IDisposable operation)
    {
        if (!RootExpiryRunIsCurrent(run, out var data) || !_worldOperations.OwnsActiveLease(operation)) return null;
        var tick = new RootExpiryTick();
        RootExpiryTicks.Add(tick, new(this, data.Provider, data.LoopNow, operation, data.Cancellation));
        return tick;
    }

    // Only the actual global loop associates a tick with its active operation.
    private sealed class RootExpiryTick { }
    private sealed record RootExpiryTickData(WorldServerFrameHandler Owner, TimeProvider Provider,
        DateTimeOffset LoopNow, IDisposable Operation, CancellationToken Cancellation);
    private static readonly ConditionalWeakTable<RootExpiryTick, RootExpiryTickData> RootExpiryTicks = new();

    // Each private global-loop issuance is for one ordered captured entry and
    // one exact Store holder. A constructed token has no association.
    internal sealed class RootExpiryAuthorization { }
    private sealed record RootExpiryAuthorizationData(WorldServerFrameHandler Owner, RootExpiryTick Tick,
        WorldSessionStore Store, ConquerTcpConnection Connection, object Identity, WorldSession Expected,
        LegacyNativeAttributeMapProof CapturedMap, LegacyRootItemEffectFlagClock.ExpiryObservation Observation,
        DateTimeOffset SubjectNow, int Index);
    private static readonly ConditionalWeakTable<RootExpiryAuthorization, RootExpiryAuthorizationData> RootExpiryAuthorizations = new();

    private bool RootExpiryTickIsCurrent(RootExpiryTick tick, out RootExpiryTickData data)
    {
        data = null!;
        if (!RootExpiryTicks.TryGetValue(tick, out var value) || !ReferenceEquals(value.Owner, this) ||
            !_runtimeUpdateLoop.Enabled || Volatile.Read(ref _legacyRuntimeGlobalLoopStarted) != 1 ||
            value.Cancellation.IsCancellationRequested || _tradeRecovery.IsBlocked ||
            !ReferenceEquals(value.Provider, _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System) ||
            !_worldOperations.OwnsActiveLease(value.Operation)) return false;
        data = value; return true;
    }

    internal static bool TryReadRootExpiryAuthorization(RootExpiryAuthorization authorization,
        WorldSessionStore store, ConquerTcpConnection connection, object identity, WorldSession expected,
        out LegacyNativeAttributeMapProof map, out LegacyRootItemEffectFlagClock.ExpiryObservation observation)
    {
        map = null!; observation = null!;
        if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
            !RootExpiryAuthorizations.TryGetValue(authorization, out var data) ||
            !ReferenceEquals(data.Store, store) || !ReferenceEquals(data.Owner._sessions, store) ||
            !ReferenceEquals(data.Connection, connection) || !ReferenceEquals(data.Identity, identity) ||
            !ReferenceEquals(data.Expected, expected) || data.Observation.Flag is not 8 and not 9 ||
            data.Index is < 0 or > 1 || !data.Owner.RootExpiryTickIsCurrent(data.Tick, out var tick) ||
            data.SubjectNow < tick.LoopNow || !data.CapturedMap.IsCurrentPlain()) return false;
        // A projected visible entry must be the actual due object from the
        // configured provider's real subject clock reading. Missing-flag
        // cleanup and genuine clockless deadlines were classified by capture.
        if (data.Observation.Projected && data.Observation.Visible &&
            (data.Observation.Entry is not { } entry || entry.ExpiresAtLocal >
                LegacyArrowBladesFlagRegistration.LocalTime(tick.Provider, data.SubjectNow))) return false;
        map = data.CapturedMap; observation = data.Observation; return true;
    }

    internal static bool ConsumeRootExpiryAuthorization(RootExpiryAuthorization authorization,
        WorldSessionStore store, ConquerTcpConnection connection, object identity, WorldSession expected) =>
        TryReadRootExpiryAuthorization(authorization, store, connection, identity, expected, out _, out _) &&
        RootExpiryAuthorizations.Remove(authorization);
}
