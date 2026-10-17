using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyArenaWatchersPacketTests
{
    [Fact]
    public void LegacyArenaWatchersOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2291, WorldPacketOpcodes.LegacyArenaWatchers);
        Assert.Equal(WorldPacketOpcodes.LegacyArenaWatchers, LegacyArenaWatchersPacket.Opcode);
    }

    [Fact]
    public void LegacyArenaWatchersQueryRoundTripsModeAndUid()
    {
        var query = LegacyArenaWatchersPacket.Query(LegacyArenaWatcherMode.RequestView, uid: 1_293_600);
        var frame = ReadFrame(query.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyArenaWatchers, frame.Header.Opcode);
        Assert.Equal(LegacyArenaWatchersPacket.QueryPayloadLength, frame.Payload.Length);
        Assert.True(LegacyArenaWatchersPacket.TryParseQuery(frame, out var parsed));
        Assert.True(parsed.IsQuery);
        Assert.Equal(LegacyArenaWatcherMode.RequestView, parsed.Mode);
        Assert.Equal(1_293_600U, parsed.Uid);
    }

    [Fact]
    public void LegacyArenaWatchersResponseRoundTripsWatcherRows()
    {
        var packet = LegacyArenaWatchersPacket.Create(
            LegacyArenaWatcherMode.Watchers,
            dwParam: 0,
            uid: 0,
            count: 1,
            player1Cheers: 2,
            player2Cheers: 3,
            [
                new LegacyArenaWatcherEntry(
                    Mesh: 1541008,
                    Name: "Watcher",
                    Uid: 400002,
                    Level: 140,
                    Class: 7051,
                    TodayRank: 12),
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(
            LegacyArenaWatchersPacket.HeaderPayloadLength + LegacyArenaWatchersPacket.EntryPayloadLength,
            frame.Payload.Length);
        Assert.True(LegacyArenaWatchersPacket.TryParse(frame, out var parsed));
        Assert.False(parsed.IsQuery);
        Assert.Equal(LegacyArenaWatcherMode.Watchers, parsed.Mode);
        Assert.Equal(1U, parsed.Count);
        Assert.Equal(2U, parsed.Player1Cheers);
        Assert.Equal(3U, parsed.Player2Cheers);
        var entry = Assert.Single(parsed.Entries);
        Assert.Equal(1541008U, entry.Mesh);
        Assert.Equal("Watcher", entry.Name);
        Assert.Equal(400002U, entry.Uid);
        Assert.Equal(140U, entry.Level);
        Assert.Equal(7051U, entry.Class);
        Assert.Equal(12U, entry.TodayRank);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
