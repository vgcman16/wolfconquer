using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyMeteorItemId = 1_088_001;

    private const uint LegacyMeteorTearItemId = 1_088_002;

    private const ushort LegacyZeroDurabilityRepairMeteorCost = 5;

    private static async ValueTask HandleRepairItemAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            MsgItemUsagePacket packet,
            CancellationToken cancellationToken)
    {
        var equipmentItem = session.PlayerItems.EquipmentItems.FirstOrDefault(item => item.Uid == packet.Id);
        var inventoryItem = session.PlayerItems.InventoryItems.FirstOrDefault(item => item.Uid == packet.Id);
        var item = equipmentItem ?? inventoryItem;
        if (item is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage repair " +
                    $"{FormatSession(context)} itemUid={packet.Id}; item was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (item.Durability == 0)
        {
            if (!TryConsumeLegacyMeteorRepairCost(
                    session.PlayerItems,
                    LegacyZeroDurabilityRepairMeteorCost,
                    out var itemsAfterMeteorCost,
                    out var meteorCostFrames))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped MsgItemUsage repair " +
                        $"{FormatSession(context)} itemUid={item.Uid}; zero-durability repair requires five meteors.")
                    .ConfigureAwait(false);
                return;
            }

            var meteorRepaired = item.WithDurability(item.MaximumDurability);
            var meteorRepairedItems = equipmentItem is not null
                ? ReplaceEquipmentItem(itemsAfterMeteorCost, item, meteorRepaired)
                : ReplaceInventoryItem(itemsAfterMeteorCost, item, meteorRepaired);
            var meteorRepairedSession = session with { PlayerItems = meteorRepairedItems };

            foreach (var frame in meteorCostFrames)
            {
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }

            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(
                        meteorRepaired,
                        mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            context.Sessions?.Set(context.Connection, meteorRepairedSession);
            await TryPersistLegacyItemUsageMutationAsync(
                    context,
                    meteorRepairedSession,
                    "zero-durability repair",
                    characterValues: null,
                    persistItems: true)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled MsgItemUsage zero-durability repair " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}.")
                .ConfigureAwait(false);
            return;
        }

        if (item.Durability >= item.MaximumDurability || item.MaximumDurability == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage repair " +
                    $"{FormatSession(context)} itemUid={item.Uid}; item does not need paid repair.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadGoldWorth(context.WorldData, item.ItemId, out var goldWorth))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage repair " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; itemtype gold worth was not loaded.")
                .ConfigureAwait(false);
            return;
        }

        var repairCost = CalculateLegacyRepairCost(item.ItemId, goldWorth, item.Durability, item.MaximumDurability);
        if (session.Character.Money < repairCost)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage repair " +
                    $"{FormatSession(context)} itemUid={item.Uid} cost={repairCost} money={session.Character.Money}.")
                .ConfigureAwait(false);
            return;
        }

        var repaired = item.WithDurability(item.MaximumDurability);
        var repairedItems = equipmentItem is not null
            ? ReplaceEquipmentItem(session.PlayerItems, item, repaired)
            : ReplaceInventoryItem(session.PlayerItems, item, repaired);
        var repairedSession = session with { PlayerItems = repairedItems };
        var updatedSession = UpdateSessionWallet(
            context,
            repairedSession,
            session.Character.Money - repairCost,
            ReadWarehouseMoney(session.Character));

        await SendWalletMoneyUpdateAsync(context, updatedSession.Character, cancellationToken).ConfigureAwait(false);
        await SendWalletMoneyUpdateAsync(context, updatedSession.Character, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(
                    repaired,
                    mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await TryPersistLegacyItemUsageMutationAsync(
                context,
                updatedSession,
                "paid repair",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Money"] = updatedSession.Character.Money.ToString(CultureInfo.InvariantCulture),
                },
                persistItems: true)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage paid repair " +
                $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId} cost={repairCost}.")
            .ConfigureAwait(false);
    }

    private static bool TryConsumeLegacyMeteorRepairCost(
            PlayerItemSnapshot items,
            ushort count,
            out PlayerItemSnapshot updatedItems,
            out IReadOnlyList<byte[]> frames)
    {
        var available = items.InventoryItems
            .Where(IsLegacyMeteorRepairMaterial)
            .Sum(item => item.StackSize);
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
                if (!IsLegacyMeteorRepairMaterial(item))
                {
                    continue;
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
