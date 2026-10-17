using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyArenaMatchScorePacketTests
{
    [Fact]
    public void LegacyArenaMatchScoreOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2059, WorldPacketOpcodes.LegacyArenaMatchScore);
        Assert.Equal(WorldPacketOpcodes.LegacyArenaMatchScore, LegacyArenaMatchScorePacket.Opcode);
    }

    [Fact]
    public void RoundTripsSourceShapedMatchScore()
    {
        var packet = LegacyArenaMatchScorePacket.Create(
            uidOne: 1_293_600,
            serverIdOne: 101,
            nameOne: "HeroOne",
            damageOne: 123_456,
            uidTwo: 400_001,
            serverIdTwo: 102,
            nameTwo: "Partner",
            damageTwo: 65_432);

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyArenaMatchScore, frame.Header.Opcode);
        Assert.Equal(LegacyArenaMatchScorePacket.PayloadLength, frame.Payload.Length);
        Assert.True(LegacyArenaMatchScorePacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
        Assert.Equal(LegacyArenaMatchScorePacket.LegacyResult, parsed.Result);
        Assert.Equal(uint.MaxValue, parsed.Result);
    }

    [Fact]
    public void PayloadLengthAndFieldOrderMatchLegacyBuilderShape()
    {
        var packet = new LegacyArenaMatchScorePacket(
            UidOne: 0x11121314,
            ServerIdOne: 0x21222324,
            NameOne: "FirstName",
            DamageOne: 0x31323334,
            UidTwo: 0x41424344,
            ServerIdTwo: 0x51525354,
            NameTwo: "SecondName",
            DamageTwo: 0x61626364,
            Result: LegacyArenaMatchScorePacket.LegacyResult);

        var frame = ReadFrame(packet.ToFrameBytes());
        var payload = frame.Payload.ToArray();

        Assert.Equal(32, LegacyArenaMatchScorePacket.FixedNameLength);
        Assert.Equal(92, LegacyArenaMatchScorePacket.PayloadLength);
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
        Assert.Equal(uint.MaxValue, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(88)));
    }

    [Fact]
    public void RejectsWrongOpcodeAndShortPayload()
    {
        var wrongOpcode = new PacketWriter(WorldPacketOpcodes.LegacyTeamArenaMatchScore, LegacyArenaMatchScorePacket.PayloadLength);
        var shortPayload = new PacketWriter(LegacyArenaMatchScorePacket.Opcode, LegacyArenaMatchScorePacket.PayloadLength - 1);

        Assert.False(LegacyArenaMatchScorePacket.TryParse(ReadFrame(wrongOpcode.ToArray()), out _));
        Assert.False(LegacyArenaMatchScorePacket.TryParse(ReadFrame(shortPayload.ToArray()), out _));
    }

    [Fact]
    public void FixedNamesAreUtf8PaddedAndTruncated()
    {
        var packet = LegacyArenaMatchScorePacket.Create(
            uidOne: 1,
            serverIdOne: 2,
            nameOne: "ABCDEFGHIJKLMNOPQRSTUVWX1234567890",
            damageOne: 3,
            uidTwo: 4,
            serverIdTwo: 5,
            nameTwo: "Blue",
            damageTwo: 6);

        var frame = ReadFrame(packet.ToFrameBytes());
        var payload = frame.Payload.ToArray();

        Assert.Equal("ABCDEFGHIJKLMNOPQRSTUVWX12345678", Encoding.UTF8.GetString(payload.AsSpan(8, 32)));
        Assert.Equal("Blue", Encoding.UTF8.GetString(payload.AsSpan(52, "Blue".Length)));
        Assert.All(payload.AsSpan(52 + "Blue".Length, 32 - "Blue".Length).ToArray(), b => Assert.Equal(0, b));
        Assert.True(LegacyArenaMatchScorePacket.TryParse(frame, out var parsed));
        Assert.Equal("ABCDEFGHIJKLMNOPQRSTUVWX12345678", parsed.NameOne);
        Assert.Equal("Blue", parsed.NameTwo);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
