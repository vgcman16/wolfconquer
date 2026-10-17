using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyTeamArenaMatchScorePacket(
    uint UidOne,
    uint RankOne,
    string NameOne,
    uint DamageOne,
    uint UidTwo,
    uint RankTwo,
    string NameTwo,
    uint DamageTwo,
    uint Result)
{
    private static readonly Encoding NameEncoding = Encoding.UTF8;

    private const int UidOneOffset = 0;
    private const int RankOneOffset = UidOneOffset + sizeof(uint);
    private const int NameOneOffset = RankOneOffset + sizeof(uint);
    private const int DamageOneOffset = NameOneOffset + FixedNameLength;
    private const int UidTwoOffset = DamageOneOffset + sizeof(uint);
    private const int RankTwoOffset = UidTwoOffset + sizeof(uint);
    private const int NameTwoOffset = RankTwoOffset + sizeof(uint);
    private const int DamageTwoOffset = NameTwoOffset + FixedNameLength;
    private const int ResultOffset = DamageTwoOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyTeamArenaMatchScore;
    public const int FixedNameLength = 32;
    public const int PayloadLength = ResultOffset + sizeof(uint);
    public const uint LegacyResult = 1;

    public static LegacyTeamArenaMatchScorePacket Create(
        uint uidOne,
        uint rankOne,
        string nameOne,
        uint damageOne,
        uint uidTwo,
        uint rankTwo,
        string nameTwo,
        uint damageTwo)
    {
        return new LegacyTeamArenaMatchScorePacket(
            uidOne,
            rankOne,
            nameOne,
            damageOne,
            uidTwo,
            rankTwo,
            nameTwo,
            damageTwo,
            LegacyResult);
    }

    public static bool TryParse(PacketFrame frame, out LegacyTeamArenaMatchScorePacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyTeamArenaMatchScorePacket(
            reader.ReadUInt32(UidOneOffset),
            reader.ReadUInt32(RankOneOffset),
            reader.ReadCString(NameOneOffset, FixedNameLength, NameEncoding),
            reader.ReadUInt32(DamageOneOffset),
            reader.ReadUInt32(UidTwoOffset),
            reader.ReadUInt32(RankTwoOffset),
            reader.ReadCString(NameTwoOffset, FixedNameLength, NameEncoding),
            reader.ReadUInt32(DamageTwoOffset),
            reader.ReadUInt32(ResultOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(UidOneOffset, UidOne);
        writer.WriteUInt32(RankOneOffset, RankOne);
        WriteFixedString(writer.Payload.Slice(NameOneOffset, FixedNameLength), NameOne);
        writer.WriteUInt32(DamageOneOffset, DamageOne);
        writer.WriteUInt32(UidTwoOffset, UidTwo);
        writer.WriteUInt32(RankTwoOffset, RankTwo);
        WriteFixedString(writer.Payload.Slice(NameTwoOffset, FixedNameLength), NameTwo);
        writer.WriteUInt32(DamageTwoOffset, DamageTwo);
        writer.WriteUInt32(ResultOffset, Result);
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
