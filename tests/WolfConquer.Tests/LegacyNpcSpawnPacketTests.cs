using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNpcSpawnPacketTests
{
    [Fact]
    public void LegacyNpcSpawnOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2210, WorldPacketOpcodes.LegacyNpcSpawn);
        Assert.Equal(WorldPacketOpcodes.LegacyNpcSpawn, LegacyNpcSpawnPacket.Opcode);
    }

    [Fact]
    public void LegacyNpcSpawnPacketWritesLegacyProtobufShape()
    {
        var packet = new LegacyNpcSpawnPacket(
            Uid: 200,
            Uid2: 200,
            Unknown: 0,
            X: 10,
            Y: 20,
            Mesh: 51700,
            Type: 2,
            Action: 0);

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyNpcSpawnPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(
        [
            0x08, 0xC8, 0x01,
            0x10, 0xC8, 0x01,
            0x18, 0x00,
            0x20, 0x0A,
            0x28, 0x14,
            0x30, 0xF4, 0x93, 0x03,
            0x38, 0x02,
            0x40, 0x00,
        ], frame.Payload.ToArray());
    }

    [Fact]
    public void LegacyNpcSpawnPacketBuildsFromSeedNpc()
    {
        var npc = new Npc(Id: 20499, Kind: 2, Lookface: 47496, MapId: 1002, X: 577, Y: 586, OptionalFlag: 1);

        var packet = LegacyNpcSpawnPacket.FromNpc(npc);

        Assert.Equal(new LegacyNpcSpawnPacket(20499, 20499, 0, 577, 586, 47496, 2, 0), packet);
    }

    [Fact]
    public void LegacyNpcSpawnPacketRoundTrips()
    {
        var packet = new LegacyNpcSpawnPacket(20499, 20499, 0, 577, 586, 47496, 2, 9);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyNpcSpawnPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyNpcSpawnPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyNpcSpawnPacket.Opcode + 1, payloadLength: 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyNpcSpawnPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
