using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyMeteorItemId = 1_088_001;

    private const uint LegacyMeteorTearItemId = 1_088_002;

    private const ushort LegacyZeroDurabilityRepairMeteorCost = 5;

    private static ValueTask HandleRepairItemAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            MsgItemUsagePacket packet,
            CancellationToken cancellationToken) =>
        HandleOwnedRepairAsync(context, packet, cancellationToken);

    // Pure data-only compatibility entry; this helper issues no owner or commit authority.
    private static bool TryConsumeLegacyMeteorRepairCost(
            PlayerItemSnapshot items,
            ushort count,
            out PlayerItemSnapshot updatedItems,
            out IReadOnlyList<byte[]> frames) =>
        TryConsumeLegacyMeteorRepairCostForTarget(items, count, 0, out updatedItems, out frames);

    private static bool TryConsumeLegacyMeteorRepairCostForTarget(
            PlayerItemSnapshot items,
            ushort count,
            uint targetUid,
            out PlayerItemSnapshot updatedItems,
            out IReadOnlyList<byte[]> frames)
    {
        var available = items.InventoryItems
            .Where(item => item.Uid != targetUid && IsLegacyMeteorRepairMaterial(item))
            .Sum(item => (long)item.StackSize);
        if (available < count)
        {
            updatedItems = items;
            frames = Array.Empty<byte[]>();
            return false;
        }

        var inventoryItems = items.InventoryItems.ToList();
        var mutationFrames = new List<byte[]>();
        var remaining = count;
        // Native Remove visits each eligible physical UID once, then starts another pass.
        while (remaining > 0)
        {
            for (var index = 0; index < inventoryItems.Count && remaining > 0; index++)
            {
                var item = inventoryItems[index];
                if (item.Uid == targetUid || item.StackSize == 0 || !IsLegacyMeteorRepairMaterial(item))
                {
                    continue;
                }

                // Removing/decrementing a material root must never discard its children.
                if (item.DepositedItems.Count != 0)
                {
                    updatedItems = items;
                    frames = Array.Empty<byte[]>();
                    return false;
                }

                if (item.StackSize > 1)
                {
                    item = item.WithStackSize((ushort)(item.StackSize - 1));
                    inventoryItems[index] = item;
                    mutationFrames.Add(
                        LegacyGameItemPacket.FromSnapshotItem(
                            item,
                            mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes());
                    remaining--;
                }
                else if (remaining > 0 && item.StackSize == 1)
                {
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
                    remaining--;
                }
            }
        }

        updatedItems = new PlayerItemSnapshot(
            inventoryItems,
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems,
            items.RuneItems,
            items.WardrobeItems)
        {
            WarehouseItems = items.WarehouseItems,
        };
        frames = mutationFrames;
        return true;
    }

    private static bool IsLegacyMeteorRepairMaterial(PlayerItemSnapshotItem item)
    {
        return item.ItemId is LegacyMeteorItemId or LegacyMeteorTearItemId;
    }

    private static bool IsLegacyNoSellItem(uint itemId)
    {
        return itemId is 2_100_075 or 2_168_735 or 2_168_725 or 2_168_715 or 2_168_705;
    }

    private static long CalculateLegacyRepairCost(
            uint itemId,
            uint goldWorth,
            ushort durability,
            ushort maximumDurability)
    {
        var quality = itemId % 10;
        var multiplier = quality switch
        {
            9 => 1.125,
            8 => 0.975,
            7 => 0.9,
            6 => 0.825,
            _ => 0.75,
        };
        var wornValue = maximumDurability == 0
            ? 0UL
            : (ulong)goldWorth * (ulong)(maximumDurability - durability) / maximumDurability;
        var repairCost = (long)Math.Ceiling(wornValue * multiplier);
        return Math.Max(1, repairCost);
    }
}
