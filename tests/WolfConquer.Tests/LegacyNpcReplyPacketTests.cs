using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNpcReplyPacketTests
{
    [Fact]
    public void LegacyNpcReplyOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2255, WorldPacketOpcodes.LegacyNpcReply);
        Assert.Equal(WorldPacketOpcodes.LegacyNpcReply, LegacyNpcReplyPacket.Opcode);
    }

    [Fact]
    public void LegacyNpcReplyMessageBoxWritesLegacyStringArrayLayout()
    {
        var packet = LegacyNpcReplyPacket.ForMessageBox("Hi");

        Assert.Equal(
        [
            0x11, 0x00, 0xCF, 0x08,
            0x00, 0x00, 0x00, 0x00,
            0xFF, 0xFF,
            0x00,
            0x06,
            0x01,
            0x02, 0x00,
            0x48, 0x69,
        ],
        packet.ToFrameBytes());
    }

    [Fact]
    public void LegacyNpcReplyMessageBoxRoundTripsFields()
    {
        var packet = LegacyNpcReplyPacket.ForMessageBox("Alien world ready");
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
        Assert.Equal(0U, parsed.Unknown);
        Assert.Equal(ushort.MaxValue, parsed.InputMaxLength);
        Assert.Equal(0, parsed.OptionId);
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, parsed.InteractType);
        Assert.Equal("Alien world ready", parsed.Text);
    }

    [Fact]
    public void LegacyNpcReplyStaticGuiRoundTripsFields()
    {
        var packet = LegacyNpcReplyPacket.ForStaticGui(LegacyStaticGuiMode.Body, "Welcome to TestSrv.");
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var parsed));

        Assert.Equal(0U, parsed.Unknown);
        Assert.Equal(0, parsed.InputMaxLength);
        Assert.Equal((byte)LegacyStaticGuiMode.Body, parsed.OptionId);
        Assert.Equal(LegacyNpcReplyInteractType.StaticGui, parsed.InteractType);
        Assert.Equal("Welcome to TestSrv.", parsed.Text);
    }

    [Fact]
    public void LegacyNpcReplyInputRoundTripsFields()
    {
        var packet = LegacyNpcReplyPacket.ForInput("Name:", optionId: 7, maxLength: 16);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var parsed));

        Assert.Equal(0U, parsed.Unknown);
        Assert.Equal(16, parsed.InputMaxLength);
        Assert.Equal(7, parsed.OptionId);
        Assert.Equal(LegacyNpcReplyInteractType.Input, parsed.InteractType);
        Assert.Equal("Name:", parsed.Text);
    }

    [Fact]
    public void LegacyNpcReplyClosedOptionUsesSourceCloseOptionId()
    {
        var packet = LegacyNpcReplyPacket.ForClosedOption("No thanks.");
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var parsed));

        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, parsed.OptionId);
        Assert.Equal(LegacyNpcReplyInteractType.Option, parsed.InteractType);
        Assert.Equal("No thanks.", parsed.Text);
    }

    [Fact]
    public void LegacyNpcReplyPageRoundTripsFields()
    {
        var packet = LegacyNpcReplyPacket.ForPage("https://example.test/npc");
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var parsed));

        Assert.Equal(0U, parsed.Unknown);
        Assert.Equal(0, parsed.InputMaxLength);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, parsed.OptionId);
        Assert.Equal(LegacyNpcReplyInteractType.Page, parsed.InteractType);
        Assert.Equal("https://example.test/npc", parsed.Text);
    }

    [Fact]
    public void LegacyNpcReplyPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyNpcReplyPacket.Opcode + 1, payloadLength: 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyNpcReplyPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyNpcReplyPacketRejectsUnknownInteractType()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcReply, payloadLength: 13);
        writer.WriteUInt32(0, 0);
        writer.WriteUInt16(4, ushort.MaxValue);
        writer.Payload[6] = 0;
        writer.Payload[7] = 99;
        writer.Payload[8] = 1;
        writer.WriteUInt16(9, 2);
        writer.Payload[11] = 0x48;
        writer.Payload[12] = 0x69;
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyNpcReplyPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
