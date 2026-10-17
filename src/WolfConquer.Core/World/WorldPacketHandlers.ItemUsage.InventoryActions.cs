using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyMeteorItemId = 1_088_001;

    private const uint LegacyMeteorTearItemId = 1_088_002;

    private const ushort LegacyZeroDurabilityRepairMeteorCost = 5;

    private static async ValueTask HandleSellItemAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            MsgItemUsagePacket packet,
            CancellationToken cancellationToken)
    {
        if (packet.DwParam > uint.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage sell " +
                    $"{FormatSession(context)} itemUid={packet.DwParam}; item UID is outside legacy range.")
                .ConfigureAwait(false);
            return;
        }

        session = LegacyAccountSecurityResolveAnimaSession(context, session);
        context = context with { Session = session };

        var npcId = packet.Id;
        var itemUid = (uint)packet.DwParam;
        if (!CanSeeSellNpc(context, session.Character, npcId))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage sell " +
                    $"{FormatSession(context)} npcUid={npcId}; visible NPC was not found.")
                .ConfigureAwait(false);
            return;
        }

        var item = session.PlayerItems.InventoryItems.FirstOrDefault(item => item.Uid == itemUid);
        if (item is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage sell " +
                    $"{FormatSession(context)} itemUid={itemUid}; inventory item was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (IsLegacySecurityAnimaProtectedItem(item) &&
            await TryBlockLegacySecurityAnimaItemOperationAsync(
                    context,
                    session,
                    LegacySecurityAnimaSellItemSource,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        if (item.Locked > 0 || item.Inscribed > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage sell " +
                    $"{FormatSession(context)} itemUid={item.Uid}; locked or inscribed items cannot be sold.")
                .ConfigureAwait(false);
            return;
        }

        if (IsLegacyNoSellItem(item.ItemId))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage sell " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; item is blocked by the legacy no-sell list.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadGoldWorth(context.WorldData, item.ItemId, out var goldWorth))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage sell " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; itemtype gold worth was not loaded.")
                .ConfigureAwait(false);
            return;
        }

        var saleValue = CalculateLegacySaleValue(goldWorth, item.Durability, item.MaximumDurability);
        var updatedSession = session with
        {
            PlayerItems = RemoveInventoryItem(session.PlayerItems, item),
        };

        long updatedMoney = updatedSession.Character.Money;
        var updatedWarehouseMoney = ReadWarehouseMoney(updatedSession.Character);
        if (saleValue > 0)
        {
            if (updatedMoney > long.MaxValue - saleValue)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped MsgItemUsage sell " +
                        $"{FormatSession(context)} itemUid={item.Uid} value={saleValue}; money overflow would occur.")
                    .ConfigureAwait(false);
                return;
            }

            updatedMoney += saleValue;
            if (updatedMoney > LegacyMaxInventoryMoney)
            {
                var overflow = (ulong)(updatedMoney - LegacyMaxInventoryMoney);
                updatedMoney = LegacyMaxInventoryMoney;
                if (updatedWarehouseMoney > ulong.MaxValue - overflow)
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped MsgItemUsage sell " +
                            $"{FormatSession(context)} itemUid={item.Uid} value={saleValue}; warehouse money overflow would occur.")
                        .ConfigureAwait(false);
                    return;
                }

                updatedWarehouseMoney += overflow;
            }
        }

        await context.Connection.SendAsync(
                new MsgItemUsagePacket(
                    item.Uid,
                    DwParam: 0,
                    MsgItemUsageAction.RemoveInventory,
                    Timestamp: 0,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        if (saleValue > 0)
        {
            updatedSession = UpdateSessionWallet(
                context,
                updatedSession,
                updatedMoney,
                updatedWarehouseMoney);
            await SendWalletMoneyUpdateAsync(context, updatedSession.Character, cancellationToken).ConfigureAwait(false);
            await SendWalletMoneyUpdateAsync(context, updatedSession.Character, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            context.Sessions?.Set(context.Connection, updatedSession);
        }

        await TryPersistLegacyItemUsageMutationAsync(
                context,
                updatedSession,
                "sell",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Money"] = updatedSession.Character.Money.ToString(CultureInfo.InvariantCulture),
                    ["WHMoney"] = ReadWarehouseMoney(updatedSession.Character).ToString(CultureInfo.InvariantCulture),
                },
                persistItems: true)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage sell " +
                $"{FormatSession(context)} npcUid={npcId} itemUid={item.Uid} itemId={item.ItemId} value={saleValue}.")
            .ConfigureAwait(false);
    }

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

    private static async ValueTask HandleDropItemAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            MsgItemUsagePacket packet,
            CancellationToken cancellationToken)
    {
        session = LegacyAccountSecurityResolveAnimaSession(context, session);
        context = context with { Session = session };

        var item = session.PlayerItems.InventoryItems.FirstOrDefault(item => item.Uid == packet.Id);
        if (item is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage drop " +
                    $"{FormatSession(context)} itemUid={packet.Id}; inventory item was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (IsLegacySecurityAnimaProtectedItem(item) &&
            await TryBlockLegacySecurityAnimaItemOperationAsync(
                    context,
                    session,
                    LegacySecurityAnimaDropItemSource,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        if (item.Locked > 0 || item.Inscribed > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage drop " +
                    $"{FormatSession(context)} itemUid={item.Uid}; locked or inscribed items cannot be dropped.")
                .ConfigureAwait(false);
            return;
        }

        var updated = session with
        {
            PlayerItems = RemoveInventoryItem(session.PlayerItems, item),
        };

        var unboundGroundDrop = item.Bound < 1;
        if (unboundGroundDrop)
        {
            var floorItem = LegacyFloorItemPacket.FromDroppedItem(
                item,
                session.Character,
                NextLegacyFloorItemUid());
            var entry = FloorItemRuntimeEntry.ForItem(session.Character.MapId, floorItem, item);
            if (context.FloorItems is { } floorItems)
            {
                if (!floorItems.TryAddVisibleOnGround(
                        entry,
                        DateTimeOffset.UtcNow,
                        0,
                        (mapId, x, y) => TryValidateStaticTerrainLocation(context, mapId, x, y, out _),
                        out var added))
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped MsgItemUsage ground drop " +
                            $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; no valid ground tile was available.")
                        .ConfigureAwait(false);
                    return;
                }

                floorItem = added.Packet;
            }

            await SendFloorItemVisibleAsync(context, session.Character, floorItem, cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                new MsgItemUsagePacket(
                    item.Uid,
                    DwParam: 0,
                    MsgItemUsageAction.RemoveInventory,
                    Timestamp: 0,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        context.Sessions?.Set(context.Connection, updated);
        await TryPersistLegacyItemUsageMutationAsync(
                context,
                updated,
                "drop",
                characterValues: null,
                persistItems: true)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer handled MsgItemUsage {(unboundGroundDrop ? "ground drop" : "bound drop")} " +
                $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}.")
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
        for (var index = 0; index < inventoryItems.Count && remaining > 0; index++)
        {
            var item = inventoryItems[index];
            if (!IsLegacyMeteorRepairMaterial(item))
            {
                continue;
            }

            while (remaining > 0 && item.StackSize > 1)
            {
                item = item.WithStackSize((ushort)(item.StackSize - 1));
                inventoryItems[index] = item;
                mutationFrames.Add(
                    LegacyGameItemPacket.FromSnapshotItem(
                        item,
                        mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes());
                remaining--;
            }

            if (remaining > 0 && item.StackSize == 1)
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

    private static long CalculateLegacySaleValue(uint goldWorth, ushort durability, ushort maximumDurability)
    {
        var saleValue = (long)(goldWorth / 3);
        if (durability > 0 && durability < maximumDurability)
        {
            saleValue = maximumDurability == 0
                ? 0
                : saleValue * durability / maximumDurability;
        }

        return durability > 0 && durability <= maximumDurability ? saleValue : 0;
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
