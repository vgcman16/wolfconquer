using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask TrySendLegacyMoveLineTideTrapAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMoveLineCoordinate landing,
        byte moveLineSpellLevel,
        CancellationToken cancellationToken)
    {
        var character = session.Character;
        var packet = new LegacyFloorItemPacket(
            NextLegacyFloorItemUid(),
            LegacyTideTrapFloorItemId,
            landing.X,
            landing.Y,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            PlusOrColor: 14,
            CheckedUInt32(character.Uid, nameof(character.Uid)),
            GuildId: 0,
            FlowerType: 0,
            Timer: 0,
            OwnerX: CheckedUInt16(character.X, nameof(character.X)),
            OwnerY: CheckedUInt16(character.Y, nameof(character.Y)),
            Name: "trap");

        if (context.FloorItems is { } floorItems)
        {
            var now = DateTimeOffset.UtcNow;
            var effect = new FloorItemRuntimeEffect(
                LegacyTideTrapSpellId,
                packet.OwnerX,
                packet.OwnerY,
                now.AddMilliseconds(LegacyTideTrapDetonationMilliseconds),
                LegacyMoveLineGeometry.LegacyDistance,
                LegacyTideTrapHitRadius,
                SpellLevel: moveLineSpellLevel);
            floorItems.AddVisible(FloorItemRuntimeEntry.ForEffect(character.MapId, packet, effect), now);
        }

        await SendFloorItemVisibleAsync(context, character, packet, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<int> SendLegacyTideTrapRemoveEffectAsync(
        FloorItemRuntimeEntry entry,
        FloorItemRuntimeEffect effect,
        IReadOnlyList<WorldActiveSession> activeSessions,
        CancellationToken cancellationToken)
    {
        if (entry.MapId == 0)
        {
            return 0;
        }

        var packet = entry.Packet with { DropType = LegacyFloorItemDropType.RemoveEffect };
        var frame = packet.ToFrameBytes();
        var sentCount = 0;
        foreach (var active in activeSessions)
        {
            if (active.Session.Character.MapId != entry.MapId ||
                active.Session.Character.MapId == LegacyAlienWorldMapId ||
                LegacyGetDistance(
                    active.Session.Character.X,
                    active.Session.Character.Y,
                    effect.OriginX,
                    effect.OriginY) > LegacyViewThreshold)
            {
                continue;
            }

            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            sentCount++;
        }

        return sentCount;
    }
}
