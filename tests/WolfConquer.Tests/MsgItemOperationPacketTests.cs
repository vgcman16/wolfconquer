using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgItemOperationPacketTests
{
    [Fact]
    public void OpcodeAndActionsMatchActive7899Proof()
    {
        Assert.Equal((ushort)2427, WorldPacketOpcodes.MsgItemOperation);
        Assert.Equal(WorldPacketOpcodes.MsgItemOperation, MsgItemOperationPacket.Opcode);
        Assert.Equal(22U, (uint)MsgItemOperationAction.AddVendingItemGold);
        Assert.Equal(23U, (uint)MsgItemOperationAction.RemoveVendingItem);
        Assert.Equal(24U, (uint)MsgItemOperationAction.BuyVendingItem);
        Assert.Equal(29U, (uint)MsgItemOperationAction.AddVendingItemConquerPoints);
        Assert.Equal(69U, (uint)MsgItemOperationAction.EmbedAnima);
        Assert.Equal(70U, (uint)MsgItemOperationAction.RemoveAnima);
    }

    [Fact]
    public void TryParseReadsExactEmbedFrameBytes()
    {
        byte[] frameBytes =
        [
            0x11, 0x00, 0x7B, 0x09,
            0x08, 0xA0, 0xFA, 0x4E,
            0x18, 0xA1, 0xFA, 0x4E,
            0x30, 0x45,
            0x38, 0xD2, 0x09,
        ];

        Assert.True(MsgItemOperationPacket.TryParse(ReadFrame(frameBytes), out var packet));
        Assert.Equal(1_293_600U, packet.HostItemUid);
        Assert.Equal(1_293_601U, packet.SecondaryItemUid);
        Assert.False(packet.HasPrice);
        Assert.True(packet.HasSecondaryValue);
        Assert.Equal(MsgItemOperationAction.EmbedAnima, packet.Action);
        Assert.Equal(1_234U, packet.TimestampToken);
    }

    [Fact]
    public void TryParseAndSerializeGoldVendorBuilderWithUInt64Price()
    {
        var expected = MsgItemOperationPacket.WithPrice(
            itemUid: 1_293_600,
            price: 4_000_000_000UL,
            MsgItemOperationAction.AddVendingItemGold,
            timestampToken: 1_234);

        Assert.True(MsgItemOperationPacket.TryParse(ReadFrame(expected.ToFrameBytes()), out var parsed));
        Assert.Equal(expected, parsed);
        Assert.True(parsed.HasPrice);
        Assert.False(parsed.HasSecondaryValue);
        Assert.Equal(4_000_000_000UL, parsed.Price);
    }

    [Fact]
    public void TryParseAndSerializePreservesMaximumUInt64Price()
    {
        var expected = MsgItemOperationPacket.WithPrice(
            itemUid: 1,
            price: ulong.MaxValue,
            MsgItemOperationAction.AddVendingItemGold,
            timestampToken: 2);

        Assert.True(MsgItemOperationPacket.TryParse(ReadFrame(expected.ToFrameBytes()), out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(MsgItemOperationAction.AddVendingItemConquerPoints, 2_000_000_000U)]
    [InlineData(MsgItemOperationAction.RemoveVendingItem, 1_393_600U)]
    [InlineData(MsgItemOperationAction.BuyVendingItem, 1_393_600U)]
    public void TryParseAndSerializeVendorSecondaryValueBuilder(
        MsgItemOperationAction action,
        uint secondaryValue)
    {
        var expected = MsgItemOperationPacket.WithSecondaryValue(
            itemUid: 1_293_601,
            secondaryValue,
            action,
            timestampToken: 9_876);

        Assert.True(MsgItemOperationPacket.TryParse(ReadFrame(expected.ToFrameBytes()), out var parsed));
        Assert.Equal(expected, parsed);
        Assert.False(parsed.HasPrice);
        Assert.True(parsed.HasSecondaryValue);
    }

    [Fact]
    public void TryParseAcceptsRemoveWithExplicitZeroSecondaryItemUid()
    {
        byte[] frameBytes =
        [
            0x0F, 0x00, 0x7B, 0x09,
            0x08, 0xA0, 0xFA, 0x4E,
            0x18, 0x00,
            0x30, 0x46,
            0x38, 0xD2, 0x09,
        ];

        Assert.True(MsgItemOperationPacket.TryParse(ReadFrame(frameBytes), out var packet));
        Assert.Equal(0U, packet.SecondaryItemUid);
        Assert.Equal(MsgItemOperationAction.RemoveAnima, packet.Action);
    }

    [Fact]
    public void TryParseSkipsUnknownFieldsOfSupportedWireTypes()
    {
        byte[] payload =
        [
            0x40, 0x96, 0x01,
            0x08, 0x11,
            0x49, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x18, 0x22,
            0x52, 0x03, 0xAA, 0xBB, 0xCC,
            0x30, 0x45,
            0x5D, 0x10, 0x20, 0x30, 0x40,
            0x38, 0x33,
        ];

        Assert.True(MsgItemOperationPacket.TryParse(ReadPayload(payload), out var packet));
        Assert.Equal(0x11U, packet.HostItemUid);
        Assert.Equal(0x22U, packet.SecondaryItemUid);
        Assert.Equal(MsgItemOperationAction.EmbedAnima, packet.Action);
        Assert.Equal(0x33U, packet.TimestampToken);
    }

    [Fact]
    public void TryParseUsesLastKnownFieldValue()
    {
        byte[] payload =
        [
            0x08, 0x01, 0x08, 0x11,
            0x18, 0x02, 0x18, 0x22,
            0x30, 0x46, 0x30, 0x45,
            0x38, 0x03, 0x38, 0x33,
        ];

        Assert.True(MsgItemOperationPacket.TryParse(ReadPayload(payload), out var packet));
        Assert.Equal(0x11U, packet.HostItemUid);
        Assert.Equal(0x22U, packet.SecondaryItemUid);
        Assert.Equal(MsgItemOperationAction.EmbedAnima, packet.Action);
        Assert.Equal(0x33U, packet.TimestampToken);
    }

    [Fact]
    public void TryParseRejectsEachMissingCommonRequiredField()
    {
        Assert.False(MsgItemOperationPacket.TryParse(
            ReadPayload([0x18, 0x22, 0x30, 0x45, 0x38, 0x33]),
            out _));
        Assert.False(MsgItemOperationPacket.TryParse(
            ReadPayload([0x08, 0x11, 0x18, 0x22, 0x38, 0x33]),
            out _));
        Assert.False(MsgItemOperationPacket.TryParse(
            ReadPayload([0x08, 0x11, 0x18, 0x22, 0x30, 0x45]),
            out _));
    }

    [Fact]
    public void TryParseRejectsMalformedOrTruncatedVarints()
    {
        Assert.False(MsgItemOperationPacket.TryParse(
            ReadPayload([0x08, 0x80]),
            out _));
        Assert.False(MsgItemOperationPacket.TryParse(
            ReadPayload([0x08, 0x11, 0x18, 0x22, 0x30, 0x45, 0x38, 0x33, 0x80]),
            out _));
        Assert.False(MsgItemOperationPacket.TryParse(
            ReadPayload([0x08, 0x11, 0x18, 0x22, 0x30, 0x45, 0x38, 0x33, 0x40, 0x80]),
            out _));
        Assert.False(MsgItemOperationPacket.TryParse(
            ReadPayload(
            [
                0x08, 0x11,
                0x10, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x02,
                0x30, 0x16,
                0x38, 0x33,
            ]),
            out _));
    }

    [Fact]
    public void TryParseRejectsMalformedOrTruncatedLengthDelimitedFields()
    {
        Assert.False(MsgItemOperationPacket.TryParse(
            ReadPayload([0x08, 0x11, 0x18, 0x22, 0x30, 0x45, 0x38, 0x33, 0x4A, 0x80]),
            out _));
        Assert.False(MsgItemOperationPacket.TryParse(
            ReadPayload([0x08, 0x11, 0x18, 0x22, 0x30, 0x45, 0x38, 0x33, 0x4A, 0x03, 0xAA, 0xBB]),
            out _));
    }

    [Fact]
    public void TryParseRejectsUInt32Overflow()
    {
        byte[] payload =
        [
            0x08, 0x80, 0x80, 0x80, 0x80, 0x10,
            0x18, 0x22,
            0x30, 0x45,
            0x38, 0x33,
        ];

        Assert.False(MsgItemOperationPacket.TryParse(ReadPayload(payload), out _));
    }

    [Fact]
    public void TryParseRejectsWrongOpcode()
    {
        var frame = ReadPayload(
            [0x08, 0x11, 0x18, 0x22, 0x30, 0x45, 0x38, 0x33],
            opcode: MsgItemOperationPacket.Opcode - 1);

        Assert.False(MsgItemOperationPacket.TryParse(frame, out _));
    }

    [Fact]
    public void TryParseSkipsWrongKnownWireTypesWhenValidInstancesFollow()
    {
        byte[] payload =
        [
            0x0A, 0x01, 0xAA,
            0x1D, 0x01, 0x02, 0x03, 0x04,
            0x31, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x3A, 0x02, 0xBB, 0xCC,
            0x08, 0x11,
            0x18, 0x22,
            0x30, 0x45,
            0x38, 0x33,
        ];

        Assert.True(MsgItemOperationPacket.TryParse(ReadPayload(payload), out var packet));
        Assert.Equal(0x11U, packet.HostItemUid);
        Assert.Equal(0x22U, packet.SecondaryItemUid);
        Assert.Equal(MsgItemOperationAction.EmbedAnima, packet.Action);
        Assert.Equal(0x33U, packet.TimestampToken);
    }

    [Fact]
    public void TryParseDoesNotCountWrongWireTypeAsCommonRequiredFieldPresence()
    {
        byte[] payload =
        [
            0x0A, 0x01, 0x11,
            0x18, 0x22,
            0x30, 0x45,
            0x38, 0x33,
        ];

        Assert.False(MsgItemOperationPacket.TryParse(ReadPayload(payload), out _));
    }

    private static PacketFrame ReadPayload(
        ReadOnlySpan<byte> payload,
        ushort opcode = MsgItemOperationPacket.Opcode)
    {
        var writer = new PacketWriter(opcode, payload.Length);
        payload.CopyTo(writer.Payload);
        return ReadFrame(writer.ToArray());
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
    }
}
