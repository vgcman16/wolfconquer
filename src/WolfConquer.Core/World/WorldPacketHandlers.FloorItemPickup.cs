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
    [PacketHandler(WorldPacketOpcodes.LegacyFloorItem)]
    private static async ValueTask HandleLegacyFloorItemAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyFloorItemPacket.TryParsePickupRequest(frame, out var floorUid))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyFloorItem pickup {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyFloorItem pickup {FormatSession(context)} floorUid={floorUid}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var character = session.Character;
        var playerUid = CheckedUInt32(character.Uid, nameof(character.Uid));
        if (context.FloorItems is not { } floorItems)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyFloorItem pickup {FormatSession(context)} floorUid={floorUid}; visible floor item was not found.")
                .ConfigureAwait(false);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var pickupStatus = floorItems.GetPickupStatus(floorUid, playerUid, now, out var entry);
        if (pickupStatus is FloorItemRuntimePickupStatus.Missing or FloorItemRuntimePickupStatus.Expired)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyFloorItem pickup " +
                    $"{FormatSession(context)} floorUid={floorUid}; visible floor item was not found status={pickupStatus}.")
                .ConfigureAwait(false);
            return;
        }

        var packet = entry.Packet;
        var allowOwnerLockBypass = pickupStatus == FloorItemRuntimePickupStatus.OwnerLocked &&
            AllowsLegacyProtectedFloorPickup(context, entry);
        if (pickupStatus == FloorItemRuntimePickupStatus.OwnerLocked)
        {
            if (!allowOwnerLockBypass)
            {
                await context.Connection.SendAsync(
                        LegacyMsgMessagePacket.SystemTalk("You have to wait a little bit before you can pick up any items dropped from monsters killed by other players.").ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyFloorItem pickup " +
                        $"{FormatSession(context)} floorUid={floorUid}; ownerUid={packet.ItemOwnerUid}.")
                    .ConfigureAwait(false);
                return;
            }

            await context.Log.WriteLineAsync(
                    "WorldServer allowed LegacyFloorItem team owner-lock pickup " +
                    $"{FormatSession(context)} floorUid={floorUid} ownerUid={packet.ItemOwnerUid} kind={entry.Kind}.")
                .ConfigureAwait(false);
        }

        var mapId = entry.MapId == 0 ? character.MapId : entry.MapId;
        var distance = LegacyGetDistance(character.X, character.Y, packet.X, packet.Y);
        if (character.MapId != mapId || distance > LegacyFloorItemPickupRange)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyFloorItem pickup " +
                    $"{FormatSession(context)} floorUid={floorUid}; map={mapId} distance={distance}.")
                .ConfigureAwait(false);
            return;
        }

        if (entry.Kind == FloorItemRuntimePickupKind.Item &&
            !HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyFloorItem item pickup " +
                    $"{FormatSession(context)} floorUid={floorUid}; inventory is full.")
                .ConfigureAwait(false);
            return;
        }

        PlayerItemSnapshotItem? pickupItem = null;
        ItemType? pickupItemType = null;
        if (entry.Kind == FloorItemRuntimePickupKind.Item &&
            !TryCreateLegacyFloorPickupItem(context, session, entry, out pickupItem, out pickupItemType))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyFloorItem item pickup " +
                    $"{FormatSession(context)} floorUid={floorUid} itemId={packet.ItemId}; itemtype data was not loaded.")
                .ConfigureAwait(false);
            return;
        }

        CharacterSnapshot updatedCurrencyCharacter = character;
        if (entry.Kind is FloorItemRuntimePickupKind.Money or FloorItemRuntimePickupKind.ConquerPoints &&
            !TryAddLegacyCurrency(character, entry, out updatedCurrencyCharacter))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyFloorItem currency pickup " +
                    $"{FormatSession(context)} floorUid={floorUid} kind={entry.Kind} amount={entry.Amount}; currency would overflow.")
                .ConfigureAwait(false);
            return;
        }

        var consumeStatus = floorItems.TryConsumePickupEntry(
            floorUid,
            playerUid,
            allowOwnerLockBypass,
            now,
            out entry);
        if (consumeStatus != FloorItemRuntimePickupStatus.Available)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyFloorItem pickup " +
                    $"{FormatSession(context)} floorUid={floorUid}; item was already consumed status={consumeStatus}.")
                .ConfigureAwait(false);
            return;
        }

        packet = entry.Packet;
        switch (entry.Kind)
        {
            case FloorItemRuntimePickupKind.Money:
                {
                    var updatedSession = UpdateSessionWallet(
                        context,
                        session,
                        updatedCurrencyCharacter.Money,
                        ReadWarehouseMoney(session.Character));
                    await SendWalletMoneyUpdateAsync(context, updatedSession.Character, cancellationToken)
                        .ConfigureAwait(false);
                    await SendLegacyFloorItemRemoveAsync(context, updatedSession.Character, packet, cancellationToken)
                        .ConfigureAwait(false);
                    await context.Connection.SendAsync(
                            LegacyMsgMessagePacket.SystemTalk($"You have picked up a {entry.Amount} silvers.").ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "WorldServer handled LegacyFloorItem money pickup " +
                            $"{FormatSession(context)} floorUid={floorUid} amount={entry.Amount}.")
                        .ConfigureAwait(false);
                    return;
                }

            case FloorItemRuntimePickupKind.ConquerPoints:
                {
                    var updatedSession = session with { Character = updatedCurrencyCharacter };
                    context.Sessions?.Set(context.Connection, updatedSession);
                    await SendConquerPointsUpdateAsync(
                            context,
                            updatedSession.Character,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await SendLegacyFloorItemRemoveAsync(context, updatedSession.Character, packet, cancellationToken)
                        .ConfigureAwait(false);
                    await context.Connection.SendAsync(
                            LegacyMsgMessagePacket.SystemTalk($"You have picked up a {entry.Amount} ConquerPoints.").ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "WorldServer handled LegacyFloorItem Conquer Point pickup " +
                            $"{FormatSession(context with { Session = updatedSession })} floorUid={floorUid} " +
                            $"amount={entry.Amount} updatedConquerPoints={updatedSession.Character.ConquerPoints} " +
                            "walletUpdate=LegacyMsgUpdate.ConquerPoints.")
                        .ConfigureAwait(false);
                    return;
                }

            default:
                {
                    var updatedItems = session.PlayerItems with
                    {
                        InventoryItems = session.PlayerItems.InventoryItems.Concat([pickupItem!]).ToArray(),
                    };
                    var updatedSession = session with { PlayerItems = updatedItems };
                    context.Sessions?.Set(context.Connection, updatedSession);

                    await context.Connection.SendAsync(
                            LegacyGameItemPacket.FromSnapshotItem(pickupItem!).ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);
                    await SendLegacyFloorItemRemoveAsync(context, updatedSession.Character, packet, cancellationToken)
                        .ConfigureAwait(false);
                    await context.Connection.SendAsync(
                            LegacyMsgMessagePacket.SystemTalk($"You have picked up a {pickupItemType?.Name ?? packet.ItemId.ToString(CultureInfo.InvariantCulture)}.").ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "WorldServer handled LegacyFloorItem item pickup " +
                            $"{FormatSession(context)} floorUid={floorUid} itemUid={pickupItem!.Uid} itemId={pickupItem.ItemId}.")
                        .ConfigureAwait(false);
                    return;
                }
        }
    }

}
