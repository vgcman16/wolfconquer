using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // NPC combine retains its existing per-stack batch frames. Its preceding
    // exact-bound count and this debit must authorize the same physical rows.
    private static bool TryConsumeLegacyInventoryItemCount(
        PlayerItemSnapshot items,
        uint itemId,
        byte bound,
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
            if (item.ItemId != itemId || item.Bound != bound)
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
}
