using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Same physical-bag selection, capacity and frames as the accepted sash transaction.
    // No warehouse ArrestToken restriction applies to this native storage route.
    private static bool TryPrepareLegacyPhysicalSashMove(WorldSession session, LegacyItemSaveGraph before,
        LegacyWarehousePacket request, out WorldSession updated, out byte[][] frames)
    {
        updated = null!;
        frames = [];
        if (!before.TryGetInventory(request.NpcId, out var sashClosure)) return false;
        var sash = sashClosure.Source;
        var capacity = ItemType.GetLegacySashCapacity(sash.ItemId);
        if (capacity == 0) return false;
        if (request.Action == LegacyWarehouseAction.ShashDepositItem)
        {
            if (sash.DepositedItems.Count >= capacity || request.ItemUid == sash.Uid ||
                !before.TryGetInventory(request.ItemUid, out var incoming)) return false;
            var moved = incoming.CreateInventoryItem();
            var updatedSash = sash.WithDepositedItems(sash.DepositedItems.Append(moved).OrderBy(item => item.Uid).ToArray());
            updated = session with { PlayerItems = MoveInventoryItemIntoSash(session.PlayerItems, sash, incoming.Source, updatedSash) };
            frames = PrepareLegacyWarehouseDepositFrames(request, moved);
            return true;
        }
        if (request.Action != LegacyWarehouseAction.ShashWithdrawItem || !HasLegacyInventorySpace(session.PlayerItems, 1)) return false;
        var captured = sashClosure.CopyForPersistence().DepositedItems.FirstOrDefault(item => item.Uid == request.ItemUid);
        if (captured is null) return false;
        var withdrawn = captured.WithWarehouseId(0, 0);
        var remainingSash = sash.WithDepositedItems(sash.DepositedItems.Where(item => item.Uid != withdrawn.Uid).ToArray());
        updated = session with { PlayerItems = MoveSashDepositedItemToInventory(session.PlayerItems, sash, remainingSash, withdrawn) };
        frames = [LegacyGameItemPacket.FromSnapshotItem(withdrawn).ToFrameBytes(),
            new LegacyWarehousePacket(request.NpcId, request.Action, withdrawn.Uid, 0, [])
            { DeclaredItemCount = 1 }.ToFrameBytes()];
        return true;
    }
}
