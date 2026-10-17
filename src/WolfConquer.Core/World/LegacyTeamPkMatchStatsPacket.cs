using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyTeamPkMatchStatsPacket(
    uint LeaderUidOne,
    uint TeamUidOne,
    string LeaderNameOne,
    string TeamNameOne,
    uint PointsOne,
    uint LeaderUidTwo,
    uint TeamUidTwo,
    string LeaderNameTwo,
    string TeamNameTwo,
    uint PointsTwo)
{
    private static readonly Encoding NameEncoding = Encoding.UTF8;

    private const int LeaderUidOneOffset = 0;
    private const int TeamUidOneOffset = LeaderUidOneOffset + sizeof(uint);
    private const int LeaderNameOneOffset = TeamUidOneOffset + sizeof(uint);
    private const int TeamNameOneOffset = LeaderNameOneOffset + FixedLeaderNameLength;
    private const int PointsOneOffset = TeamNameOneOffset + FixedTeamNameLength;
    private const int LeaderUidTwoOffset = PointsOneOffset + sizeof(uint);
    private const int TeamUidTwoOffset = LeaderUidTwoOffset + sizeof(uint);
    private const int LeaderNameTwoOffset = TeamUidTwoOffset + sizeof(uint);
    private const int TeamNameTwoOffset = LeaderNameTwoOffset + FixedLeaderNameLength;
    private const int PointsTwoOffset = TeamNameTwoOffset + FixedTeamNameLength;

    public const ushort Opcode = WorldPacketOpcodes.LegacyTeamPkMatchStats;
    public const int FixedLeaderNameLength = 32;
    public const int FixedTeamNameLength = 64;
    public const int TeamPayloadLength = PointsOneOffset + sizeof(uint);
    public const int PayloadLength = PointsTwoOffset + sizeof(uint);

    public static LegacyTeamPkMatchStatsPacket Create(
        uint leaderUidOne,
        uint teamUidOne,
        string leaderNameOne,
        string teamNameOne,
        uint pointsOne,
        uint leaderUidTwo,
        uint teamUidTwo,
        string leaderNameTwo,
        string teamNameTwo,
        uint pointsTwo)
    {
        return new LegacyTeamPkMatchStatsPacket(
            leaderUidOne,
            teamUidOne,
            leaderNameOne,
            teamNameOne,
            pointsOne,
            leaderUidTwo,
            teamUidTwo,
            leaderNameTwo,
            teamNameTwo,
            pointsTwo);
    }

    public static bool TryParse(PacketFrame frame, out LegacyTeamPkMatchStatsPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyTeamPkMatchStatsPacket(
            reader.ReadUInt32(LeaderUidOneOffset),
            reader.ReadUInt32(TeamUidOneOffset),
            reader.ReadCString(LeaderNameOneOffset, FixedLeaderNameLength, NameEncoding),
            reader.ReadCString(TeamNameOneOffset, FixedTeamNameLength, NameEncoding),
            reader.ReadUInt32(PointsOneOffset),
            reader.ReadUInt32(LeaderUidTwoOffset),
            reader.ReadUInt32(TeamUidTwoOffset),
            reader.ReadCString(LeaderNameTwoOffset, FixedLeaderNameLength, NameEncoding),
            reader.ReadCString(TeamNameTwoOffset, FixedTeamNameLength, NameEncoding),
            reader.ReadUInt32(PointsTwoOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(LeaderUidOneOffset, LeaderUidOne);
        writer.WriteUInt32(TeamUidOneOffset, TeamUidOne);
        WriteFixedString(writer.Payload.Slice(LeaderNameOneOffset, FixedLeaderNameLength), LeaderNameOne);
        WriteFixedString(writer.Payload.Slice(TeamNameOneOffset, FixedTeamNameLength), TeamNameOne);
        writer.WriteUInt32(PointsOneOffset, PointsOne);
        writer.WriteUInt32(LeaderUidTwoOffset, LeaderUidTwo);
        writer.WriteUInt32(TeamUidTwoOffset, TeamUidTwo);
        WriteFixedString(writer.Payload.Slice(LeaderNameTwoOffset, FixedLeaderNameLength), LeaderNameTwo);
        WriteFixedString(writer.Payload.Slice(TeamNameTwoOffset, FixedTeamNameLength), TeamNameTwo);
        writer.WriteUInt32(PointsTwoOffset, PointsTwo);
        return writer.ToArray();
    }

    private static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var encoder = NameEncoding.GetEncoder();
        encoder.Convert(value.AsSpan(), destination, flush: true, out _, out _, out _);
    }
}
