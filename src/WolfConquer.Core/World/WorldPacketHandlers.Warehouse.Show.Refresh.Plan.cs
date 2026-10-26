using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyWarehouseLockRefreshDelivery(LegacyWarehouseShowOwner Owner,
        byte[][] Frames, int Count, bool Committed);

    // Only displayed ROOTS receive native SendItemLocked. Deposited children and
    // other warehouses stay unchanged; no catalog/Add/Send item side effects occur.
    private static bool TryProjectLegacyWarehouseShowLocks(LegacyWarehouseShowOwner owner,
        LegacyWarehousePacket request, DateTime now, out Dictionary<uint, PlayerItemSnapshotItem> unlocked,
        out string reason)
    {
        unlocked = [];
        reason = "warehouseLockRefreshSelectionInvalid";
        if (!LegacyWarehousePacket.IsShowAction(request.Action) || !CanShowLegacyWarehouse(owner, request)) return false;
        try
        {
            foreach (var row in owner.Items.CopyWarehouseRowsForPresentation(request.NpcId))
            {
                if (!LegacyFloorPickupItemLock.TryProject(row, now, out var final, out _, out reason)) return false;
                if (!ReferenceEquals(final, row)) unlocked.Add(row.Uid, final);
            }
            if (!owner.Items.IsCurrent(owner.Session.PlayerItems)) return false;
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        { reason = "warehouseLockRefreshPreparation:" + error.GetType().Name; return false; }
    }

    private static bool TryBuildLegacyWarehouseLockRefresh(LegacyWarehouseShowOwner owner, LegacyWarehousePacket request,
        DateTime now, Dictionary<uint, PlayerItemSnapshotItem> unlocked, out WorldSession updated,
        out LegacyItemSaveGraph after, out byte[][] frames, out int count)
    {
        updated = owner.Session;
        after = null!;
        frames = [];
        count = 0;
        // The original locked rows are the native Warehouse prefix. Subsequent
        // prepared Update-mode ItemCreate frames expose the already-durable unlock.
        if (!TryPrepareLegacyWarehouseShow(owner, request, out frames, out count, now, unlocked)) return false;
        updated = owner.Session with
        {
            PlayerItems = owner.Session.PlayerItems with
            {
                WarehouseItems = owner.Session.PlayerItems.WarehouseItems.Select(row =>
                    row.WarehouseId == request.NpcId && unlocked.TryGetValue(row.Uid, out var final) ? final : row).ToArray(),
            },
        };
        return LegacyItemSaveGraph.TryCapture(updated.PlayerItems, out after) &&
            owner.Items.IsCurrent(owner.Session.PlayerItems);
    }
}
