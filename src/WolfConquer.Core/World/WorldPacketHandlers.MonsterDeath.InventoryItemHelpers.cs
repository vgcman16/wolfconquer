using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static PlayerItemSnapshot AddOrStackLegacyInventoryItem(
        PlayerItemSnapshot items,
        ItemType itemType,
        uint itemId,
        ushort stackCount,
        out PlayerItemSnapshotItem rewardItem,
        out ushort itemMode)
    {
        var inventoryItems = items.InventoryItems.ToArray();
        for (var index = 0; index < inventoryItems.Length; index++)
        {
            var candidate = inventoryItems[index];
            if (candidate.ItemId != itemId ||
                candidate.Position != 0 ||
                candidate.Bound != 0 ||
                candidate.WarehouseId != 0)
            {
                continue;
            }

            var currentStack = candidate.StackSize == 0 ? (ushort)1 : candidate.StackSize;
            var updatedStack = checked((ushort)Math.Min(ushort.MaxValue, currentStack + stackCount));
            rewardItem = candidate.WithStackSize(updatedStack);
            inventoryItems[index] = rewardItem;
            itemMode = LegacyGameItemPacket.UpdateItemMode;
            return items with { InventoryItems = inventoryItems };
        }

        rewardItem = CreatePurchasedItems(items, itemType, count: 1)[0]
            .WithStackSize(stackCount);
        itemMode = LegacyGameItemPacket.AddItemMode;
        return items with
        {
            InventoryItems = items.InventoryItems.Concat([rewardItem]).ToArray(),
        };
    }

    private static PlayerItemSnapshot AddLegacyInventoryItem(
        PlayerItemSnapshot items,
        ItemType itemType,
        ushort stackCount,
        out PlayerItemSnapshotItem rewardItem,
        out ushort itemMode)
    {
        rewardItem = CreatePurchasedItems(items, itemType, count: 1)[0]
            .WithStackSize(stackCount);
        itemMode = LegacyGameItemPacket.AddItemMode;
        return items with
        {
            InventoryItems = items.InventoryItems.Concat([rewardItem]).ToArray(),
        };
    }

    private static bool TryConsumeLegacyInventoryItemCount(
        PlayerItemSnapshot items,
        uint itemId,
        ushort count,
        out PlayerItemSnapshot updatedItems,
        out IReadOnlyList<byte[]> frames)
    {
        updatedItems = items;
        frames = [];
        if (count == 0)
        {
            return true;
        }

        var inventoryItems = items.InventoryItems.ToList();
        var mutationFrames = new List<byte[]>();
        var remaining = count;
        for (var index = 0; index < inventoryItems.Count && remaining > 0; index++)
        {
            var item = inventoryItems[index];
            if (item.ItemId != itemId)
            {
                continue;
            }

            var available = item.StackSize > 0 ? item.StackSize : (ushort)1;
            var consumed = Math.Min(available, remaining);
            remaining = checked((ushort)(remaining - consumed));
            if (available > consumed)
            {
                var updatedItem = item.WithStackSize(checked((ushort)(available - consumed)));
                inventoryItems[index] = updatedItem;
                mutationFrames.Add(
                    LegacyGameItemPacket
                        .FromSnapshotItem(updatedItem, mode: LegacyGameItemPacket.UpdateItemMode)
                        .ToFrameBytes());
                continue;
            }

            mutationFrames.Add(
                new MsgItemUsagePacket(
                    item.Uid,
                    DwParam: 0,
                    MsgItemUsageAction.RemoveInventory,
                    Timestamp: 0,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes());
            inventoryItems.RemoveAt(index);
            index--;
        }

        if (remaining != 0)
        {
            return false;
        }

        updatedItems = items with { InventoryItems = inventoryItems.ToArray() };
        frames = mutationFrames;
        return true;
    }

    private static PlayerItemSnapshot ConsumeLegacyInventoryItemCount(
        PlayerItemSnapshot items,
        uint itemId,
        int count,
        ICollection<byte[]> frames)
    {
        var updatedItems = items;
        for (var index = 0; index < count; index++)
        {
            var item = updatedItems.InventoryItems.FirstOrDefault(item => item.ItemId == itemId);
            if (item is null)
            {
                break;
            }

            updatedItems = ConsumeSingleLegacyInventoryItem(updatedItems, item, out var frame);
            frames.Add(frame);
        }

        return updatedItems;
    }

    private static int CountLegacyInventoryItem(PlayerItemSnapshot items, uint itemId)
    {
        var count = 0;
        foreach (var item in items.InventoryItems)
        {
            if (item.ItemId != itemId)
            {
                continue;
            }

            count = checked(count + Math.Max(1, (int)item.StackSize));
        }

        return count;
    }
}
