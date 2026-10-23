using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static int FindLegacyPositiveDeitylandChestKey(PlayerItemSnapshot items, uint keyItemId)
    {
        // Chest keys intentionally accept any binding; zero stock is never payment.
        for (var index = 0; index < items.InventoryItems.Count; index++)
        {
            var item = items.InventoryItems[index];
            if (item.ItemId == keyItemId && item.StackSize > 0)
            {
                return index;
            }
        }
        return -1;
    }

    private static PlayerItemSnapshot PrepareLegacyDeitylandChestKeyDebit(
        PlayerItemSnapshot items, int keyIndex, List<byte[]> mutationFrames)
    {
        var rows = items.InventoryItems.ToList();
        var key = rows[keyIndex];
        if (key.StackSize == 1)
        {
            rows.RemoveAt(keyIndex);
            mutationFrames.Add(new MsgItemUsagePacket(
                key.Uid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes());
        }
        else
        {
            var updated = key.WithStackSize(checked((ushort)(key.StackSize - 1)));
            rows[keyIndex] = updated;
            mutationFrames.Add(LegacyGameItemPacket.FromSnapshotItem(
                updated, mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes());
        }
        return items with { InventoryItems = rows.ToArray() };
    }
}
