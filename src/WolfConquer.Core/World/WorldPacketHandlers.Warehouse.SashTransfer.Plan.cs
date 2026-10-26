using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacySashTransferPlan(LegacySashTransferOwner Owner, WorldSession Updated,
        LegacyItemSaveGraph Before, LegacyItemSaveGraph After, byte[][] Frames);

    private static bool TryPrepareLegacySashTransfer(LegacySashTransferOwner owner, LegacyWarehousePacket request,
        out LegacySashTransferPlan plan, out string reason)
    {
        plan = null!;
        reason = "sashTransferItemGraphOrSelectionInvalid";
        var session = owner.Session;
        if (!LegacyItemSaveGraph.TryCapture(session.PlayerItems, out var before) ||
            !before.TryGetInventory(request.NpcId, out var sashClosure)) return false;
        var sash = sashClosure.Source;
        var capacity = ItemType.GetLegacySashCapacity(sash.ItemId);
        if (capacity == 0) return false;
        try
        {
            WorldSession updated;
            byte[][] frames;
            if (request.Action == LegacyWarehouseAction.ShashDepositItem)
            {
                if (sash.DepositedItems.Count >= capacity || request.ItemUid == sash.Uid ||
                    !before.TryGetInventory(request.ItemUid, out var incoming)) return false;
                // Detached captured row: no retired source perfection/deposit aliases cross the move.
                var moved = incoming.CreateInventoryItem();
                var updatedSash = sash.WithDepositedItems(sash.DepositedItems.Append(moved).OrderBy(item => item.Uid).ToArray());
                updated = session with { PlayerItems = MoveInventoryItemIntoSash(session.PlayerItems, sash, incoming.Source, updatedSash) };
                frames = PrepareLegacySashDepositFrames(request, moved);
            }
            else if (request.Action == LegacyWarehouseAction.ShashWithdrawItem)
            {
                if (!HasLegacyInventorySpace(session.PlayerItems, 1)) return false;
                var captured = sashClosure.CopyForPersistence().DepositedItems.FirstOrDefault(item => item.Uid == request.ItemUid);
                if (captured is null) return false;
                var moved = captured.WithWarehouseId(0, 0);
                var updatedSash = sash.WithDepositedItems(sash.DepositedItems.Where(item => item.Uid != moved.Uid).ToArray());
                updated = session with { PlayerItems = MoveSashDepositedItemToInventory(session.PlayerItems, sash, updatedSash, moved) };
                frames = [LegacyGameItemPacket.FromSnapshotItem(moved).ToFrameBytes(),
                    new LegacyWarehousePacket(request.NpcId, request.Action, moved.Uid, 0, [])
                    { DeclaredItemCount = 1 }.ToFrameBytes()];
            }
            else return false;
            // The codec can save only one deposited level. A populated bag nested
            // inside another bag must not silently lose its grandchildren on disk.
            if (!LegacyItemSaveGraph.TryCapture(updated.PlayerItems, out var after) || !before.IsCurrent(session.PlayerItems)) return false;
            plan = new(owner, updated, before, after, frames);
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        { reason = "sashTransferPreparation:" + error.GetType().Name; return false; }
    }

    private static byte[][] PrepareLegacySashDepositFrames(LegacyWarehousePacket request, PlayerItemSnapshotItem moved)
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
