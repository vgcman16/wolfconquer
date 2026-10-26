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
