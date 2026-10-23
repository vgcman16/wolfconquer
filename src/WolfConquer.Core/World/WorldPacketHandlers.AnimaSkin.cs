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
    private const string RuntimeAnimaSkinLoadedAttribute = "RuntimeAnimaSkinLoaded";
    private const string LegacyAnimaSkinAttribute = "AnimaSkin";
    private const uint LegacyDragonSkinOneItemId = 4_200_018;
    private const uint LegacyDragonSkinTwoItemId = 4_200_016;
    private const uint LegacyDragonSkinThreeItemId = 4_200_017;

    [PacketHandler(WorldPacketOpcodes.LegacyAnimaSkin)]
    private static async ValueTask HandleLegacyDragonSkinAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyDragonSkinPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyDragonSkin packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyDragonSkin " +
                $"{FormatSession(context)} type={packet.Type} userId={packet.UserID} " +
                $"skinType={packet.SkinType} ok={packet.OK}.")
            .ConfigureAwait(false);

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyDragonSkin {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        switch (packet.Type)
        {
            case LegacyDragonSkinType.MyShow:
                await HandleLegacyDragonSkinMyShowAsync(context, session, cancellationToken).ConfigureAwait(false);
                break;

            case LegacyDragonSkinType.PlayerView:
                await HandleLegacyDragonSkinPlayerViewAsync(context, packet, session, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyDragonSkinType.Select:
                await HandleLegacyDragonSkinSelectAsync(context, packet, session, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyDragonSkinType.Restore:
                await HandleLegacyDragonSkinRestoreAsync(context, session, cancellationToken).ConfigureAwait(false);
                break;

            case LegacyDragonSkinType.Unlock:
                await HandleLegacyDragonSkinUnlockAsync(context, packet, session, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyDragonSkinType.Retrieve:
                await HandleLegacyDragonSkinRetrieveAsync(context, packet, session, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyDragonSkinType.Effect:
                await HandleLegacyDragonSkinEffectAsync(context, packet).ConfigureAwait(false);
                break;
        }
    }

    private static async ValueTask HandleLegacyDragonSkinMyShowAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var state = LegacyDragonSkinState.FromSnapshot(session.Character);
        var userId = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var sentUnlocks = 0;

        if (!ReadAttributeBool(session.Character.Attributes, RuntimeAnimaSkinLoadedAttribute))
        {
            foreach (var skinType in state.OwnedSkins)
            {
                await context.Connection.SendAsync(
                        new LegacyDragonSkinPacket(
                            LegacyDragonSkinType.Unlock,
                            userId,
                            skinType,
                            OK: true).ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                sentUnlocks++;
            }

            context.Sessions?.Set(
                context.Connection,
                session with
                {
                    Character = WithCharacterAttribute(
                        session.Character,
                        RuntimeAnimaSkinLoadedAttribute,
                        "1"),
                });
        }

        await context.Connection.SendAsync(
                new LegacyDragonSkinPacket(
                    LegacyDragonSkinType.MyShow,
                    userId,
                    state.EquippedSkin,
                    OK: false).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyDragonSkin MyShow " +
                $"{FormatSession(context)} skinType={state.EquippedSkin} unlocks={sentUnlocks}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyDragonSkinPlayerViewAsync(
        WorldPacketHandlerContext context,
        LegacyDragonSkinPacket packet,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var selfUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var targetUserId = packet.UserID == 0 ? selfUid : packet.UserID;
        var targetSession = targetUserId == selfUid
            ? session
            : TryGetSessionByUid(context, targetUserId, out var active)
                ? active.Session
                : null;

        if (targetSession is null)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyDragonSkin PlayerView {FormatSession(context)} userId={packet.UserID}; target session was not found.")
                .ConfigureAwait(false);
            return;
        }

        var targetState = LegacyDragonSkinState.FromSnapshot(targetSession.Character);
        await context.Connection.SendAsync(
                new LegacyDragonSkinPacket(
                    LegacyDragonSkinType.PlayerView,
                    targetUserId,
                    targetState.EquippedSkin,
                    OK: false).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyDragonSkin PlayerView " +
                $"{FormatSession(context)} userId={targetUserId} skinType={targetState.EquippedSkin}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyDragonSkinSelectAsync(
        WorldPacketHandlerContext context,
        LegacyDragonSkinPacket packet,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var state = LegacyDragonSkinState.FromSnapshot(session.Character);
        if (!state.Owns(packet.SkinType))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyDragonSkin Select {FormatSession(context)} skinType={packet.SkinType}; skin is not owned.")
                .ConfigureAwait(false);
            return;
        }

        var userId = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var updatedState = state.WithEquipped(packet.SkinType);
        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            LegacyAnimaSkinAttribute,
            updatedState.ToSnapshotValue());
        context.Sessions?.Set(context.Connection, session with { Character = updatedCharacter });

        await context.Connection.SendAsync(
                new LegacyDragonSkinPacket(
                    LegacyDragonSkinType.MyShow,
                    userId,
                    packet.SkinType,
                    OK: false).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer selected LegacyDragonSkin {FormatSession(context)} skinType={packet.SkinType}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyDragonSkinRestoreAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var state = LegacyDragonSkinState.FromSnapshot(session.Character);
        var restoredState = state.Restore();
        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            LegacyAnimaSkinAttribute,
            restoredState.ToSnapshotValue());
        context.Sessions?.Set(context.Connection, session with { Character = updatedCharacter });

        await context.Connection.SendAsync(
                new LegacyDragonSkinPacket(
                    LegacyDragonSkinType.Restore,
                    CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
                    SkinType: 0,
                    OK: state.OwnedSkins.Count > 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer restored LegacyDragonSkin {FormatSession(context)} owned={state.OwnedSkins.Count}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyDragonSkinUnlockAsync(
        WorldPacketHandlerContext context,
        LegacyDragonSkinPacket packet,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var state = LegacyDragonSkinState.FromSnapshot(session.Character);
        if (state.Owns(packet.SkinType))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyDragonSkin Unlock {FormatSession(context)} skinType={packet.SkinType}; skin is already owned.")
                .ConfigureAwait(false);
            return;
        }

        var userId = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var ok = false;
        if (TryGetLegacyDragonSkinItemId(packet.SkinType, out var itemId))
        {
            var unlockItem = session.PlayerItems.InventoryItems.FirstOrDefault(item =>
                item.ItemId == itemId && item.Bound == 0 && item.StackSize > 0);
            if (unlockItem is null)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyDragonSkin Unlock " +
                        $"{FormatSession(context)} skinType={packet.SkinType}; itemId={itemId} has no positive unbound inventory stack.")
                    .ConfigureAwait(false);
            }
            else
            {
                var updatedItems = ConsumeSingleLegacyInventoryItem(session.PlayerItems, unlockItem, out var consumeFrame);
                var updatedState = state.AddOwned(packet.SkinType);
                var updatedCharacter = WithCharacterAttribute(
                    session.Character,
                    LegacyAnimaSkinAttribute,
                    updatedState.ToSnapshotValue());
                var updatedSession = session with
                {
                    Character = updatedCharacter,
                    PlayerItems = updatedItems,
                };
                context.Sessions?.Set(context.Connection, updatedSession);

                await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);
                await TryPersistLegacyDragonSkinStateAsync(context, updatedSession).ConfigureAwait(false);

                ok = true;
                await context.Log.WriteLineAsync(
                        "WorldServer unlocked LegacyDragonSkin " +
                        $"{FormatSession(context with { Session = updatedSession })} skinType={packet.SkinType} itemId={itemId}.")
                    .ConfigureAwait(false);
            }
        }
        else
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyDragonSkin Unlock {FormatSession(context)} skinType={packet.SkinType}; skin has no legacy item mapping.")
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                new LegacyDragonSkinPacket(
                    LegacyDragonSkinType.Unlock,
                    userId,
                    packet.SkinType,
                    ok).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyDragonSkinRetrieveAsync(
        WorldPacketHandlerContext context,
        LegacyDragonSkinPacket packet,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var state = LegacyDragonSkinState.FromSnapshot(session.Character);
        var userId = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var ok = false;

        if (!state.Owns(packet.SkinType))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyDragonSkin Retrieve {FormatSession(context)} skinType={packet.SkinType}; skin is not owned.")
                .ConfigureAwait(false);
        }
        else if (!TryGetLegacyDragonSkinItemId(packet.SkinType, out var itemId))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyDragonSkin Retrieve {FormatSession(context)} skinType={packet.SkinType}; skin has no legacy item mapping.")
                .ConfigureAwait(false);
        }
        else if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyDragonSkin Retrieve {FormatSession(context)} skinType={packet.SkinType}; inventory is full.")
                .ConfigureAwait(false);
        }
        else
        {
            var retrievedItem = CreateLegacyDragonSkinRetrievedItem(context, session, itemId);
            var updatedState = state.RemoveOwned(packet.SkinType);
            var updatedCharacter = WithCharacterAttribute(
                session.Character,
                LegacyAnimaSkinAttribute,
                updatedState.ToSnapshotValue());
            var updatedItems = session.PlayerItems with
            {
                InventoryItems = session.PlayerItems.InventoryItems.Concat([retrievedItem]).ToArray(),
            };
            var updatedSession = session with
            {
                Character = updatedCharacter,
                PlayerItems = updatedItems,
            };
            context.Sessions?.Set(context.Connection, updatedSession);

            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(retrievedItem).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await TryPersistLegacyDragonSkinStateAsync(context, updatedSession).ConfigureAwait(false);

            ok = true;
            await context.Log.WriteLineAsync(
                    "WorldServer retrieved LegacyDragonSkin " +
                    $"{FormatSession(context with { Session = updatedSession })} skinType={packet.SkinType} itemId={itemId}.")
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                new LegacyDragonSkinPacket(
                    LegacyDragonSkinType.Retrieve,
                    userId,
                    packet.SkinType,
                    ok).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyDragonSkinEffectAsync(
        WorldPacketHandlerContext context,
        LegacyDragonSkinPacket packet)
    {
        await context.Log.WriteLineAsync(
                "WorldServer ignored LegacyDragonSkin Effect " +
                $"{FormatSession(context)} userId={packet.UserID} skinType={packet.SkinType}; legacy source has no inbound Effect branch.")
            .ConfigureAwait(false);
    }

    private static PlayerItemSnapshotItem CreateLegacyDragonSkinRetrievedItem(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint itemId)
    {
        var durability = TryReadItemType(context.WorldData, itemId, out var itemType)
            ? checked((ushort)itemType.LegacyDurability)
            : (ushort)0;

        return new PlayerItemSnapshotItem
        {
            Uid = GetNextSessionItemUid(session.PlayerItems),
            ItemId = itemId,
            Durability = durability,
            MaximumDurability = durability,
            Position = 0,
            StackSize = 1,
        };
    }

    private static bool TryGetLegacyDragonSkinItemId(uint skinType, out uint itemId)
    {
        itemId = skinType switch
        {
            1 => LegacyDragonSkinOneItemId,
            2 => LegacyDragonSkinTwoItemId,
            3 => LegacyDragonSkinThreeItemId,
            _ => 0,
        };
        return itemId != 0;
    }

    private static async ValueTask TryPersistLegacyDragonSkinStateAsync(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        if (!session.Character.Attributes.TryGetValue(LegacyAnimaSkinAttribute, out var animaSkin))
        {
            return;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [LegacyAnimaSkinAttribute] = animaSkin,
        };

        if (!TryPersistLegacyCharacterAttributes(session.UsersRoot, session.AccountId, values, out var path, out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyDragonSkin persistence " +
                    $"{FormatSession(context with { Session = session })} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyDragonSkin " +
                $"{FormatSession(context with { Session = session })} path={Path.GetFileName(path)} keys=AnimaSkin.")
            .ConfigureAwait(false);
    }

}
