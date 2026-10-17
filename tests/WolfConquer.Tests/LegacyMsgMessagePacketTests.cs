using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMsgMessagePacketTests
{
    [Fact]
    public void AnswerOkWritesLegacyDialogMessageShape()
    {
        var frame = ReadFrame(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes());

        Assert.True(LegacyMsgMessagePacket.TryParse(frame, out var packet));
        Assert.Equal((ushort)2179, frame.Header.Opcode);
        Assert.Equal(LegacyMsgMessagePacket.RedColor, packet.Color);
        Assert.Equal(LegacyMsgMessagePacket.DialogChatType, packet.ChatType);
        Assert.Equal(0U, packet.MessageUid1);
        Assert.Equal(0U, packet.MessageUid2);
        Assert.Equal(0U, packet.ServerId);
        Assert.Equal(0U, packet.Mesh);
        Assert.Equal(0U, packet.UnionType);
        Assert.Equal(1U, packet.Count);
        Assert.Equal(0U, packet.New1);
        Assert.Equal(0U, packet.New2);
        Assert.Equal(
        [
            "SYSTEM",
            "ALLUSERS",
            string.Empty,
            "ANSWER_OK",
            string.Empty,
            string.Empty,
            string.Empty
        ], packet.Content);
    }

    [Fact]
    public void AnswerOkWritesExpectedFramePrefix()
    {
        var bytes = LegacyMsgMessagePacket.AnswerOk().ToFrameBytes();

        Assert.Equal(65, bytes.Length);
        Assert.Equal(
        [
            0x41, 0x00, 0x83, 0x08,
            0x08, 0x80, 0x80, 0xFC, 0x07,
            0x10, 0xB5, 0x10
        ], bytes[..12]);
    }

    [Fact]
    public void SystemTalkWritesLegacyWelcomeMessageShape()
    {
        var frame = ReadFrame(LegacyMsgMessagePacket.SystemTalk("Welcome to TestSrv.").ToFrameBytes());

        Assert.True(LegacyMsgMessagePacket.TryParse(frame, out var packet));
        Assert.Equal(LegacyMsgMessagePacket.RedColor, packet.Color);
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, packet.ChatType);
        Assert.Equal(
        [
            "SYSTEM",
            "ALL",
            string.Empty,
            "Welcome to TestSrv.",
            string.Empty,
            string.Empty,
            string.Empty
        ], packet.Content);
    }

    [Fact]
    public void SystemTopLeftUsesSourceTopLeftSystemChannel()
    {
        var frame = ReadFrame(LegacyMsgMessagePacket.SystemTopLeft("....").ToFrameBytes());

        Assert.True(LegacyMsgMessagePacket.TryParse(frame, out var packet));
        Assert.Equal(LegacyMsgMessagePacket.RedColor, packet.Color);
        Assert.Equal(2_005U, packet.ChatType);
        Assert.Equal("....", packet.Content[3]);
    }

    [Fact]
    public void SystemWebsiteWritesWebsiteMessageShape()
    {
        const string url = "https://example.test/vote";

        var frame = ReadFrame(LegacyMsgMessagePacket.SystemWebsite(url).ToFrameBytes());

        Assert.True(LegacyMsgMessagePacket.TryParse(frame, out var packet));
        Assert.Equal(2_105U, LegacyMsgMessagePacket.WebsiteChatType);
        Assert.Equal(LegacyMsgMessagePacket.Opcode, frame.Header.Opcode);
        Assert.Equal(LegacyMsgMessagePacket.WhiteColor, packet.Color);
        Assert.Equal(LegacyMsgMessagePacket.WebsiteChatType, packet.ChatType);
        Assert.Equal(0U, packet.MessageUid1);
        Assert.Equal(0U, packet.MessageUid2);
        Assert.Equal(0U, packet.ServerId);
        Assert.Equal(0U, packet.Mesh);
        Assert.Equal(0U, packet.UnionType);
        Assert.Equal(1U, packet.Count);
        Assert.Equal(0U, packet.New1);
        Assert.Equal(0U, packet.New2);
        Assert.Equal(url, packet.Content[3]);
        Assert.Equal(
        [
            "SYSTEM",
            "ALL",
            string.Empty,
            url,
            string.Empty,
            string.Empty,
            string.Empty
        ], packet.Content);
    }

    [Fact]
    public void SystemWebsiteRejectsNullUrl()
    {
        Assert.Throws<ArgumentNullException>(() => LegacyMsgMessagePacket.SystemWebsite(null!));
    }

    [Fact]
    public void SystemBroadcastWritesLegacyBroadcastMessageShape()
    {
        var frame = ReadFrame(LegacyMsgMessagePacket.SystemBroadcast("GM server is open in Twin City.").ToFrameBytes());

        Assert.True(LegacyMsgMessagePacket.TryParse(frame, out var packet));
        Assert.Equal(LegacyMsgMessagePacket.RedColor, packet.Color);
        Assert.Equal(LegacyMsgMessagePacket.BroadcastChatType, packet.ChatType);
        Assert.Equal(
        [
            "SYSTEM",
            "ALL",
            string.Empty,
            "GM server is open in Twin City.",
            string.Empty,
            string.Empty,
            string.Empty
        ], packet.Content);
    }

    [Fact]
    public void SystemRedCenterWritesLegacyPromotionMessageShape()
    {
        var frame = ReadFrame(LegacyMsgMessagePacket.SystemRedCenter("Donor has been promoted to King.").ToFrameBytes());

        Assert.True(LegacyMsgMessagePacket.TryParse(frame, out var packet));
        Assert.Equal(LegacyMsgMessagePacket.RedColor, packet.Color);
        Assert.Equal(LegacyMsgMessagePacket.CenterChatType, packet.ChatType);
        Assert.Equal(
        [
            "SYSTEM",
            "ALL",
            string.Empty,
            "Donor has been promoted to King.",
            string.Empty,
            string.Empty,
            string.Empty
        ], packet.Content);
    }

    [Fact]
    public void SystemQualifierWritesLegacyArenaResultMessageShape()
    {
        var frame = ReadFrame(LegacyMsgMessagePacket.SystemQualifier("Target has defeated HeroOne in the Qualifier.").ToFrameBytes());

        Assert.True(LegacyMsgMessagePacket.TryParse(frame, out var packet));
        Assert.Equal(LegacyMsgMessagePacket.RedColor, packet.Color);
        Assert.Equal(LegacyMsgMessagePacket.QualifierChatType, packet.ChatType);
        Assert.Equal(
        [
            "SYSTEM",
            "ALL",
            string.Empty,
            "Target has defeated HeroOne in the Qualifier.",
            string.Empty,
            string.Empty,
            string.Empty
        ], packet.Content);
    }

    [Fact]
    public void LegacyMsgMessageRejectsWrongOpcode()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyMsgMessage + 1, payloadLength: 0);

        Assert.False(LegacyMsgMessagePacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    [Fact]
    public void LegacyMsgMessageRejectsMissingContentSlots()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyMsgMessage, payloadLength: 4);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = 0x01;
        writer.Payload[2] = 0x10;
        writer.Payload[3] = 0x01;

        Assert.False(LegacyMsgMessagePacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
