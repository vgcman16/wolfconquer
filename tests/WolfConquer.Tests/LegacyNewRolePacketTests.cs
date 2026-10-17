using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNewRolePacketTests
{
    [Fact]
    public void LegacyNewRoleOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2150, WorldPacketOpcodes.LegacyNewRole);
        Assert.Equal(WorldPacketOpcodes.LegacyNewRole, LegacyNewRolePacket.Opcode);
    }

    [Fact]
    public void LegacyNewRolePacketRoundTripsLegacyOffsets()
    {
        var packet = new LegacyNewRolePacket("FirstRole", 1008, 12);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyNewRolePacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyNewRolePacketRejectsShortPayload()
    {
        var writer = new PacketWriter(LegacyNewRolePacket.Opcode, LegacyNewRolePacket.MinimumPayloadLength - 1);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyNewRolePacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
