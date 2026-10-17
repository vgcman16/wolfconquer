using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyRemoveEntityPacket(
    uint Uid,
    uint Timestamp = 0,
    ushort Facing = 0,
    uint PositionX = 0,
    uint PositionY = 0)
{
    public const ushort Opcode = MsgActionPacket.Opcode;

    public static LegacyRemoveEntityPacket FromEntity(uint uid)
    {
        return new LegacyRemoveEntityPacket(uid);
    }

    public static LegacyRemoveEntityPacket FromPositionedEntity(
        uint uid,
        uint timestamp,
        ushort facing,
        uint positionX,
        uint positionY)
    {
        return new LegacyRemoveEntityPacket(uid, timestamp, facing, positionX, positionY);
    }

    public MsgActionPacket ToActionPacket()
    {
        return new MsgActionPacket
        {
            ObjId = Uid,
            Timestamp = Timestamp,
            Type = MsgActionType.RemoveEntity,
            Facing = Facing,
            PositionX = PositionX,
            PositionY = PositionY,
        };
    }

    public byte[] ToFrameBytes()
    {
        return ToActionPacket().ToFrameBytes();
    }

    public static bool TryParse(PacketFrame frame, out LegacyRemoveEntityPacket packet)
    {
        packet = default!;

        if (!MsgActionPacket.TryParse(frame, out var action) ||
            action.Type != MsgActionType.RemoveEntity ||
            !HasOnlyRemoveEntityFields(action) ||
            !HasSupportedRemovePosition(action))
        {
            return false;
        }

        packet = new LegacyRemoveEntityPacket(
            action.ObjId,
            action.Timestamp,
            action.Facing,
            action.PositionX,
            action.PositionY);
        return true;
    }

    private static bool HasOnlyRemoveEntityFields(MsgActionPacket action)
    {
        return action.TargetUid == 0 &&
            action.DwParam == 0 &&
            action.DwParam2 == 0 &&
            action.DwParam3 == 0 &&
            !action.SucDone &&
            action.TargetPositionX == 0 &&
            action.TargetPositionY == 0 &&
            action.NpcId == 0 &&
            action.CheatLogData1 == 0 &&
            action.CheatLogData2 == 0 &&
            action.MapId == 0 &&
            action.CheatLogData3 == 0 &&
            action.ScheduleTime == 0 &&
            action.LinkIndex == 0 &&
            action.CheatLogData4 == 0 &&
            action.RemainTime == 0 &&
            !action.MountCharge &&
            action.Strings.Count == 0 &&
            action.UnknownFields.Count == 0;
    }

    private static bool HasSupportedRemovePosition(MsgActionPacket action)
    {
        var hasDefaultPosition = action.Timestamp == 0 &&
            action.Facing == 0 &&
            action.PositionX == 0 &&
            action.PositionY == 0;
        var hasPositionedRemove = action.PositionX != 0 &&
            action.PositionY != 0;
        return hasDefaultPosition || hasPositionedRemove;
    }
}
