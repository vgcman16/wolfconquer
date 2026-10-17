using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeamArenaInfoPlayersPacketTests
{
    [Fact]
    public void LegacyTeamArenaInfoPlayersOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2314, WorldPacketOpcodes.LegacyTeamArenaInfoPlayers);
        Assert.Equal(WorldPacketOpcodes.LegacyTeamArenaInfoPlayers, LegacyTeamArenaInfoPlayersPacket.Opcode);
    }

    [Fact]
    public void ResponseRoundTripsHeaderAndRows()
    {
        var packet = new LegacyTeamArenaInfoPlayersPacket(
            Mode: (uint)LegacyTeamArenaInfoPlayerKind.Opponents,
            LeaderUid: 1_293_600,
            Count: 2,
            [
                new LegacyTeamArenaInfoPlayerEntry(
                    Uid: 1_293_600,
                    Level: 140,
                    Class: 7051,
                    Mesh: 1541008,
                    TodayRank: 3,
                    ArenaPoints: 4500,
                    Name: "HeroOne"),
                new LegacyTeamArenaInfoPlayerEntry(
                    Uid: 400_001,
                    Level: 139,
                    Class: 7052,
                    Mesh: 1541009,
                    TodayRank: 7,
                    ArenaPoints: 3900,
                    Name: "Partner"),
            ]);

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyTeamArenaInfoPlayers, frame.Header.Opcode);
        Assert.Equal(
            LegacyTeamArenaInfoPlayersPacket.EntriesOffset + (2 * LegacyTeamArenaInfoPlayersPacket.EntryPayloadLength),
            frame.Payload.Length);
        Assert.True(LegacyTeamArenaInfoPlayersPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet.Mode, parsed.Mode);
        Assert.Equal(packet.LeaderUid, parsed.LeaderUid);
        Assert.Equal(packet.Count, parsed.Count);
        Assert.Equal(packet.Entries, parsed.Entries);
    }

    [Fact]
    public void EmptyResponseRoundTripsHeaderOnly()
    {
        var packet = LegacyTeamArenaInfoPlayersPacket.Empty(LegacyTeamArenaInfoPlayerKind.Neutral, leaderUid: 1_293_600);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyTeamArenaInfoPlayers, frame.Header.Opcode);
        Assert.Equal(LegacyTeamArenaInfoPlayersPacket.EntriesOffset, frame.Payload.Length);
        Assert.True(LegacyTeamArenaInfoPlayersPacket.TryParse(frame, out var parsed));
        Assert.Equal(0U, parsed.Mode);
        Assert.Equal(1_293_600U, parsed.LeaderUid);
        Assert.Equal(0U, parsed.Count);
        Assert.Empty(parsed.Entries);
    }

    [Fact]
    public void RowLengthAndFieldOrderMatchLegacyBuilderShape()
    {
        var packet = new LegacyTeamArenaInfoPlayersPacket(
            Mode: (uint)LegacyTeamArenaInfoPlayerKind.Opponents,
            LeaderUid: 0x01020304,
            Count: 1,
            [
                new LegacyTeamArenaInfoPlayerEntry(
                    Uid: 0x11121314,
                    Level: 0x21222324,
                    Class: 0x31323334,
                    Mesh: 0x41424344,
                    TodayRank: 0x51525354,
                    ArenaPoints: 0x61626364,
                    Name: "LegacyName"),
            ]);

        var frame = ReadFrame(packet.ToFrameBytes());
        var payload = frame.Payload.ToArray();
        var rowOffset = LegacyTeamArenaInfoPlayersPacket.EntriesOffset;

        Assert.Equal(56, LegacyTeamArenaInfoPlayersPacket.EntryPayloadLength);
        Assert.Equal(68, frame.Payload.Length);
        Assert.Equal(1U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0)));
        Assert.Equal(0x01020304U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(4)));
        Assert.Equal(1U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(8)));
        Assert.Equal(0x11121314U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(rowOffset)));
        Assert.Equal(0x21222324U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(rowOffset + 4)));
        Assert.Equal(0x31323334U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(rowOffset + 8)));
        Assert.Equal(0x41424344U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(rowOffset + 12)));
        Assert.Equal(0x51525354U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(rowOffset + 16)));
        Assert.Equal(0x61626364U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(rowOffset + 20)));
        Assert.Equal("LegacyName", Encoding.UTF8.GetString(payload.AsSpan(rowOffset + 24, "LegacyName".Length)));
        Assert.All(payload.AsSpan(rowOffset + 24 + "LegacyName".Length, 32 - "LegacyName".Length).ToArray(), b => Assert.Equal(0, b));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
