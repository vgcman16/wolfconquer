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

    [PacketHandler(WorldPacketOpcodes.LegacyCoatStorage)]
    private static async ValueTask HandleLegacyCoatStorageAsync(
            WorldPacketHandlerContext context,
            PacketFrame frame,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyCoatStoragePacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyCoatStorage packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyCoatStorage " +
                    $"{FormatSession(context)} action={packet.ActionId} dwParam1={packet.DwParam1} " +
                    $"dwParam2={packet.DwParam2} dwParam3={packet.DwParam3} items={packet.Items.Count}; " +
                    "no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.ActionId == LegacyCoatStoragePacket.InventoryToWardrobeActionId)
        {
            await HandleLegacyCoatStorageInventoryToWardrobeAsync(context, context.Session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.ActionId == LegacyCoatStoragePacket.RetrieveActionId)
        {
            await HandleLegacyCoatStorageRetrieveAsync(context, context.Session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.ActionId == LegacyCoatStoragePacket.EquipRetrieveNoOpActionId)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyCoatStorage EquipRetrieveNoOp " +
                    $"{FormatSession(context)} action={packet.ActionId} dwParam1={packet.DwParam1} " +
                    $"dwParam2={packet.DwParam2} dwParam3={packet.DwParam3} items={packet.Items.Count}; " +
                    "source switch has an explicit no-op branch.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.ActionId == LegacyCoatStoragePacket.EquipFromWardrobeActionId)
        {
            await HandleLegacyCoatStorageEquipFromWardrobeAsync(context, context.Session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.ActionId == LegacyCoatStoragePacket.TakeOffActionId)
        {
            await HandleLegacyCoatStorageTakeOffAsync(context, context.Session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer deferred LegacyCoatStorage " +
                $"{FormatSession(context)} action={packet.ActionId} dwParam1={packet.DwParam1} " +
                $"dwParam2={packet.DwParam2} dwParam3={packet.DwParam3} items={packet.Items.Count}; " +
                "wardrobe and equipment mutation is not modeled.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyCoatStorageInventoryToWardrobeAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyCoatStoragePacket packet,
            CancellationToken cancellationToken)
    {
        var inventoryItem = session.PlayerItems.InventoryItems.FirstOrDefault(item => item.Uid == packet.DwParam1);
        if (inventoryItem is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyCoatStorage InventoryToWardrobe " +
                    $"{FormatSession(context)} itemUid={packet.DwParam1}; inventory item was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (EnumerateLegacyWardrobeItems(session.PlayerItems).Any(item => item.ItemId == inventoryItem.ItemId))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyCoatStorage InventoryToWardrobe " +
                    $"{FormatSession(context)} itemUid={inventoryItem.Uid} itemId={inventoryItem.ItemId}; " +
                    "wardrobe already contains this item type.")
                .ConfigureAwait(false);
            return;
        }

        var position = ItemType.GetLegacyEquipmentPosition(inventoryItem.ItemId);
        if (position is not (9 or 17))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer deferred LegacyCoatStorage " +
                    $"{FormatSession(context)} action={packet.ActionId} itemUid={inventoryItem.Uid} " +
                    $"itemId={inventoryItem.ItemId} slot={position}; " +
                    "only InventoryToWardrobe for inventory garment and mount armor slots is modeled.")
                .ConfigureAwait(false);
            return;
        }

        if (position == 9 && ReadAttributeUInt64(session.Character.Attributes, "SpecialGarment") != 0)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("Item can't be unequiped during the event.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyCoatStorage InventoryToWardrobe " +
                    $"{FormatSession(context)} itemUid={inventoryItem.Uid} slot={position}; special garment is active.")
                .ConfigureAwait(false);
            return;
        }

        var stored = inventoryItem.WithWarehouseId(100, position: (ushort)0);
        var updated = session with
        {
            PlayerItems = MoveInventoryItemToWardrobe(session.PlayerItems, inventoryItem, stored),
        };

        await context.Connection.SendAsync(
                LegacyCoatStoragePacket.CreateInventoryToWardrobeResponse(stored).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        context.Sessions?.Set(context.Connection, updated);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyCoatStorage InventoryToWardrobe " +
                $"{FormatSession(context)} itemUid={inventoryItem.Uid} itemId={inventoryItem.ItemId} slot={position}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyCoatStorageRetrieveAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyCoatStoragePacket packet,
            CancellationToken cancellationToken)
    {
        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("Please make 1 more space in your inventory.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyCoatStorage Retrieve " +
                    $"{FormatSession(context)} itemUid={packet.DwParam1}; inventory is full.")
                .ConfigureAwait(false);
            return;
        }

        var equipped = session.PlayerItems.EquipmentItems.FirstOrDefault(item => item.Uid == packet.DwParam1);
        var stored = session.PlayerItems.WardrobeItems.FirstOrDefault(item => item.Uid == packet.DwParam1);
        var sourceItem = equipped ?? stored;
        if (sourceItem is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyCoatStorage Retrieve " +
                    $"{FormatSession(context)} itemUid={packet.DwParam1}; wardrobe item was not found.")
                .ConfigureAwait(false);
            return;
        }

        var position = ItemType.GetLegacyEquipmentPosition(sourceItem.ItemId);
        if (position is not (9 or 17))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer deferred LegacyCoatStorage " +
                    $"{FormatSession(context)} action={packet.ActionId} itemUid={sourceItem.Uid} " +
                    $"itemId={sourceItem.ItemId} slot={position}; " +
                    "only Retrieve for wardrobe garment and mount armor slots is modeled.")
                .ConfigureAwait(false);
            return;
        }

        if (sourceItem.WarehouseId != 100 ||
            (equipped is not null && sourceItem.Position != position) ||
            (equipped is null && sourceItem.Position != 0))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer deferred LegacyCoatStorage " +
                    $"{FormatSession(context)} action={packet.ActionId} itemUid={sourceItem.Uid} " +
                    $"itemId={sourceItem.ItemId} position={sourceItem.Position} warehouseId={sourceItem.WarehouseId}; " +
                    "only source wardrobe storage items are modeled.")
                .ConfigureAwait(false);
            return;
        }

        var inventoryItem = CreateLegacyCoatStorageRetrievedItem(sourceItem);
        var updatedItems = equipped is not null
            ? MoveEquippedWardrobeItemToInventory(session.PlayerItems, equipped, inventoryItem)
            : MoveStoredWardrobeItemToInventory(session.PlayerItems, sourceItem, inventoryItem);
        var updated = session with
        {
            PlayerItems = updatedItems,
        };

        if (equipped is not null)
        {
            await context.Connection.SendAsync(
                    LegacyCoatStoragePacket.CreateTakeOffResponse(sourceItem).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(
                    LegacyShowEquipmentPacket.CreateEquipmentRefresh(
                        updated.PlayerItems,
                        updated.AlternateEquipmentActive).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(inventoryItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        var retrieveFrame = LegacyCoatStoragePacket.CreateRetrieveResponse(inventoryItem).ToFrameBytes();
        await context.Connection.SendAsync(retrieveFrame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(retrieveFrame, cancellationToken).ConfigureAwait(false);

        context.Sessions?.Set(context.Connection, updated);
        if (equipped is not null)
        {
            await SendPlayerSpawnToVisiblePeersAsync(context, updated, cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyCoatStorage Retrieve " +
                $"{FormatSession(context)} itemUid={sourceItem.Uid} itemId={sourceItem.ItemId} " +
                $"slot={position} equipped={(equipped is not null ? 1 : 0)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyCoatStorageEquipFromWardrobeAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyCoatStoragePacket packet,
            CancellationToken cancellationToken)
    {
        var wardrobeItem = session.PlayerItems.WardrobeItems.FirstOrDefault(item => item.Uid == packet.DwParam1);
        if (wardrobeItem is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyCoatStorage EquipFromWardrobe " +
                    $"{FormatSession(context)} itemUid={packet.DwParam1}; wardrobe item was not found.")
                .ConfigureAwait(false);
            return;
        }

        var position = ItemType.GetLegacyEquipmentPosition(wardrobeItem.ItemId);
        if (position is not (9 or 17))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer deferred LegacyCoatStorage " +
                    $"{FormatSession(context)} action={packet.ActionId} itemUid={wardrobeItem.Uid} " +
                    $"itemId={wardrobeItem.ItemId} slot={position}; " +
                    "only EquipFromWardrobe for wardrobe garment and mount armor slots is modeled.")
                .ConfigureAwait(false);
            return;
        }

        if (wardrobeItem.WarehouseId != 100 || wardrobeItem.Position != 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer deferred LegacyCoatStorage " +
                    $"{FormatSession(context)} action={packet.ActionId} itemUid={wardrobeItem.Uid} " +
                    $"itemId={wardrobeItem.ItemId} position={wardrobeItem.Position} warehouseId={wardrobeItem.WarehouseId}; " +
                    "only source wardrobe storage items are modeled.")
                .ConfigureAwait(false);
            return;
        }

        if (context.WorldData is not null)
        {
            if (!TryReadItemType(context.WorldData, wardrobeItem.ItemId, out var itemType))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyCoatStorage EquipFromWardrobe " +
                        $"{FormatSession(context)} itemUid={wardrobeItem.Uid} itemId={wardrobeItem.ItemId}; " +
                        "item type data was not found.")
                    .ConfigureAwait(false);
                return;
            }

            if (!PassesLegacyItemGenderRequirement(session.Character, itemType))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyCoatStorage EquipFromWardrobe " +
                        $"{FormatSession(context)} itemUid={wardrobeItem.Uid} itemId={wardrobeItem.ItemId}; " +
                        $"character body {session.Character.Body} does not meet the legacy item gender requirement {itemType.RequiredGender}.")
                    .ConfigureAwait(false);
                return;
            }
        }

        var displaced = session.PlayerItems.EquipmentItems.FirstOrDefault(item => item.Position == position);
        PlayerItemSnapshot updatedItems;
        if (displaced is null)
        {
            updatedItems = MoveWardrobeItemToEquipment(session.PlayerItems, wardrobeItem, wardrobeItem.WithPosition(position));
        }
        else
        {
            var displacedNaturalPosition = ItemType.GetLegacyEquipmentPosition(displaced.ItemId);
            if (displaced.WarehouseId != 100 || displacedNaturalPosition != position)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer deferred LegacyCoatStorage " +
                        $"{FormatSession(context)} action={packet.ActionId} itemUid={wardrobeItem.Uid} " +
                        $"itemId={wardrobeItem.ItemId} slot={position} displacedItemUid={displaced.Uid} " +
                        $"displacedItemId={displaced.ItemId} displacedWarehouseId={displaced.WarehouseId}; " +
                        "ordinary equipment displacement is not modeled.")
                    .ConfigureAwait(false);
                return;
            }

            updatedItems = SwapWardrobeItemIntoEquipment(
                session.PlayerItems,
                wardrobeItem,
                wardrobeItem.WithPosition(position),
                displaced,
                displaced.WithPosition(0));
        }

        var updated = session with
        {
            PlayerItems = updatedItems,
        };

        await context.Connection.SendAsync(
                LegacyShowEquipmentPacket.CreateEquipmentRefresh(
                    updated.PlayerItems,
                    updated.AlternateEquipmentActive).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyCoatStoragePacket.CreateEquipFromWardrobeResponse(wardrobeItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        context.Sessions?.Set(context.Connection, updated);
        await SendPlayerSpawnToVisiblePeersAsync(context, updated, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyCoatStorage EquipFromWardrobe " +
                $"{FormatSession(context)} itemUid={wardrobeItem.Uid} itemId={wardrobeItem.ItemId} " +
                $"slot={position} replacedItemUid={displaced?.Uid ?? 0}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyCoatStorageTakeOffAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyCoatStoragePacket packet,
            CancellationToken cancellationToken)
    {
        if (packet.DwParam2 > ushort.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyCoatStorage TakeOff " +
                    $"{FormatSession(context)} slot={packet.DwParam2}; slot is outside legacy range.")
                .ConfigureAwait(false);
            return;
        }

        var position = (ushort)packet.DwParam2;
        if (position is not (9 or 17))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer deferred LegacyCoatStorage " +
                    $"{FormatSession(context)} action={packet.ActionId} dwParam1={packet.DwParam1} " +
                    $"dwParam2={packet.DwParam2} dwParam3={packet.DwParam3} items={packet.Items.Count}; " +
                    "only TakeOff for wardrobe garment and mount armor slots is modeled.")
                .ConfigureAwait(false);
            return;
        }

        var equipped = session.PlayerItems.EquipmentItems.FirstOrDefault(item => item.Position == position);
        if (equipped is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyCoatStorage TakeOff " +
                    $"{FormatSession(context)} slot={position}; equipped wardrobe item was not found.")
                .ConfigureAwait(false);
            return;
        }

        var naturalPosition = ItemType.GetLegacyEquipmentPosition(equipped.ItemId);
        if (equipped.WarehouseId != 100 || naturalPosition != position)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer deferred LegacyCoatStorage " +
                    $"{FormatSession(context)} action={packet.ActionId} itemUid={equipped.Uid} " +
                    $"itemId={equipped.ItemId} slot={position} warehouseId={equipped.WarehouseId}; " +
                    "only source wardrobe storage items are modeled.")
                .ConfigureAwait(false);
            return;
        }

        if (position == 9 && ReadAttributeUInt64(session.Character.Attributes, "SpecialGarment") != 0)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("Item can't be unequiped during the event.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyCoatStorage TakeOff " +
                    $"{FormatSession(context)} itemUid={equipped.Uid} slot={position}; special garment is active.")
                .ConfigureAwait(false);
            return;
        }

        var stored = equipped.WithPosition(0);
        var updated = session with
        {
            PlayerItems = MoveEquippedItemToWardrobe(session.PlayerItems, equipped, stored),
        };

        await context.Connection.SendAsync(
                LegacyCoatStoragePacket.CreateTakeOffResponse(stored).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyShowEquipmentPacket.CreateEquipmentRefresh(
                    updated.PlayerItems,
                    updated.AlternateEquipmentActive).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        context.Sessions?.Set(context.Connection, updated);
        await SendPlayerSpawnToVisiblePeersAsync(context, updated, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyCoatStorage TakeOff " +
                $"{FormatSession(context)} itemUid={equipped.Uid} itemId={equipped.ItemId} slot={position}.")
            .ConfigureAwait(false);
    }

}
