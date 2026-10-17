using System.Buffers;
using System.Net;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBeastsOptPacketTests : IDisposable
{
    private readonly string _dbRoot = Path.Combine(Path.GetTempPath(), "wolf-beasts-opt-handler-" + Guid.NewGuid());

    [Fact]
    public void LegacyBeastsOptOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2342, WorldPacketOpcodes.LegacyBeastsOpt);
        Assert.Equal(WorldPacketOpcodes.LegacyBeastsOpt, LegacyBeastsOptPacket.Opcode);
    }

    [Fact]
    public void LegacyBeastsOptTypeZeroRoundTripsWithoutItems()
    {
        var packet = new LegacyBeastsOptPacket(Type: 0, Items: []);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyBeastsOpt, frame.Header.Opcode);
        Assert.True(LegacyBeastsOptPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyBeastsOptTypeOneRoundTripsWithOneItem()
    {
        var packet = new LegacyBeastsOptPacket(
            Type: 1,
            Items: [new LegacyBeastsOptPacket.Item(UID: 5_000_001, Stack: 25)]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyBeastsOpt, frame.Header.Opcode);
        Assert.True(LegacyBeastsOptPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
        var item = Assert.Single(parsed.Items);
        Assert.Equal(5_000_001U, item.UID);
        Assert.Equal((ushort)25, item.Stack);
    }

    [Fact]
    public async Task HandlerActivatesBeastsWhenTypeZeroArrivesWithFiveEquippedRunes()
    {
        var (handler, connection, sent) = await LoginWithRunePositionsAsync(101, 102, 103, 104, 105);
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyBeastsOptPacket(Type: 0, Items: []).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 2, sent.Count);
        Assert.True(LegacyBeastsInfoPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var beastsInfo));
        Assert.True(beastsInfo.Activated);
        Assert.Equal((byte)1, beastsInfo.Level);
        Assert.True(LegacyBeastsOptPacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var echo));
        Assert.Equal(0U, echo.Type);
        Assert.Empty(echo.Items);
    }

    [Fact]
    public async Task HandlerDoesNotActivateBeastsWhenTypeZeroArrivesWithFewerThanFiveEquippedRunes()
    {
        var (handler, connection, sent) = await LoginWithRunePositionsAsync(101, 102, 103, 104);
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyBeastsOptPacket(Type: 0, Items: []).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 2, sent.Count);
        Assert.True(LegacyBeastsInfoPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var beastsInfo));
        Assert.False(beastsInfo.Activated);
        Assert.Equal((byte)0, beastsInfo.Level);
        Assert.True(LegacyBeastsOptPacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var echo));
        Assert.Equal(0U, echo.Type);
        Assert.Empty(echo.Items);
    }

    [Fact]
    public async Task HandlerRefreshesBeastsInfoWhenTypeTwoArrivesAndDoesNotLogDeferred()
    {
        using var log = new StringWriter();
        var (handler, connection, sent) = await LoginWithRunePositionsAsync(log, 101, 102, 103, 104, 105);
        var countBeforeRequest = sent.Count;
        var request = new LegacyBeastsOptPacket(
            Type: 2,
            Items: [new LegacyBeastsOptPacket.Item(UID: 9_999_001, Stack: 7)]);

        await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 2, sent.Count);
        Assert.True(LegacyBeastsInfoPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var beastsInfo));
        Assert.Equal(1_293_600U, beastsInfo.Uid);
        Assert.True(beastsInfo.Activated);
        Assert.Equal((byte)1, beastsInfo.Level);
        Assert.True(LegacyBeastsOptPacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var echo));
        Assert.Equal(request, echo);

        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyBeastsOpt", output);
        Assert.Contains("type=2", output);
        Assert.DoesNotContain("WorldServer deferred LegacyBeastsOpt", output);
    }

    [Fact]
    public async Task HandlerTreatsOtherBeastsOptTypesAsLegacyNoOpRefreshAndEcho()
    {
        using var log = new StringWriter();
        var (handler, connection, sent) = await LoginWithRunePositionsAsync(log, 101, 102, 103, 104);
        var countBeforeRequest = sent.Count;
        var request = new LegacyBeastsOptPacket(
            Type: 3,
            Items: [new LegacyBeastsOptPacket.Item(UID: 9_999_001, Stack: 2)]);

        await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 2, sent.Count);
        Assert.True(LegacyBeastsInfoPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var beastsInfo));
        Assert.False(beastsInfo.Activated);
        Assert.Equal((byte)0, beastsInfo.Level);
        Assert.True(LegacyBeastsOptPacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var echo));
        Assert.Equal(request, echo);

        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyBeastsOpt no-op", output);
        Assert.Contains("type=3", output);
        Assert.DoesNotContain("WorldServer deferred LegacyBeastsOpt", output);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dbRoot))
        {
            Directory.Delete(_dbRoot, recursive: true);
        }
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)>
        LoginWithRunePositionsAsync(params ushort[] runePositions)
    {
        return await LoginWithRunePositionsAsync(log: null, runePositions)
            .ConfigureAwait(false);
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)>
        LoginWithRunePositionsAsync(TextWriter? log, params ushort[] runePositions)
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        WriteSnapshot(usersRoot, accountId: 1293600, uid: 1293600);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                runePositions
                    .Select((position, index) => PlayerItemSnapshotLoaderTests.DbItem(
                        uid: (uint)(5_000_000 + index),
                        itemId: (uint)(4010501 + index),
                        position: position,
                        stackSize: 1))
                    .ToArray(),
                []));

        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), handoff, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        return (handler, connection, sent);
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

    private static void WriteSnapshot(string usersRoot, long accountId, long uid)
    {
        File.WriteAllText(
            Path.Combine(usersRoot, accountId + ".ini"),
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
