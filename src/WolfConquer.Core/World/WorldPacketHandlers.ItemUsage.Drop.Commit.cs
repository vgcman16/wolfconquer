namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryCommitLegacyItemDrop(WorldPacketHandlerContext context, WorldSession current,
        LegacyItemDropPlan plan, FloorItemRuntimeStateStore.GroundReservation? reservation,
        out bool durable, out string reason)
    {
        // Retain the pre-existing wholly-unbound, no-file fixture behavior. A
        // physical Player or any configured file root never gets this fallback.
        var inMemoryCompatibility = context.Connection.NativeGameClient is null && current.NativeGameClient is null &&
            string.IsNullOrWhiteSpace(current.UsersRoot) && string.IsNullOrWhiteSpace(context.PlayerItemsRoot);
        durable = !inMemoryCompatibility;
        reason = string.Empty;
        var failure = string.Empty;
        bool PersistAndPublishGround()
        {
            if (!LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, current))
            { failure = "secondaryPasswordOwnerChanged"; return false; }
            if (!inMemoryCompatibility && !LegacyNpcSalePersistence.TryPersistItemRemoval(
                current, plan.Updated, context.PlayerItemsRoot, out failure)) return false;
            // Only the exact local reservation can publish; no socket or ordinary
            // session-save callback is invoked inside the persisted transaction.
            if (reservation is not null && !reservation.TryPublish(DateTimeOffset.UtcNow))
            { failure = LegacyTradeJournal.RecoveryRequired + ":dropFloorPublicationLost"; return false; }
            return true;
        }
        bool committed;
        if (context.Sessions is { } sessions)
            committed = sessions.TryCommitSession(context.Connection, current, plan.Updated, PersistAndPublishGround);
        else if (context.Connection.NativeGameClient is null && current.NativeGameClient is null)
            committed = PersistAndPublishGround(); // Original standalone managed fixture compatibility.
        else
        { reason = "sessionStoreUnavailable"; return false; }
        if (!committed)
        {
            if (failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal))
                context.TradeRecovery?.Block(failure);
            reason = "commitRejected:" + (failure.Length == 0 ? "sessionChanged" : failure);
            return false;
        }
        if (!LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, plan.Updated))
        {
            context.TradeRecovery?.Block("dropRecoveryRequired:ownerChangedAfterCommit");
            reason = "ownerChangedAfterCommit";
            return false;
        }
        reason = "committed";
        return true;
    }
}
