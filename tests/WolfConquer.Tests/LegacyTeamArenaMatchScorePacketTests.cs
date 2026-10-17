using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeamArenaMatchScorePacketTests
{
    [Fact]
    public void LegacyTeamArenaMatchScoreOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2190, WorldPacketOpcodes.LegacyTeamArenaMatchScore);
        Assert.Equal(WorldPacketOpcodes.LegacyTeamArenaMatchScore, LegacyTeamArenaMatchScorePacket.Opcode);
    }

    [Fact]
    public void RoundTripsSourceShapedMatchScore()
    {
        var packet = LegacyTeamArenaMatchScorePacket.Create(
            uidOne: 1_293_600,
            rankOne: 3,
            nameOne: "HeroOne",
            damageOne: 123_456,
            uidTwo: 400_001,
            rankTwo: 7,
            nameTwo: "Partner",
            damageTwo: 65_432);

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyTeamArenaMatchScore, frame.Header.Opcode);
        Assert.Equal(LegacyTeamArenaMatchScorePacket.PayloadLength, frame.Payload.Length);
        Assert.True(LegacyTeamArenaMatchScorePacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void PayloadLengthAndFieldOrderMatchLegacyBuilderShape()
    {
        var packet = new LegacyTeamArenaMatchScorePacket(
            UidOne: 0x11121314,
            RankOne: 0x21222324,
            NameOne: "FirstName",
            DamageOne: 0x31323334,
            UidTwo: 0x41424344,
            RankTwo: 0x51525354,
            NameTwo: "SecondName",
            DamageTwo: 0x61626364,
            Result: LegacyTeamArenaMatchScorePacket.LegacyResult);

        var frame = ReadFrame(packet.ToFrameBytes());
        var payload = frame.Payload.ToArray();

        Assert.Equal(32, LegacyTeamArenaMatchScorePacket.FixedNameLength);
        Assert.Equal(92, LegacyTeamArenaMatchScorePacket.PayloadLength);
        Assert.Equal(92, frame.Payload.Length);
        Assert.Equal(0x11121314U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0)));
        Assert.Equal(0x21222324U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(4)));
        Assert.Equal("FirstName", Encoding.UTF8.GetString(payload.AsSpan(8, "FirstName".Length)));
        Assert.All(payload.AsSpan(8 + "FirstName".Length, 32 - "FirstName".Length).ToArray(), b => Assert.Equal(0, b));
        Assert.Equal(0x31323334U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(40)));
        Assert.Equal(0x41424344U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(44)));
        Assert.Equal(0x51525354U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(48)));
        Assert.Equal("SecondName", Encoding.UTF8.GetString(payload.AsSpan(52, "SecondName".Length)));
        Assert.All(payload.AsSpan(52 + "SecondName".Length, 32 - "SecondName".Length).ToArray(), b => Assert.Equal(0, b));
        Assert.Equal(0x61626364U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(84)));
        Assert.Equal(1U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(88)));
    }

    [Fact]
    public void FixedNamesAreUtf8PaddedAndTruncated()
    {
        var packet = LegacyTeamArenaMatchScorePacket.Create(
            uidOne: 1,
            rankOne: 2,
            nameOne: "ABCDEFGHIJKLMNOPQRSTUVWX1234567890",
            damageOne: 3,
            uidTwo: 4,
            rankTwo: 5,
            nameTwo: "Blue",
            damageTwo: 6);

        var frame = ReadFrame(packet.ToFrameBytes());
        var payload = frame.Payload.ToArray();

        Assert.Equal("ABCDEFGHIJKLMNOPQRSTUVWX12345678", Encoding.UTF8.GetString(payload.AsSpan(8, 32)));
        Assert.Equal("Blue", Encoding.UTF8.GetString(payload.AsSpan(52, "Blue".Length)));
        Assert.All(payload.AsSpan(52 + "Blue".Length, 32 - "Blue".Length).ToArray(), b => Assert.Equal(0, b));
        Assert.True(LegacyTeamArenaMatchScorePacket.TryParse(frame, out var parsed));
        Assert.Equal("ABCDEFGHIJKLMNOPQRSTUVWX12345678", parsed.NameOne);
        Assert.Equal("Blue", parsed.NameTwo);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
