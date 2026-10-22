using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTradePacketTests
{
    [Fact]
    public void LegacyTradeOpcodeMatchesLegacyCatalog()
    {
        Assert.Equal((ushort)2119, WorldPacketOpcodes.LegacyTrade);
        Assert.Equal(WorldPacketOpcodes.LegacyTrade, LegacyTradePacket.Opcode);
    }

    [Fact]
    public void LegacyTradeLiveQueryRoundTripsCapturedType()
    {
        var packet = new LegacyTradePacket(Type: 2);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyTradePacket.Opcode, frame.Header.Opcode);
        Assert.Equal([0x08, 0x02], frame.Payload.ToArray());
        Assert.True(LegacyTradePacket.TryParse(frame, out var parsed));
        Assert.Equal(2U, parsed.Type);
    }

    [Fact]
    public void LegacyTradeRejectsWrongOpcodeMissingTypeAndUnknownFields()
    {
        Assert.False(LegacyTradePacket.TryParse(
            ReadFrame(new PacketWriter(WorldPacketOpcodes.LegacyTradePartner, payloadLength: 2).ToArray()),
            out _));

        Assert.False(LegacyTradePacket.TryParse(
            ReadFrame(new PacketWriter(WorldPacketOpcodes.LegacyTrade, payloadLength: 0).ToArray()),
            out _));

        var unknown = new PacketWriter(WorldPacketOpcodes.LegacyTrade, payloadLength: 4);
        unknown.Payload[0] = 0x08;
        unknown.Payload[1] = 0x02;
        unknown.Payload[2] = 0x78;
        unknown.Payload[3] = 0x01;
        Assert.False(LegacyTradePacket.TryParse(ReadFrame(unknown.ToArray()), out _));
    }

    [Fact]
    public void LegacyTradeNativeOutputWritesAllSixRequiredFieldsIncludingZero()
    {
        var packet = new LegacyTradePacket(Type: 2);
        var frame = ReadFrame(packet.ToNativeFrameBytes());

        Assert.Equal((ushort)2119, frame.Header.Opcode);
        Assert.Equal(new byte[] { 0x08, 2, 0x10, 0, 0x18, 0, 0x20, 0, 0x28, 0, 0x30, 0 },
            frame.Payload.ToArray());
        Assert.True(LegacyTradePacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyTradeNativeFieldsKeepSignedMoneyAndFullUnsignedWidths()
    {
        var packet = new LegacyTradePacket(Type: 2)
        {
            Action = 0x1_0000_0002UL,
            Money = -1,
            ItemUID = ulong.MaxValue,
            CPS = uint.MaxValue,
            Unknown = 0x8000_0000_0000_0000UL,
            UID = ulong.MaxValue
        };
        byte[] expected =
        [
            0x08, 0x82, 0x80, 0x80, 0x80, 0x10,
            0x10, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01,
            0x18, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01,
            0x20, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F,
            0x28, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01,
            0x30, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01
        ];

        var frame = ReadFrame(packet.ToNativeFrameBytes());

        Assert.Equal(expected, frame.Payload.ToArray());
        Assert.True(LegacyTradePacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
        Assert.Equal(2U, parsed.Type); // Native switch narrows Action to its uint enum.
        Assert.Equal(0x1_0000_0002UL, parsed.Action);
        Assert.Equal(ulong.MaxValue, parsed.UID);
        Assert.Equal(ulong.MaxValue, parsed.ItemUID);
        Assert.Equal(-1L, parsed.Money); // int64, not zigzag sint64.
    }

    [Fact]
    public void LegacyTradeKnownInputFieldsAllowReorderingAndLastValueWins()
    {
        byte[] payload = [0x30, 0x7F, 0x08, 2, 0x10, 3, 0x20, 5, 0x08, 11, 0x30, 0x80, 1];

        Assert.True(LegacyTradePacket.TryParse(ReadPayload(payload), out var parsed));

        Assert.Equal(11U, parsed.Type);
        Assert.Equal(11UL, parsed.Action);
        Assert.Equal(3L, parsed.Money);
        Assert.Equal(5U, parsed.CPS);
        Assert.Equal(128UL, parsed.UID);
        Assert.Equal(0UL, parsed.ItemUID);
        Assert.Equal(0UL, parsed.Unknown);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void LegacyTradeRejectsWrongWireTypeAndTruncatedKnownVarints(int field)
    {
        Assert.False(LegacyTradePacket.TryParse(
            ReadPayload([0x08, 2, (byte)((field << 3) | 2), 1, 0]), out _));
        Assert.False(LegacyTradePacket.TryParse(
            ReadPayload([0x08, 2, (byte)(field << 3), 0x80]), out _));
        Assert.False(LegacyTradePacket.TryParse(
            ReadPayload([0x08, 2, (byte)(field << 3),
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 2]), out _));
    }

    [Fact]
    public void LegacyTradeRejectsCpsOverflowAndHighFieldNumberAliasing()
    {
        Assert.False(LegacyTradePacket.TryParse(
            ReadPayload([0x08, 2, 0x20, 0x80, 0x80, 0x80, 0x80, 0x10]), out _));
        Assert.False(LegacyTradePacket.TryParse(
            ReadPayload([0x08, 2, 0x88, 0x80, 0x80, 0x80, 0x80, 1, 1]), out _));
        Assert.False(LegacyTradePacket.TryParse(ReadPayload([0x08]), out _));
        Assert.False(LegacyTradePacket.TryParse(ReadPayload([0x10, 0]), out _));
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(0L)]
    [InlineData(long.MaxValue)]
    public void LegacyTradeNativeMoneyRoundTripsFullInt64Range(long money)
    {
        var packet = new LegacyTradePacket(8) { Money = money };

        Assert.True(LegacyTradePacket.TryParse(ReadFrame(packet.ToNativeFrameBytes()), out var parsed));

        Assert.Equal(money, parsed.Money);
    }

    [Fact]
    public void LegacyTradeCapturedWriterStaysTypeOnlyWhenNativeDataIsPresent()
    {
        var packet = new LegacyTradePacket(2) { Action = 11, Money = 500, UID = ulong.MaxValue };

        Assert.Equal(new byte[] { 0x08, 2 }, ReadFrame(packet.ToFrameBytes()).Payload.ToArray());
        Assert.Equal(11UL, packet.Action);
    }

    private static PacketFrame ReadPayload(byte[] payload)
    {
        var writer = new PacketWriter(LegacyTradePacket.Opcode, payload.Length);
        payload.CopyTo(writer.Payload);
        return ReadFrame(writer.ToArray());
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
