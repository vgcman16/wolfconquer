using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Only owned private staging lists enter here. A false result discards the
    // ENTIRE purchase plan; partial rows/frames never reach the session/sender.
    // The shared legacy helper used by unrelated NPC families is unchanged.
    private static bool TryConsumeLegacyExchangeMaterial(
        List<PlayerItemSnapshotItem> inventory, uint itemTypeId, uint count, List<byte[]> frames)
    {
        uint remaining = count;
        for (int index = 0; index < inventory.Count && remaining != 0; index++)
        {
            var item = inventory[index];
            if (item.ItemId != itemTypeId) continue;
            uint available = item.StackSize == 0 ? 1U : item.StackSize;
            uint consumed = Math.Min(available, remaining);
            if (consumed == available)
            {
                frames.Add(new MsgItemUsagePacket(item.Uid, DwParam: 0,
                    MsgItemUsageAction.RemoveInventory, Timestamp: 0,
                    DwParam2: 0, DwParam3: 0, DwParam4: 0).ToFrameBytes());
                inventory.RemoveAt(index);
                index--;
            }
            else
            {
                var next = item.WithStackSize(checked((ushort)(available - consumed)));
                inventory[index] = next;
                frames.Add(LegacyGameItemPacket.FromSnapshotItem(next,
                    mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes());
            }
            remaining -= consumed;
        }
        return remaining == 0;
    }
}
