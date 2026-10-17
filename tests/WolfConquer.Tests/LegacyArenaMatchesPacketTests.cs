using System.Buffers;
using System.Net;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyArenaMatchesPacketTests : IDisposable
{
    private readonly string _dbRoot = Path.Combine(Path.GetTempPath(), "wolf-arena-matches-" + Guid.NewGuid());
    private readonly string _usersRoot;

    public LegacyArenaMatchesPacketTests()
    {
        _usersRoot = Path.Combine(_dbRoot, "Users");
    }

    [Fact]
    public void LegacyArenaMatchesOpcodesMatchLegacyWorldOpcodes()
    {
        Assert.Equal((ushort)2097, WorldPacketOpcodes.LegacyArenaMatches);
        Assert.Equal((ushort)2069, WorldPacketOpcodes.LegacyTeamArenaMatches);
        Assert.Equal(WorldPacketOpcodes.LegacyArenaMatches, LegacyArenaMatchesPacket.ArenaOpcode);
        Assert.Equal(WorldPacketOpcodes.LegacyTeamArenaMatches, LegacyArenaMatchesPacket.TeamArenaOpcode);
    }

    [Theory]
    [InlineData(WorldPacketOpcodes.LegacyArenaMatches)]
    [InlineData(WorldPacketOpcodes.LegacyTeamArenaMatches)]
    public void LegacyArenaMatchesQueryRoundTripsPage(ushort opcode)
    {
        var query = LegacyArenaMatchesPacket.Query(opcode, page: 1);
        var frame = ReadFrame(query.ToFrameBytes());

        Assert.Equal(opcode, frame.Header.Opcode);
        Assert.Equal(LegacyArenaMatchesPacket.QueryPayloadLength, frame.Payload.Length);
        Assert.True(LegacyArenaMatchesPacket.TryParseQuery(frame, out var parsed));
        Assert.Equal(opcode, parsed.Opcode);
        Assert.Equal(1U, parsed.Page);
    }

    [Fact]
    public void LegacyArenaMatchesQueryIgnoresTrailingLegacyPayloadBytes()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyArenaMatches, LegacyArenaMatchesPacket.QueryPayloadLength + 4);
        writer.WriteUInt32(0, 2);
        writer.WriteUInt32(4, 0xAABBCCDD);
        var frame = ReadFrame(writer.ToArray());

        Assert.True(LegacyArenaMatchesPacket.TryParseQuery(frame, out var parsed));
        Assert.Equal(WorldPacketOpcodes.LegacyArenaMatches, parsed.Opcode);
        Assert.Equal(2U, parsed.Page);
    }

    [Theory]
    [InlineData(WorldPacketOpcodes.LegacyArenaMatches)]
    [InlineData(WorldPacketOpcodes.LegacyTeamArenaMatches)]
    public void LegacyArenaMatchesEmptyResponseUsesSourceHeaderShape(ushort opcode)
    {
        var packet = LegacyArenaMatchesPacket.Empty(opcode, page: 1);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(opcode, frame.Header.Opcode);
        Assert.Equal(LegacyArenaMatchesPacket.HeaderPayloadLength, frame.Payload.Length);
        Assert.True(LegacyArenaMatchesPacket.TryParse(frame, out var parsed));
        Assert.Equal(opcode, parsed.Opcode);
        Assert.Equal(1U, parsed.Page);
        Assert.Equal(LegacyArenaMatchesPacket.QualifierListArenaId, parsed.ArenaId);
        Assert.Equal(0U, parsed.MatchCount);
        Assert.Equal(0U, parsed.PlayerCount);
        Assert.Equal(0U, parsed.Reserved);
        Assert.Equal(0U, parsed.PageCount);
        Assert.Empty(parsed.Entries);
    }

    [Fact]
    public void LegacyTeamArenaMatchesResponseRoundTripsTeamRows()
    {
        var packet = LegacyArenaMatchesPacket.CreatePage(
            WorldPacketOpcodes.LegacyTeamArenaMatches,
            page: 1,
            [
                new LegacyArenaMatchesEntry(
                    FirstUid: 1,
                    FirstName: "HeroOne",
                    FirstMemberCount: 2,
                    SecondUid: 2,
                    SecondName: "Target",
                    SecondMemberCount: 1),
            ],
            playerCount: 0);

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyTeamArenaMatches, frame.Header.Opcode);
        Assert.Equal(
            LegacyArenaMatchesPacket.HeaderPayloadLength + LegacyArenaMatchesPacket.EntryPayloadLength,
            frame.Payload.Length);
        Assert.True(LegacyArenaMatchesPacket.TryParse(frame, out var parsed));
        Assert.Equal(1U, parsed.Page);
        Assert.Equal(LegacyArenaMatchesPacket.QualifierListArenaId, parsed.ArenaId);
        Assert.Equal(1U, parsed.MatchCount);
        Assert.Equal(0U, parsed.PlayerCount);
        Assert.Equal(1U, parsed.PageCount);
        var entry = Assert.Single(parsed.Entries);
        Assert.Equal(1U, entry.FirstUid);
        Assert.Equal("HeroOne", entry.FirstName);
        Assert.Equal(2U, entry.FirstMemberCount);
        Assert.Equal(2U, entry.SecondUid);
        Assert.Equal("Target", entry.SecondName);
        Assert.Equal(1U, entry.SecondMemberCount);
    }

    [Theory]
    [InlineData(WorldPacketOpcodes.LegacyArenaMatches)]
    [InlineData(WorldPacketOpcodes.LegacyTeamArenaMatches)]
    public async Task HandlerRepliesWithEmptyArenaMatchesResponseWhenNoMatchesAreRegistered(ushort opcode)
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync();
        var countBeforeQuery = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(LegacyArenaMatchesPacket.Query(opcode, page: 1).ToFrameBytes()));

        Assert.Equal(countBeforeQuery + 1, sent.Count);
        Assert.True(LegacyArenaMatchesPacket.TryParse(ReadFrame(sent[countBeforeQuery]), out var response));
        Assert.Equal(opcode, response.Opcode);
        Assert.Equal(1U, response.Page);
        Assert.Equal(LegacyArenaMatchesPacket.QualifierListArenaId, response.ArenaId);
        Assert.Equal(0U, response.MatchCount);
        Assert.Equal(0U, response.PlayerCount);
        Assert.Equal(0U, response.PageCount);
        Assert.Empty(response.Entries);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dbRoot))
        {
            Directory.Delete(_dbRoot, recursive: true);
        }
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)>
        LoginLegacyWorldAsync()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        return (handler, connection, sent);
    }

    private void WriteSnapshot(long accountId, long uid)
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, accountId + ".ini"),
            $$"""
            [Character]
            UID={{uid}}
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Conquer=0
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            RacePoints=0
            ChampionPoints=0
            GuildDontion=900
            PkPoints=0
            Online=7
            Day={{DateTime.Now.DayOfYear}}
            Activeness=0/80/2/1/10/1/22/0/0/

            [PaidNobility]
            Rank=King
            Identifier=1293600
            """);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }
}
