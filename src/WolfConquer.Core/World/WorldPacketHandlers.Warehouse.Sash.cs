using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{

    private static async ValueTask HandleLegacyWarehouseSashShowAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyWarehousePacket packet,
            CancellationToken cancellationToken)
    {
        if (!TryGetLegacyInventorySash(session, packet.NpcId, out var sash, out var capacity))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyWarehouse sash show {FormatSession(context)} sashUid={packet.NpcId}; sash item was not found.")
                .ConfigureAwait(false);
            return;
        }

        var response = new LegacyWarehousePacket(
            packet.NpcId,
            packet.Action,
            ItemUid: 0,
            capacity,
            sash.DepositedItems.OrderBy(item => item.Uid).ToArray());
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await SendLegacyItemExtraAggregateAsync(
                context,
                response.Items,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer sent LegacyWarehouse sash show {FormatSession(context)} sashUid={packet.NpcId} count={sash.DepositedItems.Count}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyWarehouseSashDepositAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyWarehousePacket packet,
            CancellationToken cancellationToken)
    {
        if (!TryGetLegacyInventorySash(session, packet.NpcId, out var sash, out var capacity))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyWarehouse sash deposit {FormatSession(context)} sashUid={packet.NpcId}; sash item was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (sash.DepositedItems.Count >= capacity)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyWarehouse sash deposit {FormatSession(context)} sashUid={packet.NpcId}; sash is full.")
                .ConfigureAwait(false);
            return;
        }

        var item = session.PlayerItems.InventoryItems.FirstOrDefault(item => item.Uid == packet.ItemUid);
        if (item is null)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyWarehouse sash deposit {FormatSession(context)} itemUid={packet.ItemUid}; inventory item was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (item.Uid == sash.Uid ||
            sash.DepositedItems.Any(deposited => deposited.Uid == item.Uid))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyWarehouse sash deposit {FormatSession(context)} itemUid={item.Uid}; item is already in this sash.")
                .ConfigureAwait(false);
            return;
        }

        var depositedItem = item.WithWarehouseId(0, position: 0);
        var updatedSash = sash.WithDepositedItems(
            sash.DepositedItems.Concat([depositedItem]).OrderBy(deposited => deposited.Uid).ToArray());
        var updated = session with
        {
            PlayerItems = MoveInventoryItemIntoSash(session.PlayerItems, sash, item, updatedSash),
        };

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

        await context.Connection.SendAsync(
                new LegacyWarehousePacket(
                    packet.NpcId,
                    packet.Action,
                    ItemUid: 0,
                    Capacity: 0,
                    Items: [depositedItem]).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyItemFollowupsAsync(context, depositedItem, cancellationToken).ConfigureAwait(false);

        context.Sessions?.Set(context.Connection, updated);
        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyWarehouse sash deposit {FormatSession(context)} sashUid={packet.NpcId} itemUid={item.Uid} itemId={item.ItemId}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyWarehouseSashWithdrawAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyWarehousePacket packet,
            CancellationToken cancellationToken)
    {
        if (!TryGetLegacyInventorySash(session, packet.NpcId, out var sash, out _))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyWarehouse sash withdraw {FormatSession(context)} sashUid={packet.NpcId}; sash item was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyWarehouse sash withdraw {FormatSession(context)} itemUid={packet.ItemUid}; inventory is full.")
                .ConfigureAwait(false);
            return;
        }

        var item = sash.DepositedItems.FirstOrDefault(item => item.Uid == packet.ItemUid);
        if (item is null)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyWarehouse sash withdraw {FormatSession(context)} itemUid={packet.ItemUid}; deposited item was not found.")
                .ConfigureAwait(false);
            return;
        }

        var inventoryItem = item.WithWarehouseId(0, position: 0);
        var updatedSash = sash.WithDepositedItems(
            sash.DepositedItems.Where(deposited => deposited.Uid != item.Uid).ToArray());
        var updated = session with
        {
            PlayerItems = MoveSashDepositedItemToInventory(session.PlayerItems, sash, updatedSash, inventoryItem),
        };

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(inventoryItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                new LegacyWarehousePacket(
                    packet.NpcId,
                    packet.Action,
                    packet.ItemUid,
                    Capacity: 0,
                    Items: [])
                {
                    DeclaredItemCount = 1,
                }.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        context.Sessions?.Set(context.Connection, updated);
        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyWarehouse sash withdraw {FormatSession(context)} sashUid={packet.NpcId} itemUid={item.Uid} itemId={item.ItemId}.")
            .ConfigureAwait(false);
    }

    private static bool TryGetLegacyInventorySash(
            WorldSession session,
            uint sashUid,
            out PlayerItemSnapshotItem sash,
            out ushort capacity)
    {
        sash = session.PlayerItems.InventoryItems.FirstOrDefault(item => item.Uid == sashUid)!;
        capacity = 0;

        if (sash is null)
        {
            return false;
        }

        capacity = ItemType.GetLegacySashCapacity(sash.ItemId);
        return capacity > 0;
    }

    private static PlayerItemSnapshot MoveInventoryItemIntoSash(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem sash,
            PlayerItemSnapshotItem depositedItem,
            PlayerItemSnapshotItem updatedSash)
    {
        return new PlayerItemSnapshot(
            items.InventoryItems
                .Where(item => item.Uid != depositedItem.Uid)
                .Select(item => item.Uid == sash.Uid ? updatedSash : item)
                .OrderBy(item => item.Uid)
                .ToArray(),
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems,
            items.RuneItems,
            items.WardrobeItems)
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

    private static PlayerItemSnapshot MoveSashDepositedItemToInventory(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem sash,
            PlayerItemSnapshotItem updatedSash,
            PlayerItemSnapshotItem inventoryItem)
    {
        return new PlayerItemSnapshot(
            items.InventoryItems
                .Select(item => item.Uid == sash.Uid ? updatedSash : item)
                .Concat([inventoryItem])
                .OrderBy(item => item.Uid)
                .ToArray(),
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems,
            items.RuneItems,
            items.WardrobeItems)
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

}
