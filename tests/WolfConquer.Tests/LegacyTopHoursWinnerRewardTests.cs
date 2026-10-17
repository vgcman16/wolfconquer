using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTopHoursWinnerRewardTests : IDisposable
{
    private const uint TopHoursOutxx42NpcId = 19_963;
    private const long TopHoursEventMapId = 10_441;
    private const long WinnerUid = 1_293_600;
    private const long RivalUid = 1_293_601;
    private const long WinnerAccountId = 100;
    private const long RivalAccountId = 101;
    private const long RewardConquerPoints = 500_000;

    private readonly string _usersRoot = Path.Combine(
        Path.GetTempPath(),
        "wolf-top-hours-winner-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SoleAliveInvokerReceivesPersistedSourceRewardBroadcastAndTeleport()
    {
        const long startingConquerPoints = 123;
        var snapshotPath = WriteCharacterSnapshot(
            WinnerAccountId,
            WinnerUid,
            "TopHourHero",
            TopHoursEventMapId,
            startingConquerPoints);
        var winnerSent = new List<byte[]>();
        var winnerConnection = CreateSendingConnection(winnerSent);
        var winnerSession = CreateSession(
            WinnerAccountId,
            WinnerUid,
            "TopHourHero",
            TopHoursEventMapId,
            hitPoints: 1,
            startingConquerPoints,
            _usersRoot);
        var observerSent = new List<byte[]>();
        var observerConnection = CreateSendingConnection(observerSent);
        var observerSession = CreateSession(
            RivalAccountId,
            RivalUid,
            "Observer",
            mapId: 1_002,
            hitPoints: 1,
            conquerPoints: 77,
            usersRoot: null);

        CharacterSnapshot? persistedCharacterAtCommit = null;
        var rewardCommitCount = 0;
        var sessions = new WorldSessionStore((_, previous, current) =>
        {
            if (current.Character.Uid != WinnerUid ||
                previous is null ||
                previous.Character.ConquerPoints == current.Character.ConquerPoints)
            {
                return;
            }

            rewardCommitCount++;
            persistedCharacterAtCommit = ReadPersistedCharacter(snapshotPath);
        });
        sessions.Set(winnerConnection, winnerSession);
        sessions.Set(observerConnection, observerSession);
        using var log = new StringWriter();

        var handled = await InvokeRewardAsync(
            winnerConnection,
            log,
            winnerSession,
            sessions,
            minute: 46);

        Assert.True(handled);
        var expectedConquerPoints = startingConquerPoints + RewardConquerPoints;
        Assert.Equal(1, rewardCommitCount);
        Assert.NotNull(persistedCharacterAtCommit);
        AssertWinnerRewardState(persistedCharacterAtCommit, expectedConquerPoints, bankConquerPoints: 0);
        AssertWinnerRewardState(ReadPersistedCharacter(snapshotPath), expectedConquerPoints, bankConquerPoints: 0);

        var updatedWinner = GetSession(sessions, winnerConnection);
        AssertWinnerRewardState(updatedWinner.Character, expectedConquerPoints, bankConquerPoints: 0);
        Assert.Equal(77, GetSession(sessions, observerConnection).Character.ConquerPoints);

        Assert.Contains(
            ParseMsgUpdates(winnerSent),
            packet =>
                packet.Uid == WinnerUid &&
                packet.Updates.Any(update =>
                    update.Type == LegacyMsgUpdateDataType.ConquerPoints &&
                    update.Value == checked((ulong)expectedConquerPoints)));
        Assert.DoesNotContain(
            ParseMsgUpdates(winnerSent),
            packet => packet.Updates.Any(update => update.Type == LegacyMsgUpdateDataType.Conquer));

        var expectedBroadcast =
            " Congratulations TopHourHero claimed the prize [ 500000 Cps ] for winning the Top TopHours +   .";
        AssertCenterBroadcast(winnerSent, expectedBroadcast);
        AssertCenterBroadcast(observerSent, expectedBroadcast);

        var teleport = Assert.Single(
            ParseMsgActions(winnerSent),
            action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_002U, teleport.DwParam);
        Assert.Equal(410U, teleport.PositionX);
        Assert.Equal(355U, teleport.PositionY);
        Assert.DoesNotContain(ParseMsgActions(observerSent), action => action.Type == MsgActionType.Teleport);
        Assert.Empty(ParseNpcReplies(winnerSent));

        var output = log.ToString();
        Assert.Contains("source=NpcHandler.TopHoursOutxx42 lineRange=NpcHandler.cs:1887-1965", output);
        Assert.Contains("option=1 map=10441 alivePlayers=1", output);
        Assert.Contains("conquerPoints=500000 previousConquerPoints=123 updatedConquerPoints=500123", output);
        Assert.Contains("winner=TopHourHero count=2", output);
        Assert.Contains("source=NpcHandler.TopHoursOutxx42 map=1002 x=410 y=355", output);
    }

    [Fact]
    public async Task SourceCpSetterCapSpillsToBankBeforeUpdateBroadcastAndTeleport()
    {
        const long startingConquerPoints = 1_999_750_000;
        const long startingBankConquerPoints = 80;
        const long expectedOverflow = 250_000;
        const long expectedConquerPoints = 2_000_000_000;
        const long expectedBankConquerPoints = startingBankConquerPoints + expectedOverflow;
        var snapshotPath = WriteCharacterSnapshot(
            WinnerAccountId,
            WinnerUid,
            "CapWinner",
            TopHoursEventMapId,
            startingConquerPoints,
            bankConquerPoints: startingBankConquerPoints);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            WinnerAccountId,
            WinnerUid,
            "CapWinner",
            TopHoursEventMapId,
            hitPoints: 1,
            startingConquerPoints,
            _usersRoot,
            bankConquerPoints: startingBankConquerPoints);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        using var log = new StringWriter();

        var handled = await InvokeRewardAsync(connection, log, session, sessions, minute: 46);

        Assert.True(handled);
        AssertWinnerRewardState(
            GetSession(sessions, connection).Character,
            expectedConquerPoints,
            expectedBankConquerPoints);
        AssertWinnerRewardState(
            ReadPersistedCharacter(snapshotPath),
            expectedConquerPoints,
            expectedBankConquerPoints);

        Assert.True(sent.Count >= 4);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[0]), out var overflowBox));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, overflowBox.InteractType);
        Assert.Equal(
            "250000 Your ConquerPoints[CPS] has Transfare To Your Bank Thanks.",
            overflowBox.Text);

        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[1]), out var conquerPointsUpdate));
        Assert.Contains(
            conquerPointsUpdate.Updates,
            update =>
                update.Type == LegacyMsgUpdateDataType.ConquerPoints &&
                update.Value == checked((ulong)expectedConquerPoints));

        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[2]), out var centerBroadcast));
        Assert.Equal(LegacyMsgMessagePacket.CenterChatType, centerBroadcast.ChatType);
        Assert.Contains(
            " Congratulations CapWinner claimed the prize [ 500000 Cps ] for winning the Top TopHours +   .",
            centerBroadcast.Content);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[3]), out var teleport));
        Assert.Equal(MsgActionType.Teleport, teleport.Type);
        Assert.Equal(1_002U, teleport.DwParam);
        Assert.Equal(410U, teleport.PositionX);
        Assert.Equal(355U, teleport.PositionY);
        Assert.Contains("overflowConquerPoints=250000 previousBankCps=80 updatedBankCps=250080", log.ToString());
    }

    [Fact]
    public async Task PersistedTeleportMakesReloadedWinnerIneligibleForSecondClaim()
    {
        const long startingConquerPoints = 310;
        var snapshotPath = WriteCharacterSnapshot(
            WinnerAccountId,
            WinnerUid,
            "ReloadedWinner",
            TopHoursEventMapId,
            startingConquerPoints);
        var firstSent = new List<byte[]>();
        var firstConnection = CreateSendingConnection(firstSent);
        var firstSession = CreateSession(
            WinnerAccountId,
            WinnerUid,
            "ReloadedWinner",
            TopHoursEventMapId,
            hitPoints: 1,
            startingConquerPoints,
            _usersRoot);
        var firstSessions = new WorldSessionStore();
        firstSessions.Set(firstConnection, firstSession);
        using var firstLog = new StringWriter();

        Assert.True(await InvokeRewardAsync(firstConnection, firstLog, firstSession, firstSessions, minute: 46));

        var expectedConquerPoints = startingConquerPoints + RewardConquerPoints;
        var reloadedCharacter = ReadPersistedCharacter(snapshotPath);
        AssertWinnerRewardState(reloadedCharacter, expectedConquerPoints, bankConquerPoints: 0);

        var reloadSent = new List<byte[]>();
        var reloadConnection = CreateSendingConnection(reloadSent);
        var reloadedSession = firstSession with { Character = reloadedCharacter };
        var reloadedSessions = new WorldSessionStore();
        reloadedSessions.Set(reloadConnection, reloadedSession);
        using var reloadLog = new StringWriter();

        Assert.True(await InvokeRewardAsync(
            reloadConnection,
            reloadLog,
            reloadedSession,
            reloadedSessions,
            minute: 46));

        Assert.Empty(reloadSent);
        AssertWinnerRewardState(
            GetSession(reloadedSessions, reloadConnection).Character,
            expectedConquerPoints,
            bankConquerPoints: 0);
        AssertWinnerRewardState(
            ReadPersistedCharacter(snapshotPath),
            expectedConquerPoints,
            bankConquerPoints: 0);
        Assert.Contains("alivePlayers=0 reason=soleAliveWinnerRequired", reloadLog.ToString());
    }

    [Fact]
    public async Task WrongMapInvokerCannotClaimTheSoleAlivePlayersReward()
    {
        const long startingConquerPoints = 50;
        var snapshotPath = WriteCharacterSnapshot(
            WinnerAccountId,
            WinnerUid,
            "WrongMapInvoker",
            mapId: 1_002,
            startingConquerPoints);
        var invokerSent = new List<byte[]>();
        var invokerConnection = CreateSendingConnection(invokerSent);
        var invokerSession = CreateSession(
            WinnerAccountId,
            WinnerUid,
            "WrongMapInvoker",
            mapId: 1_002,
            hitPoints: 1,
            startingConquerPoints,
            _usersRoot);
        var winnerSent = new List<byte[]>();
        var winnerConnection = CreateSendingConnection(winnerSent);
        var winnerSession = CreateSession(
            RivalAccountId,
            RivalUid,
            "ActualWinner",
            TopHoursEventMapId,
            hitPoints: 1,
            conquerPoints: 9,
            usersRoot: null);
        var sessions = new WorldSessionStore();
        sessions.Set(invokerConnection, invokerSession);
        sessions.Set(winnerConnection, winnerSession);
        using var log = new StringWriter();

        var handled = await InvokeRewardAsync(invokerConnection, log, invokerSession, sessions, minute: 46);

        Assert.True(handled);
        Assert.Empty(invokerSent);
        Assert.Empty(winnerSent);
        AssertUnchanged(sessions, invokerConnection, mapId: 1_002, startingConquerPoints);
        AssertUnchanged(sessions, winnerConnection, TopHoursEventMapId, conquerPoints: 9);
        Assert.Equal(startingConquerPoints, ReadPersistedConquerPoints(snapshotPath));
        Assert.Contains("alivePlayers=1 reason=invokerIsNotWinner", log.ToString());
    }

    [Fact]
    public async Task MultipleAlivePlayersDoNotReceiveRewardResults()
    {
        const long startingConquerPoints = 75;
        var snapshotPath = WriteCharacterSnapshot(
            WinnerAccountId,
            WinnerUid,
            "Contender",
            TopHoursEventMapId,
            startingConquerPoints);
        var invokerSent = new List<byte[]>();
        var invokerConnection = CreateSendingConnection(invokerSent);
        var invokerSession = CreateSession(
            WinnerAccountId,
            WinnerUid,
            "Contender",
            TopHoursEventMapId,
            hitPoints: 1,
            startingConquerPoints,
            _usersRoot);
        var rivalSent = new List<byte[]>();
        var rivalConnection = CreateSendingConnection(rivalSent);
        var rivalSession = CreateSession(
            RivalAccountId,
            RivalUid,
            "Rival",
            TopHoursEventMapId,
            hitPoints: 1,
            conquerPoints: 20,
            usersRoot: null);
        var sessions = new WorldSessionStore();
        sessions.Set(invokerConnection, invokerSession);
        sessions.Set(rivalConnection, rivalSession);
        using var log = new StringWriter();

        var handled = await InvokeRewardAsync(invokerConnection, log, invokerSession, sessions, minute: 46);

        Assert.True(handled);
        Assert.Empty(invokerSent);
        Assert.Empty(rivalSent);
        AssertUnchanged(sessions, invokerConnection, TopHoursEventMapId, startingConquerPoints);
        AssertUnchanged(sessions, rivalConnection, TopHoursEventMapId, conquerPoints: 20);
        Assert.Equal(startingConquerPoints, ReadPersistedConquerPoints(snapshotPath));
        Assert.Contains("alivePlayers=2 reason=soleAliveWinnerRequired", log.ToString());
    }

    [Fact]
    public async Task ZeroAlivePlayersDoNotReceiveRewardResults()
    {
        const long startingConquerPoints = 90;
        var snapshotPath = WriteCharacterSnapshot(
            WinnerAccountId,
            WinnerUid,
            "NoWinner",
            mapId: 1_002,
            startingConquerPoints);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            WinnerAccountId,
            WinnerUid,
            "NoWinner",
            mapId: 1_002,
            hitPoints: 1,
            startingConquerPoints,
            _usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        using var log = new StringWriter();

        var handled = await InvokeRewardAsync(connection, log, session, sessions, minute: 46);

        Assert.True(handled);
        Assert.Empty(sent);
        AssertUnchanged(sessions, connection, mapId: 1_002, startingConquerPoints);
        Assert.Equal(startingConquerPoints, ReadPersistedConquerPoints(snapshotPath));
        Assert.Contains("alivePlayers=0 reason=soleAliveWinnerRequired", log.ToString());
    }

    [Fact]
    public async Task DeadInvokerCannotClaimWhenAnotherPlayerIsTheSoleAliveWinner()
    {
        const long startingConquerPoints = 110;
        var snapshotPath = WriteCharacterSnapshot(
            WinnerAccountId,
            WinnerUid,
            "DeadInvoker",
            TopHoursEventMapId,
            startingConquerPoints,
            hitPoints: 0);
        var invokerSent = new List<byte[]>();
        var invokerConnection = CreateSendingConnection(invokerSent);
        var invokerSession = CreateSession(
            WinnerAccountId,
            WinnerUid,
            "DeadInvoker",
            TopHoursEventMapId,
            hitPoints: 0,
            startingConquerPoints,
            _usersRoot);
        var winnerSent = new List<byte[]>();
        var winnerConnection = CreateSendingConnection(winnerSent);
        var winnerSession = CreateSession(
            RivalAccountId,
            RivalUid,
            "LivingWinner",
            TopHoursEventMapId,
            hitPoints: 1,
            conquerPoints: 30,
            usersRoot: null);
        var sessions = new WorldSessionStore();
        sessions.Set(invokerConnection, invokerSession);
        sessions.Set(winnerConnection, winnerSession);
        using var log = new StringWriter();

        var handled = await InvokeRewardAsync(invokerConnection, log, invokerSession, sessions, minute: 46);

        Assert.True(handled);
        Assert.Empty(invokerSent);
        Assert.Empty(winnerSent);
        AssertUnchanged(sessions, invokerConnection, TopHoursEventMapId, startingConquerPoints);
        Assert.Equal(0, GetSession(sessions, invokerConnection).Character.MinHitPoints);
        AssertUnchanged(sessions, winnerConnection, TopHoursEventMapId, conquerPoints: 30);
        Assert.Equal(startingConquerPoints, ReadPersistedConquerPoints(snapshotPath));
        Assert.Contains("alivePlayers=1 reason=invokerIsNotWinner", log.ToString());
    }

    [Fact]
    public async Task OutsideRewardWindowSendsOnlyTheSourceEndDialog()
    {
        const long startingConquerPoints = 140;
        var snapshotPath = WriteCharacterSnapshot(
            WinnerAccountId,
            WinnerUid,
            "EarlyWinner",
            TopHoursEventMapId,
            startingConquerPoints);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            WinnerAccountId,
            WinnerUid,
            "EarlyWinner",
            TopHoursEventMapId,
            hitPoints: 1,
            startingConquerPoints,
            _usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        using var log = new StringWriter();

        var handled = await InvokeRewardAsync(connection, log, session, sessions, minute: 45);

        Assert.True(handled);
        AssertUnchanged(sessions, connection, TopHoursEventMapId, startingConquerPoints);
        Assert.Equal(startingConquerPoints, ReadPersistedConquerPoints(snapshotPath));
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "TopHours ends at xx:59.");
        Assert.Contains(replies, reply => reply.Text == "Thanks" && reply.OptionId == 58);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 63);
        AssertNoRewardResultPackets(sent);
    }

    [Fact]
    public async Task RewardWindowIncludesMinute59AndExcludesMinute00()
    {
        const long startingConquerPoints = 160;
        var minute59Path = WriteCharacterSnapshot(
            WinnerAccountId,
            WinnerUid,
            "Minute59Winner",
            TopHoursEventMapId,
            startingConquerPoints);
        var minute59Sent = new List<byte[]>();
        var minute59Connection = CreateSendingConnection(minute59Sent);
        var minute59Session = CreateSession(
            WinnerAccountId,
            WinnerUid,
            "Minute59Winner",
            TopHoursEventMapId,
            hitPoints: 1,
            startingConquerPoints,
            _usersRoot);
        var minute59Sessions = new WorldSessionStore();
        minute59Sessions.Set(minute59Connection, minute59Session);
        using var minute59Log = new StringWriter();

        Assert.True(await InvokeRewardAsync(
            minute59Connection,
            minute59Log,
            minute59Session,
            minute59Sessions,
            minute: 59));

        AssertWinnerRewardState(
            ReadPersistedCharacter(minute59Path),
            startingConquerPoints + RewardConquerPoints,
            bankConquerPoints: 0);
        Assert.Contains(ParseMsgActions(minute59Sent), action => action.Type == MsgActionType.Teleport);

        var minute00Path = WriteCharacterSnapshot(
            RivalAccountId,
            RivalUid,
            "Minute00Winner",
            TopHoursEventMapId,
            startingConquerPoints);
        var minute00Sent = new List<byte[]>();
        var minute00Connection = CreateSendingConnection(minute00Sent);
        var minute00Session = CreateSession(
            RivalAccountId,
            RivalUid,
            "Minute00Winner",
            TopHoursEventMapId,
            hitPoints: 1,
            startingConquerPoints,
            _usersRoot);
        var minute00Sessions = new WorldSessionStore();
        minute00Sessions.Set(minute00Connection, minute00Session);
        using var minute00Log = new StringWriter();

        Assert.True(await InvokeRewardAsync(
            minute00Connection,
            minute00Log,
            minute00Session,
            minute00Sessions,
            minute: 0));

        AssertUnchanged(minute00Sessions, minute00Connection, TopHoursEventMapId, startingConquerPoints);
        Assert.Equal(startingConquerPoints, ReadPersistedConquerPoints(minute00Path));
        Assert.Contains(ParseNpcReplies(minute00Sent), reply => reply.Text == "TopHours ends at xx:59.");
        AssertNoRewardResultPackets(minute00Sent);
    }

    [Fact]
    public async Task MissingSessionStoreLeavesPersistedAndRuntimeStateUntouched()
    {
        const long startingConquerPoints = 175;
        var snapshotPath = WriteCharacterSnapshot(
            WinnerAccountId,
            WinnerUid,
            "UntrackedWinner",
            TopHoursEventMapId,
            startingConquerPoints);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            WinnerAccountId,
            WinnerUid,
            "UntrackedWinner",
            TopHoursEventMapId,
            hitPoints: 1,
            startingConquerPoints,
            _usersRoot);
        using var log = new StringWriter();

        var handled = await InvokeRewardAsync(connection, log, session, sessions: null, minute: 46);

        Assert.True(handled);
        AssertRewardUnavailable(sent);
        AssertNoRewardResultPackets(sent);
        Assert.Equal(startingConquerPoints, session.Character.ConquerPoints);
        Assert.Equal(TopHoursEventMapId, session.Character.MapId);
        Assert.Equal(startingConquerPoints, ReadPersistedConquerPoints(snapshotPath));
        Assert.Contains("PARITY-GAP LegacyNpc TopHours", log.ToString());
        Assert.Contains("reason=sessionsMissing", log.ToString());
    }

    [Fact]
    public async Task MissingPersistenceRootDoesNotCommitOrEmitRewardResults()
    {
        const long startingConquerPoints = 1_999_750_000;
        const long startingBankConquerPoints = 33;
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            WinnerAccountId,
            WinnerUid,
            "EphemeralWinner",
            TopHoursEventMapId,
            hitPoints: 1,
            startingConquerPoints,
            usersRoot: null,
            bankConquerPoints: startingBankConquerPoints);
        var sessionChanges = 0;
        var sessions = new WorldSessionStore((_, previous, _) =>
        {
            if (previous is not null)
            {
                sessionChanges++;
            }
        });
        sessions.Set(connection, session);
        using var log = new StringWriter();

        var handled = await InvokeRewardAsync(connection, log, session, sessions, minute: 46);

        Assert.True(handled);
        AssertRewardUnavailable(sent);
        AssertNoRewardResultPackets(sent);
        Assert.DoesNotContain(
            ParseNpcReplies(sent),
            reply => reply.InteractType == LegacyNpcReplyInteractType.MessageBox);
        Assert.Equal(0, sessionChanges);
        AssertUnchanged(
            sessions,
            connection,
            TopHoursEventMapId,
            startingConquerPoints,
            startingBankConquerPoints);
        Assert.Contains("PARITY-GAP LegacyNpc TopHours", log.ToString());
        Assert.Contains("reason=persistence:usersRootMissing", log.ToString());
    }

    private static ValueTask<bool> InvokeRewardAsync(
        ConquerTcpConnection connection,
        StringWriter log,
        WorldSession session,
        WorldSessionStore? sessions,
        int minute)
    {
        return WorldPacketHandlers.TryHandleLegacyTopHoursNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            TopHoursOutxx42NpcId,
            optionId: 1,
            new DateTime(2026, 6, 20, 8, minute, 0),
            CancellationToken.None);
    }

    private static WorldSession CreateSession(
        long accountId,
        long uid,
        string name,
        long mapId,
        long hitPoints,
        long conquerPoints,
        string? usersRoot,
        long bankConquerPoints = 0)
    {
        return new WorldSession(
            AccountId: accountId,
            Username: name,
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: name,
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: mapId,
                X: 45,
                Y: 45,
                PreviousMapId: mapId,
                PreviousMapX: 45,
                PreviousMapY: 45,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: hitPoints,
                MinMana: 0,
                Money: 0,
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: bankConquerPoints,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DynamicID"] = "0",
                    ["ConquerPoints"] = conquerPoints.ToString(CultureInfo.InvariantCulture),
                    ["BankCps"] = bankConquerPoints.ToString(CultureInfo.InvariantCulture),
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private string WriteCharacterSnapshot(
        long accountId,
        long uid,
        string name,
        long mapId,
        long conquerPoints,
        long hitPoints = 1,
        long bankConquerPoints = 0)
    {
        Directory.CreateDirectory(_usersRoot);
        var path = Path.Combine(_usersRoot, accountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        File.WriteAllText(
            path,
            $$"""
            [Character]
            UID={{uid.ToString(CultureInfo.InvariantCulture)}}
            Body=1003
            Face=0
            Name={{name}}
            Class=7051
            Map={{mapId.ToString(CultureInfo.InvariantCulture)}}
            X=45
            Y=45
            FirstClass=7051
            SecoundeClass=0
            Reborn=0
            Level=140
            Haire=0
            Experience=0
            PMap={{mapId.ToString(CultureInfo.InvariantCulture)}}
            PMapX=45
            PMapY=45
            Agility=0
            Strength=0
            Vitaliti=0
            Spirit=0
            Atributes=0
            MinHitPoints={{hitPoints.ToString(CultureInfo.InvariantCulture)}}
            MinMana=0
            ConquerPoints={{conquerPoints.ToString(CultureInfo.InvariantCulture)}}
            BankCps={{bankConquerPoints.ToString(CultureInfo.InvariantCulture)}}
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=0

            [PaidNobility]
            Identifier=0
            LastNobilityDonation=0
            PaidRank=0
            PeriodTime=0
            IsActive=False
            Rank=Serf
            Position=-1
            """);
        return path;
    }

    private static long ReadPersistedConquerPoints(string path)
    {
        return ReadPersistedCharacter(path).ConquerPoints;
    }

    private static CharacterSnapshot ReadPersistedCharacter(string path)
    {
        return CharacterSnapshotParser.ParseText(File.ReadAllText(path));
    }

    private static WorldSession GetSession(WorldSessionStore sessions, ConquerTcpConnection connection)
    {
        Assert.True(sessions.TryGet(connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private static void AssertUnchanged(
        WorldSessionStore sessions,
        ConquerTcpConnection connection,
        long mapId,
        long conquerPoints,
        long bankConquerPoints = 0)
    {
        var session = GetSession(sessions, connection);
        Assert.Equal(mapId, session.Character.MapId);
        Assert.Equal(45, session.Character.X);
        Assert.Equal(45, session.Character.Y);
        Assert.Equal(mapId, session.Character.PreviousMapId);
        Assert.Equal(45, session.Character.PreviousMapX);
        Assert.Equal(45, session.Character.PreviousMapY);
        Assert.Equal(conquerPoints, session.Character.ConquerPoints);
        Assert.Equal(bankConquerPoints, session.Character.BankConquerPoints);
    }

    private static void AssertWinnerRewardState(
        CharacterSnapshot character,
        long conquerPoints,
        long bankConquerPoints)
    {
        Assert.Equal(conquerPoints, character.ConquerPoints);
        Assert.Equal(bankConquerPoints, character.BankConquerPoints);
        Assert.Equal(1_002, character.MapId);
        Assert.Equal(410, character.X);
        Assert.Equal(355, character.Y);
        Assert.Equal(TopHoursEventMapId, character.PreviousMapId);
        Assert.Equal(45, character.PreviousMapX);
        Assert.Equal(45, character.PreviousMapY);
        Assert.Equal(conquerPoints.ToString(CultureInfo.InvariantCulture), character.Attributes["ConquerPoints"]);
        Assert.Equal(bankConquerPoints.ToString(CultureInfo.InvariantCulture), character.Attributes["BankCps"]);
        Assert.Equal("1002", character.Attributes["Map"]);
        Assert.Equal("410", character.Attributes["X"]);
        Assert.Equal("355", character.Attributes["Y"]);
        Assert.Equal(TopHoursEventMapId.ToString(CultureInfo.InvariantCulture), character.Attributes["PMap"]);
        Assert.Equal("45", character.Attributes["PMapX"]);
        Assert.Equal("45", character.Attributes["PMapY"]);
        Assert.Equal("0", character.Attributes["DynamicID"]);
    }

    private static void AssertCenterBroadcast(IEnumerable<byte[]> sent, string expected)
    {
        Assert.Contains(
            ParseMsgMessages(sent),
            message =>
                message.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                message.Content.Any(text => string.Equals(text, expected, StringComparison.Ordinal)));
    }

    private static void AssertRewardUnavailable(IEnumerable<byte[]> sent)
    {
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "TopHours winner rewards are not available yet.");
        Assert.Contains(replies, reply => reply.Text == "I understand." && reply.OptionId == byte.MaxValue);
    }

    private static void AssertNoRewardResultPackets(IEnumerable<byte[]> sent)
    {
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(update =>
                update.Type is LegacyMsgUpdateDataType.Conquer or LegacyMsgUpdateDataType.ConquerPoints));
        Assert.DoesNotContain(
            ParseMsgMessages(sent),
            message => message.ChatType == LegacyMsgMessagePacket.CenterChatType);
        Assert.DoesNotContain(ParseMsgActions(sent), action => action.Type == MsgActionType.Teleport);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMsgMessagePacket> ParseMsgMessages(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgMessagePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<MsgActionPacket> ParseMsgActions(IEnumerable<byte[]> sent)
    {
        var packets = new List<MsgActionPacket>();
        foreach (var bytes in sent)
        {
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    public void Dispose()
    {
        if (Directory.Exists(_usersRoot))
        {
            Directory.Delete(_usersRoot, recursive: true);
        }
    }
}
