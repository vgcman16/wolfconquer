using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record EquipmentMoveCatalog(ItemType Source, string[] Fields);
    private sealed record EquipmentMovePlan(WorldSession Updated, EquipmentMoveInputs Inputs,
        LegacyItemSaveGraph After, byte[][] Frames, string Operation, bool QueryUnconditionally,
        EquipmentMoveCatalog? Catalog);

    private static bool TryPrepareEquipmentMove(WorldPacketHandlerContext context, EquipmentMoveOwner owner,
        MsgItemUsagePacket packet, out EquipmentMovePlan plan, out string reason)
    {
        plan = null!; reason = "equipmentMoveInvalidState";
        var inputs = new EquipmentMoveInputs(owner.Expected);
        var stable = inputs.Stable;
        if (stable.PlayerItems.InventoryItems.Count > LegacyInventorySlots ||
            stable.PlayerItems.EquipmentItems.Select(row => row.Position).Distinct().Count() != stable.PlayerItems.EquipmentItems.Count)
            return false;
        PlayerItemSnapshot items; byte[][] frames; bool query; EquipmentMoveCatalog? catalog = null;
        if (packet.Action == MsgItemUsageAction.Unequip)
        {
            if (packet.DwParam > ushort.MaxValue || packet.DwParam is 9 or 17)
            { reason = "equipmentMoveSlotInvalid"; return false; }
            var equipped = stable.PlayerItems.EquipmentItems.SingleOrDefault(row => row.Position == (ushort)packet.DwParam);
            if (equipped is null || equipped.WarehouseId != 0)
            { reason = "equipmentMoveEquippedOwnerMissing"; return false; }
            if (!HasLegacyInventorySpace(stable.PlayerItems, 1))
            { reason = "equipmentMoveInventoryFull"; return false; }
            var moved = equipped.WithPosition(0);
            items = MoveEquippedItemToInventory(stable.PlayerItems, equipped, moved);
            frames = [new MsgItemUsagePacket(equipped.Uid, equipped.Position, MsgItemUsageAction.Unequip,
                    Timestamp: 0, DwParam2: 0, DwParam3: 0, DwParam4: 0).ToFrameBytes(),
                LegacyGameItemPacket.FromSnapshotItem(moved).ToFrameBytes()];
            query = true;
        }
        else
        {
            var item = stable.PlayerItems.InventoryItems.SingleOrDefault(row => row.Uid == packet.Id);
            if (item is null || item.WarehouseId != 0 || item.Position != 0)
            { reason = "equipmentMoveInventoryOwnerMissing"; return false; }
            if (!TryCaptureEquipmentMoveCatalog(context, item.ItemId, out catalog, out var itemType))
            { reason = "equipmentMoveCatalogUnavailableOrInvalid"; return false; }
            if (!TryResolveEquipPosition(stable, item, packet.DwParam, out var position, out var alternate))
            { reason = "equipmentMoveSlotOrWeaponRuleRefused"; return false; }
            if (!PassesLegacyItemGenderRequirement(stable.Character, itemType) ||
                !PassesLegacyRebornEquipBypass(stable.Character, itemType) &&
                    (!PassesLegacyItemClassRequirement(stable.Character, itemType) ||
                     !PassesLegacyItemLevelRequirement(stable.Character, itemType) ||
                     !PassesLegacyItemStatRequirement(stable.Character, itemType)))
            { reason = "equipmentMoveRequirementsRefused"; return false; }
            var displaced = stable.PlayerItems.EquipmentItems.FirstOrDefault(row => row.Position == position);
            var displacedItems = new List<(PlayerItemSnapshotItem Equipped, PlayerItemSnapshotItem Inventory)>();
            if (displaced is not null)
            {
                if (displaced.WarehouseId != 0) { reason = "equipmentMoveDisplacedOwnerInvalid"; return false; }
                displacedItems.Add((displaced, displaced.WithPosition(0)));
            }
            var leftHand = FindLeftHandItemToClearForTwoHandEquip(stable.PlayerItems, item, position);
            if (leftHand is not null)
            {
                if (leftHand.WarehouseId != 0) { reason = "equipmentMoveDisplacedOwnerInvalid"; return false; }
                displacedItems.Add((leftHand, leftHand.WithPosition(0)));
            }
            var finalCount = stable.PlayerItems.InventoryItems.Count - 1 + displacedItems.Count;
            if (finalCount > LegacyInventorySlots) { reason = "equipmentMoveInventoryFull"; return false; }
            var equippedItem = item.WithPosition(position);
            items = displacedItems.Count == 0 ? MoveInventoryItemToEquipment(stable.PlayerItems, item, equippedItem) :
                ReplaceEquippedItems(stable.PlayerItems, item, displacedItems, equippedItem);
            frames = PrepareEquipmentMoveFrames(item, equippedItem, displacedItems, displaced is null, alternate);
            query = !alternate;
        }
        // Status preimage is calculated without exposing the proposed graph.
        var retained = PreserveLegacyPhysicalStatusForItemMutation(context, stable, stable with { PlayerItems = items });
        var updated = owner.Expected with { PlayerItems = items, PhysicalStatusOverlay = retained.PhysicalStatusOverlay,
            EquipmentHitpointsStatus = retained.EquipmentHitpointsStatus };
        if (!LegacyItemSaveGraph.TryCapture(updated.PlayerItems, out var after)) return false;
        plan = new(updated, inputs, after, frames, packet.Action == MsgItemUsageAction.Unequip ? "unequip" : "equip", query, catalog);
        reason = string.Empty; return true;
    }

    private static byte[][] PrepareEquipmentMoveFrames(PlayerItemSnapshotItem item, PlayerItemSnapshotItem equipped,
        IReadOnlyList<(PlayerItemSnapshotItem Equipped, PlayerItemSnapshotItem Inventory)> displaced, bool emptySlot, bool alternate)
    {
        var frames = new List<byte[]> { new MsgItemUsagePacket(item.Uid, 0, MsgItemUsageAction.RemoveInventory,
            Timestamp: 0, DwParam2: 0, DwParam3: 0, DwParam4: 0).ToFrameBytes() };
        foreach (var row in displaced)
        {
            frames.Add(new MsgItemUsagePacket(row.Equipped.Uid, row.Equipped.Position, MsgItemUsageAction.Unequip,
                Timestamp: 0, DwParam2: 0, DwParam3: 0, DwParam4: 0).ToFrameBytes());
            frames.Add(LegacyGameItemPacket.FromSnapshotItem(row.Inventory).ToFrameBytes());
        }
        if (alternate) frames.Add(new MsgItemUsagePacket(equipped.Uid, equipped.Position, MsgItemUsageAction.Unequip,
            Timestamp: 0, DwParam2: 0, DwParam3: 0, DwParam4: 0).ToFrameBytes());
        frames.Add(LegacyGameItemPacket.FromSnapshotItem(equipped).ToFrameBytes());
        if (emptySlot) frames.Add(LegacyGameItemPacket.FromSnapshotItem(equipped, mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes());
        if (alternate) frames.Add(new LegacyShowEquipmentPacket(WParam: equipped.Uid, Alternante: equipped.Position,
            Action: 5, EquipmentSlots: default).ToFrameBytes());
        return frames.ToArray();
    }
}
