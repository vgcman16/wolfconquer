using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static ValueTask SendLegacyFloorItemRemoveAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        LegacyFloorItemPacket packet,
        CancellationToken cancellationToken)
    {
        return SendFloorItemVisibleAsync(
            context,
            character,
            packet with { DropType = LegacyFloorItemDropType.Remove },
            cancellationToken);
    }

    private static async ValueTask SendFloorItemVisibleAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        LegacyFloorItemPacket packet,
        CancellationToken cancellationToken)
    {
        var frame = packet.ToFrameBytes();
        await SendLegacyTeleportFrameAsync(context, context.Connection, frame, cancellationToken).ConfigureAwait(false);

        var sentCount = 1;
        if (context.Sessions is { } sessions)
        {
            foreach (var active in sessions.Snapshot())
            {
                if (ReferenceEquals(active.Connection, context.Connection) ||
                    active.Session.Character.Uid == character.Uid ||
                    !CanSeeFloorItem(active.Session.Character, character.MapId, packet.X, packet.Y))
                {
                    continue;
                }

                await SendLegacyTeleportFrameAsync(context, active.Connection, frame, cancellationToken).ConfigureAwait(false);
                sentCount++;
            }
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyFloorItem " +
                $"{FormatSession(context)} map={character.MapId} floorUid={packet.Uid} itemId={packet.ItemId} playerCount={sentCount}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendVisibleFloorItemsAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        CancellationToken cancellationToken,
        CharacterSnapshot? previousCharacter = null)
    {
        if (context.FloorItems is not { } floorItems)
        {
            return;
        }

        var entries = floorItems.SnapshotVisible(DateTimeOffset.UtcNow);
        if (entries.Count == 0)
        {
            return;
        }

        var sentCount = 0;
        foreach (var entry in entries)
        {
            var packet = entry.Packet;
            if (packet.DropType != LegacyFloorItemDropType.Visible)
            {
                continue;
            }

            if (entry.DynamicId.HasValue)
            {
                if (!CanRefreshScopedFloorItem(context, character, entry, previousCharacter)) continue;
            }
            else
            {
                // Untagged/effect compatibility is deliberately not changed
                // by ordinary-loot scope capture.
                var mapId = entry.MapId == 0 ? character.MapId : entry.MapId;
                if (!CanSeeFloorItem(character, mapId, packet.X, packet.Y) ||
                    previousCharacter is { } previous && CanSeeFloorItem(previous, mapId, packet.X, packet.Y))
                {
                    continue;
                }
            }

            await SendLegacyTeleportFrameAsync(context, context.Connection, packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            sentCount++;
        }

        if (sentCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent visible LegacyFloorItem refresh " +
                    $"{FormatSession(context)} map={character.MapId} floorItemCount={sentCount}.")
                .ConfigureAwait(false);
        }
    }

    private static bool CanSeeFloorItem(CharacterSnapshot character, long mapId, ushort x, ushort y)
    {
        return character.MapId == mapId &&
            character.MapId != LegacyAlienWorldMapId &&
            LegacyGetDistance(character.X, character.Y, x, y) <= LegacyViewThreshold;
    }

    private static uint NextLegacyFloorItemUid()
    {
        return unchecked((uint)Interlocked.Increment(ref s_legacyFloorItemUid));
    }

    private static uint NextLegacyFloorItemUidRangeStart(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Floor item UID range count must be positive.");
        }

        var last = Interlocked.Add(ref s_legacyFloorItemUid, count);
        return unchecked((uint)(last - count + 1));
    }
}
