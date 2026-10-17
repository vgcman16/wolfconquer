using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNpcDialogRequestPacketTests
{
    [Fact]
    public void LegacyNpcDialogMessageBoxOkParsesClientReplyLayout()
    {
        var frame = ReadFrame(BuildDialogReply(optionId: 0, interactType: 6, action: 0, input: ""));

        Assert.True(LegacyNpcDialogRequestPacket.TryParse(frame, out var packet));

        Assert.Equal(0U, packet.NpcId);
        Assert.Equal(0, packet.Mesh);
        Assert.Equal(0, packet.OptionId);
        Assert.Equal(6, packet.InteractType);
        Assert.Equal(0, packet.Action);
        Assert.Equal("", packet.Input);
        Assert.True(packet.IsMessageBox);
        Assert.True(packet.IsAcceptedMessageBox);
    }

    [Fact]
    public void LegacyNpcDialogMessageBoxCancelKeepsOptionId()
    {
        var frame = ReadFrame(BuildDialogReply(optionId: 255, interactType: 6, action: 0, input: "no"));

        Assert.True(LegacyNpcDialogRequestPacket.TryParse(frame, out var packet));

        Assert.Equal(255, packet.OptionId);
        Assert.Equal("no", packet.Input);
        Assert.True(packet.IsMessageBox);
        Assert.False(packet.IsAcceptedMessageBox);
    }

    [Fact]
    public void LegacyNpcDialogRequestWritesClientReplyLayout()
    {
        var packet = new LegacyNpcDialogRequestPacket(
            NpcId: 20_499,
            Mesh: 321,
            OptionId: 7,
            InteractType: (byte)LegacyNpcReplyInteractType.Input,
            Action: 5,
            Input: "ok");

        Assert.Equal(
        [
            0x11, 0x00, 0xCF, 0x08,
            0x13, 0x50, 0x00, 0x00,
            0x41, 0x01,
            0x07,
            0x03,
            0x05,
            0x02, 0x00,
            0x6F, 0x6B,
        ],
        packet.ToFrameBytes());

        Assert.True(LegacyNpcDialogRequestPacket.TryParse(ReadFrame(packet.ToFrameBytes()), out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyNpcDialogParsedClientReplyCreatesSourceScriptRequestWithInput()
    {
        var frame = ReadFrame(BuildDialogReply(
            optionId: 4,
            interactType: (byte)LegacyNpcReplyInteractType.Input,
            action: 0,
            input: "Phoenix"));

        Assert.True(LegacyNpcDialogRequestPacket.TryParse(frame, out var packet));
        Assert.True(LegacyNpcServerRequestPacket.TryCreateScriptRequest(
            packet,
            20_508,
            out var request,
            out var rejectionReason));

        Assert.Equal(LegacyNpcScriptRequestRejectionReason.None, rejectionReason);
        Assert.Equal("npcId=20508 optionId=4 interactType=Input", packet.FormatScriptKey(20_508));
        Assert.Equal(
            LegacyNpcServerRequestPacket.Script(
                20_508,
                optionId: 4,
                LegacyNpcReplyInteractType.Input,
                input: "Phoenix"),
            request);
    }

    [Fact]
    public void LegacyNpcDialogRequestCreatesScriptRequestFromActiveNpc()
    {
        var packet = new LegacyNpcDialogRequestPacket(
            NpcId: 0,
            Mesh: 0,
            OptionId: 7,
            InteractType: (byte)LegacyNpcReplyInteractType.Input,
            Action: 0,
            Input: "Emerald");

        Assert.True(packet.IsScriptReply);
        Assert.Equal(20_499U, packet.ResolveNpcId(20_499));
        Assert.True(packet.TryCreateScriptRequest(20_499, out var request, out var rejectionReason));

        Assert.Equal(LegacyNpcScriptRequestRejectionReason.None, rejectionReason);
        Assert.Equal(
            LegacyNpcServerRequestPacket.Script(
                20_499,
                optionId: 7,
                LegacyNpcReplyInteractType.Input,
                input: "Emerald"),
            request);
    }

    [Fact]
    public void LegacyNpcDialogRequestCreatesScriptRequestForDialogOptionAndInputReplies()
    {
        var cases = new[]
        {
            (LegacyNpcReplyInteractType.Dialog, OptionId: (byte)0, Input: ""),
            (LegacyNpcReplyInteractType.Option, OptionId: (byte)2, Input: ""),
            (LegacyNpcReplyInteractType.Input, OptionId: (byte)4, Input: "Phoenix"),
        };

        foreach (var (interactType, optionId, input) in cases)
        {
            var packet = new LegacyNpcDialogRequestPacket(
                NpcId: 20_499,
                Mesh: 0,
                OptionId: optionId,
                InteractType: (byte)interactType,
                Action: 0,
                Input: input);

            Assert.True(packet.IsScriptReply);
            Assert.True(packet.TryCreateScriptRequest(123, out var request, out var rejectionReason));

            Assert.Equal(LegacyNpcScriptRequestRejectionReason.None, rejectionReason);
            Assert.Equal(LegacyNpcServerAction.Script, request.Action);
            Assert.Equal(20_499U, request.NpcId);
            Assert.Equal(optionId, request.OptionId);
            Assert.Equal((byte)interactType, request.InteractType);
            Assert.Equal(input, request.Input);
            Assert.Equal(
                $"npcId=20499 optionId={optionId} interactType={interactType}",
                packet.FormatScriptKey(123));
        }
    }

    [Fact]
    public void LegacyNpcDialogRequestDoesNotCreateScriptRequestForMessageBoxClosedUnknownOrUnresolvedNpc()
    {
        var cases = new[]
        {
            (
                new LegacyNpcDialogRequestPacket(0, 0, 0, LegacyNpcDialogRequestPacket.MessageBoxInteractType, 0, ""),
                20_499U,
                LegacyNpcScriptRequestRejectionReason.UnsupportedInteractType,
                "npcId=20499 optionId=0 interactType=MessageBox"),
            (new LegacyNpcDialogRequestPacket(
                20_499,
                0,
                LegacyNpcDialogRequestPacket.ClosedOptionId,
                (byte)LegacyNpcReplyInteractType.Dialog,
                0,
                ""),
                0U,
                LegacyNpcScriptRequestRejectionReason.ClosedOption,
                "npcId=20499 optionId=255 interactType=Dialog"),
            (
                new LegacyNpcDialogRequestPacket(20_499, 0, 1, 99, 0, ""),
                0U,
                LegacyNpcScriptRequestRejectionReason.InvalidInteractType,
                "npcId=20499 optionId=1 interactType=Invalid(99)"),
            (
                new LegacyNpcDialogRequestPacket(0, 0, 1, (byte)LegacyNpcReplyInteractType.Dialog, 0, ""),
                0U,
                LegacyNpcScriptRequestRejectionReason.MissingNpcId,
                "npcId=0 optionId=1 interactType=Dialog"),
        };

        foreach (var (packet, activeNpcId, expectedReason, expectedKey) in cases)
        {
            Assert.False(packet.TryCreateScriptRequest(activeNpcId, out var request, out var rejectionReason));
            Assert.Equal(default, request);
            Assert.Equal(expectedReason, rejectionReason);
            Assert.Equal(expectedKey, packet.FormatScriptKey(activeNpcId));
            Assert.Contains(expectedKey, packet.DescribeForLog(activeNpcId), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LegacyNpcDialogRequestRejectsWrongOpcode()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcReply + 1, payloadLength: 11);

        Assert.False(LegacyNpcDialogRequestPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    [Fact]
    public void LegacyNpcDialogRequestRejectsTruncatedMinimumPayload()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcReply, payloadLength: 10);

        Assert.False(LegacyNpcDialogRequestPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    [Fact]
    public void LegacyNpcDialogRequestRejectsDeclaredInputLengthMismatch()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcReply, payloadLength: 12);
        writer.WriteUInt16(9, 2);
        writer.Payload[11] = (byte)'x';

        Assert.False(LegacyNpcDialogRequestPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    [Fact]
    public void LegacyNpcDialogRequestIgnoresSourceTrailingZeroPadding()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcReply, payloadLength: 12);
        writer.Payload[7] = LegacyNpcDialogRequestPacket.MessageBoxInteractType;
        writer.Payload[11] = 0;

        Assert.True(LegacyNpcDialogRequestPacket.TryParse(ReadFrame(writer.ToArray()), out var packet));
        Assert.Equal(0U, packet.NpcId);
        Assert.Equal(0, packet.Mesh);
        Assert.Equal(0, packet.OptionId);
        Assert.Equal(LegacyNpcDialogRequestPacket.MessageBoxInteractType, packet.InteractType);
        Assert.Equal(0, packet.Action);
        Assert.Equal("", packet.Input);
        Assert.True(packet.IsAcceptedMessageBox);
    }

    [Fact]
    public void LegacyNpcDialogRequestRejectsNonZeroTrailingPadding()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcReply, payloadLength: 12);
        writer.Payload[7] = LegacyNpcDialogRequestPacket.MessageBoxInteractType;
        writer.Payload[11] = 1;

        Assert.False(LegacyNpcDialogRequestPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    private static byte[] BuildDialogReply(byte optionId, byte interactType, byte action, string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcReply, payloadLength: 11 + inputBytes.Length);
        writer.WriteUInt32(0, 0);
        writer.WriteUInt16(4, 0);
        writer.Payload[6] = optionId;
        writer.Payload[7] = interactType;
        writer.Payload[8] = action;
        writer.WriteUInt16(9, (ushort)inputBytes.Length);
        inputBytes.CopyTo(writer.Payload[11..]);
        return writer.ToArray();
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
