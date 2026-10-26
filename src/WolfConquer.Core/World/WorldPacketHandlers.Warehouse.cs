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

        if (LegacyWarehousePacket.IsShowAction(packet.Action) || packet.Action == LegacyWarehouseAction.ShashShow)
        {
            await HandleLegacyWarehouseShowAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                $"WorldServer skipped unsupported LegacyWarehouse action {FormatSession(context)} npcId={packet.NpcId} action={packet.Action}.")
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
}
