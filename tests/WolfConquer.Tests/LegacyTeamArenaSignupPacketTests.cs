using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Text;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeamArenaSignupPacketTests : IDisposable
{
    private readonly string _dbRoot = Path.Combine(Path.GetTempPath(), "wolf-team-arena-signup-" + Guid.NewGuid());
    private readonly string _usersRoot;

    public LegacyTeamArenaSignupPacketTests()
    {
        _usersRoot = Path.Combine(_dbRoot, "Users");
    }

    [Theory]
    [InlineData(0, LegacyTeamArenaSignupDialogType.ArenaIconOn)]
    [InlineData(1, LegacyTeamArenaSignupDialogType.ArenaIconOff)]
    [InlineData(2, LegacyTeamArenaSignupDialogType.StartCountDown)]
    [InlineData(3, LegacyTeamArenaSignupDialogType.ArenaGui)]
    [InlineData(4, LegacyTeamArenaSignupDialogType.OpponentGaveUp)]
    [InlineData(5, LegacyTeamArenaSignupDialogType.BuyPoints)]
    [InlineData(6, LegacyTeamArenaSignupDialogType.Match)]
    [InlineData(7, LegacyTeamArenaSignupDialogType.YouAreKicked)]
    [InlineData(8, LegacyTeamArenaSignupDialogType.StartTheFight)]
    [InlineData(9, LegacyTeamArenaSignupDialogType.Dialog)]
    [InlineData(10, LegacyTeamArenaSignupDialogType.Dialog2)]
    [InlineData(11, LegacyTeamArenaSignupDialogType.Continue)]
    public void TeamArenaSignupDialogTypeDefinesLegacyNumericValues(int expected, LegacyTeamArenaSignupDialogType dialogType)
    {
        Assert.Equal((uint)expected, (uint)dialogType);
    }

    [Fact]
    public void TeamArenaSignupDialogButtonDefinesLegacyNumericValues()
    {
        Assert.Equal(0U, (uint)LegacyTeamArenaSignupDialogButton.SignUp);
        Assert.Equal(1U, (uint)LegacyTeamArenaSignupDialogButton.Win);
        Assert.Equal(1U, (uint)LegacyTeamArenaSignupDialogButton.Accept);
        Assert.Equal(2U, (uint)LegacyTeamArenaSignupDialogButton.DoGiveUp);
        Assert.Equal(3U, (uint)LegacyTeamArenaSignupDialogButton.Lose);
        Assert.Equal(3U, (uint)LegacyTeamArenaSignupDialogButton.MatchOff);
        Assert.Equal(5U, (uint)LegacyTeamArenaSignupDialogButton.MatchOn);
    }

    [Fact]
    public void TeamArenaInfoStatusDefinesLegacyNumericValues()
    {
        Assert.Equal(0U, (uint)LegacyTeamArenaInfoStatus.NotSignedUp);
        Assert.Equal(1U, (uint)LegacyTeamArenaInfoStatus.WaitingForOpponent);
        Assert.Equal(2U, (uint)LegacyTeamArenaInfoStatus.WaitingInactive);
    }

    [Fact]
    public void TeamArenaOpcodesMatchLegacyWorldOpcodes()
    {
        Assert.Equal((ushort)2060, WorldPacketOpcodes.LegacyTeamArenaSignup);
        Assert.Equal(WorldPacketOpcodes.LegacyTeamArenaSignup, LegacyTeamArenaSignupPacket.Opcode);
        Assert.Equal((ushort)2238, WorldPacketOpcodes.LegacyTeamArenaInfo);
        Assert.Equal(WorldPacketOpcodes.LegacyTeamArenaInfo, LegacyTeamArenaInfoPacket.Opcode);
    }

    [Fact]
    public void TeamArenaSignupQueryRoundTripsTypeAndButton()
    {
        var packet = new LegacyTeamArenaSignupPacket(
            LegacyTeamArenaSignupDialogType.BuyPoints,
            LegacyTeamArenaSignupDialogButton.SignUp);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyTeamArenaSignupPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void TeamArenaSignupResponseWritesSourceShapeWithoutArenaReservedUidGap()
    {
        var packet = new LegacyTeamArenaSignupResponsePacket(
            LegacyTeamArenaSignupDialogType.ArenaIconOn,
            LegacyTeamArenaSignupDialogButton.SignUp,
            CharacterUid: 1_293_600,
            CharacterName: "HeroOne",
            TodayRank: 7,
            Class: 7051,
            ArenaPoints: 4321,
            Level: 140);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(PacketReader.TryRead(frame.Frame.ToArray(), out var reader));
        Assert.Equal(LegacyTeamArenaSignupResponsePacket.Opcode, reader.Header.Opcode);
        Assert.Equal(68, reader.Header.Length);
        Assert.Equal(LegacyTeamArenaSignupResponsePacket.PayloadLength, reader.Payload.Length);
        Assert.Equal((uint)LegacyTeamArenaSignupDialogType.ArenaIconOn, reader.ReadUInt32(0));
        Assert.Equal((uint)LegacyTeamArenaSignupDialogButton.SignUp, reader.ReadUInt32(4));
        Assert.Equal(1_293_600U, reader.ReadUInt32(8));
        Assert.Equal("HeroOne", reader.ReadCString(12, 32, Encoding.UTF8));
        Assert.Equal(7U, reader.ReadUInt32(44));
        Assert.Equal(7051U, reader.ReadUInt32(48));
        Assert.Equal(0U, reader.ReadUInt32(52));
        Assert.Equal(4321U, reader.ReadUInt32(56));
        Assert.Equal(140U, reader.ReadUInt32(60));
    }

    [Fact]
    public void TeamArenaInfoWritesSourceElevenUIntShape()
    {
        var packet = new LegacyTeamArenaInfoPacket(
            TodayRank: 2,
            LegacyTeamArenaInfoStatus.WaitingForOpponent,
            TotalWin: 12,
            TotalLose: 3,
            TodayWin: 4,
            TodayBattles: 5,
            HistoryHonor: 600,
            CurrentHonor: 500,
            ArenaPoints: 4500,
            Unknown2: 9,
            Unknown3: 10);
        var frame = ReadFrame(packet.ToFrameBytes());
        var payload = frame.Payload.ToArray();

        Assert.Equal(WorldPacketOpcodes.LegacyTeamArenaInfo, frame.Header.Opcode);
        Assert.Equal(44, LegacyTeamArenaInfoPacket.PayloadLength);
        Assert.Equal(48, frame.Header.Length);
        Assert.Equal(2U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0)));
        Assert.Equal(1U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(4)));
        Assert.Equal(12U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(8)));
        Assert.Equal(3U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(12)));
        Assert.Equal(4U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(16)));
        Assert.Equal(5U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(20)));
        Assert.Equal(600U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(24)));
        Assert.Equal(500U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(28)));
        Assert.Equal(4500U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(32)));
        Assert.Equal(9U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(36)));
        Assert.Equal(10U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(40)));
        Assert.True(LegacyTeamArenaInfoPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public async Task HandlerTeamArenaInfoQueryRepliesFromSeededRecords()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(
            new TeamArenaStatisticsProvider(
            [
                TeamArenaRecord(400001, "Higher", points: 6000),
                TeamArenaRecord(1293600, "HeroOne", points: 4500, currentHonor: 500, historyHonor: 600, todayBattles: 5, todayWin: 4, totalLose: 3, totalWin: 12),
            ]));
        var countBeforeQuery = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(LegacyTeamArenaInfoPacket.QueryFrameBytes()));

        Assert.Equal(countBeforeQuery + 1, sent.Count);
        Assert.True(LegacyTeamArenaInfoPacket.TryParse(ReadFrame(sent[countBeforeQuery]), out var info));
        Assert.Equal(LegacyTeamArenaInfoStatus.NotSignedUp, info.Status);
        Assert.Equal(2U, info.TodayRank);
        Assert.Equal(4500U, info.ArenaPoints);
        Assert.Equal(12U, info.TotalWin);
        Assert.Equal(3U, info.TotalLose);
        Assert.Equal(4U, info.TodayWin);
        Assert.Equal(5U, info.TodayBattles);
        Assert.Equal(600U, info.HistoryHonor);
        Assert.Equal(500U, info.CurrentHonor);
    }

    [Fact]
    public async Task HandlerTeamArenaIconOnSendsSignupEchoAndTeamArenaInfo()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(
            new TeamArenaStatisticsProvider([TeamArenaRecord(1293600, "HeroOne", points: 4500)]));
        await CreateTeamAsync(handler, connection, sent);
        var countBeforeSignup = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));

        Assert.Equal(countBeforeSignup + 2, sent.Count);
        Assert.True(LegacyTeamArenaSignupResponsePacket.TryParse(ReadFrame(sent[countBeforeSignup]), out var response));
        Assert.Equal(LegacyTeamArenaSignupDialogType.ArenaIconOn, response.DialogType);
        Assert.Equal(1_293_600U, response.CharacterUid);
        Assert.Equal("HeroOne", response.CharacterName);
        Assert.Equal(1U, response.TodayRank);
        Assert.Equal(4500U, response.ArenaPoints);

        Assert.True(LegacyTeamArenaInfoPacket.TryParse(ReadFrame(sent[countBeforeSignup + 1]), out var info));
        Assert.Equal(LegacyTeamArenaInfoStatus.WaitingForOpponent, info.Status);
        Assert.Equal(1U, info.TodayRank);
        Assert.Equal(4500U, info.ArenaPoints);
    }

    [Fact]
    public async Task HandlerTeamArenaIconOnWithoutTeamSendsOnlySignupEcho()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(
            new TeamArenaStatisticsProvider([TeamArenaRecord(1293600, "HeroOne", points: 4500)]));
        var countBeforeSignup = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));

        Assert.Equal(countBeforeSignup + 1, sent.Count);
        Assert.True(LegacyTeamArenaSignupResponsePacket.TryParse(ReadFrame(sent[countBeforeSignup]), out var response));
        Assert.Equal(LegacyTeamArenaSignupDialogType.ArenaIconOn, response.DialogType);
    }

    [Fact]
    public async Task HandlerTeamArenaIconOffReturnsNotSignedUpAfterSignup()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(
            new TeamArenaStatisticsProvider([TeamArenaRecord(1293600, "HeroOne", points: 4500)]));
        await CreateTeamAsync(handler, connection, sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOn,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));
        var countBeforeOff = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaIconOff,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));

        Assert.Equal(countBeforeOff + 2, sent.Count);
        Assert.True(LegacyTeamArenaSignupResponsePacket.TryParse(ReadFrame(sent[countBeforeOff]), out var response));
        Assert.Equal(LegacyTeamArenaSignupDialogType.ArenaIconOff, response.DialogType);
        Assert.True(LegacyTeamArenaInfoPacket.TryParse(ReadFrame(sent[countBeforeOff + 1]), out var info));
        Assert.Equal(LegacyTeamArenaInfoStatus.NotSignedUp, info.Status);
    }

    [Fact]
    public async Task HandlerTeamArenaContinueSendsInfoWithoutSignupEcho()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(
            new TeamArenaStatisticsProvider([TeamArenaRecord(1293600, "HeroOne", points: 4500)]));
        await CreateTeamAsync(handler, connection, sent);
        var countBeforeContinue = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.Continue,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));

        Assert.Equal(countBeforeContinue + 1, sent.Count);
        Assert.True(LegacyTeamArenaInfoPacket.TryParse(ReadFrame(sent[countBeforeContinue]), out var info));
        Assert.Equal(LegacyTeamArenaInfoStatus.WaitingForOpponent, info.Status);
    }

    [Fact]
    public async Task HandlerTeamArenaGiveUpSendsWaitingInactiveWithoutSignupEcho()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(
            new TeamArenaStatisticsProvider([TeamArenaRecord(1293600, "HeroOne", points: 4500)]));
        await CreateTeamAsync(handler, connection, sent);
        var countBeforeGiveUp = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.ArenaGui,
                LegacyTeamArenaSignupDialogButton.DoGiveUp).ToFrameBytes()));

        Assert.Equal(countBeforeGiveUp + 1, sent.Count);
        Assert.True(LegacyTeamArenaInfoPacket.TryParse(ReadFrame(sent[countBeforeGiveUp]), out var info));
        Assert.Equal(LegacyTeamArenaInfoStatus.WaitingInactive, info.Status);
    }

    [Fact]
    public async Task HandlerMsgTeamCreateSendsLegacySendAndCreateFrames()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(
            new TeamArenaStatisticsProvider([TeamArenaRecord(1293600, "HeroOne", points: 4500)]),
            nativeTeamMemberVitals: true);
        var countBeforeCreate = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));

        Assert.Equal(countBeforeCreate + 4, sent.Count);
        Assert.True(LegacyTeamMemberInfoPacket.TryParse(ReadFrame(sent[countBeforeCreate]), out var memberInfo));
        var member = Assert.Single(memberInfo.Entries);
        Assert.Equal(LegacyTeamMemberInfoAction.AddMember, memberInfo.Action);
        Assert.Equal("HeroOne", member.Name);
        Assert.Equal(1_293_600U, member.Uid);
        Assert.Equal(1_541_008U, member.Mesh);
        Assert.Equal(61_258U, member.MaxHitpoints);
        Assert.Equal(61_258U, member.MinHitpoints);
        Assert.True(LegacyTeamLeadershipPacket.TryParse(ReadFrame(sent[countBeforeCreate + 1]), out var leadership));
        Assert.Equal(LegacyTeamLeadershipMode.Leader, leadership.Mode);
        Assert.Equal(1_293_600U, leadership.Uid);
        Assert.Equal(1_293_600U, leadership.LeaderUid);
        Assert.Equal(1, leadership.Count);
        Assert.True(MsgTeamPacket.TryParse(ReadFrame(sent[countBeforeCreate + 2]), out var send));
        Assert.Equal(MsgTeamType.Send, send.Type);
        Assert.Equal(0U, send.Uid);
        Assert.True(MsgTeamPacket.TryParse(ReadFrame(sent[countBeforeCreate + 3]), out var create));
        Assert.Equal(MsgTeamType.Create, create.Type);
        Assert.Equal(0U, create.Uid);
    }

    [Fact]
    public async Task HandlerTeamArenaBuyPointsUpdatesMoneyAndSubsequentTeamArenaInfo()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(
            new TeamArenaStatisticsProvider([TeamArenaRecord(1293600, "HeroOne", points: 1000)]),
            new ArenaStatisticsProvider([ArenaRecord(1293600, "HeroOne", points: 4500)]));
        var countBeforeBuy = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyTeamArenaSignupPacket(
                LegacyTeamArenaSignupDialogType.BuyPoints,
                LegacyTeamArenaSignupDialogButton.SignUp).ToFrameBytes()));

        Assert.Equal(countBeforeBuy + 2, sent.Count);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[countBeforeBuy]), out var update));
        var moneyUpdate = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.Money, moneyUpdate.Type);
        Assert.Equal(98_593_439_956UL, moneyUpdate.Value);
        Assert.True(MsgArenaInfoPacket.TryParse(ReadFrame(sent[countBeforeBuy + 1]), out var arenaInfo));
        Assert.Equal(4500U, arenaInfo.ArenaPoints);

        await handler.HandleFrameAsync(connection, ReadFrame(LegacyTeamArenaInfoPacket.QueryFrameBytes()));

        Assert.True(LegacyTeamArenaInfoPacket.TryParse(ReadFrame(sent[countBeforeBuy + 2]), out var teamArenaInfo));
        Assert.Equal(2500U, teamArenaInfo.ArenaPoints);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dbRoot))
        {
            Directory.Delete(_dbRoot, recursive: true);
        }
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)>
        LoginLegacyWorldAsync(
            TeamArenaStatisticsProvider teamArenaStatistics,
            ArenaStatisticsProvider? arenaStatistics = null,
            bool nativeTeamMemberVitals = false)
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600);
        WorldDataCatalog? worldData = null;
        if (nativeTeamMemberVitals)
        {
            // Native bare7051 HP10836 + declared ItemHP50422 = the existing61258 team oracle.
            // The unchanged loader derives this real sibling item file; no MaxHP/cache outcome is seeded.
            var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
            Directory.CreateDirectory(itemsRoot);
            File.WriteAllBytes(Path.Combine(itemsRoot, "1293600.bin"),
                PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                    [PlayerItemSnapshotLoaderTests.DbItem(uid: 2001, itemId: 500_109, position: 4)], []));
            var itemFields = Enumerable.Repeat("0", 60).ToArray();
            itemFields[0] = "500109";
            itemFields[1] = "NativeTeamVitals";
            itemFields[18] = "50422";
            itemFields[19] = "5000";
            worldData = LegacyItemWorldDataFixture.Create(
                new ItemType(500_109, itemFields[1], itemFields));
        }
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoff,
            arenaStatistics ?? ArenaStatisticsProvider.Empty,
            teamArenaStatistics,
            worldData,
            LegacyRuntimeUpdateLoopOptions.Disabled);
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

    private static TeamArenaRecord TeamArenaRecord(
        int playerId,
        string name,
        int points,
        int level = 140,
        int @class = 7051,
        int mesh = 1541008,
        int currentHonor = 0,
        int historyHonor = 0,
        int todayBattles = 0,
        int todayWin = 0,
        int totalLose = 0,
        int totalWin = 0)
    {
        return new TeamArenaRecord(
            playerId,
            name,
            [level, @class, mesh, points, currentHonor, historyHonor, todayBattles, todayWin, totalLose, totalWin, 0, 0, 0, 0],
            "TeamArena.ini",
            playerId,
            $"{playerId}/{name}/{level}/{@class}/{mesh}/{points}/{currentHonor}/{historyHonor}/{todayBattles}/{todayWin}/{totalLose}/{totalWin}/0/0/0/0/",
            [
                playerId.ToString(),
                name,
                level.ToString(),
                @class.ToString(),
                mesh.ToString(),
                points.ToString(),
                currentHonor.ToString(),
                historyHonor.ToString(),
                todayBattles.ToString(),
                todayWin.ToString(),
                totalLose.ToString(),
                totalWin.ToString(),
                "0",
                "0",
                "0",
                "0",
                "",
            ]);
    }

    private static ArenaRecord ArenaRecord(int playerId, string name, int points)
    {
        return new ArenaRecord(
            playerId,
            name,
            [140, 7051, 1541008, points, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            "Arena.ini",
            playerId,
            $"{playerId}/{name}/140/7051/1541008/{points}/0/0/0/0/0/0/0/0/0/0/",
            [playerId.ToString(), name, "140", "7051", "1541008", points.ToString(), "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", ""]);
    }

    private static async Task CreateTeamAsync(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        List<byte[]> sent)
    {
        var countBeforeCreate = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes()));

        Assert.Equal(countBeforeCreate + 4, sent.Count);
        Assert.True(LegacyTeamMemberInfoPacket.TryParse(ReadFrame(sent[countBeforeCreate]), out var memberInfo));
        Assert.Equal(LegacyTeamMemberInfoAction.AddMember, memberInfo.Action);
        Assert.Single(memberInfo.Entries);
        Assert.True(LegacyTeamLeadershipPacket.TryParse(ReadFrame(sent[countBeforeCreate + 1]), out var leadership));
        Assert.Equal(LegacyTeamLeadershipMode.Leader, leadership.Mode);
        Assert.True(MsgTeamPacket.TryParse(ReadFrame(sent[countBeforeCreate + 2]), out var send));
        Assert.Equal(MsgTeamType.Send, send.Type);
        Assert.True(MsgTeamPacket.TryParse(ReadFrame(sent[countBeforeCreate + 3]), out var create));
        Assert.Equal(MsgTeamType.Create, create.Type);
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
