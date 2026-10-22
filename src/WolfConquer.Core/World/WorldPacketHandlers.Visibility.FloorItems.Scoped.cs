using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static ValueTask SendLegacyFloorItemRemoveAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        FloorItemRuntimeEntry entry,
        CancellationToken cancellationToken)
    {
        // The floor retains its birth scope even if the picker has moved or is
        // in another DynamicID. Native pickup lookup itself does not check it.
        return SendFloorItemVisibleAsync(context, character,
            entry with { Packet = entry.Packet with { DropType = LegacyFloorItemDropType.Remove } },
            cancellationToken);
    }

    private static async ValueTask SendFloorItemVisibleAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        FloorItemRuntimeEntry entry,
        CancellationToken cancellationToken)
    {
        if (!entry.DynamicId.HasValue)
        {
            // Existing untagged/effect-only producers remain compatibility
            // paths, not a claim of native floor-instance ownership.
            await SendFloorItemVisibleAsync(context, character, entry.Packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        var frame = entry.Packet.ToFrameBytes();
        var sentCount = 0;
        if (LegacyFloorItemVisibility.CanBroadcast(character, entry))
        {
            await SendLegacyTeleportFrameAsync(context, context.Connection, frame, cancellationToken).ConfigureAwait(false);
            sentCount++;
        }

        if (context.Sessions is { } sessions)
        {
            foreach (var active in sessions.Snapshot())
            {
                if (ReferenceEquals(active.Connection, context.Connection) ||
                    active.Session.Character.Uid == character.Uid ||
                    !LegacyFloorItemVisibility.CanBroadcast(active.Session.Character, entry))
                {
                    continue;
                }

                await SendLegacyTeleportFrameAsync(context, active.Connection, frame, cancellationToken).ConfigureAwait(false);
                sentCount++;
            }
        }

        await context.Log.WriteLineAsync("WorldServer sent LegacyFloorItem " +
            $"{FormatSession(context)} map={entry.MapId} dynamicId={entry.DynamicId} floorUid={entry.Packet.Uid} " +
            $"itemId={entry.Packet.ItemId} playerCount={sentCount}.").ConfigureAwait(false);
    }

    private static bool CanRefreshScopedFloorItem(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        FloorItemRuntimeEntry entry,
        CharacterSnapshot? previousCharacter)
    {
        if (!LegacyFloorItemVisibility.CanRefresh(character, entry)) return false;

        // RoleView's ordinary-item branch: map 4006 only refreshes ID 810.
        // Other maps consult the real physical player when one is present;
        // absent native owners retain fresh-player false compatibility only.
        if (character.MapId == 4006)
        {
            if (entry.Packet.ItemId != 810) return false;
        }
        else if (context.Session?.NativePlayer?.SpawinEffect == true) return false;

        // Player.InView is geometric: old distance >18, current <=18. It does
        // not reinterpret the old position using the viewer's previous scope.
        // A caller without a previous position is the existing forced refresh.
        return previousCharacter is not { } previous ||
            LegacyGetDistance(previous.X, previous.Y, entry.Packet.X, entry.Packet.Y) > 18;
    }
}
