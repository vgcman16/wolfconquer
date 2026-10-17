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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
