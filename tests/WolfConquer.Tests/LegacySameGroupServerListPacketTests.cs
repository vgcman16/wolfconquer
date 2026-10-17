using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySameGroupServerListPacketTests
{
    [Fact]
    public void LegacySameGroupServerListOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2162, WorldPacketOpcodes.LegacySameGroupServerList);
        Assert.Equal(WorldPacketOpcodes.LegacySameGroupServerList, LegacySameGroupServerListPacket.Opcode);
    }

    [Fact]
    public void LegacySameGroupServerListPacketWritesLegacyProtobufShape()
    {
        var packet = new LegacySameGroupServerListPacket(
        [
            new LegacySameGroupServer(2, 1, "TestSrv", 3935, 222, 214, 0),
        ]);
        var bytes = packet.ToFrameBytes();

        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        Assert.Equal(LegacySameGroupServerListPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(
        [
            0x0A, 0x16,
            0x08, 0x02,
            0x10, 0x01,
            0x1A, 0x07, (byte)'T', (byte)'e', (byte)'s', (byte)'t', (byte)'S', (byte)'r', (byte)'v',
            0x20, 0xDF, 0x1E,
            0x28, 0xDE, 0x01,
            0x30, 0xD6, 0x01,
        ], frame.Payload.ToArray());
    }

    [Fact]
    public void LegacySameGroupServerListBuildsFromClientConfigRows()
    {
        Assert.True(LegacySameGroupServerListPacket.TryCreateFromClientConfigs(
        [
            ClientConfig(
                serverId: 2,
                serverName: "TestSrv",
                mapId: 3935,
                xRaw: "0222",
                x: 222,
                yRaw: "0214",
                y: 214,
                host: "127.0.0.1",
                port: 9220),
        ], out var packet));

        var server = Assert.Single(packet.Servers);
        Assert.Equal(2U, server.ServerId);
        Assert.Equal(LegacySameGroupServerListPacket.StartupServerType, server.Type);
        Assert.Equal("TestSrv", server.Name);
        Assert.Equal(3935U, server.MapId);
        Assert.Equal(222U, server.X);
        Assert.Equal(214U, server.Y);
        Assert.Equal(0U, server.Unknown);
    }

    [Fact]
    public void LegacySameGroupServerListPacketRoundTrips()
    {
        var packet = new LegacySameGroupServerListPacket(
        [
            new LegacySameGroupServer(2, 1, "TestSrv", 3935, 222, 214, 0),
            new LegacySameGroupServer(101, 1, "Realm1", 3935, 328, 198, 0),
        ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacySameGroupServerListPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet.Servers, parsed.Servers);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static ClientConfigRecord ClientConfig(
        int serverId,
        string serverName,
        int mapId,
        string xRaw,
        int x,
        string yRaw,
        int y,
        string host,
        int port)
    {
        return new ClientConfigRecord(
            serverId,
            serverName,
            mapId,
            xRaw,
            x,
            yRaw,
            y,
            "0001",
            1,
            "0000",
            0,
            1,
            3,
            host,
            port,
            "client_config.ini",
            serverId,
            $"{serverId} {serverName} {mapId} {xRaw} {yRaw} 0001 0000 1 3 {host} {port}",
            [$"{serverId}", serverName, $"{mapId}", xRaw, yRaw, "0001", "0000", "1", "3", host, $"{port}"]);
    }
}
