using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMapStatusPacketTests
{
    [Fact]
    public void LegacyMapStatusOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2213, WorldPacketOpcodes.LegacyMapStatus);
        Assert.Equal(WorldPacketOpcodes.LegacyMapStatus, LegacyMapStatusPacket.Opcode);
    }

    [Fact]
    public void LegacyMapStatusPacketWritesLegacyProtobufShape()
    {
        var packet = new LegacyMapStatusPacket(1002, 1002, 8192);
        var bytes = packet.ToFrameBytes();

        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        Assert.Equal(LegacyMapStatusPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(
        [
            0x08, 0xEA, 0x07,
            0x10, 0xEA, 0x07,
            0x18, 0x80, 0x40,
        ], frame.Payload.ToArray());
    }

    [Fact]
    public void LegacyMapStatusPacketWritesOptionalWeatherWhenPresent()
    {
        var frame = ReadFrame(new LegacyMapStatusPacket(1002, 1002, 8192, Weather: 7).ToFrameBytes());

        Assert.Equal(
        [
            0x08, 0xEA, 0x07,
            0x10, 0xEA, 0x07,
            0x18, 0x80, 0x40,
            0x20, 0x07,
        ], frame.Payload.ToArray());
    }

    [Fact]
    public void LegacyMapStatusPacketRoundTrips()
    {
        var packet = new LegacyMapStatusPacket(3935, 3935, 846641133264903, Weather: 3);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyMapStatusPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyMapStatusPacketBuildsFromMapMetadataTypeStatus()
    {
        var maps = new Dictionary<int, MapMetadata>
        {
            [1015] = Map(1015, type: 4_294_967_295),
        };

        var packet = LegacyMapStatusPacket.FromMap(1015, maps);

        Assert.Equal(new LegacyMapStatusPacket(1015, 1015, 4_294_967_295), packet);
    }

    [Fact]
    public void LegacyMapStatusPacketFallsBackToCurrentMapWithZeroStatusWhenMetadataIsMissing()
    {
        var packet = LegacyMapStatusPacket.FromMap(1015, mapsById: null);

        Assert.Equal(new LegacyMapStatusPacket(1015, 1015, Status: 0), packet);
    }

    [Fact]
    public void LegacyMapStatusPacketUsesBaseMapForKnownAliasMaps()
    {
        var maps = new Dictionary<int, MapMetadata>
        {
            [1351] = Map(1351, type: 123),
        };

        var packet = LegacyMapStatusPacket.FromMap(2353, maps);

        Assert.Equal(new LegacyMapStatusPacket(1351, 1351, Status: 123), packet);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static MapMetadata Map(int id, long type)
    {
        return new MapMetadata(
            id,
            $"{id}.ini",
            "TestMap",
            MapDoc: 1000,
            TypeRaw: type.ToString(),
            Type: type,
            Portal0X: null,
            Portal0Y: null,
            RebornMap: 1002,
            RebornPortal: 0,
            ResLev: 0,
            LinkMap: 0,
            LinkX: 0,
            LinkY: 0,
            ColorRaw: "0",
            Color: 0,
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = id.ToString(),
                ["type"] = type.ToString(),
                ["color"] = "0",
            });
    }
}
