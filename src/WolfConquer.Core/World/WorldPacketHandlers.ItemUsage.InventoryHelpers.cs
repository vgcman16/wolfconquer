using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static PlayerItemSnapshot RemoveInventoryItem(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem inventoryItem)
    {
        return new PlayerItemSnapshot(
            items.InventoryItems.Where(item => item.Uid != inventoryItem.Uid).ToArray(),
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems,
            items.RuneItems,
            items.WardrobeItems)
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

    private static bool HasLegacyInventorySpace(PlayerItemSnapshot items, int count)
    {
        return items.InventoryItems.Count + count <= LegacyInventorySlots;
    }

    private static bool AllowsLegacyProtectedFloorPickup(
        WorldPacketHandlerContext context,
        FloorItemRuntimeEntry entry)
    {
        var ownerUid = entry.Packet.ItemOwnerUid;
        return ownerUid != 0 &&
            context.TeamStates?.AllowsProtectedFloorPickup(context.Connection, ownerUid, entry.Kind) == true;
    }

    private static bool TryCreateLegacyFloorPickupItem(
        WorldPacketHandlerContext context,
        WorldSession session,
        FloorItemRuntimeEntry entry,
        out PlayerItemSnapshotItem? item,
        out ItemType? itemType)
    {
        if (entry.Item is not null)
        {
            item = entry.Item.WithPosition(0);
            itemType = TryReadLegacyItemType(context.WorldData, entry.Packet.ItemId, out var existingType)
                ? existingType
                : null;
            return true;
        }

        if (!TryReadLegacyItemType(context.WorldData, entry.Packet.ItemId, out itemType))
        {
            item = null;
            return false;
        }

        item = CreatePurchasedItems(session.PlayerItems, itemType, 1, isBound: false, plus: entry.Packet.PlusOrColor)[0];
        return true;
    }

    private static bool TryReadLegacyItemType(
        WorldDataCatalog? worldData,
        uint itemId,
        out ItemType itemType)
    {
        if (worldData is null ||
            itemId > int.MaxValue ||
            !worldData.ItemsById.TryGetValue((int)itemId, out var itemTypes) ||
            itemTypes.Count == 0)
        {
            itemType = default!;
            return false;
        }

        itemType = itemTypes[0];
        return true;
    }

    private static bool TryAddLegacyCurrency(
        CharacterSnapshot character,
        FloorItemRuntimeEntry entry,
        out CharacterSnapshot updatedCharacter)
    {
        updatedCharacter = character;
        if (entry.Amount <= 0)
        {
            return false;
        }

        if (entry.Kind == FloorItemRuntimePickupKind.Money)
        {
            if (character.Money > long.MaxValue - entry.Amount)
            {
                return false;
            }

            updatedCharacter = character with { Money = character.Money + entry.Amount };
            return true;
        }

        if (entry.Kind == FloorItemRuntimePickupKind.ConquerPoints)
        {
            if (character.ConquerPoints > long.MaxValue - entry.Amount)
            {
                return false;
            }

            updatedCharacter = character with { ConquerPoints = character.ConquerPoints + entry.Amount };
            return true;
        }

        return false;
    }

    private static PlayerItemSnapshot ConsumeSingleLegacyInventoryItem(
        PlayerItemSnapshot items,
        PlayerItemSnapshotItem inventoryItem,
        out byte[] frame)
    {
        if (inventoryItem.StackSize > 1)
        {
            var updatedItem = inventoryItem.WithStackSize((ushort)(inventoryItem.StackSize - 1));
            frame = LegacyGameItemPacket.FromSnapshotItem(
                updatedItem,
                mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes();
            return ReplaceInventoryItem(items, inventoryItem, updatedItem);
        }

        frame = new MsgItemUsagePacket(
            inventoryItem.Uid,
            DwParam: 0,
            MsgItemUsageAction.RemoveInventory,
            Timestamp: 0,
            DwParam2: 0,
            DwParam3: 0,
            DwParam4: 0).ToFrameBytes();
        return RemoveInventoryItem(items, inventoryItem);
    }

    private static IReadOnlyList<PlayerItemSnapshotItem> CreatePurchasedItems(
        PlayerItemSnapshot items,
        ItemType itemType,
        int count,
        bool isBound = false,
        byte plus = 0)
    {
        var nextUid = GetNextSessionItemUid(items);
        var durability = checked((ushort)itemType.LegacyDurability);
        var itemId = CheckedUInt32(itemType.Id, nameof(itemType.Id));
        var purchased = new PlayerItemSnapshotItem[count];
        for (var index = 0; index < purchased.Length; index++)
        {
            purchased[index] = new PlayerItemSnapshotItem
            {
                Uid = checked(nextUid + (uint)index),
                ItemId = itemId,
                Durability = durability,
                MaximumDurability = durability,
                Position = 0,
                StackSize = 1,
                Bound = isBound ? (byte)1 : (byte)0,
                Plus = plus,
            };
        }

        return purchased;
    }

    private static uint GetNextSessionItemUid(PlayerItemSnapshot items)
    {
        var maxUid = items.InventoryItems
            .Concat(items.RelicFuseItems)
            .Concat(items.MythsoulBagItems)
            .Concat(items.EquipmentItems)
            .Concat(items.RuneItems)
            .Concat(items.WardrobeItems)
            .Concat(items.WarehouseItems)
            .Select(item => item.Uid)
            .DefaultIfEmpty(0U)
            .Max();

        return checked(maxUid + 1);
    }

    private static bool TryReadGoldWorth(WorldDataCatalog? worldData, uint itemId, out uint goldWorth)
    {
        goldWorth = 0;
        if (worldData is null ||
            itemId > int.MaxValue ||
            !worldData.ItemsById.TryGetValue((int)itemId, out var items) ||
            items.Count == 0)
        {
            return false;
        }

        goldWorth = items[0].GoldWorth;
        return true;
    }

    private static bool TryReadItemType(
        WorldDataCatalog? worldData,
        uint itemId,
        out ItemType itemType)
    {
        itemType = null!;
        if (worldData is null ||
            itemId > int.MaxValue ||
            !worldData.ItemsById.TryGetValue((int)itemId, out var items) ||
            items.Count == 0)
        {
            return false;
        }

        itemType = items[0];
        return true;
    }
}
