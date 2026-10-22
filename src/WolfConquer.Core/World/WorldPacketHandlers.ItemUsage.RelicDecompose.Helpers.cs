using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryReadLegacyRelicDecomposeArgs(
        MsgItemUsagePacket packet, PacketFrame frame, out IReadOnlyList<uint> args)
    {
        args = [];
        if (packet.DwParam2 is 0 or >= 50)
        {
            return true;
        }

        var completeWords = ReadLegacyItemUsageArgs(frame);
        var count = checked((int)packet.DwParam2);
        if (completeWords.Count < count)
        {
            // Native GetUsageItem uses unsafe reads here; do not claim native short-body parity.
            return false;
        }

        args = completeWords.Take(count).ToArray();
        return true;
    }

    private static PlayerItemSnapshot RemoveLegacyRelicDecomposeUnit(
        PlayerItemSnapshot items, uint selectedUid, ICollection<byte[]> frames)
    {
        var selected = items.InventoryItems.FirstOrDefault(item => item.Uid == selectedUid);
        if (selected is not null)
        {
            var updated = ConsumeSingleLegacyInventoryItem(items, selected, out var frame);
            frames.Add(frame);
            return updated;
        }

        // Native RemoveStackItem falls back from an absent UID to ITEM_ID == supplied UID.
        // With Count=1, zero stacks are removed while the remaining count stays one.
        var remaining = items;
        foreach (var fallback in items.InventoryItems.Where(item => item.ItemId == selectedUid))
        {
            remaining = ConsumeSingleLegacyInventoryItem(remaining, fallback, out var frame);
            frames.Add(frame);
            if (fallback.StackSize != 0)
            {
                break;
            }
        }

        return remaining;
    }

    private static PlayerItemSnapshot AddLegacyRelicDecomposeCrystals(
        WorldDataCatalog? worldData, PlayerItemSnapshot items, ICollection<byte[]> frames)
    {
        if (!HasLegacyInventorySpace(items, LegacyRelicDecomposeCrystalStack) ||
            !TryReadItemType(worldData, LegacyRelicDecomposeCrystalItemId, out var itemType))
        {
            return items;
        }

        // Nine separate one-unit ADD rows, not one stack-nine row and not stack merging.
        // This reuses the managed session UID allocator; native SQL/random/global-counter effects remain open.
        var crystals = CreatePurchasedItems(items, itemType, LegacyRelicDecomposeCrystalStack);
        foreach (var crystal in crystals)
        {
            frames.Add(LegacyGameItemPacket.FromSnapshotItem(
                crystal, mode: LegacyGameItemPacket.AddItemMode).ToFrameBytes());
        }

        return items with { InventoryItems = items.InventoryItems.Concat(crystals).ToArray() };
    }
}
