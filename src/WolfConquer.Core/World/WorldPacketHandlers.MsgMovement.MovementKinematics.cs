using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryCalculateMovementPosition(
        CharacterSnapshot character,
        MsgMovementPacket packet,
        out long x,
        out long y,
        out uint angle)
    {
        var direction = packet.Running == MsgMovementMode.Steed
            ? packet.Direction % 24
            : packet.Direction % 8;
        angle = direction;

        var delta = packet.Running == MsgMovementMode.Steed
            ? LegacyMountDeltas[(int)direction]
            : GetLegacyWalkDelta(direction);
        x = character.X + delta.X;
        y = character.Y + delta.Y;
        return x is >= 0 and <= ushort.MaxValue &&
            y is >= 0 and <= ushort.MaxValue;
    }

    private static bool TryGetJumpDestination(
        MsgActionPacket packet,
        out long x,
        out long y)
    {
        var hasTargetPosition = packet.TargetPositionX != 0 || packet.TargetPositionY != 0;
        var rawX = hasTargetPosition ? packet.TargetPositionX : packet.PositionX;
        var rawY = hasTargetPosition ? packet.TargetPositionY : packet.PositionY;

        x = rawX;
        y = rawY;
        return (rawX != 0 || rawY != 0) &&
            rawX <= ushort.MaxValue &&
            rawY <= ushort.MaxValue;
    }

    private static uint DecrementSteedMovementVigor(CharacterSnapshot character)
    {
        var vigor = ReadAttributeUInt64(character.Attributes, "Vigor");
        if (vigor > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(character), vigor, "Legacy steed vigor cannot be outside uint range.");
        }

        return LegacyMovementSideEffects.ApplyMsgMovementSteedVigor((uint)vigor).UpdatedVigor;
    }

    private static uint DecrementJumpRideVigor(CharacterSnapshot character, long legacyDistance)
    {
        var vigor = ReadAttributeUInt64(character.Attributes, "Vigor");
        if (vigor > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(character), vigor, "Legacy ride vigor cannot be outside uint range.");
        }

        if (legacyDistance > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(legacyDistance), legacyDistance, "Legacy jump distance cannot exceed Int16 range.");
        }

        return LegacyMovementSideEffects
            .ApplyMsgDataPlayerJumpRideVigor((uint)vigor, checked((short)legacyDistance))
            .UpdatedVigor;
    }

    private static (int X, int Y) GetLegacyWalkDelta(uint direction)
    {
        return direction switch
        {
            0 => (0, 1),
            1 => (-1, 1),
            2 => (-1, 0),
            3 => (-1, -1),
            4 => (0, -1),
            5 => (1, -1),
            6 => (1, 0),
            7 => (1, 1),
            _ => (0, 0),
        };
    }
}
