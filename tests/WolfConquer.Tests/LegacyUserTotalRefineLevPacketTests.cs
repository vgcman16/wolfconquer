using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyUserTotalRefineLevPacketTests
{
    [Fact]
    public void LegacyUserTotalRefineLevOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2128, WorldPacketOpcodes.LegacyUserTotalRefineLev);
        Assert.Equal(WorldPacketOpcodes.LegacyUserTotalRefineLev, LegacyUserTotalRefineLevPacket.Opcode);
    }

    [Fact]
    public void LegacyUserTotalRefineLevSyntheticFrameParsesAndWritesExactly()
    {
        // Independently encoded protobuf tags with two-byte varints for UID 300 and level 150.
        byte[] expected = [12, 0, 0x50, 0x08, 0x08, 1, 0x10, 0xac, 0x02, 0x18, 0x96, 0x01];
        var frame = ReadFrame(expected);

        Assert.True(LegacyUserTotalRefineLevPacket.TryParse(frame, out var packet));

        Assert.Equal(1U, packet.Type);
        Assert.Equal(300U, packet.Uid);
        Assert.Equal(150U, packet.Level);
        Assert.Equal(expected, packet.ToFrameBytes());
    }

    [Fact]
    public void LegacyUserTotalRefineLevRoundTripsFields()
    {
        var packet = new LegacyUserTotalRefineLevPacket(Type: 0, Uid: 9_000_001, Level: 12);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyUserTotalRefineLevPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
