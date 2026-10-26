namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Caller owns actual Exclusive then Trades.SyncRoot. Existing persistence
    // retains its Store -> ordered file locks and exact one-item-file outcome proof.
    private static LegacyWarehouseLockRefreshDelivery? TryCommitLegacyWarehouseShowLocks(WorldPacketHandlerContext context,
        LegacyWarehousePacket request, CancellationToken cancellationToken, out string reason)
    {
        reason = "warehouseLockRefreshOwnerChanged";
        if (!TryCaptureLegacyWarehouseShowOwner(context, out var owner) || !CanShowLegacyWarehouse(owner, request) ||
            context.TradeRecovery?.IsBlocked == true) return null;
        var now = DateTime.Now;
        if (!LegacyServerIdentityRuntime.IsOnOwnServer(context.Connection, owner.Session, context.LocalServerIdentity))
            return TryPrepareLegacyWarehouseShow(owner, request, out var readOnlyFrames, out var readOnlyCount, now)
                ? new(owner, readOnlyFrames, readOnlyCount, false) : null;
        if (!TryProjectLegacyWarehouseShowLocks(owner, request, now, out var unlocked, out reason)) return null;
        if (unlocked.Count == 0)
            return TryPrepareLegacyWarehouseShow(owner, request, out var frames, out var count, now)
                ? new(owner, frames, count, false) : null;

        var recovery = context.TradeRecovery!;
        if (!recovery.IsReadyFor(owner.Session.UsersRoot, context.PlayerItemsRoot) ||
            !TryCaptureLegacyWarehouseItemTransferOwner(context, out var transferOwner) ||
            !ReferenceEquals(transferOwner.Session, owner.Session) ||
            !TryReadLegacyWarehouseShowCapacity(owner, request.NpcId, out var capacity) ||
            !TryBuildLegacyWarehouseLockRefresh(owner, request, now, unlocked, out var updated, out var after,
                out var prepared, out var selectedCount))
        { reason = "warehouseLockRefreshRequiredStateOrPlanInvalid"; return null; }

        var failure = "warehouseLockRefreshOwnerChanged";
        if (!context.Sessions!.TryCommitSession(context.Connection, owner.Session, updated, () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!recovery.IsReadyFor(owner.Session.UsersRoot, context.PlayerItemsRoot) ||
                !IsCurrentLegacyWarehouseShowOwner(context, owner) ||
                !IsCurrentLegacyWarehouseItemTransferOwner(context, transferOwner, owner.Session) ||
                !LegacyWarehouseItemTransferAttributesCurrent(transferOwner) ||
                !HasCoherentLegacyWarehouseTransferTrade(context, owner.Session, owner.Session.Character.Attributes) ||
                !LegacyServerIdentityRuntime.IsOnOwnServer(context.Connection, owner.Session, context.LocalServerIdentity) ||
                !after.IsCurrent(updated.PlayerItems)) return false;
            var saved = LegacyWarehouseItemTransferPersistence.TryPersist(owner.Session, updated, context.PlayerItemsRoot!,
                owner.Items, after, request.NpcId == (uint)owner.Session.Character.Uid ? (ulong?)capacity : null, out failure);
            if (!saved && failure.StartsWith(LegacyWarehouseItemTransferPersistence.RecoveryRequired, StringComparison.Ordinal))
                recovery.Block(failure);
            return saved;
        }))
        { reason = failure; return null; }

        reason = string.Empty;
        return new(owner with { Session = updated, Items = after }, prepared, selectedCount, true);
    }

    private static bool CanPublishLegacyWarehouseLockRefresh(WorldPacketHandlerContext context, LegacyWarehouseShowOwner owner)
    {
        if (context.Trades is not { } trades || context.TradeRecovery is not { } recovery) return false;
        lock (trades.SyncRoot)
            return recovery.IsReadyFor(owner.Session.UsersRoot, context.PlayerItemsRoot) &&
                IsCurrentLegacyWarehouseShowOwner(context, owner) &&
                LegacyServerIdentityRuntime.IsOnOwnServer(context.Connection, owner.Session, context.LocalServerIdentity) &&
                HasCoherentLegacyWarehouseTransferTrade(context, owner.Session, owner.Session.Character.Attributes);
    }
}
