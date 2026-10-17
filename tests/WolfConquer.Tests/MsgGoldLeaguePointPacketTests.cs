using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgGoldLeaguePointPacketTests
{
    [Fact]
    public void MsgGoldLeaguePointOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2256, WorldPacketOpcodes.LegacyGoldLeaguePoint);
        Assert.Equal(WorldPacketOpcodes.LegacyGoldLeaguePoint, MsgGoldLeaguePointPacket.Opcode);
    }

    [Fact]
    public void MsgGoldLeaguePointWritesLegacyProtobufShape()
    {
        var frame = ReadFrame(new MsgGoldLeaguePointPacket(Points: 300, HistoryPoints: 0).ToFrameBytes());

        Assert.Equal(MsgGoldLeaguePointPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(
        [
            0x08, 0xAC, 0x02,
            0x10, 0x00,
        ], frame.Payload.ToArray());
    }

    [Fact]
    public void MsgGoldLeaguePointRoundTripsLegacyPayload()
    {
        var packet = new MsgGoldLeaguePointPacket(Points: 304_400, HistoryPoints: 12);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(MsgGoldLeaguePointPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void MsgGoldLeaguePointRejectsWrongOpcode()
    {
        var writer = new PacketWriter(MsgGoldLeaguePointPacket.Opcode + 1, payloadLength: 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(MsgGoldLeaguePointPacket.TryParse(frame, out _));
    }

    [Fact]
    public async Task HandlerAfterLegacyWorldLoginRepliesWithChampionPointsFromSession()
    {
        var usersRoot = CreateUsersRoot();
        try
        {
            WriteSnapshot(usersRoot, accountId: 1293600, championPoints: 304_400, historyChampionPoints: 12);
            var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot));
            var sent = new List<byte[]>();
            var connection = CreateSendingConnection(sent);

            await LoginLegacyWorldAsync(handler, connection, sent, key: 1293600);

            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new MsgGoldLeaguePointPacket(Points: 0, HistoryPoints: 0).ToFrameBytes()));

            var response = AssertSingleGoldLeaguePointResponse(sent);
            Assert.Equal(304_400U, response.Points);
            Assert.Equal(12U, response.HistoryPoints);
        }
        finally
        {
            DeleteUsersRoot(usersRoot);
        }
    }

    [Fact]
    public async Task HandlerAfterLegacyWorldLoginIgnoresInboundGoldLeaguePointPayload()
    {
        var usersRoot = CreateUsersRoot();
        try
        {
            WriteSnapshot(usersRoot, accountId: 1293600, championPoints: 88_001, historyChampionPoints: 33);
            var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot));
            var sent = new List<byte[]>();
            var connection = CreateSendingConnection(sent);

            await LoginLegacyWorldAsync(handler, connection, sent, key: 1293600);

            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new MsgGoldLeaguePointPacket(Points: 999_999, HistoryPoints: 777_777).ToFrameBytes()));

            var response = AssertSingleGoldLeaguePointResponse(sent);
            Assert.Equal(88_001U, response.Points);
            Assert.Equal(33U, response.HistoryPoints);
        }
        finally
        {
            DeleteUsersRoot(usersRoot);
        }
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static string CreateUsersRoot()
    {
        var usersRoot = Path.Combine(Path.GetTempPath(), "wolf-gold-league-point-" + Guid.NewGuid());
        Directory.CreateDirectory(usersRoot);
        return usersRoot;
    }

    private static void DeleteUsersRoot(string usersRoot)
    {
        if (Directory.Exists(usersRoot))
        {
            Directory.Delete(usersRoot, recursive: true);
        }
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static async Task LoginLegacyWorldAsync(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        List<byte[]> sent,
        uint key)
    {
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(key).ToFrameBytes()));
        Assert.Contains(sent, frameBytes => LegacyMsgMessagePacket.TryParse(ReadFrame(frameBytes), out var packet) &&
            packet.Content.Contains("ANSWER_OK"));
        sent.Clear();
    }

    private static MsgGoldLeaguePointPacket AssertSingleGoldLeaguePointResponse(IReadOnlyList<byte[]> sent)
    {
        Assert.Single(sent);

        var responses = sent
            .Select(ReadFrame)
            .Where(frame => MsgGoldLeaguePointPacket.TryParse(frame, out _))
            .Select(frame =>
            {
                Assert.True(MsgGoldLeaguePointPacket.TryParse(frame, out var packet));
                return packet;
            })
            .ToArray();

        return Assert.Single(responses);
    }

    private static void WriteSnapshot(
        string usersRoot,
        long accountId,
        long championPoints,
        long historyChampionPoints)
    {
        File.WriteAllText(
            Path.Combine(usersRoot, accountId + ".ini"),
            $$"""
            [Character]
            UID={{accountId}}
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
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
            ChampionPoints={{championPoints}}
            HistoryChampionPoints={{historyChampionPoints}}
            PkPoints=0
            Online=7
            Achivement=0/171966688/262144/523784/939949824/524288/1342381056/3/576/0/0/524288/25167856/6817280/0/
            Activeness=0/80/2/1/10/1/22/0/0/
            Dragon=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Pheonix=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Tiger=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Turtle=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            ChiPoints=153850

            [PaidNobility]
            Rank=King
            Identifier=1293600
            """);
    }
}
