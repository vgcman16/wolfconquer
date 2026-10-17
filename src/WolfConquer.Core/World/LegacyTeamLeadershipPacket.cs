using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyTeamLeadershipMode : uint
{
    Leader = 1,
    Teammate = 2
}

public readonly record struct LegacyTeamLeadershipPacket(
    LegacyTeamLeadershipMode Mode,
    uint Uid,
    uint LeaderUid,
    int Count,
    uint Unknown)
{
    public const ushort Opcode = WorldPacketOpcodes.LegacyTeamLeadership;
    public const int PayloadLength = sizeof(uint) * 5;

    private const int ModeOffset = 0;
    private const int UidOffset = 4;
    private const int LeaderUidOffset = 8;
    private const int CountOffset = 12;
    private const int UnknownOffset = 16;

    public static bool TryParse(PacketFrame frame, out LegacyTeamLeadershipPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyTeamLeadershipPacket(
            (LegacyTeamLeadershipMode)reader.ReadUInt32(ModeOffset),
            reader.ReadUInt32(UidOffset),
            reader.ReadUInt32(LeaderUidOffset),
            reader.ReadInt32(CountOffset),
            reader.ReadUInt32(UnknownOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(ModeOffset, (uint)Mode);
        writer.WriteUInt32(UidOffset, Uid);
        writer.WriteUInt32(LeaderUidOffset, LeaderUid);
        writer.WriteInt32(CountOffset, Count);
        writer.WriteUInt32(UnknownOffset, Unknown);
        return writer.ToArray();
    }
}
