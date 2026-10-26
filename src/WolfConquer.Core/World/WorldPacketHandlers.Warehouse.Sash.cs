using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static PlayerItemSnapshot MoveInventoryItemIntoSash(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem sash,
            PlayerItemSnapshotItem depositedItem,
            PlayerItemSnapshotItem updatedSash)
    {
        return new PlayerItemSnapshot(
            items.InventoryItems
                .Where(item => item.Uid != depositedItem.Uid)
                .Select(item => item.Uid == sash.Uid ? updatedSash : item)
                .OrderBy(item => item.Uid)
                .ToArray(),
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems,
            items.RuneItems,
            items.WardrobeItems)
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

    private static PlayerItemSnapshot MoveSashDepositedItemToInventory(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem sash,
            PlayerItemSnapshotItem updatedSash,
            PlayerItemSnapshotItem inventoryItem)
    {
        return new PlayerItemSnapshot(
            items.InventoryItems
                .Select(item => item.Uid == sash.Uid ? updatedSash : item)
                .Concat([inventoryItem])
                .OrderBy(item => item.Uid)
                .ToArray(),
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems,
            items.RuneItems,
            items.WardrobeItems)
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

}
