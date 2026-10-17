using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum MsgTeamType : uint
{
    Create = 0,
    JoinRequest = 1,
    ExitTeam = 2,
    AcceptInvitation = 3,
    InviteRequest = 4,
    AcceptJoinRequest = 5,
    Dismiss = 6,
    Kick = 7,
    ForbidJoining = 8,
    UnforbidJoining = 9,
    LootMoneyOff = 10,
    LootMoneyOn = 11,
    LootItemsOff = 12,
    LootItemsOn = 13,
    RejectInvitation = 14,
    Send = 15
}

public readonly record struct MsgTeamPacket(MsgTeamType Type, uint Uid)
{
    public const ushort Opcode = WorldPacketOpcodes.MsgTeam;
    public const int PayloadLength = sizeof(uint) + sizeof(uint);

    public static bool TryParse(PacketFrame frame, out MsgTeamPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new MsgTeamPacket(
            (MsgTeamType)reader.ReadUInt32(0),
            reader.ReadUInt32(4));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(0, (uint)Type);
        writer.WriteUInt32(4, Uid);
        return writer.ToArray();
    }
}
