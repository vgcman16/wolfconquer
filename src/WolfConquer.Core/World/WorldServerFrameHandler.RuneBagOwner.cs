namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal LegacyOwnedAdmissionRuntime RequireRuneBagRuntime(WorldSessionStore store, WorldPacketHandlerContext context)
    {
        if (!ReferenceEquals(store, _sessions) || !ReferenceEquals(context.Sessions, _sessions) ||
            _ownedAdmissionRuntime is not { } runtime || !ReferenceEquals(runtime.Recovery, _tradeRecovery) ||
            !ReferenceEquals(context.TradeRecovery, _tradeRecovery))
            throw new InvalidOperationException("Direct RuneBag requires its actual owned Runtime/Frame.");
        var actual = RequireOwnedNativeItemContext();
        if (!ReferenceEquals(actual.Catalog, context.WorldData) || actual.Profile != context.ProtocolProfile)
            throw new InvalidOperationException("Direct RuneBag catalog/profile differs from its actual Frame.");
        return runtime;
    }
}
