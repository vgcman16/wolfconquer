using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMailboxPacketTests
{
    [Fact]
    public void LegacyMailboxOpcodesMatchLegacyFinalizeConstants()
    {
        Assert.Equal((ushort)2183, WorldPacketOpcodes.LegacyMailList);
        Assert.Equal((ushort)2264, WorldPacketOpcodes.LegacyMailboxMode);
        Assert.Equal((ushort)2058, WorldPacketOpcodes.LegacyMailContent);
        Assert.Equal(WorldPacketOpcodes.LegacyMailList, LegacyMailListPacket.Opcode);
        Assert.Equal(WorldPacketOpcodes.LegacyMailboxMode, LegacyMailboxModePacket.Opcode);
        Assert.Equal(WorldPacketOpcodes.LegacyMailContent, LegacyMailContentPacket.Opcode);
    }

    [Fact]
    public void LegacyMailboxModeWritesExactBinaryRequestAckShape()
    {
        var packet = new LegacyMailboxModePacket(
            LegacyMailboxModeType.ClaimAll,
            Id: 0x11223344,
            Items: [0x0102030405060708UL, 0x1112131415161718UL]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyMailboxMode, frame.Header.Opcode);
        Assert.Equal(
            [
                0x07, 0x00,
                0x44, 0x33, 0x22, 0x11,
                0x02, 0x00, 0x00, 0x00,
                0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01,
                0x18, 0x17, 0x16, 0x15, 0x14, 0x13, 0x12, 0x11,
            ],
            frame.Payload.ToArray());
    }

    [Fact]
    public void LegacyMailboxModeRoundTripsContentRequestWithoutItems()
    {
        var packet = new LegacyMailboxModePacket(LegacyMailboxModeType.Content, Id: 1234, Items: []);

        Assert.True(LegacyMailboxModePacket.TryParse(ReadFrame(packet.ToFrameBytes()), out var parsed));

        Assert.Equal(packet.Type, parsed.Type);
        Assert.Equal(packet.Id, parsed.Id);
        Assert.Equal(packet.Items, parsed.Items);
        Assert.Equal(0U, parsed.Count);
    }

    [Fact]
    public void LegacyMailContentWritesFixed512ByteContentShape()
    {
        var packet = new LegacyMailContentPacket(MailId: 0x01020304, Content: "Prize");
        var frame = ReadFrame(packet.ToFrameBytes());
        var payload = frame.Payload.ToArray();

        Assert.Equal(WorldPacketOpcodes.LegacyMailContent, frame.Header.Opcode);
        Assert.Equal(LegacyMailContentPacket.PayloadLength, payload.Length);
        Assert.Equal(0x01020304U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, sizeof(uint))));
        Assert.Equal("Prize"u8.ToArray(), payload[4..9]);
        Assert.All(payload[9..], value => Assert.Equal(0, value));
    }

    [Fact]
    public void LegacyMailContentRoundTripsAndTrimsZeroPadding()
    {
        var packet = new LegacyMailContentPacket(MailId: 77, Content: "Hello mailbox");

        Assert.True(LegacyMailContentPacket.TryParse(ReadFrame(packet.ToFrameBytes()), out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyMailListWritesType2NewMailWithDefaultZeroCount()
    {
        var packet = LegacyMailListPacket.NewMail(Mail());
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyMailList, frame.Header.Opcode);
        Assert.Equal(
            "080210001a28080110e807181920c03e284d30023801420a54657374207469746c654a0a5465737453656e646572",
            Convert.ToHexString(frame.Payload.ToArray()).ToLowerInvariant());
    }

    [Fact]
    public void LegacyMailListRoundTrips()
    {
        var packet = new LegacyMailListPacket(
            LegacyMailListType.FullPage,
            Count: 1,
            [Mail()]);

        Assert.True(LegacyMailListPacket.TryParse(ReadFrame(packet.ToFrameBytes()), out var parsed));

        Assert.Equal(packet.Type, parsed.Type);
        Assert.Equal(packet.Count, parsed.Count);
        Assert.Equal(packet.Mails, parsed.Mails);
    }

    [Fact]
    public void LegacyMailListType2KeepsSourceCountDefaultWhileCarryingMail()
    {
        var packet = LegacyMailListPacket.NewMail(Mail());

        Assert.True(LegacyMailListPacket.TryParse(ReadFrame(packet.ToFrameBytes()), out var parsed));

        Assert.Equal(LegacyMailListType.NewMail, parsed.Type);
        Assert.Equal(0, parsed.Count);
        Assert.Single(parsed.Mails);
    }

    private static LegacyMailListEntry Mail()
    {
        return new LegacyMailListEntry(
            Id: 1,
            Money: 1000,
            EMoney: 25,
            ExpirationDate: 8000,
            ItemId: 77,
            EMoneyRecordType: 2,
            Flag: 1,
            Title: "Test title",
            SenderName: "TestSender");
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
