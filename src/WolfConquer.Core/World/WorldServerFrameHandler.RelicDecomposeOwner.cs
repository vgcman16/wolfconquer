using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal LegacyOwnedAdmissionRuntime RequireRelicDecomposeRuntime(WorldSessionStore store, WorldPacketHandlerContext context)
    {
        if (!ReferenceEquals(store, _sessions) || !ReferenceEquals(context.Sessions, _sessions) ||
            _ownedAdmissionRuntime is not { } runtime || !ReferenceEquals(runtime.Recovery, _tradeRecovery) ||
            !ReferenceEquals(context.TradeRecovery, _tradeRecovery) ||
            !ReferenceEquals(context.Trades, _tradeStates) || !ReferenceEquals(context.TradeOperations, _worldOperations) ||
            !_worldOperations.OwnsActiveExclusiveLease(context.FrameOperationLease) ||
            !Monitor.IsEntered(_tradeStates.SyncRoot))
            throw new InvalidOperationException("Relic decomposition requires its actual owned Runtime/Frame.");
        var actual = RequireOwnedNativeItemContext();
        if (!ReferenceEquals(actual.Catalog, context.WorldData) || actual.Profile != context.ProtocolProfile ||
            actual.Profile != ConquerProtocolProfile.VisibleDx8D13 ||
            context.RuneRankings is not null && !ReferenceEquals(context.RuneRankings, _runeRankings))
            throw new InvalidOperationException("Relic decomposition catalog/profile differs from its actual Frame.");
        return runtime;
    }
}
