using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNpcServerRequestPacketTests
{
    [Fact]
    public void LegacyNpcServerOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2084, WorldPacketOpcodes.LegacyNpcServer);
        Assert.Equal(WorldPacketOpcodes.LegacyNpcServer, LegacyNpcServerRequestPacket.Opcode);
    }

    [Fact]
    public void LegacyNpcServerRequestWritesLegacyProtobufShape()
    {
        var packet = LegacyNpcServerRequestPacket.Script(20_499, optionId: 0, interactType: 1, input: "hi");
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyNpcServerRequestPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(
        [
            0x08, 0x00,
            0x10, 0x93, 0xA0, 0x01,
            0x18, 0x00,
            0x20, 0x01,
            0x2A, 0x02, 0x68, 0x69,
        ], frame.Payload.ToArray());
    }

    [Fact]
    public void LegacyNpcServerRequestRoundTripsFields()
    {
        var packet = new LegacyNpcServerRequestPacket(
            LegacyNpcServerAction.PlaceFurniture,
            NpcId: 3124,
            OptionId: 2,
            InteractType: 3,
            Input: "chair");
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyNpcServerRequestPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyNpcServerScriptHelperAcceptsTypedReplyInteractType()
    {
        var packet = LegacyNpcServerRequestPacket.Script(
            20_499,
            optionId: 3,
            LegacyNpcReplyInteractType.Input,
            input: "dragon");
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyNpcServerRequestPacket.TryParse(frame, out var parsed));

        Assert.Equal(LegacyNpcServerAction.Script, parsed.Action);
        Assert.Equal(20_499U, parsed.NpcId);
        Assert.Equal(3, parsed.OptionId);
        Assert.Equal((byte)LegacyNpcReplyInteractType.Input, parsed.InteractType);
        Assert.True(parsed.TryGetInteractType(out var interactType));
        Assert.Equal(LegacyNpcReplyInteractType.Input, interactType);
        Assert.Equal("dragon", parsed.Input);
        Assert.Equal("npcId=20499 optionId=3 interactType=Input", parsed.FormatScriptKey());
        Assert.Equal(
            "action=Script npcId=20499 optionId=3 interactType=Input inputLength=6",
            parsed.DescribeForLog());
    }

    [Fact]
    public void LegacyNpcServerScriptRequestFromDialogReplyUsesActiveNpcFallbackAndPreservesInput()
    {
        var reply = new LegacyNpcDialogRequestPacket(
            NpcId: 0,
            Mesh: 321,
            OptionId: 7,
            InteractType: (byte)LegacyNpcReplyInteractType.Input,
            Action: 0,
            Input: "Emerald");

        Assert.True(LegacyNpcServerRequestPacket.TryCreateScriptRequest(
            reply,
            20_499,
            out var request,
            out var rejectionReason));

        Assert.Equal(LegacyNpcScriptRequestRejectionReason.None, rejectionReason);
        Assert.Equal(LegacyNpcServerAction.Script, request.Action);
        Assert.Equal(20_499U, request.NpcId);
        Assert.Equal(7, request.OptionId);
        Assert.Equal((byte)LegacyNpcReplyInteractType.Input, request.InteractType);
        Assert.Equal("Emerald", request.Input);

        var frame = ReadFrame(request.ToFrameBytes());
        Assert.Equal(LegacyNpcServerRequestPacket.Opcode, frame.Header.Opcode);
        Assert.True(LegacyNpcServerRequestPacket.TryParse(frame, out var parsed));
        Assert.Equal(request, parsed);
    }

    [Fact]
    public void LegacyNpcServerScriptRequestFromDialogReplyRejectsClosedOption()
    {
        var reply = new LegacyNpcDialogRequestPacket(
            NpcId: 20_499,
            Mesh: 0,
            OptionId: LegacyNpcDialogRequestPacket.ClosedOptionId,
            InteractType: (byte)LegacyNpcReplyInteractType.Dialog,
            Action: 0,
            Input: "ignored");

        Assert.False(LegacyNpcServerRequestPacket.TryCreateScriptRequest(
            reply,
            20_508,
            out var request,
            out var rejectionReason));

        Assert.Equal(LegacyNpcScriptRequestRejectionReason.ClosedOption, rejectionReason);
        Assert.Equal(default, request);
        Assert.Equal(
            "packetNpcId=20499 activeNpcId=20508 npcId=20499 optionId=255 interactType=Dialog action=0 inputLength=7",
            reply.DescribeForLog(20_508));
    }

    [Fact]
    public void LegacyNpcServerScriptRequestFromDialogReplyRejectsMissingNpcIdWithReason()
    {
        var reply = new LegacyNpcDialogRequestPacket(
            NpcId: 0,
            Mesh: 0,
            OptionId: 1,
            InteractType: (byte)LegacyNpcReplyInteractType.Dialog,
            Action: 0,
            Input: "");

        Assert.False(LegacyNpcServerRequestPacket.TryCreateScriptRequest(
            reply,
            activeNpcId: 0,
            out var request,
            out var rejectionReason));

        Assert.Equal(LegacyNpcScriptRequestRejectionReason.MissingNpcId, rejectionReason);
        Assert.Equal(default, request);
        Assert.Equal("npcId=0 optionId=1 interactType=Dialog", reply.FormatScriptKey());
        Assert.Equal(
            "packetNpcId=0 activeNpcId=0 npcId=0 optionId=1 interactType=Dialog action=0 inputLength=0",
            reply.DescribeForLog());
    }

    [Fact]
    public void LegacyNpcServerScriptRequestFromDialogReplyRejectsInvalidInteractTypeWithReason()
    {
        var reply = new LegacyNpcDialogRequestPacket(
            NpcId: 20_499,
            Mesh: 0,
            OptionId: 1,
            InteractType: 99,
            Action: 0,
            Input: "");

        Assert.False(LegacyNpcServerRequestPacket.TryCreateScriptRequest(
            reply,
            activeNpcId: 0,
            out var request,
            out var rejectionReason));

        Assert.Equal(LegacyNpcScriptRequestRejectionReason.InvalidInteractType, rejectionReason);
        Assert.Equal(default, request);
        Assert.Equal("npcId=20499 optionId=1 interactType=Invalid(99)", reply.FormatScriptKey());

        var packet = new LegacyNpcServerRequestPacket(
            LegacyNpcServerAction.Script,
            NpcId: 20_499,
            OptionId: 1,
            InteractType: 99,
            Input: "");

        Assert.Equal("npcId=20499 optionId=1 interactType=Invalid(99)", packet.FormatScriptKey());
        Assert.Equal(
            "action=Script npcId=20499 optionId=1 interactType=Invalid(99) inputLength=0",
            packet.DescribeForLog());
    }

    [Fact]
    public void LegacyNpcServerScriptRequestFromDialogReplyRejectsUnsupportedInteractTypeWithReason()
    {
        var reply = new LegacyNpcDialogRequestPacket(
            NpcId: 20_499,
            Mesh: 0,
            OptionId: 0,
            InteractType: LegacyNpcDialogRequestPacket.MessageBoxInteractType,
            Action: 0,
            Input: "");

        Assert.False(LegacyNpcServerRequestPacket.TryCreateScriptRequest(
            reply,
            activeNpcId: 0,
            out var request,
            out var rejectionReason));

        Assert.Equal(LegacyNpcScriptRequestRejectionReason.UnsupportedInteractType, rejectionReason);
        Assert.Equal(default, request);
    }

    [Fact]
    public void LegacyNpcServerRequestIdentifiesClosedOption()
    {
        var packet = LegacyNpcServerRequestPacket.Script(
            20_499,
            LegacyNpcReplyPacket.ClosedOptionId,
            LegacyNpcReplyInteractType.Dialog);

        Assert.True(packet.IsClosedOption);
    }

    [Fact]
    public void LegacyNpcServerRequestDefaultsMissingOptionalInputToEmpty()
    {
        var frame = ReadFrame(LegacyNpcServerRequestPacket.Script(20_499).ToFrameBytes());

        Assert.True(LegacyNpcServerRequestPacket.TryParse(frame, out var parsed));

        Assert.Equal("", parsed.Input);
    }

    [Fact]
    public void LegacyNpcServerRequestRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyNpcServerRequestPacket.Opcode + 1, payloadLength: 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyNpcServerRequestPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyNpcServerRequestRejectsMissingRequiredField()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcServer, payloadLength: 8);
        Convert.FromHexString("08001093A0011800").CopyTo(writer.Payload);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyNpcServerRequestPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyNpcServerRequestRejectsByteOverflow()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcServer, payloadLength: 11);
        Convert.FromHexString("08001093A00118AC022001").CopyTo(writer.Payload);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyNpcServerRequestPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
