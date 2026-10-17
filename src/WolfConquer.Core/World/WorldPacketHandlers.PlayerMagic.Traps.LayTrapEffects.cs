using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<int> SendLegacyLayTrapRemoveEffectAsync(
        FloorItemRuntimeEntry entry,
        FloorItemRuntimeEffect effect,
        IReadOnlyList<WorldActiveSession> activeSessions,
        CancellationToken cancellationToken,
        ushort? visibilityCenterX = null,
        ushort? visibilityCenterY = null)
    {
        if (entry.MapId == 0)
        {
            return 0;
        }

        byte[]? actionFrame = null;
        if (effect.RemovalActionType != 0)
        {
            var action = new MsgActionPacket
            {
                ObjId = entry.Packet.ItemOwnerUid,
                TargetPositionX = entry.Packet.X,
                TargetPositionY = entry.Packet.Y,
                PositionX = entry.Packet.OwnerX,
                PositionY = entry.Packet.OwnerY,
                Type = (MsgActionType)effect.RemovalActionType,
            };
            actionFrame = action.ToFrameBytes();
        }

        var removeFrame = (entry.Packet with { DropType = LegacyFloorItemDropType.RemoveEffect }).ToFrameBytes();
        var centerX = visibilityCenterX ?? entry.Packet.X;
        var centerY = visibilityCenterY ?? entry.Packet.Y;
        var sentCount = 0;
        foreach (var active in activeSessions)
        {
            if (active.Session.Character.MapId != entry.MapId ||
                active.Session.Character.MapId == LegacyAlienWorldMapId ||
                LegacyGetDistance(
                    active.Session.Character.X,
                    active.Session.Character.Y,
                    centerX,
                    centerY) > effect.RemovalVisibilityRadius)
            {
                continue;
            }

            if (actionFrame is not null)
            {
                await active.Connection.SendAsync(actionFrame, cancellationToken).ConfigureAwait(false);
            }

            await active.Connection.SendAsync(removeFrame, cancellationToken).ConfigureAwait(false);
            sentCount++;
        }

        return sentCount;
    }

    private static bool IsWithinLegacyLayTrapHitRadius(
        FloorItemRuntimeEntry entry,
        ushort x,
        ushort y)
    {
        return entry.Effect is { } effect &&
            LegacyGetDistance(entry.Packet.X, entry.Packet.Y, x, y) <= effect.HitRadius;
    }
}
