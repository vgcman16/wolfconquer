using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPetInfoPacketTests
{
    [Fact]
    public void LegacyPetInfoOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal(2192, LegacyPetInfoPacket.Opcode);
    }

    [Fact]
    public void LegacyPetInfoPacketRoundTripsSourceShape()
    {
        var packet = new LegacyPetInfoPacket(
            Uid: 700_201,
            PetId: 4_264,
            PetType: 9,
            Mesh: 980,
            Range: 14,
            X: 831,
            Y: 585,
            Name: "Thundercloud");
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyPetInfoPacket.TryParse(frame, out var parsed));

        Assert.Equal(700_201U, parsed.Uid);
        Assert.Equal(4_264U, parsed.PetId);
        Assert.Equal(9U, parsed.PetType);
        Assert.Equal(980UL, parsed.Mesh);
        Assert.Equal(14, parsed.Range);
        Assert.Equal(831, parsed.X);
        Assert.Equal(585, parsed.Y);
        Assert.Equal("Thundercloud", parsed.Name);
    }

    [Fact]
    public void LegacyPetInfoPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyPetInfoPacket.Opcode + 1, payloadLength: 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyPetInfoPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
