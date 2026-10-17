using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask SendReciprocalPlayerRemoveAsync(
        WorldPacketHandlerContext context,
        WorldSession movedSessionBeforeMove,
        WorldActiveSession peer,
        CancellationToken cancellationToken)
    {
        var moverRemove = CreatePlayerRemovePacket(peer.Session.Character);
        var peerRemove = CreatePlayerRemovePacket(movedSessionBeforeMove.Character);
        await context.Connection.SendAsync(moverRemove.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await peer.Connection.SendAsync(peerRemove.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
    }

    private static MsgActionPacket CreatePlayerRemovePacket(CharacterSnapshot character)
    {
        return new MsgActionPacket
        {
            ObjId = CheckedUInt32(character.Uid, nameof(character.Uid)),
            Timestamp = unchecked((uint)Environment.TickCount),
            Type = MsgActionType.RemoveEntity,
            Facing = ReadAttributeUInt16(character.Attributes, "Angle"),
            PositionX = CheckedUInt32(character.X, nameof(character.X)),
            PositionY = CheckedUInt32(character.Y, nameof(character.Y)),
        };
    }

    private static async ValueTask SendReciprocalPlayerSpawnAsync(
        WorldPacketHandlerContext context,
        WorldSession movedSession,
        WorldActiveSession peer,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await context.Connection.SendAsync(
                CreatePlayerSpawnPacket(peer.Session, context.WorldData).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyPlayerBlackSpotActiveReplayIfNeededAsync(
                context.Connection,
                peer.Session.Character,
                now,
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var cloneSpawn in CreateShadowCloneSpawnPackets(peer.Session, context.WorldData))
        {
            await context.Connection.SendAsync(cloneSpawn.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var petSpawn in CreateLivePetSpawnPackets(peer.Session, context.ClientCompatibility))
        {
            await context.Connection.SendAsync(petSpawn.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        await peer.Connection.SendAsync(
                CreatePlayerSpawnPacket(movedSession, context.WorldData).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyPlayerBlackSpotActiveReplayIfNeededAsync(
                peer.Connection,
                movedSession.Character,
                now,
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var cloneSpawn in CreateShadowCloneSpawnPackets(movedSession, context.WorldData))
        {
            await peer.Connection.SendAsync(cloneSpawn.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var petSpawn in CreateLivePetSpawnPackets(movedSession, context.ClientCompatibility))
        {
            await peer.Connection.SendAsync(petSpawn.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask SendMovementActivePickRemovalIfNeededAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        bool removedActivePick,
        CancellationToken cancellationToken)
    {
        if (!removedActivePick)
        {
            return;
        }

        var action = new MsgActionPacket
        {
            ObjId = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            DwParam = LegacyActionPickDwParam,
            Timestamp = unchecked((uint)Environment.TickCount),
            Type = MsgActionType.ActionPick,
            Facing = 2,
            RemainTime = 0,
            Strings = ["Interrupted"],
        };

        await context.Connection.SendAsync(action.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent MsgMovement ActivePick interruption " +
                $"{FormatSession(context)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendSteedRaceItemPickupAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        RaceItemPickupResult pickup,
        CancellationToken cancellationToken)
    {
        if (pickup.GameUpdate is { } gameUpdate)
        {
            await context.Connection.SendAsync(gameUpdate.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }

        if (pickup.Potion is { } potion)
        {
            await context.Connection.SendAsync(potion.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyStringPacket.PlayerEffect(CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)), "eidolon")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var action = new MsgActionPacket
        {
            ObjId = pickup.StaticRoleUid,
            Timestamp = unchecked((uint)Environment.TickCount),
            Type = MsgActionType.RemoveEntity,
            Facing = ReadAttributeUInt16(session.Character.Attributes, "Angle"),
            PositionX = CheckedUInt32(session.Character.X, nameof(session.Character.X)),
            PositionY = CheckedUInt32(session.Character.Y, nameof(session.Character.Y)),
        };
        await context.Connection.SendAsync(action.ToFrameBytes(), cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer sent MsgMovement steed race item pickup " +
                $"{FormatSession(context)} staticRoleUid={pickup.StaticRoleUid} " +
                FormatRaceItemPickupLog(pickup))
            .ConfigureAwait(false);
    }

    private static string FormatRaceItemPickupLog(RaceItemPickupResult pickup)
    {
        if (pickup.Potion is { } potion)
        {
            return $"potionType={potion.PotionType} location={potion.Location} amount={potion.Amount} effect=eidolon.";
        }

        if (pickup.GameUpdate is { } gameUpdate)
        {
            return $"potionType={pickup.Type} status={gameUpdate.Type} time={gameUpdate.Time} effect=eidolon.";
        }

        return $"potionType={pickup.Type} effect=eidolon.";
    }

    private static async ValueTask SendNewGuildWarItemPickupsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        IReadOnlyList<NewGuildWarItemPickupResult> pickups,
        CancellationToken cancellationToken)
    {
        foreach (var pickup in pickups)
        {
            var packet = new LegacyFloorItemPacket(
                pickup.Uid,
                pickup.ItemId,
                pickup.X,
                pickup.Y,
                MaxLife: 40,
                LegacyFloorItemDropType.RemoveEffect,
                Life: 0,
                PlusOrColor: 0,
                ItemOwnerUid: 0,
                GuildId: 0,
                FlowerType: 0,
                Timer: 0,
                OwnerX: 0,
                OwnerY: 0,
                pickup.Name);
            var frame = packet.ToFrameBytes();
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);

            var sentCount = 1;
            if (context.Sessions is { } sessions)
            {
                foreach (var active in sessions.Snapshot())
                {
                    if (ReferenceEquals(active.Connection, context.Connection) ||
                        active.Session.Character.Uid == session.Character.Uid ||
                        !CanSeeFloorItem(active.Session.Character, session.Character.MapId, pickup.X, pickup.Y))
                    {
                        continue;
                    }

                    await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                    sentCount++;
                }
            }

            await context.Log.WriteLineAsync(
                    "WorldServer sent MsgMovement NEWGUILDWAR item pickup " +
                    $"{FormatSession(context)} floorUid={pickup.Uid} itemId={pickup.ItemId} " +
                    $"points={pickup.Points} totalPoints={pickup.TotalPoints} playerCount={sentCount} " +
                    $"message=STR_ID_tNewGuildWars[Talk][UserScore]@@{pickup.TotalPoints}@@.")
                .ConfigureAwait(false);
        }
    }
}
