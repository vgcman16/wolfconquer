using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static PlayerItemSnapshotItem CreateLegacyCoatStorageRetrievedItem(PlayerItemSnapshotItem item)
    {
        var activate = item.TimeLeftInMinutes != 0 ? 1u : item.Activate;
        return new PlayerItemSnapshotItem
        {
            Uid = item.Uid,
            ItemId = item.ItemId,
            Durability = item.Durability,
            MaximumDurability = item.MaximumDurability,
            Position = 0,
            SocketProgress = item.SocketProgress,
            SocketOne = item.SocketOne,
            SocketTwo = item.SocketTwo,
            Effect = item.Effect,
            Plus = item.Plus,
            Bless = item.Bless,
            Bound = item.Bound,
            Enchant = item.Enchant,
            Suspicious = item.Suspicious,
            Locked = item.Locked,
            PlusProgress = item.PlusProgress,
            Inscribed = item.Inscribed,
            Activate = activate,
            TimeLeftInMinutes = item.TimeLeftInMinutes,
            StackSize = item.StackSize,
            WarehouseId = 0,
            Color = item.Color,
            UnlockTimer = item.UnlockTimer,
            ItemPoints = item.ItemPoints,
            PurificationItemId = item.PurificationItemId,
            PurificationLevel = item.PurificationLevel,
            PurificationDuration = item.PurificationDuration,
            PurificationAddedOn = item.PurificationAddedOn,
            RefineryEffectId = item.RefineryEffectId,
            RefineryEffectLevel = item.RefineryEffectLevel,
            RefineryEffectPercent = item.RefineryEffectPercent,
            RefineryEffectPercent2 = item.RefineryEffectPercent2,
            RefineryEffectDuration = item.RefineryEffectDuration,
            RefineryEffectAddedOn = item.RefineryEffectAddedOn,
            AnimaItemId = item.AnimaItemId,
            RuneExperience = item.RuneExperience,
            Attribute1 = item.Attribute1,
            Attribute2 = item.Attribute2,
            Attribute3 = item.Attribute3,
            Attribute4 = item.Attribute4,
            Attribute5 = item.Attribute5,
            Expiration = item.Expiration,
            MythSoul = item.MythSoul,
            MythSoulPoints = item.MythSoulPoints,
            PerfectionLevel = item.PerfectionLevel,
            PerfectionProgress = item.PerfectionProgress,
            OwnerUid = item.OwnerUid,
            OwnerName = item.OwnerName,
            Signature = item.Signature,
            DepositedItems = item.DepositedItems,
        };
    }

    private static PlayerItemSnapshot MoveStoredWardrobeItemToInventory(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem wardrobeItem,
            PlayerItemSnapshotItem inventoryItem)
    {
        return new PlayerItemSnapshot(
            items.InventoryItems.Concat([inventoryItem]).OrderBy(item => item.Uid).ToArray(),
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems,
            items.RuneItems,
            items.WardrobeItems.Where(item => item.Uid != wardrobeItem.Uid).ToArray())
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

    private static PlayerItemSnapshot MoveEquippedWardrobeItemToInventory(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem equipped,
            PlayerItemSnapshotItem inventoryItem)
    {
        return new PlayerItemSnapshot(
            items.InventoryItems.Concat([inventoryItem]).OrderBy(item => item.Uid).ToArray(),
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems.Where(item => item.Uid != equipped.Uid).ToArray(),
            items.RuneItems,
            items.WardrobeItems)
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

    private static PlayerItemSnapshot MoveEquippedItemToWardrobe(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem equipped,
            PlayerItemSnapshotItem stored)
    {
        return new PlayerItemSnapshot(
            items.InventoryItems,
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems.Where(item => item.Uid != equipped.Uid).ToArray(),
            items.RuneItems,
            items.WardrobeItems.Concat([stored]).ToArray())
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

    private static PlayerItemSnapshot MoveInventoryItemToWardrobe(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem inventoryItem,
            PlayerItemSnapshotItem stored)
    {
        return new PlayerItemSnapshot(
            items.InventoryItems.Where(item => item.Uid != inventoryItem.Uid).ToArray(),
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems,
            items.RuneItems,
            items.WardrobeItems.Concat([stored]).ToArray())
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

    private static PlayerItemSnapshot MoveWardrobeItemToEquipment(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem wardrobeItem,
            PlayerItemSnapshotItem equippedItem)
    {
        return new PlayerItemSnapshot(
            items.InventoryItems,
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems.Concat([equippedItem]).OrderBy(item => item.Position).ThenBy(item => item.Uid).ToArray(),
            items.RuneItems,
            items.WardrobeItems.Where(item => item.Uid != wardrobeItem.Uid).ToArray())
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

    private static PlayerItemSnapshot SwapWardrobeItemIntoEquipment(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem wardrobeItem,
            PlayerItemSnapshotItem equippedItem,
            PlayerItemSnapshotItem displaced,
            PlayerItemSnapshotItem storedDisplaced)
    {
        return new PlayerItemSnapshot(
            items.InventoryItems,
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems
                .Where(item => item.Uid != displaced.Uid)
                .Concat([equippedItem])
                .OrderBy(item => item.Position)
                .ThenBy(item => item.Uid)
                .ToArray(),
            items.RuneItems,
            items.WardrobeItems
                .Where(item => item.Uid != wardrobeItem.Uid)
                .Concat([storedDisplaced])
                .ToArray())
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

    private static IEnumerable<PlayerItemSnapshotItem> EnumerateLegacyWardrobeItems(PlayerItemSnapshot playerItems)
    {
        foreach (var item in playerItems.WardrobeItems)
        {
            yield return item;
        }

        foreach (var item in playerItems.EquipmentItems)
        {
            if (item.WarehouseId == 100)
            {
                yield return item;
            }
        }
    }
}
