using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyVoteManagerVoteFlowTests : IDisposable
{
    private const uint VoteManagerNpcId = 52_300;
    private const string VoteUrl = "https://example.invalid/vote";
    private const string PendingMessage = "Please wait while the system checks your vote.";
    private const string DeniedMessage =
        "You have already claimed the vote reward. Each IP address can vote only once every 12 hours.";
    private const string ClaimMessage = "You`ve received VIP4 (11 Hours) And 1 points.";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-vote-manager-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Option40AllowedSendsWebsiteThenReplacesDeadlineThenSendsTopLeftWithoutLevelRecheck()
    {
        var nowUtc = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(nowUtc, TimeZoneInfo.Utc);
        var usersRoot = CreateUsersRoot();
        var votes = LegacyVoteRuntimeStateStore.Load(VotesPath);
        var sent = new List<byte[]>();
        var session = CreateSession(usersRoot, level: 1);
        var exposurePacketCounts = new List<int>();
        var exposedDeadlines = new List<DateTimeOffset?>();
        var observe = false;
        var sessions = new WorldSessionStore((_, _, updated) =>
        {
            if (!observe)
            {
                return;
            }

            exposurePacketCounts.Add(sent.Count);
            exposedDeadlines.Add(updated.LegacyVoteClaimDueAtUtc);
            var website = ParseMessage(sent[^1]);
            Assert.Equal(LegacyMsgMessagePacket.WebsiteChatType, website.ChatType);
        });
        var connection = CreateConnection(sent);
        sessions.Set(connection, session);
        observe = true;
        var context = CreateContext(connection, session, sessions, votes, timeProvider);

        Assert.True(await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            context,
            session,
            VoteManagerNpcId,
            optionId: 40,
            CancellationToken.None));

        var firstPending = GetSession(sessions, connection);
        Assert.Equal(nowUtc.AddSeconds(90), firstPending.LegacyVoteClaimDueAtUtc);

        timeProvider.SetUtcNow(nowUtc.AddSeconds(30));
        Assert.True(await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            context with { Session = firstPending },
            firstPending,
            VoteManagerNpcId,
            optionId: 40,
            CancellationToken.None));

        var secondPending = GetSession(sessions, connection);
        Assert.Equal(nowUtc.AddSeconds(120), secondPending.LegacyVoteClaimDueAtUtc);
        Assert.Equal([1, 3], exposurePacketCounts);
        Assert.Equal(
            [nowUtc.AddSeconds(90), nowUtc.AddSeconds(120)],
            exposedDeadlines);

        Assert.Equal(4, sent.Count);
        AssertMessage(sent[0], LegacyMsgMessagePacket.WebsiteChatType, VoteUrl);
        AssertMessage(sent[1], LegacyMsgMessagePacket.TopLeftSystemChatType, PendingMessage);
        AssertMessage(sent[2], LegacyMsgMessagePacket.WebsiteChatType, VoteUrl);
        AssertMessage(sent[3], LegacyMsgMessagePacket.TopLeftSystemChatType, PendingMessage);
        Assert.Empty(votes.Snapshot);
    }

    [Fact]
    public async Task Option40DeniedSendsExactAsciiDialogCloseAvatarAndFinish()
    {
        var nowUtc = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var localTimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "VoteManagerTestUtcMinusFive",
            TimeSpan.FromHours(-5),
            "Vote Manager Test UTC-5",
            "Vote Manager Test UTC-5");
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, localTimeZone).DateTime;
        var storedTimer = localNow.AddHours(-7).AddTicks(-1);
        var usersRoot = CreateUsersRoot();
        var session = CreateSession(usersRoot, level: 1);
        var ipAddress = IPAddress.Parse("192.0.2.40");
        WriteVotes(
            $"Count=1\r\n{session.Character.Uid.ToString(CultureInfo.InvariantCulture)}/{ipAddress}/{storedTimer.Ticks.ToString(CultureInfo.InvariantCulture)}/\r\n");
        var votes = LegacyVoteRuntimeStateStore.Load(VotesPath);
        var sent = new List<byte[]>();
        var sessions = new WorldSessionStore();
        var connection = CreateConnection(sent, ipAddress);
        sessions.Set(connection, session);

        Assert.True(await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            CreateContext(connection, session, sessions, votes, new MutableTimeProvider(nowUtc, localTimeZone)),
            session,
            VoteManagerNpcId,
            optionId: 40,
            CancellationToken.None));

        var replies = sent.Select(ParseNpcReply).ToArray();
        Assert.Equal(
            [
                LegacyNpcReplyInteractType.Dialog,
                LegacyNpcReplyInteractType.Option,
                LegacyNpcReplyInteractType.Avatar,
                LegacyNpcReplyInteractType.Finish,
            ],
            replies.Select(reply => reply.InteractType));
        Assert.Equal(DeniedMessage, replies[0].Text);
        Assert.Equal("I understand.", replies[1].Text);
        Assert.Equal(byte.MaxValue, replies[1].OptionId);
        Assert.Equal((ushort)63, replies[2].InputMaxLength);
        Assert.All(replies, reply => AssertAscii(reply.Text));
        Assert.Null(GetSession(sessions, connection).LegacyVoteClaimDueAtUtc);
    }

    [Theory]
    [InlineData("vote-store-missing")]
    [InlineData("session-missing")]
    [InlineData("remote-ip-missing")]
    public async Task Option40MissingRequiredRuntimeDependencyFailsClosed(string missing)
    {
        var nowUtc = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var usersRoot = CreateUsersRoot();
        var session = CreateSession(usersRoot);
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent, includeRemoteEndPoint: missing != "remote-ip-missing");
        var sessions = missing == "session-missing" ? null : new WorldSessionStore();
        sessions?.Set(connection, session);
        var votes = missing == "vote-store-missing"
            ? null
            : LegacyVoteRuntimeStateStore.Load(VotesPath);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions,
            VoteStates: votes,
            TimeProvider: new MutableTimeProvider(nowUtc, TimeZoneInfo.Utc));

        Assert.True(await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            context,
            session,
            VoteManagerNpcId,
            optionId: 40,
            CancellationToken.None));

        Assert.Empty(sent);
        Assert.Null(sessions is null ? session.LegacyVoteClaimDueAtUtc : GetSession(sessions, connection).LegacyVoteClaimDueAtUtc);
        Assert.Contains("PARITY-GAP LegacyNpc VoteManager fenced", log.ToString(), StringComparison.Ordinal);
        Assert.Contains("reason=" + missing, log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DueClaimUsesStrictBoundaryWrapsBytePersistsBeforeExposureAndSendsExactPackets()
    {
        var dueAtUtc = new DateTimeOffset(2026, 7, 11, 12, 1, 30, TimeSpan.Zero);
        var localAtDue = new DateTime(2026, 7, 11, 7, 1, 30, DateTimeKind.Unspecified);
        var claimNowUtc = dueAtUtc.AddTicks(1);
        var claimLocalNow = localAtDue.AddTicks(1);
        var expectedExpiry = claimLocalNow.AddHours(11);
        var expectedVipTime = expectedExpiry.ToBinary().ToString(CultureInfo.InvariantCulture);
        var usersRoot = CreateUsersRoot();
        var session = CreateSession(
            usersRoot,
            votePoints: "255",
            vipLevel: "6",
            vipTime: localAtDue.AddHours(-1).ToBinary().ToString(CultureInfo.InvariantCulture),
            dueAtUtc: dueAtUtc);
        WriteCharacter(session);
        var votes = LegacyVoteRuntimeStateStore.Load(VotesPath);
        var sent = new List<byte[]>();
        var observe = false;
        var exposureCount = 0;
        var sessions = new WorldSessionStore((_, _, updated) =>
        {
            if (!observe)
            {
                return;
            }

            exposureCount++;
            Assert.Empty(sent);
            Assert.Null(updated.LegacyVoteClaimDueAtUtc);
            Assert.Equal("0", updated.Character.Attributes["VotePoints"]);
            Assert.Equal("4", updated.Character.Attributes["VipLevel"]);
            Assert.Equal(expectedVipTime, updated.Character.Attributes["VipTime"]);
            var persisted = ReadCharacterAttributes(session);
            Assert.Equal("0", persisted["VotePoints"]);
            Assert.Equal("4", persisted["VipLevel"]);
            Assert.Equal(expectedVipTime, persisted["VipTime"]);
            Assert.Equal(ExpectedVotesText(session, claimLocalNow), ReadVotes());
            Assert.Single(votes.Snapshot);
        });
        var connection = CreateConnection(sent);
        sessions.Set(connection, session);
        var context = CreateContext(connection, session, sessions, votes, TimeProvider.System);

        await WorldPacketHandlers.ProcessDueLegacyVoteClaimAsync(
            context,
            dueAtUtc,
            localAtDue,
            CancellationToken.None);

        Assert.Empty(sent);
        Assert.Empty(votes.Snapshot);
        Assert.Equal("255", ReadCharacterAttributes(session)["VotePoints"]);
        Assert.Equal(dueAtUtc, GetSession(sessions, connection).LegacyVoteClaimDueAtUtc);

        observe = true;
        await WorldPacketHandlers.ProcessDueLegacyVoteClaimAsync(
            context,
            claimNowUtc,
            claimLocalNow,
            CancellationToken.None);

        Assert.Equal(1, exposureCount);
        Assert.Equal(4, sent.Count);
        Assert.Equal(
            [
                LegacyMsgUpdatePacket.Opcode,
                LegacyMsgUpdatePacket.Opcode,
                LegacyVipStatusPacket.Opcode,
                LegacyNpcReplyPacket.Opcode,
            ],
            sent.Select(bytes => ReadFrame(bytes).Header.Opcode));
        Assert.Equal(sent[0], sent[1]);

        var firstVip = ParseUpdate(sent[0]);
        var secondVip = ParseUpdate(sent[1]);
        AssertVipLevelFourUpdate(firstVip, session);
        AssertVipLevelFourUpdate(secondVip, session);
        Assert.All(
            new[] { firstVip, secondVip }.SelectMany(packet => packet.Updates),
            update => Assert.Equal(LegacyMsgUpdateDataType.VIPLevel, update.Type));

        var vipStatus = ParseVipStatus(sent[2]);
        Assert.Equal(LegacyVipStatusFlags.VipLevelOne, vipStatus.Flags);
        var messageBox = ParseNpcReply(sent[3]);
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, messageBox.InteractType);
        Assert.Equal(ClaimMessage, messageBox.Text);
        AssertAscii(messageBox.Text);

        var updated = GetSession(sessions, connection);
        Assert.Null(updated.LegacyVoteClaimDueAtUtc);
        Assert.Equal("0", updated.Character.Attributes["VotePoints"]);
        Assert.Equal("4", updated.Character.Attributes["VipLevel"]);
        Assert.Equal(expectedVipTime, updated.Character.Attributes["VipTime"]);

        var reloaded = LegacyVoteRuntimeStateStore.Load(VotesPath);
        var record = Assert.Single(reloaded.Snapshot);
        Assert.Equal((uint)session.Character.Uid, record.Uid);
        Assert.Equal(claimLocalNow.Ticks, record.Timer.Ticks);
        AssertNoVoteTransactionArtifacts();
    }

    [Fact]
    public async Task DueClaimExtendsActiveVipExpiryAndDowngradesVipSixToFour()
    {
        var nowUtc = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var localNow = new DateTime(2026, 7, 11, 7, 0, 0, DateTimeKind.Unspecified);
        var activeExpiry = localNow.AddHours(5);
        var usersRoot = CreateUsersRoot();
        var session = CreateSession(
            usersRoot,
            votePoints: "9",
            vipLevel: "6",
            vipTime: activeExpiry.ToBinary().ToString(CultureInfo.InvariantCulture),
            dueAtUtc: nowUtc.AddSeconds(-1));
        WriteCharacter(session);
        var votes = LegacyVoteRuntimeStateStore.Load(VotesPath);
        var sent = new List<byte[]>();
        var sessions = new WorldSessionStore();
        var connection = CreateConnection(sent);
        sessions.Set(connection, session);

        await WorldPacketHandlers.ProcessDueLegacyVoteClaimAsync(
            CreateContext(connection, session, sessions, votes, TimeProvider.System),
            nowUtc,
            localNow,
            CancellationToken.None);

        var updated = GetSession(sessions, connection);
        Assert.Equal("10", updated.Character.Attributes["VotePoints"]);
        Assert.Equal("4", updated.Character.Attributes["VipLevel"]);
        Assert.Equal(
            activeExpiry.AddHours(11),
            DateTime.FromBinary(long.Parse(updated.Character.Attributes["VipTime"], CultureInfo.InvariantCulture)));
        Assert.Equal(4, sent.Count);
        Assert.Equal(LegacyVipStatusFlags.VipLevelOne, ParseVipStatus(sent[2]).Flags);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("256")]
    [InlineData("not-a-byte")]
    public async Task DueClaimRejectsVotePointsOutsideStoredByteRange(string rawVotePoints)
    {
        var nowUtc = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var usersRoot = CreateUsersRoot();
        var session = CreateSession(
            usersRoot,
            votePoints: rawVotePoints,
            dueAtUtc: nowUtc.AddSeconds(-1));
        WriteCharacter(session);
        var originalCharacter = File.ReadAllBytes(CharacterPath(session));
        var votes = LegacyVoteRuntimeStateStore.Load(VotesPath);
        var sent = new List<byte[]>();
        var sessions = new WorldSessionStore();
        var connection = CreateConnection(sent);
        sessions.Set(connection, session);

        await WorldPacketHandlers.ProcessDueLegacyVoteClaimAsync(
            CreateContext(connection, session, sessions, votes, TimeProvider.System),
            nowUtc,
            nowUtc.DateTime,
            CancellationToken.None);

        Assert.Empty(sent);
        Assert.Empty(votes.Snapshot);
        Assert.False(File.Exists(VotesPath));
        Assert.Equal(originalCharacter, File.ReadAllBytes(CharacterPath(session)));
        Assert.Equal(rawVotePoints, GetSession(sessions, connection).Character.Attributes["VotePoints"]);
        Assert.NotNull(GetSession(sessions, connection).LegacyVoteClaimDueAtUtc);
    }

    [Fact]
    public async Task ExistingVotesFileFailureRollsBackCharacterAndLeavesStoreSessionAndPacketsHidden()
    {
        var nowUtc = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var usersRoot = CreateUsersRoot();
        var session = CreateSession(usersRoot, votePoints: "3", dueAtUtc: nowUtc.AddSeconds(-1));
        WriteCharacter(session);
        WriteVotes("Count=1\r\n900/198.51.100.90/1/\r\n");
        var originalCharacter = File.ReadAllBytes(CharacterPath(session));
        var originalVotes = File.ReadAllBytes(VotesPath);
        var votes = LegacyVoteRuntimeStateStore.Load(VotesPath);
        var sent = new List<byte[]>();
        var sessions = new WorldSessionStore();
        var connection = CreateConnection(sent);
        sessions.Set(connection, session);

        using (new FileStream(VotesPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await WorldPacketHandlers.ProcessDueLegacyVoteClaimAsync(
                CreateContext(connection, session, sessions, votes, TimeProvider.System),
                nowUtc,
                nowUtc.DateTime,
                CancellationToken.None);
        }

        Assert.Equal(originalCharacter, File.ReadAllBytes(CharacterPath(session)));
        Assert.Equal(originalVotes, File.ReadAllBytes(VotesPath));
        Assert.Single(votes.Snapshot);
        Assert.Equal("3", GetSession(sessions, connection).Character.Attributes["VotePoints"]);
        Assert.NotNull(GetSession(sessions, connection).LegacyVoteClaimDueAtUtc);
        Assert.Empty(sent);
        AssertNoVoteTransactionArtifacts();
    }

    [Fact]
    public async Task MissingVotesFileCallbackFailureRollsBackThenRetryAwardsExactlyOnceWithoutArtifacts()
    {
        var nowUtc = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var usersRoot = CreateUsersRoot();
        var session = CreateSession(usersRoot, votePoints: "20", dueAtUtc: nowUtc.AddSeconds(-1));
        WriteCharacter(session);
        var originalCharacter = File.ReadAllBytes(CharacterPath(session));
        var votes = LegacyVoteRuntimeStateStore.Load(VotesPath);
        Directory.CreateDirectory(VotesPath);
        var sent = new List<byte[]>();
        var sessions = new WorldSessionStore();
        var connection = CreateConnection(sent);
        sessions.Set(connection, session);
        var context = CreateContext(connection, session, sessions, votes, TimeProvider.System);

        await WorldPacketHandlers.ProcessDueLegacyVoteClaimAsync(
            context,
            nowUtc,
            nowUtc.DateTime,
            CancellationToken.None);

        Assert.Equal(originalCharacter, File.ReadAllBytes(CharacterPath(session)));
        Assert.True(Directory.Exists(VotesPath));
        Assert.Empty(votes.Snapshot);
        Assert.Equal("20", GetSession(sessions, connection).Character.Attributes["VotePoints"]);
        Assert.NotNull(GetSession(sessions, connection).LegacyVoteClaimDueAtUtc);
        Assert.Empty(sent);
        AssertNoVoteTransactionArtifacts();

        Directory.Delete(VotesPath);
        await WorldPacketHandlers.ProcessDueLegacyVoteClaimAsync(
            context,
            nowUtc.AddTicks(1),
            nowUtc.DateTime.AddTicks(1),
            CancellationToken.None);

        Assert.Equal(4, sent.Count);
        Assert.Equal("21", GetSession(sessions, connection).Character.Attributes["VotePoints"]);
        Assert.Null(GetSession(sessions, connection).LegacyVoteClaimDueAtUtc);
        Assert.Single(votes.Snapshot);
        Assert.True(File.Exists(VotesPath));
        AssertNoVoteTransactionArtifacts();

        sent.Clear();
        await WorldPacketHandlers.ProcessDueLegacyVoteClaimAsync(
            context,
            nowUtc.AddMinutes(1),
            nowUtc.DateTime.AddMinutes(1),
            CancellationToken.None);

        Assert.Empty(sent);
        Assert.Equal("21", GetSession(sessions, connection).Character.Attributes["VotePoints"]);
        Assert.Single(votes.Snapshot);
        AssertNoVoteTransactionArtifacts();
    }

    [Fact]
    public async Task SharedIpPreclaimsBothRewardWhileOneVoteRecordIsUpdated()
    {
        var startUtc = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var claimUtc = startUtc.AddSeconds(90).AddTicks(1);
        var timeProvider = new MutableTimeProvider(startUtc, TimeZoneInfo.Utc);
        var usersRoot = CreateUsersRoot();
        var first = CreateSession(usersRoot, accountId: 100, uid: 1_293_600, votePoints: "0");
        var second = CreateSession(usersRoot, accountId: 101, uid: 1_293_601, votePoints: "0");
        WriteCharacter(first);
        WriteCharacter(second);
        var votes = LegacyVoteRuntimeStateStore.Load(VotesPath);
        var ipAddress = IPAddress.Parse("203.0.113.77");
        var firstSent = new List<byte[]>();
        var secondSent = new List<byte[]>();
        var firstConnection = CreateConnection(firstSent, ipAddress, port: 9958);
        var secondConnection = CreateConnection(secondSent, ipAddress, port: 9959);
        var sessions = new WorldSessionStore();
        sessions.Set(firstConnection, first);
        sessions.Set(secondConnection, second);
        var firstContext = CreateContext(firstConnection, first, sessions, votes, timeProvider);
        var secondContext = CreateContext(secondConnection, second, sessions, votes, timeProvider);

        Assert.True(await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            firstContext,
            first,
            VoteManagerNpcId,
            optionId: 40,
            CancellationToken.None));
        Assert.True(await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            secondContext,
            second,
            VoteManagerNpcId,
            optionId: 40,
            CancellationToken.None));
        Assert.Equal(2, firstSent.Count);
        Assert.Equal(2, secondSent.Count);
        Assert.Empty(votes.Snapshot);

        firstSent.Clear();
        secondSent.Clear();
        await WorldPacketHandlers.ProcessDueLegacyVoteClaimAsync(
            firstContext with { Session = GetSession(sessions, firstConnection) },
            claimUtc,
            claimUtc.DateTime,
            CancellationToken.None);
        await WorldPacketHandlers.ProcessDueLegacyVoteClaimAsync(
            secondContext with { Session = GetSession(sessions, secondConnection) },
            claimUtc,
            claimUtc.DateTime,
            CancellationToken.None);

        Assert.Equal(4, firstSent.Count);
        Assert.Equal(4, secondSent.Count);
        Assert.Equal("1", GetSession(sessions, firstConnection).Character.Attributes["VotePoints"]);
        Assert.Equal("1", GetSession(sessions, secondConnection).Character.Attributes["VotePoints"]);
        Assert.Null(GetSession(sessions, firstConnection).LegacyVoteClaimDueAtUtc);
        Assert.Null(GetSession(sessions, secondConnection).LegacyVoteClaimDueAtUtc);
        var record = Assert.Single(votes.Snapshot);
        Assert.Equal((uint)first.Character.Uid, record.Uid);
        Assert.Equal(ipAddress.ToString(), record.IpAddress);
        Assert.StartsWith("Count=1\r\n", ReadVotes(), StringComparison.Ordinal);
        AssertNoVoteTransactionArtifacts();
    }

    [Fact]
    public async Task DueClaimDoesNothingWhenConnectionSessionIsAbsent()
    {
        var nowUtc = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var usersRoot = CreateUsersRoot();
        var detachedSession = CreateSession(usersRoot, dueAtUtc: nowUtc.AddSeconds(-1));
        WriteCharacter(detachedSession);
        var originalCharacter = File.ReadAllBytes(CharacterPath(detachedSession));
        var votes = LegacyVoteRuntimeStateStore.Load(VotesPath);
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();

        await WorldPacketHandlers.ProcessDueLegacyVoteClaimAsync(
            CreateContext(connection, detachedSession, sessions, votes, TimeProvider.System),
            nowUtc,
            nowUtc.DateTime,
            CancellationToken.None);

        Assert.Empty(sent);
        Assert.Empty(votes.Snapshot);
        Assert.False(File.Exists(VotesPath));
        Assert.Equal(originalCharacter, File.ReadAllBytes(CharacterPath(detachedSession)));
        Assert.False(sessions.TryGet(connection, out _));
        AssertNoVoteTransactionArtifacts();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string VotesPath => Path.Combine(_root, "Votes.txt");

    private string CreateUsersRoot()
    {
        var usersRoot = Path.Combine(_root, "Users");
        Directory.CreateDirectory(usersRoot);
        return usersRoot;
    }

    private static WorldSession CreateSession(
        string usersRoot,
        long accountId = 100,
        long uid = 1_293_600,
        long level = 140,
        string votePoints = "0",
        string vipLevel = "0",
        string vipTime = "0",
        DateTimeOffset? dueAtUtc = null)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VotePoints"] = votePoints,
            ["VipLevel"] = vipLevel,
            ["VipTime"] = vipTime,
        };
        return new WorldSession(
            AccountId: accountId,
            Username: "legacy-vote-manager-test",
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: "VoteTester" + uid.ToString(CultureInfo.InvariantCulture),
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: level,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: Path.Combine(usersRoot, accountId.ToString(CultureInfo.InvariantCulture) + ".ini"),
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot,
            LegacyVoteClaimDueAtUtc: dueAtUtc);
    }

    private static WorldPacketHandlerContext CreateContext(
        ConquerTcpConnection connection,
        WorldSession session,
        WorldSessionStore sessions,
        LegacyVoteRuntimeStateStore votes,
        TimeProvider timeProvider)
    {
        return new WorldPacketHandlerContext(
            connection,
            TextWriter.Null,
            Session: session,
            Sessions: sessions,
            VoteStates: votes,
            TimeProvider: timeProvider);
    }

    private static ConquerTcpConnection CreateConnection(
        List<byte[]> sent,
        IPAddress? ipAddress = null,
        int port = 9958,
        bool includeRemoteEndPoint = true)
    {
        EndPoint? remoteEndPoint = includeRemoteEndPoint
            ? new IPEndPoint(ipAddress ?? IPAddress.Parse("192.0.2.40"), port)
            : null;
        return new ConquerTcpConnection(null, remoteEndPoint, (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static WorldSession GetSession(WorldSessionStore sessions, ConquerTcpConnection connection)
    {
        Assert.True(sessions.TryGet(connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private void WriteCharacter(WorldSession session)
    {
        var attributes = session.Character.Attributes;
        File.WriteAllText(
            CharacterPath(session),
            "[Character]\r\n" +
            "UID=" + session.Character.Uid.ToString(CultureInfo.InvariantCulture) + "\r\n" +
            "Name=" + session.Character.Name + "\r\n" +
            "VotePoints=" + attributes["VotePoints"] + "\r\n" +
            "VipLevel=" + attributes["VipLevel"] + "\r\n" +
            "VipTime=" + attributes["VipTime"] + "\r\n" +
            "[Other]\r\nKeep=unchanged\r\n",
            Encoding.Latin1);
    }

    private static string CharacterPath(WorldSession session)
    {
        return Path.Combine(
            session.UsersRoot!,
            session.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
    }

    private static IReadOnlyDictionary<string, string> ReadCharacterAttributes(WorldSession session)
    {
        return IniDocument.Parse(File.ReadAllText(CharacterPath(session), Encoding.Latin1))
            .GetSection("Character");
    }

    private void WriteVotes(string text)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(VotesPath, text, Encoding.ASCII);
    }

    private string ReadVotes()
    {
        return Encoding.ASCII.GetString(File.ReadAllBytes(VotesPath));
    }

    private static string ExpectedVotesText(WorldSession session, DateTime claimLocalNow)
    {
        return "Count=1\r\n" +
            session.Character.Uid.ToString(CultureInfo.InvariantCulture) +
            "/192.0.2.40/" +
            claimLocalNow.Ticks.ToString(CultureInfo.InvariantCulture) +
            "/\r\n";
    }

    private void AssertNoVoteTransactionArtifacts()
    {
        Assert.Empty(Directory.EnumerateFileSystemEntries(
            _root,
            "*.vote-claim-*",
            SearchOption.AllDirectories));
    }

    private static void AssertMessage(byte[] bytes, uint chatType, string text)
    {
        var message = ParseMessage(bytes);
        Assert.Equal(chatType, message.ChatType);
        Assert.Equal(text, message.Content[3]);
        AssertAscii(message.Content[3]);
    }

    private static LegacyMsgMessagePacket ParseMessage(byte[] bytes)
    {
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message));
        return message;
    }

    private static LegacyNpcReplyPacket ParseNpcReply(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply));
        return reply;
    }

    private static LegacyMsgUpdatePacket ParseUpdate(byte[] bytes)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update));
        return update;
    }

    private static LegacyVipStatusPacket ParseVipStatus(byte[] bytes)
    {
        Assert.True(LegacyVipStatusPacket.TryParse(ReadFrame(bytes), out var status));
        return status;
    }

    private static void AssertVipLevelFourUpdate(LegacyMsgUpdatePacket packet, WorldSession session)
    {
        Assert.Equal((uint)session.Character.Uid, packet.Uid);
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.VIPLevel, update.Type);
        Assert.Equal(4UL, update.Value);
    }

    private static void AssertAscii(string text)
    {
        Assert.All(text, character => Assert.InRange(character, (char)0x20, (char)0x7E));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow, TimeZoneInfo localTimeZone) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override TimeZoneInfo LocalTimeZone { get; } = localTimeZone;

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void SetUtcNow(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }
    }
}
