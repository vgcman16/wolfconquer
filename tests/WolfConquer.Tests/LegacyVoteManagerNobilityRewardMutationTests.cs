using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyVoteManagerNobilityRewardMutationTests : IDisposable
{
    private const uint DonorUid = 1_293_600;
    private const long DonorAccountId = DonorUid;
    private const uint VoteManagerNpcId = 52_300;
    private const byte NobilityRewardOption = 48;
    private const ulong NobilityReward = 2_000_000_000;
    private const long Money = 987_654;
    private const long ConquerPoints = 123_456;
    private const long BoundConquerPoints = 7_890;
    private const string PreservedPeriodTime = "3155378975999999999";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyVoteManagerNobilityRewardMutationTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Option48AddsLiteralTwoBillionPersistsBeforeExposureAndFansOutSourceOrder()
    {
        const ulong initialDonation = 100_000_000;
        const ulong expectedDonation = initialDonation + NobilityReward;
        var result = await InvokeAsync(
            votePoints: 4,
            initialDonation,
            includeRankingPeers: true);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        AssertRewardState(
            result.UpdatedSession.Character,
            votePoints: 3,
            expectedDonation,
            LegacyNobilityRanking.RankKing,
            position: 0);
        AssertWalletUnchanged(result.OriginalSession.Character, result.UpdatedSession.Character);

        var persistedAtExposure = Assert.IsType<CharacterSnapshot>(result.PersistedAtSessionExposure);
        AssertRewardState(
            persistedAtExposure,
            votePoints: 3,
            expectedDonation,
            LegacyNobilityRanking.RankKing,
            position: 0);
        AssertWalletUnchanged(result.OriginalSession.Character, persistedAtExposure);
        var reloaded = Assert.IsType<CharacterSnapshot>(result.ReloadedCharacter);
        AssertRewardState(
            reloaded,
            votePoints: 3,
            expectedDonation,
            LegacyNobilityRanking.RankKing,
            position: 0);
        AssertWalletUnchanged(result.OriginalSession.Character, reloaded);

        var firstExposure = Assert.IsType<ExposureEvent>(result.ExposureOrder.FirstOrDefault());
        Assert.Equal("session", firstExposure.Kind);
        Assert.Equal(DonorUid, firstExposure.Uid);

        var packetEvents = result.ExposureOrder
            .Where(entry => entry.Kind == "packet")
            .ToArray();
        Assert.Equal(8, packetEvents.Length);
        Assert.Equal(
            new ulong[] { DonorUid, 10, 20, 30 },
            packetEvents.Take(4).Select(entry => AssertNobilityIcon(entry.Frame!).Uid).ToArray());
        Assert.All(packetEvents.Take(4), entry =>
        {
            var frame = ReadFrame(entry.Frame!);
            Assert.Equal(LegacyNobilityIconPacket.Opcode, frame.Header.Opcode);
        });
        Assert.All(packetEvents.Skip(4), entry => AssertPromotion(entry.Frame!, "VoteNobilityHero"));

        Assert.Collection(
            result.SentByUid[DonorUid],
            frame => AssertIcon(
                frame,
                DonorUid,
                expectedDonation,
                LegacyNobilityRanking.RankKing,
                position: 0),
            frame => AssertPromotion(frame, "VoteNobilityHero"));
        AssertCollectionForDisplaced(
            result.SentByUid[10],
            uid: 10,
            donation: 400_000_000,
            LegacyNobilityRanking.RankKing,
            position: 1);
        AssertCollectionForDisplaced(
            result.SentByUid[20],
            uid: 20,
            donation: 390_000_000,
            LegacyNobilityRanking.RankKing,
            position: 2);
        AssertCollectionForDisplaced(
            result.SentByUid[30],
            uid: 30,
            donation: 380_000_000,
            LegacyNobilityRanking.RankPrince,
            position: 3);

        AssertPeerState(result.UpdatedSessions[10].Character, 400_000_000, LegacyNobilityRanking.RankKing, 1);
        AssertPeerState(result.UpdatedSessions[20].Character, 390_000_000, LegacyNobilityRanking.RankKing, 2);
        AssertPeerState(result.UpdatedSessions[30].Character, 380_000_000, LegacyNobilityRanking.RankPrince, 3);

        AssertNoForbiddenRewardPackets(result.SentByUid.Values.SelectMany(static frames => frames));
        Assert.Contains("sourceLines=25846-25854", result.Log);
        Assert.Contains("donation=100000000->2100000000", result.Log);
        Assert.Contains("promotionBroadcast=True", result.Log);
        Assert.Contains("packetOrder=NobilityIcon,ChangedNobilityIcons,PromotionBroadcast", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Option48DoesNotBroadcastWhenDonationLeavesKingRankAndPositionUnchanged()
    {
        const ulong initialDonation = 3_000_000_000;
        var result = await InvokeAsync(votePoints: 1, initialDonation);

        Assert.True(result.Handled);
        AssertRewardState(
            result.UpdatedSession.Character,
            votePoints: 0,
            initialDonation + NobilityReward,
            LegacyNobilityRanking.RankKing,
            position: 0);
        var donorFrame = Assert.Single(result.SentByUid[DonorUid]);
        AssertIcon(
            donorFrame,
            DonorUid,
            initialDonation + NobilityReward,
            LegacyNobilityRanking.RankKing,
            position: 0);
        Assert.False(LegacyMsgMessagePacket.TryParse(ReadFrame(donorFrame), out _));
        Assert.Contains("promotionBroadcast=False", result.Log);
        AssertNoForbiddenRewardPackets(result.SentByUid[DonorUid]);
    }

    [Fact]
    public async Task InsufficientVotePointsIsHandledCompletelySilently()
    {
        var result = await InvokeAsync(votePoints: 0, initialDonation: 100_000_000);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtSessionExposure);
        Assert.Empty(result.ExposureOrder);
        Assert.Empty(result.SentByUid[DonorUid]);
        Assert.Equal(string.Empty, result.Log);
        AssertOriginalState(result.UpdatedSession.Character, votePoints: 0, donation: 100_000_000);
        AssertWalletUnchanged(result.OriginalSession.Character, result.UpdatedSession.Character);
        Assert.Equal(result.PreHandleBytes, File.ReadAllBytes(result.CharacterPath));
        AssertOriginalState(
            Assert.IsType<CharacterSnapshot>(result.ReloadedCharacter),
            votePoints: 0,
            donation: 100_000_000);
        AssertNoPersistenceArtifacts(result.UsersRoot);
    }

    [Fact]
    public async Task DonationOverflowFailsClosedWithoutDebitPersistenceOrPackets()
    {
        const ulong initialDonation = ulong.MaxValue - 1_000_000_000;
        var result = await InvokeAsync(votePoints: 1, initialDonation);

        AssertFence(result);
        AssertOriginalState(result.UpdatedSession.Character, votePoints: 1, initialDonation);
        AssertWalletUnchanged(result.OriginalSession.Character, result.UpdatedSession.Character);
        Assert.Equal(result.PreHandleBytes, File.ReadAllBytes(result.CharacterPath));
        AssertOriginalState(
            Assert.IsType<CharacterSnapshot>(result.ReloadedCharacter),
            votePoints: 1,
            initialDonation);
        Assert.Contains("failure=DonationOverflow", result.Log);
        Assert.Contains("no session mutation or packets were committed", result.Log);
        AssertNoPersistenceArtifacts(result.UsersRoot);
    }

    [Fact]
    public async Task MissingSessionStoreFailsClosedBeforePersistence()
    {
        var result = await InvokeAsync(
            votePoints: 1,
            initialDonation: 100_000_000,
            includeSessionStore: false);

        AssertFence(result);
        AssertOriginalState(result.UpdatedSession.Character, votePoints: 1, donation: 100_000_000);
        AssertWalletUnchanged(result.OriginalSession.Character, result.UpdatedSession.Character);
        Assert.Equal(result.PreHandleBytes, File.ReadAllBytes(result.CharacterPath));
        AssertOriginalState(
            Assert.IsType<CharacterSnapshot>(result.ReloadedCharacter),
            votePoints: 1,
            donation: 100_000_000);
        Assert.Contains("reason=session-store-missing", result.Log);
        Assert.Contains("sourceLines=25846-25854", result.Log);
        AssertNoPersistenceArtifacts(result.UsersRoot);
    }

    [Fact]
    public async Task PersistenceFailureRollsBackAndLeavesNoTransactionArtifacts()
    {
        var result = await InvokeAsync(
            votePoints: 1,
            initialDonation: 100_000_000,
            lockSnapshotBeforeHandle: true);

        AssertFence(result);
        AssertOriginalState(result.UpdatedSession.Character, votePoints: 1, donation: 100_000_000);
        AssertWalletUnchanged(result.OriginalSession.Character, result.UpdatedSession.Character);
        Assert.Equal(result.PreHandleBytes, File.ReadAllBytes(result.CharacterPath));
        AssertOriginalState(
            Assert.IsType<CharacterSnapshot>(result.ReloadedCharacter),
            votePoints: 1,
            donation: 100_000_000);
        Assert.Contains("nobility reward persistence", result.Log);
        Assert.Contains("reason=snapshotReplace", result.Log);
        Assert.Contains("no session mutation or packets were committed", result.Log);
        AssertNoPersistenceArtifacts(result.UsersRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        ulong votePoints,
        ulong initialDonation,
        bool includeRankingPeers = false,
        bool includeSessionStore = true,
        bool lockSnapshotBeforeHandle = false)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(invocationRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        var characterPath = Path.Combine(
            usersRoot,
            DonorAccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        WriteDonorSnapshot(characterPath, votePoints, initialDonation);

        var loader = new CharacterSnapshotLoader(usersRoot);
        var donorCharacter = Assert.IsType<CharacterSnapshot>(loader.LoadByAccountId(DonorAccountId));
        var donorSession = CreateSession(DonorAccountId, donorCharacter, usersRoot);
        var preHandleBytes = File.ReadAllBytes(characterPath);
        var sentByUid = new Dictionary<uint, List<byte[]>>();
        var exposureOrder = new List<ExposureEvent>();
        var connections = new Dictionary<uint, ConquerTcpConnection>();
        var donorConnection = CreateSendingConnection(DonorUid, sentByUid, exposureOrder);
        connections[DonorUid] = donorConnection;

        CharacterSnapshot? persistedAtSessionExposure = null;
        WorldSessionStore? sessions = null;
        if (includeSessionStore)
        {
            sessions = new WorldSessionStore((_, previous, current) =>
            {
                if (previous is null || ReferenceEquals(previous, current))
                {
                    return;
                }

                var uid = checked((uint)current.Character.Uid);
                if (uid == DonorUid)
                {
                    persistedAtSessionExposure = Assert.IsType<CharacterSnapshot>(
                        loader.LoadByAccountId(DonorAccountId));
                }

                exposureOrder.Add(new ExposureEvent("session", uid, Frame: null));
            });
            sessions.Set(donorConnection, donorSession);

            if (includeRankingPeers)
            {
                AddPeer(sessions, connections, sentByUid, exposureOrder, usersRoot, 10, "First", 400_000_000);
                AddPeer(sessions, connections, sentByUid, exposureOrder, usersRoot, 20, "Second", 390_000_000);
                AddPeer(sessions, connections, sentByUid, exposureOrder, usersRoot, 30, "Third", 380_000_000);
            }
        }

        using var log = new StringWriter();
        await using var snapshotLock = lockSnapshotBeforeHandle
            ? new FileStream(characterPath, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null;
        var handled = await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
                new WorldPacketHandlerContext(
                    donorConnection,
                    log,
                    Session: donorSession,
                    Sessions: sessions),
                donorSession,
                VoteManagerNpcId,
                NobilityRewardOption,
                CancellationToken.None)
            .ConfigureAwait(false);

        var updatedSession = donorSession;
        var updatedSessions = new Dictionary<uint, WorldSession>
        {
            [DonorUid] = donorSession,
        };
        if (sessions is not null)
        {
            Assert.True(sessions.TryGet(donorConnection, out var storedDonorSession));
            updatedSession = Assert.IsType<WorldSession>(storedDonorSession);
            updatedSessions = sessions.Snapshot().ToDictionary(
                active => checked((uint)active.Session.Character.Uid),
                active => active.Session);
        }

        var reloadedCharacter = loader.LoadByAccountId(DonorAccountId);

        return new InvokeResult(
            handled,
            donorSession,
            updatedSession,
            updatedSessions,
            persistedAtSessionExposure,
            reloadedCharacter,
            sentByUid.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<byte[]>)pair.Value),
            exposureOrder,
            log.ToString(),
            usersRoot,
            characterPath,
            preHandleBytes);
    }

    private static void AddPeer(
        WorldSessionStore sessions,
        IDictionary<uint, ConquerTcpConnection> connections,
        IDictionary<uint, List<byte[]>> sentByUid,
        IList<ExposureEvent> exposureOrder,
        string usersRoot,
        uint uid,
        string name,
        ulong donation)
    {
        var connection = CreateSendingConnection(uid, sentByUid, exposureOrder);
        connections[uid] = connection;
        sessions.Set(
            connection,
            CreateSession(
                uid,
                CreateCharacter(uid, name, votePoints: 0, donation, paidActive: false),
                usersRoot));
    }

    private static ConquerTcpConnection CreateSendingConnection(
        uint uid,
        IDictionary<uint, List<byte[]>> sentByUid,
        IList<ExposureEvent> exposureOrder)
    {
        var sent = new List<byte[]>();
        sentByUid[uid] = sent;
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9_958), (frame, _) =>
        {
            var bytes = frame.ToArray();
            sent.Add(bytes);
            exposureOrder.Add(new ExposureEvent("packet", uid, bytes));
            return ValueTask.CompletedTask;
        });
    }

    private static WorldSession CreateSession(long accountId, CharacterSnapshot character, string usersRoot)
    {
        return new WorldSession(
            accountId,
            Username: "legacy-vote-nobility-reward-test",
            Character: character,
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static CharacterSnapshot CreateCharacter(
        uint uid,
        string name,
        ulong votePoints,
        ulong donation,
        bool paidActive)
    {
        var rank = LegacyNobilityRanking.ComputeRank(donation, LegacyNobilityRanking.DefaultPosition);
        return new CharacterSnapshot(
            Uid: uid,
            Name: name,
            Body: 1003,
            Face: 154,
            Hair: 0,
            Class: 7_051,
            FirstClass: 7_051,
            SecondClass: 0,
            Reborn: 0,
            Level: 140,
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
            Money,
            ConquerPoints,
            BoundConquerPoints,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 0,
            SourceFile: "fixture",
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["VotePoints"] = votePoints.ToString(CultureInfo.InvariantCulture),
                ["DonationNobility"] = donation.ToString(CultureInfo.InvariantCulture),
                ["LastNobilityDonation"] = donation.ToString(CultureInfo.InvariantCulture),
                ["NobilityRank"] = rank.ToString(CultureInfo.InvariantCulture),
                ["NobilityPosition"] = LegacyNobilityRanking.DefaultPosition.ToString(CultureInfo.InvariantCulture),
                ["Sentinel"] = "keep-character",
            },
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Identifier"] = "7",
                ["LastNobilityDonation"] = "11",
                ["PaidRank"] = rank.ToString(CultureInfo.InvariantCulture),
                ["Rank"] = LegacyNobilityRanking.RankName(rank),
                ["Position"] = LegacyNobilityRanking.DefaultPosition.ToString(CultureInfo.InvariantCulture),
                ["IsActive"] = paidActive.ToString(CultureInfo.InvariantCulture),
                ["PeriodTime"] = PreservedPeriodTime,
                ["PaidSentinel"] = "keep-paid",
            });
    }

    private static void WriteDonorSnapshot(string path, ulong votePoints, ulong donation)
    {
        var character = CreateCharacter(
            DonorUid,
            "VoteNobilityHero",
            votePoints,
            donation,
            paidActive: true);
        File.WriteAllText(
            path,
            $"[Character]{Environment.NewLine}" +
            $"UID={DonorUid}{Environment.NewLine}" +
            $"Body={character.Body}{Environment.NewLine}" +
            $"Face={character.Face}{Environment.NewLine}" +
            $"Name={character.Name}{Environment.NewLine}" +
            $"Class={character.Class}{Environment.NewLine}" +
            $"Map={character.MapId}{Environment.NewLine}" +
            $"X={character.X}{Environment.NewLine}" +
            $"Y={character.Y}{Environment.NewLine}" +
            $"FirstClass={character.FirstClass}{Environment.NewLine}" +
            $"SecoundeClass={character.SecondClass}{Environment.NewLine}" +
            $"Reborn={character.Reborn}{Environment.NewLine}" +
            $"Level={character.Level}{Environment.NewLine}" +
            $"Haire={character.Hair}{Environment.NewLine}" +
            $"Experience={character.Experience}{Environment.NewLine}" +
            $"PMap={character.PreviousMapId}{Environment.NewLine}" +
            $"PMapX={character.PreviousMapX}{Environment.NewLine}" +
            $"PMapY={character.PreviousMapY}{Environment.NewLine}" +
            $"Agility={character.Agility}{Environment.NewLine}" +
            $"Strength={character.Strength}{Environment.NewLine}" +
            $"Vitaliti={character.Vitality}{Environment.NewLine}" +
            $"Spirit={character.Spirit}{Environment.NewLine}" +
            $"Atributes={character.AttributePoints}{Environment.NewLine}" +
            $"MinHitPoints={character.MinHitPoints}{Environment.NewLine}" +
            $"MinMana={character.MinMana}{Environment.NewLine}" +
            $"Money={Money}{Environment.NewLine}" +
            $"ConquerPoints={ConquerPoints}{Environment.NewLine}" +
            $"BankCps=0{Environment.NewLine}" +
            $"BoundConquerPoints={BoundConquerPoints}{Environment.NewLine}" +
            $"PkPoints=0{Environment.NewLine}" +
            $"Online=0{Environment.NewLine}" +
            $"VotePoints={votePoints.ToString(CultureInfo.InvariantCulture)}{Environment.NewLine}" +
            $"DonationNobility={donation.ToString(CultureInfo.InvariantCulture)}{Environment.NewLine}" +
            $"LastNobilityDonation={donation.ToString(CultureInfo.InvariantCulture)}{Environment.NewLine}" +
            $"NobilityRank={character.Attributes["NobilityRank"]}{Environment.NewLine}" +
            $"NobilityPosition={LegacyNobilityRanking.DefaultPosition}{Environment.NewLine}" +
            $"Sentinel=keep-character{Environment.NewLine}" +
            Environment.NewLine +
            $"[PaidNobility]{Environment.NewLine}" +
            $"Identifier=7{Environment.NewLine}" +
            $"LastNobilityDonation=11{Environment.NewLine}" +
            $"PaidRank={character.PaidNobility["PaidRank"]}{Environment.NewLine}" +
            $"Rank={character.PaidNobility["Rank"]}{Environment.NewLine}" +
            $"Position={LegacyNobilityRanking.DefaultPosition}{Environment.NewLine}" +
            $"PeriodTime={PreservedPeriodTime}{Environment.NewLine}" +
            $"IsActive=True{Environment.NewLine}" +
            $"PaidSentinel=keep-paid{Environment.NewLine}");
    }

    private static void AssertRewardState(
        CharacterSnapshot character,
        ulong votePoints,
        ulong donation,
        byte rank,
        int position)
    {
        Assert.Equal(votePoints.ToString(CultureInfo.InvariantCulture), character.Attributes["VotePoints"]);
        Assert.Equal(donation.ToString(CultureInfo.InvariantCulture), character.Attributes["DonationNobility"]);
        Assert.Equal(donation.ToString(CultureInfo.InvariantCulture), character.Attributes["LastNobilityDonation"]);
        Assert.Equal(rank.ToString(CultureInfo.InvariantCulture), character.Attributes["NobilityRank"]);
        Assert.Equal(position.ToString(CultureInfo.InvariantCulture), character.Attributes["NobilityPosition"]);
        Assert.Equal("keep-character", character.Attributes["Sentinel"]);

        Assert.Equal(DonorUid.ToString(CultureInfo.InvariantCulture), character.PaidNobility["Identifier"]);
        Assert.Equal(donation.ToString(CultureInfo.InvariantCulture), character.PaidNobility["LastNobilityDonation"]);
        Assert.Equal(rank.ToString(CultureInfo.InvariantCulture), character.PaidNobility["PaidRank"]);
        Assert.Equal(LegacyNobilityRanking.RankName(rank), character.PaidNobility["Rank"]);
        Assert.Equal(position.ToString(CultureInfo.InvariantCulture), character.PaidNobility["Position"]);
        Assert.Equal(bool.TrueString, character.PaidNobility["IsActive"]);
        Assert.Equal(PreservedPeriodTime, character.PaidNobility["PeriodTime"]);
        Assert.Equal("keep-paid", character.PaidNobility["PaidSentinel"]);
    }

    private static void AssertOriginalState(
        CharacterSnapshot character,
        ulong votePoints,
        ulong donation)
    {
        var rank = LegacyNobilityRanking.ComputeRank(donation, LegacyNobilityRanking.DefaultPosition);
        Assert.Equal(votePoints.ToString(CultureInfo.InvariantCulture), character.Attributes["VotePoints"]);
        Assert.Equal(donation.ToString(CultureInfo.InvariantCulture), character.Attributes["DonationNobility"]);
        Assert.Equal(donation.ToString(CultureInfo.InvariantCulture), character.Attributes["LastNobilityDonation"]);
        Assert.Equal(rank.ToString(CultureInfo.InvariantCulture), character.Attributes["NobilityRank"]);
        Assert.Equal(
            LegacyNobilityRanking.DefaultPosition.ToString(CultureInfo.InvariantCulture),
            character.Attributes["NobilityPosition"]);
        Assert.Equal("keep-character", character.Attributes["Sentinel"]);
        Assert.Equal("7", character.PaidNobility["Identifier"]);
        Assert.Equal("11", character.PaidNobility["LastNobilityDonation"]);
        Assert.Equal(rank.ToString(CultureInfo.InvariantCulture), character.PaidNobility["PaidRank"]);
        Assert.Equal(LegacyNobilityRanking.RankName(rank), character.PaidNobility["Rank"]);
        Assert.Equal(bool.TrueString, character.PaidNobility["IsActive"]);
        Assert.Equal(PreservedPeriodTime, character.PaidNobility["PeriodTime"]);
        Assert.Equal("keep-paid", character.PaidNobility["PaidSentinel"]);
    }

    private static void AssertWalletUnchanged(CharacterSnapshot before, CharacterSnapshot after)
    {
        Assert.Equal(before.Money, after.Money);
        Assert.Equal(before.ConquerPoints, after.ConquerPoints);
        Assert.Equal(before.BoundConquerPoints, after.BoundConquerPoints);
        Assert.Equal(Money, after.Money);
        Assert.Equal(ConquerPoints, after.ConquerPoints);
        Assert.Equal(BoundConquerPoints, after.BoundConquerPoints);
    }

    private static void AssertPeerState(CharacterSnapshot character, ulong donation, byte rank, int position)
    {
        Assert.Equal(donation.ToString(CultureInfo.InvariantCulture), character.Attributes["DonationNobility"]);
        Assert.Equal(rank.ToString(CultureInfo.InvariantCulture), character.Attributes["NobilityRank"]);
        Assert.Equal(position.ToString(CultureInfo.InvariantCulture), character.Attributes["NobilityPosition"]);
    }

    private static void AssertCollectionForDisplaced(
        IReadOnlyList<byte[]> frames,
        uint uid,
        ulong donation,
        byte rank,
        int position)
    {
        Assert.Collection(
            frames,
            frame => AssertIcon(frame, uid, donation, rank, position),
            frame => AssertPromotion(frame, "VoteNobilityHero"));
    }

    private static void AssertIcon(byte[] bytes, uint uid, ulong donation, byte rank, int position)
    {
        var icon = AssertNobilityIcon(bytes);
        Assert.Equal(uid, icon.Uid);
        Assert.Equal(donation, icon.Donation);
        Assert.Equal(rank, icon.Rank);
        Assert.Equal(position, icon.Position);
    }

    private static LegacyNobilityIconPacket AssertNobilityIcon(byte[] bytes)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(LegacyNobilityIconPacket.Opcode, frame.Header.Opcode);
        Assert.True(LegacyNobilityIconPacket.TryParse(frame, out var icon));
        return icon;
    }

    private static void AssertPromotion(byte[] bytes, string donorName)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(LegacyMsgMessagePacket.Opcode, frame.Header.Opcode);
        Assert.True(LegacyMsgMessagePacket.TryParse(frame, out var message));
        Assert.Equal(LegacyMsgMessagePacket.RedColor, message.Color);
        Assert.Equal(LegacyMsgMessagePacket.CenterChatType, message.ChatType);
        Assert.Equal($"{donorName} has been promoted to King.", message.Content[3]);
    }

    private static void AssertNoForbiddenRewardPackets(IEnumerable<byte[]> frames)
    {
        foreach (var bytes in frames)
        {
            var frame = ReadFrame(bytes);
            Assert.False(LegacyMsgUpdatePacket.TryParse(frame, out _));
            Assert.False(LegacyNpcReplyPacket.TryParse(frame, out _));
            Assert.False(LegacyNobilityRankListPacket.TryParse(frame, out _));
            Assert.Contains(
                frame.Header.Opcode,
                new[] { LegacyNobilityIconPacket.Opcode, LegacyMsgMessagePacket.Opcode });
        }
    }

    private static void AssertFence(InvokeResult result)
    {
        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtSessionExposure);
        Assert.Empty(result.ExposureOrder);
        Assert.All(result.SentByUid.Values, Assert.Empty);
    }

    private static void AssertNoPersistenceArtifacts(string usersRoot)
    {
        Assert.Empty(Directory.EnumerateFiles(usersRoot, "*.paid-nobility-*"));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record ExposureEvent(string Kind, uint Uid, byte[]? Frame);

    private sealed record InvokeResult(
        bool Handled,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        IReadOnlyDictionary<uint, WorldSession> UpdatedSessions,
        CharacterSnapshot? PersistedAtSessionExposure,
        CharacterSnapshot? ReloadedCharacter,
        IReadOnlyDictionary<uint, IReadOnlyList<byte[]>> SentByUid,
        IReadOnlyList<ExposureEvent> ExposureOrder,
        string Log,
        string UsersRoot,
        string CharacterPath,
        byte[] PreHandleBytes);
}
