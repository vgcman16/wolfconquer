using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyElitePkMatchStatsPacket(
    uint UidOne,
    string NameOne,
    uint ServerIdOne,
    uint PointsOne,
    uint UidTwo,
    string NameTwo,
    uint ServerIdTwo,
    uint PointsTwo,
    uint Reserved)
{
    private static readonly Encoding NameEncoding = Encoding.UTF8;

    private const int UidOneOffset = 0;
    private const int NameOneOffset = UidOneOffset + sizeof(uint);
    private const int ServerIdOneOffset = NameOneOffset + FixedNameLength;
    private const int PointsOneOffset = ServerIdOneOffset + sizeof(uint);
    private const int UidTwoOffset = PointsOneOffset + sizeof(uint);
    private const int NameTwoOffset = UidTwoOffset + sizeof(uint);
    private const int ServerIdTwoOffset = NameTwoOffset + FixedNameLength;
    private const int PointsTwoOffset = ServerIdTwoOffset + sizeof(uint);
    private const int ReservedOffset = PointsTwoOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyElitePkMatchStats;
    public const int FixedNameLength = 32;
    public const int PayloadLength = ReservedOffset + sizeof(uint);

    public static LegacyElitePkMatchStatsPacket Create(
        uint uidOne,
        string nameOne,
        uint serverIdOne,
        uint pointsOne,
        uint uidTwo,
        string nameTwo,
        uint serverIdTwo,
        uint pointsTwo)
    {
        return new LegacyElitePkMatchStatsPacket(
            uidOne,
            nameOne,
            serverIdOne,
            pointsOne,
            uidTwo,
            nameTwo,
            serverIdTwo,
            pointsTwo,
            Reserved: 0);
    }

    public static bool TryParse(PacketFrame frame, out LegacyElitePkMatchStatsPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyElitePkMatchStatsPacket(
            reader.ReadUInt32(UidOneOffset),
            reader.ReadCString(NameOneOffset, FixedNameLength, NameEncoding),
            reader.ReadUInt32(ServerIdOneOffset),
            reader.ReadUInt32(PointsOneOffset),
            reader.ReadUInt32(UidTwoOffset),
            reader.ReadCString(NameTwoOffset, FixedNameLength, NameEncoding),
            reader.ReadUInt32(ServerIdTwoOffset),
            reader.ReadUInt32(PointsTwoOffset),
            reader.ReadUInt32(ReservedOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(UidOneOffset, UidOne);
        WriteFixedString(writer.Payload.Slice(NameOneOffset, FixedNameLength), NameOne);
        writer.WriteUInt32(ServerIdOneOffset, ServerIdOne);
        writer.WriteUInt32(PointsOneOffset, PointsOne);
        writer.WriteUInt32(UidTwoOffset, UidTwo);
        WriteFixedString(writer.Payload.Slice(NameTwoOffset, FixedNameLength), NameTwo);
        writer.WriteUInt32(ServerIdTwoOffset, ServerIdTwo);
        writer.WriteUInt32(PointsTwoOffset, PointsTwo);
        writer.WriteUInt32(ReservedOffset, Reserved);
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
