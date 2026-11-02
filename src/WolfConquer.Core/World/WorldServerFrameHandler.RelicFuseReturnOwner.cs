namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Binds the return producer to the actual Frame dispatch and its one
    // world/trade owner; a caller-selected lock grants no authority.
    internal void RequireRelicFuseReturnLifecycle(WorldPacketHandlerContext context)
    {
        if (!ReferenceEquals(context.Sessions, _sessions) || !ReferenceEquals(context.Trades, _tradeStates) ||
            !ReferenceEquals(context.TradeOperations, _worldOperations) ||
            !_worldOperations.OwnsActiveExclusiveLease(context.FrameOperationLease) ||
            !Monitor.IsEntered(_tradeStates.SyncRoot) || !ReferenceEquals(context.TradeRecovery, _tradeRecovery))
            throw new InvalidOperationException("relicFuseReturnRequiresActualFrameTradeOwner");
    }
}
