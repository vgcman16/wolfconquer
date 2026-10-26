using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryPrepareLegacyWarehouseMove(LegacyWarehouseItemTransferOwner owner, LegacyItemSaveGraph before,
        LegacyWarehousePacket request, out WorldSession updated, out byte[][] frames, out ulong? inventorySashCount)
    {
        updated = null!;
        frames = [];
        inventorySashCount = null;
        var session = owner.Session;
        // Keep the existing known-warehouse / own-UID authority. A house-labelled
        // action is not proof of a house owner or an arbitrary dynamic-map warehouse.
        if (!CanUseLegacyWarehouse(session, request.NpcId, request.Action)) return false;
        if (LegacyWarehouseItemTransferActions.IsDeposit(request.Action))
        {
            if (!before.TryGetInventory(request.ItemUid, out var incoming) ||
                !incoming.AllSourceRows(static item => item.ItemId != LegacyArrestTokenItemId) ||
                !TryGetLegacyWarehouseDepositCapacity(owner, request.NpcId, out var capacity, out inventorySashCount) ||
                (ulong)session.PlayerItems.WarehouseItems.Count(item => item.WarehouseId == request.NpcId) >= capacity) return false;
            var moved = incoming.CopyForPersistence().WithWarehouseId(request.NpcId, 0);
            updated = session with { PlayerItems = MoveInventoryItemToWarehouse(session.PlayerItems, incoming.Source, moved) };
            frames = PrepareLegacyWarehouseDepositFrames(request, moved);
            return true;
        }
        if (request.Action is not (LegacyWarehouseAction.WithdrawItem or LegacyWarehouseAction.WithdrawItem_WH_House or
                LegacyWarehouseAction.InventorySashWithdrawItem) ||
            !HasLegacyInventorySpace(session.PlayerItems, 1) ||
            !before.TryGetWarehouse(request.NpcId, request.ItemUid, out var outgoing)) return false;
        // Withdrawal is a safe exit even when capacity metadata is invalid or the
        // existing store is overfull. Bound/locked items and stored tokens remain movable.
        var withdrawn = outgoing.CreateInventoryItem();
        updated = session with { PlayerItems = MoveWarehouseItemToInventory(session.PlayerItems, outgoing.Source, withdrawn) };
        frames = [LegacyGameItemPacket.FromSnapshotItem(withdrawn).ToFrameBytes(),
            new LegacyWarehousePacket(request.NpcId, request.Action, withdrawn.Uid, 0, []).ToFrameBytes()];
        return true;
    }

    private static bool TryGetLegacyWarehouseDepositCapacity(LegacyWarehouseItemTransferOwner owner, uint warehouseId,
        out ulong capacity, out ulong? inventorySashCount)
    {
        capacity = LegacyWarehousePacket.DefaultCapacity;
        inventorySashCount = null;
        if (warehouseId != (uint)owner.Session.Character.Uid) return true;
        var fields = owner.Fields.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        if (!LegacyInventorySashUpgradePersistence.TryReadCount(fields, out capacity) || capacity > 300) return false;
        inventorySashCount = capacity;
        return true;
    }
}
