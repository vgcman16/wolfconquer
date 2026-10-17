using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyStaticRoleSpawnPacketTests
{
    [Fact]
    public void LegacyStaticRoleSpawnOpcodeMatchesLegacyViewOpcode()
    {
        Assert.Equal(WorldPacketOpcodes.LegacyRoleSpawn, LegacyStaticRoleSpawnPacket.Opcode);
    }

    [Fact]
    public void LegacyStaticRoleSpawnPacketWritesLegacyProtobufShape()
    {
        var packet = LegacyStaticRoleSpawnPacket.FromStaticRole(
            x: 120,
            y: 340,
            name: "Flag",
            mesh: 8910);

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyStaticRoleSpawnPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(
        [
            0x08, 0xCE, 0x45,
            0x10, 0x94, 0xAC, 0x07,
            0x18, 0x00,
            0x20, 0x00,
            0x28, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x30, 0x00,
            0x40, 0x00,
            0x48, 0x00,
            0x50, 0x00,
            0x58, 0x00,
            0x60, 0x00,
            0xD0, 0x01, 0x01,
            0xD8, 0x01, 0x78,
            0xE0, 0x01, 0xD4, 0x02,
            0xE8, 0x01, 0x00,
            0xF0, 0x01, 0x00,
            0xF8, 0x01, 0x00,
            0x90, 0x02, 0x00,
            0x98, 0x02, 0x01,
            0xA0, 0x02, 0x00,
            0xA8, 0x02, 0x00,
            0xB0, 0x02, 0x00,
            0xD0, 0x02, 0x00,
            0xD8, 0x02, 0x00,
            0xF8, 0x03, 0x00,
            0x80, 0x04, 0x00,
            0x88, 0x04, 0x00,
            0x90, 0x04, 0x00,
            0x98, 0x04, 0x00,
            0xA0, 0x04, 0x00,
            0xA8, 0x04, 0x00,
            0xF8, 0x04, 0x00,
            0xDA, 0x06, 0x04, 0x46, 0x6C, 0x61, 0x67,
            0xDA, 0x06, 0x00,
            0xE0, 0x06, 0x00,
            0xF0, 0x06, 0x00,
            0xF8, 0x06, 0x00,
            0x80, 0x07, 0x00,
            0x88, 0x07, 0x00,
            0xC8, 0x07, 0x00,
        ], frame.Payload.ToArray());
    }

    [Fact]
    public void LegacyStaticRoleSpawnPacketBuildsFromLegacyStaticRoleDefaults()
    {
        var packet = LegacyStaticRoleSpawnPacket.FromStaticRole(x: 120, y: 340);

        Assert.Equal(120340U, packet.Uid);
        Assert.Equal(8910U, packet.Mesh);
        Assert.Equal((byte)1, packet.MonsterLevel);
        Assert.Equal((ushort)120, packet.X);
        Assert.Equal((ushort)340, packet.Y);
        Assert.Equal((ushort)0, packet.Action);
        Assert.Equal((ushort)1, packet.Level);
        Assert.Equal("Flag", packet.Name);
        Assert.Equal(new ulong[10], packet.StatusFlags);
    }

    [Fact]
    public void LegacyStaticRoleSpawnPacketRoundTrips()
    {
        var statusFlags = new ulong[10];
        statusFlags[2] = 0x80;
        var packet = LegacyStaticRoleSpawnPacket.FromStaticRole(
            x: 321,
            y: 654,
            name: "Frozen Trap",
            mesh: 761,
            level: 1,
            action: 250,
            statusFlags: statusFlags);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyStaticRoleSpawnPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet.Uid, parsed.Uid);
        Assert.Equal(packet.Mesh, parsed.Mesh);
        Assert.Equal(packet.MonsterLevel, parsed.MonsterLevel);
        Assert.Equal(packet.X, parsed.X);
        Assert.Equal(packet.Y, parsed.Y);
        Assert.Equal(packet.Action, parsed.Action);
        Assert.Equal(packet.Level, parsed.Level);
        Assert.Equal(packet.Name, parsed.Name);
        Assert.Equal(packet.StatusFlags, parsed.StatusFlags);
    }

    [Fact]
    public void LegacyStaticRoleSpawnPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyStaticRoleSpawnPacket.Opcode + 1, payloadLength: 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyStaticRoleSpawnPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
