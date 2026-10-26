using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyWarehouseItemTransferPlan(LegacyWarehouseItemTransferOwner Owner, WorldSession Updated,
        LegacyItemSaveGraph Before, LegacyItemSaveGraph After, byte[][] Frames, ulong? InventorySashCount);

    private static bool TryPrepareLegacyWarehouseItemTransfer(LegacyWarehouseItemTransferOwner owner, LegacyWarehousePacket request,
        out LegacyWarehouseItemTransferPlan plan, out string reason)
    {
        plan = null!;
        reason = "warehouseItemTransferItemGraphOrSelectionInvalid";
        var session = owner.Session;
        if (!LegacyItemSaveGraph.TryCapture(session.PlayerItems, out var before)) return false;
        try
        {
            WorldSession updated;
            byte[][] frames;
            ulong? inventorySashCount = null;
            var prepared = LegacyWarehouseItemTransferActions.IsPhysicalSash(request.Action)
                ? TryPrepareLegacyPhysicalSashMove(session, before, request, out updated, out frames)
                : TryPrepareLegacyWarehouseMove(owner, before, request, out updated, out frames, out inventorySashCount);
            if (!prepared || !LegacyItemSaveGraph.TryCapture(updated.PlayerItems, out var after) ||
                !before.IsCurrent(session.PlayerItems)) return false;
            plan = new(owner, updated, before, after, frames, inventorySashCount);
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        { reason = "warehouseItemTransferPreparation:" + error.GetType().Name; return false; }
    }

    private static byte[][] PrepareLegacyWarehouseDepositFrames(LegacyWarehousePacket request, PlayerItemSnapshotItem moved)
    {
        var frames = new List<byte[]>
        {
            new MsgItemUsagePacket(moved.Uid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(),
            new LegacyWarehousePacket(request.NpcId, request.Action, 0, 0, [moved]).ToFrameBytes(),
        };
        var now = DateTime.Now;
        if (LegacyItemExtraPacket.TryCreate(moved, now, out var extra)) frames.Add(extra.ToFrameBytes());
        if (LegacyItemLockPacket.TryCreateUnlockDate(moved, now, out var itemLock)) frames.Add(itemLock.ToFrameBytes());
        return frames.ToArray();
    }
}
