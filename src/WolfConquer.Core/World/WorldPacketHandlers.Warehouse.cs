using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{

    private const uint LegacyMailboxWarehouseId = 1_000;

    private const string LegacyWarehouseDialogText =
            "I am the most reliable man in Twin City! Your valuable things will be safe in my warehouse!";

    private const string LegacyWarehouseOpenOption = "Open~my~Warehouse.";

    private const string LegacyWarehouseCloseOption = "Thanks.";

    private const uint LegacyReturnedItemsWarehouseId = 65_535;

    [PacketHandler(WorldPacketOpcodes.LegacyWarehouse)]
    private static async ValueTask HandleLegacyWarehouseAsync(
            WorldPacketHandlerContext context,
            PacketFrame frame,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyWarehousePacket.TryParseRequest(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyWarehouse packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyWarehouse {FormatSession(context)} npcId={packet.NpcId} action={packet.Action} itemUid={packet.ItemUid}.")
            .ConfigureAwait(false);

        if (LegacyWarehouseItemTransferActions.IsMutation(packet.Action))
        {
            await HandleLegacyWarehouseItemTransferAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyWarehouse {FormatSession(context)} action={packet.Action}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (LegacyWarehousePacket.IsShowAction(packet.Action))
        {
            await HandleLegacyWarehouseShowAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        switch (packet.Action)
        {
            case LegacyWarehouseAction.ShashShow:
                await HandleLegacyWarehouseSashShowAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
                return;
            default:
                await context.Log.WriteLineAsync(
                        $"WorldServer skipped unsupported LegacyWarehouse action {FormatSession(context)} npcId={packet.NpcId} action={packet.Action}.")
                    .ConfigureAwait(false);
                return;
        }
    }

    private static async ValueTask HandleLegacyWarehouseShowAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyWarehousePacket packet,
            CancellationToken cancellationToken)
    {
        if (packet.Action == LegacyWarehouseAction.ShowInventorySash &&
            packet.NpcId != CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyWarehouse inventory sash show {FormatSession(context)} npcId={packet.NpcId}; player uid mismatch.")
                .ConfigureAwait(false);
            return;
        }

        var warehouseItems = session.PlayerItems.WarehouseItems
            .Where(item => item.WarehouseId == packet.NpcId)
            .OrderBy(item => item.Uid)
            .ToArray();
        if (warehouseItems.Length == 0)
        {
            if (packet.Action == LegacyWarehouseAction.ShowInventorySash)
            {
                await context.Log.WriteLineAsync(
                        $"WorldServer skipped LegacyWarehouse inventory sash show {FormatSession(context)} npcId={packet.NpcId}; inventory sash is empty (loadedInventoryItems={session.PlayerItems.InventoryItems.Count} loadedWarehouseItems={session.PlayerItems.WarehouseItems.Count}).")
                    .ConfigureAwait(false);
                return;
            }

            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyWarehouse show {FormatSession(context)} npcId={packet.NpcId}; no source items are loaded.")
                .ConfigureAwait(false);
            return;
        }

        var capacity = GetLegacyWarehouseCapacity(session, packet.NpcId);
        foreach (var chunk in warehouseItems.Chunk(4))
        {
            var response = new LegacyWarehousePacket(
                packet.NpcId,
                packet.Action,
                ItemUid: 0,
                capacity,
                chunk);
            await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                $"WorldServer sent LegacyWarehouse {FormatSession(context)} npcId={packet.NpcId} action={packet.Action} count={warehouseItems.Length}.")
            .ConfigureAwait(false);
    }


    private static async ValueTask SendLegacyItemFollowupsAsync(
            WorldPacketHandlerContext context,
            PlayerItemSnapshotItem item,
            CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        if (LegacyItemExtraPacket.TryCreate(item, now, out var itemExtra))
        {
            await context.Connection.SendAsync(itemExtra.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        if (LegacyItemLockPacket.TryCreateUnlockDate(item, now, out var itemLock))
        {
            await context.Connection.SendAsync(itemLock.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask SendLegacyItemExtraAggregateAsync(
            WorldPacketHandlerContext context,
            IReadOnlyList<PlayerItemSnapshotItem> items,
            CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var purifications = new List<LegacyItemExtraPurification>();
        var refineries = new List<LegacyItemExtraRefinery>();

        foreach (var item in items)
        {
            if (!LegacyItemExtraPacket.TryCreate(item, now, out var itemExtra))
            {
                continue;
            }

            foreach (var purification in itemExtra.Purifications)
            {
                purifications.Add(purification);
            }

            foreach (var refinery in itemExtra.Refineries)
            {
                refineries.Add(refinery);
            }
        }

        if (purifications.Count == 0 && refineries.Count == 0)
        {
            return;
        }

        await context.Connection.SendAsync(
                new LegacyItemExtraPacket(purifications, refineries).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }


    private static bool CanUseLegacyWarehouse(
            WorldSession session,
            uint npcId,
            LegacyWarehouseAction action)
    {
        return action is LegacyWarehouseAction.ShowInventorySash or
                LegacyWarehouseAction.InventorySashDepositItem or
                LegacyWarehouseAction.InventorySashWithdrawItem
            ? npcId == CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid))
            : IsLegacyWarehouseNpc(npcId) || npcId == CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
    }

    private static bool IsLegacyWarehouseNpc(uint npcId)
    {
        return npcId is 8 or 44 or 4101 or 7762 or 10011 or 10012 or 10027 or 10028 ||
            npcId == LegacyReturnedItemsWarehouseId;
    }

    private static PlayerItemSnapshot MoveInventoryItemToWarehouse(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem inventoryItem,
            PlayerItemSnapshotItem warehouseItem)
    {
        return new PlayerItemSnapshot(
            items.InventoryItems.Where(item => item.Uid != inventoryItem.Uid).ToArray(),
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems,
            items.RuneItems,
            items.WardrobeItems)
        {
            WarehouseItems = items.WarehouseItems
                .Concat([warehouseItem])
                .OrderBy(item => item.WarehouseId)
                .ThenBy(item => item.Uid)
                .ToArray(),
        };
    }

    private static PlayerItemSnapshot MoveWarehouseItemToInventory(
            PlayerItemSnapshot items,
            PlayerItemSnapshotItem warehouseItem,
            PlayerItemSnapshotItem inventoryItem)
    {
        return new PlayerItemSnapshot(
            items.InventoryItems.Concat([inventoryItem]).OrderBy(item => item.Uid).ToArray(),
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems,
            items.RuneItems,
            items.WardrobeItems)
        {
            WarehouseItems = items.WarehouseItems
                .Where(item => item.Uid != warehouseItem.Uid || item.WarehouseId != warehouseItem.WarehouseId)
                .ToArray(),
        };
    }

    private static async ValueTask SendLegacyWarehouseDialogAsync(
            WorldPacketHandlerContext context,
            uint npcId,
            CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForDialog(LegacyWarehouseDialogText).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForOption(LegacyWarehouseOpenOption, 1).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForOption(LegacyWarehouseCloseOption, LegacyNpcClosedOption).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForAvatar(10).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForFinish().ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer sent LegacyWarehouse dialog {FormatSession(context)} npcId={npcId}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWarehouseOpenActionAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            uint npcId,
            byte optionId,
            CancellationToken cancellationToken)
    {
        var character = session.Character;
        var action = new MsgActionPacket
        {
            ObjId = CheckedUInt32(character.Uid, nameof(character.Uid)),
            DwParam = 4,
            Type = MsgActionType.OpenClientWindow,
            PositionX = CheckedUInt32(character.X, nameof(character.X)),
            PositionY = CheckedUInt32(character.Y, nameof(character.Y)),
        };
        await context.Connection.SendAsync(action.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyWarehouse open action " +
                $"{FormatSession(context)} npcId={npcId} option={optionId}.")
            .ConfigureAwait(false);
    }

    private static ushort GetLegacyWarehouseCapacity(WorldSession session, uint npcId)
    {
        return npcId == CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid))
            ? ReadAttributeUInt16(session.Character.Attributes, "InventorySashCount")
            : LegacyWarehousePacket.DefaultCapacity;
    }

}
