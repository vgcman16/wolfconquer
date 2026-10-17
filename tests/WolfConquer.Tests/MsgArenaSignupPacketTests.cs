using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgArenaSignupPacketTests
{
    [Theory]
    [InlineData(0, MsgArenaSignupDialogType.ArenaIconOn)]
    [InlineData(1, MsgArenaSignupDialogType.ArenaIconOff)]
    [InlineData(2, MsgArenaSignupDialogType.StartCountDown)]
    [InlineData(3, MsgArenaSignupDialogType.ArenaGui)]
    [InlineData(4, MsgArenaSignupDialogType.OpponentGaveUp)]
    [InlineData(5, MsgArenaSignupDialogType.Match)]
    [InlineData(6, MsgArenaSignupDialogType.YouAreKicked)]
    [InlineData(7, MsgArenaSignupDialogType.StartTheFight)]
    [InlineData(8, MsgArenaSignupDialogType.Dialog)]
    [InlineData(9, MsgArenaSignupDialogType.Dialog2)]
    [InlineData(10, MsgArenaSignupDialogType.Continue)]
    public void MsgArenaSignupDialogTypeDefinesLegacyNumericValues(int expected, MsgArenaSignupDialogType dialogType)
    {
        Assert.Equal((uint)expected, (uint)dialogType);
    }

    [Fact]
    public void MsgArenaSignupDialogButtonDefinesLegacyNumericValues()
    {
        Assert.Equal(0U, (uint)MsgArenaSignupDialogButton.SignUp);
        Assert.Equal(1U, (uint)MsgArenaSignupDialogButton.Win);
        Assert.Equal(1U, (uint)MsgArenaSignupDialogButton.Accept);
        Assert.Equal(2U, (uint)MsgArenaSignupDialogButton.DoGiveUp);
        Assert.Equal(3U, (uint)MsgArenaSignupDialogButton.Lose);
        Assert.Equal(3U, (uint)MsgArenaSignupDialogButton.MatchOff);
        Assert.Equal(5U, (uint)MsgArenaSignupDialogButton.MatchOn);
    }

    [Fact]
    public void MsgArenaSignupOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2140, WorldPacketOpcodes.MsgArenaSignup);
        Assert.Equal(WorldPacketOpcodes.MsgArenaSignup, MsgArenaSignupPacket.Opcode);
    }

    [Fact]
    public void MsgArenaSignupPacketRoundTripsDialogTypeAndButton()
    {
        var packet = new MsgArenaSignupPacket(
            MsgArenaSignupDialogType.Match,
            MsgArenaSignupDialogButton.Accept);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(MsgArenaSignupPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void MsgArenaSignupPacketIgnoresTrailingLegacyStructBytes()
    {
        var writer = new PacketWriter(MsgArenaSignupPacket.Opcode, MsgArenaSignupPacket.PayloadLength + 52);
        writer.WriteUInt32(0, (uint)MsgArenaSignupDialogType.ArenaGui);
        writer.WriteUInt32(4, (uint)MsgArenaSignupDialogButton.DoGiveUp);
        writer.WriteUInt32(8, 0xAABBCCDD);
        var frame = ReadFrame(writer.ToArray());

        Assert.True(MsgArenaSignupPacket.TryParse(frame, out var parsed));

        Assert.Equal(MsgArenaSignupDialogType.ArenaGui, parsed.DialogType);
        Assert.Equal(MsgArenaSignupDialogButton.DoGiveUp, parsed.DialogButton);
    }

    [Fact]
    public void MsgArenaSignupPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(MsgArenaSignupPacket.Opcode + 1, MsgArenaSignupPacket.PayloadLength);
        writer.WriteUInt32(0, (uint)MsgArenaSignupDialogType.ArenaGui);
        writer.WriteUInt32(4, (uint)MsgArenaSignupDialogButton.DoGiveUp);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(MsgArenaSignupPacket.TryParse(frame, out _));
    }

    [Fact]
    public void MsgArenaSignupPacketRejectsShortPayload()
    {
        var frame = ReadFrame(
        [
            0x0B, 0x00, 0x5C, 0x08,
            0x03, 0x00, 0x00, 0x00,
            0x02, 0x00, 0x00
        ]);

        Assert.False(MsgArenaSignupPacket.TryParse(frame, out _));
    }

    [Fact]
    public void MsgArenaSignupPacketPreservesUnknownDialogTypeAndButtonValues()
    {
        const uint unknownDialogType = 0xA0B0C0D0;
        const uint unknownDialogButton = 0x01020304;
        var packet = new MsgArenaSignupPacket(
            (MsgArenaSignupDialogType)unknownDialogType,
            (MsgArenaSignupDialogButton)unknownDialogButton);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(MsgArenaSignupPacket.TryParse(frame, out var parsed));

        Assert.Equal(unknownDialogType, (uint)parsed.DialogType);
        Assert.Equal(unknownDialogButton, (uint)parsed.DialogButton);
    }

    [Fact]
    public void MsgArenaSignupPacketWritesLegacyLittleEndianShape()
    {
        var packet = new MsgArenaSignupPacket(
            MsgArenaSignupDialogType.ArenaGui,
            MsgArenaSignupDialogButton.DoGiveUp);

        Assert.Equal(
        [
            0x0C, 0x00, 0x5C, 0x08,
            0x03, 0x00, 0x00, 0x00,
            0x02, 0x00, 0x00, 0x00
        ],
        packet.ToFrameBytes());
    }

    [Fact]
    public void MsgArenaSignupResponseWritesLegacyArenaSignupCreateShape()
    {
        var packet = new MsgArenaSignupResponsePacket(
            MsgArenaSignupDialogType.ArenaIconOn,
            MsgArenaSignupDialogButton.SignUp,
            CharacterUid: 1_293_600,
            CharacterName: "HeroOne",
            TodayRank: 7,
            Class: 7051,
            ArenaPoints: 4321,
            Level: 140);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(PacketReader.TryRead(frame.Frame.ToArray(), out var reader));
        Assert.Equal(MsgArenaSignupResponsePacket.Opcode, reader.Header.Opcode);
        Assert.Equal(72, reader.Header.Length);
        Assert.Equal(MsgArenaSignupResponsePacket.PayloadLength, reader.Payload.Length);
        Assert.Equal((uint)MsgArenaSignupDialogType.ArenaIconOn, reader.ReadUInt32(0));
        Assert.Equal((uint)MsgArenaSignupDialogButton.SignUp, reader.ReadUInt32(4));
        Assert.Equal(1_293_600U, reader.ReadUInt32(8));
        Assert.Equal(0U, reader.ReadUInt32(12));
        Assert.Equal("HeroOne", reader.ReadCString(16, 32, Encoding.UTF8));
        Assert.Equal(7U, reader.ReadUInt32(48));
        Assert.Equal(7051U, reader.ReadUInt32(52));
        Assert.Equal(0U, reader.ReadUInt32(56));
        Assert.Equal(4321U, reader.ReadUInt32(60));
        Assert.Equal(140U, reader.ReadUInt32(64));
    }

    [Fact]
    public void MsgArenaSignupResponseRoundTripsLegacyPayload()
    {
        var packet = MsgArenaSignupResponsePacket.FromRequest(
            new MsgArenaSignupPacket(MsgArenaSignupDialogType.ArenaIconOff, MsgArenaSignupDialogButton.SignUp),
            Character(),
            new ArenaStatistics(TodayRank: 2, ArenaPoints: 4500));
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(MsgArenaSignupResponsePacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static CharacterSnapshot Character()
    {
        return new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "HeroOne",
            Body: 1008,
            Face: 154,
            Hair: 27,
            Class: 7051,
            FirstClass: 0,
            SecondClass: 7050,
            Reborn: 2,
            Level: 140,
            Experience: 0,
            MapId: 1015,
            X: 830,
            Y: 584,
            PreviousMapId: 1000,
            PreviousMapX: 500,
            PreviousMapY: 650,
            Strength: 0,
            Agility: 36,
            Vitality: 447,
            Spirit: 0,
            AttributePoints: 12,
            MinHitPoints: 61258,
            MinMana: 5000,
            Money: 98602439956,
            ConquerPoints: 639778904,
            BoundConquerPoints: 456,
            BankConquerPoints: 123,
            PkPoints: 0,
            Online: 7,
            SourceFile: "1293600.ini",
            Attributes: new Dictionary<string, string>(),
            PaidNobility: new Dictionary<string, string>());
    }
}
