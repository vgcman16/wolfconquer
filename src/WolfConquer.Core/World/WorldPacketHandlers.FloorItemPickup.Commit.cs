namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryCommitLegacyFloorPickup(WorldPacketHandlerContext context, WorldSession current,
        LegacyFloorPickupPlan plan, FloorItemRuntimeStateStore.PickupReservation reservation,
        out bool durable, out string reason)
    {
        var inMemoryCompatibility = context.Connection.NativeGameClient is null && current.NativeGameClient is null &&
            string.IsNullOrWhiteSpace(current.UsersRoot) && string.IsNullOrWhiteSpace(context.PlayerItemsRoot);
        durable = !inMemoryCompatibility;
        reason = string.Empty;
        var failure = string.Empty;
        bool PersistAndConsume()
        {
            if (context.TradeRecovery?.IsBlocked == true || context.Trades?.HasConnectionEnded(context.Connection) == true ||
                !IsCurrentLegacyFloorPickupOwner(context, current))
            { failure = "pickupOwnerChangedOrRecoveryRequired"; return false; }
            if (!inMemoryCompatibility && !(plan.InventoryChange is { } inventory
                ? LegacyNpcSalePersistence.TryPersistFloorPickupInventory(current, plan.Updated,
                    context.PlayerItemsRoot, plan.Entry, inventory, out failure)
                : LegacyNpcSalePersistence.TryPersistFloorPickup(current, plan.Updated,
                    context.PlayerItemsRoot, plan.Entry, plan.Item, out failure))) return false;
            if (!reservation.TryConsume(out var consumed) || !ReferenceEquals(consumed, plan.Entry))
            { failure = LegacyTradeJournal.RecoveryRequired + ":pickupFloorReservationLost"; return false; }
            return true;
        }
        bool committed;
        if (context.Sessions is { } sessions)
            committed = sessions.TryCommitSession(context.Connection, current, plan.Updated, PersistAndConsume);
        else if (inMemoryCompatibility) committed = PersistAndConsume();
        else { reason = "sessionStoreUnavailable"; return false; }
        if (!committed)
        {
            if (failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal))
                context.TradeRecovery?.Block(failure);
            reason = "commitRejected:" + (failure.Length == 0 ? "sessionChanged" : failure);
            return false;
        }
        if (!IsCurrentLegacyFloorPickupOwner(context, plan.Updated))
        {
            context.TradeRecovery?.Block("pickupRecoveryRequired:ownerChangedAfterCommit");
            reason = "ownerChangedAfterCommit";
            return false;
        }
        reason = "committed";
        return true;
    }

    private static bool IsCurrentLegacyFloorPickupOwner(WorldPacketHandlerContext context, WorldSession current) =>
        context.Connection.NativeGameClient is null && current.NativeGameClient is null ||
        LegacyServerIdentityRuntime.IsOnOwnServer(context.Connection, current, context.LocalServerIdentity);
}
