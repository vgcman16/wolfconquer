namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal LegacyOwnedAdmissionRuntime RequireRuneDecomposeRuntime(WorldSessionStore store, WorldPacketHandlerContext context)
    {
        if (!ReferenceEquals(store, _sessions) || !ReferenceEquals(context.Sessions, _sessions) ||
            _ownedAdmissionRuntime is not { } runtime || !ReferenceEquals(runtime.Recovery, _tradeRecovery) ||
            !ReferenceEquals(context.TradeRecovery, _tradeRecovery))
            throw new InvalidOperationException("Rune decomposition requires its actual owned Runtime/Frame.");
        var actual = RequireOwnedNativeItemContext();
        if (!ReferenceEquals(actual.Catalog, context.WorldData) || actual.Profile != context.ProtocolProfile)
            throw new InvalidOperationException("Rune decomposition catalog/profile differs from its actual Frame.");
        return runtime;
    }
}
