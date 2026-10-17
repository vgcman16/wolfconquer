using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeleportQualifierRideOrderingParityTests
{
    private const uint CharacterUid = 1_293_760;
    private const uint PeerUid = CharacterUid + 1;
    private const int OrdinarySourceMapId = 1_015;
    private const int GuardedSourceMapId = 6_001;
    private const int DeadGuardedSourceMapId = 1_038;
    private const int OrdinaryDestinationMapId = 1_016;
    private const int QualifierMapId = 700;
    private const int RideStatusFlag = 0x32;
    private const int UnrelatedStatusFlag = 0x12;

    private static readonly string[] SyntheticQualifierAttributes =
    [
        "InQualifier",
        "Qualifier",
        "InQualifierMatch",
        "InTeamQualifier",
        "TeamQualifier",
        "InTeamQualifierMatch",
    ];

    [Fact]
    public async Task ArenaEndRetainsRealMembershipForFreshOwnershipGuard()
    {
        var harness = CreateHarness(onMyOwnServer: false);
        ActivateArenaMatch(harness.ArenaStates, harness.Connection, harness.PeerConnection);

        var outcome = await RunPreflightAsync(
            harness,
            OrdinaryDestinationMapId,
            requestedDynamicId: 77);

        Assert.True(outcome.Allowed);
        Assert.True(outcome.QualifierEndRequested);
        Assert.True(outcome.EndedQualifier);
        Assert.True(outcome.InQualifier);
        Assert.True(harness.ArenaStates.IsQualifierFighter(harness.Connection));
        Assert.Single(harness.ArenaStates.GetActiveMatches());
        Assert.False(harness.ArenaStates.TryAddDamage(
            harness.Connection,
            harness.PeerConnection,
            damage: 1,
            out _));
        Assert.False(harness.ArenaStates.TryCompleteActiveMatchAsLoser(
            harness.Connection,
            out _));
        AssertNoSyntheticQualifierAttributes(outcome.Session.Character);
    }

    [Fact]
    public async Task TeamArenaLeaderEndReturnsBeforeElitePkEnd()
    {
        var harness = CreateHarness(onMyOwnServer: false);
        var teamArenaPair = ActivateTeamArenaMatch(harness);
        harness.ElitePkStates.ActivateMatch(
            harness.Connection,
            harness.PeerConnection,
            dynamicId: 77);

        var outcome = await RunPreflightAsync(
            harness,
            OrdinaryDestinationMapId,
            requestedDynamicId: 77);

        Assert.True(outcome.Allowed);
        Assert.True(outcome.EndedQualifier);
        Assert.True(outcome.InQualifier);
        Assert.True(harness.TeamArenaStates.IsQualifierTeam(harness.Connection));
        Assert.False(harness.TeamArenaStates.TryAddDamage(
            teamArenaPair,
            firstTeam: true,
            damage: 1,
            out _));
        Assert.False(harness.TeamArenaStates.TryCompleteActiveMatchAsLoser(
            harness.Connection,
            out _));
        Assert.True(harness.ElitePkStates.TryAddDamage(
            harness.Connection,
            harness.PeerConnection,
            damage: 1,
            out _));
        AssertNoSyntheticQualifierAttributes(outcome.Session.Character);
    }

    [Fact]
    public async Task ElitePkEndRetainsRealMembershipAndStopsDamage()
    {
        var harness = CreateHarness(onMyOwnServer: false);
        harness.ElitePkStates.ActivateMatch(
            harness.Connection,
            harness.PeerConnection,
            dynamicId: 77);

        var outcome = await RunPreflightAsync(
            harness,
            OrdinaryDestinationMapId,
            requestedDynamicId: 77);

        Assert.True(outcome.Allowed);
        Assert.True(outcome.EndedQualifier);
        Assert.True(outcome.InQualifier);
        Assert.True(harness.ElitePkStates.IsQualifierFighter(harness.Connection));
        Assert.False(harness.ElitePkStates.TryAddDamage(
            harness.Connection,
            harness.PeerConnection,
            damage: 1,
            out _));
        AssertNoSyntheticQualifierAttributes(outcome.Session.Character);
    }

    [Fact]
    public async Task TeamPkLeaderEndRetainsRealMembershipAndStopsDamage()
    {
        var harness = CreateHarness(onMyOwnServer: false);
        ActivateTeamPkMatch(harness);

        var outcome = await RunPreflightAsync(
            harness,
            OrdinaryDestinationMapId,
            requestedDynamicId: 77);

        Assert.True(outcome.Allowed);
        Assert.True(outcome.EndedQualifier);
        Assert.True(outcome.InQualifier);
        Assert.True(harness.TeamPkStates.IsQualifierTeam(harness.Connection));
        var pair = Assert.Single(harness.TeamPkStates.GetActiveMatches());
        Assert.False(harness.TeamPkStates.TryAddDamage(
            pair,
            firstTeam: true,
            damage: 1,
            out _));
        AssertNoSyntheticQualifierAttributes(outcome.Session.Character);
    }

    [Fact]
    public async Task RealArenaWatcherMembershipAllowsForeignDynamicTeleport()
    {
        var harness = CreateHarness(onMyOwnServer: false);
        var thirdSent = new List<byte[]>();
        var thirdConnection = CreateSendingConnection(
            thirdSent,
            harness.Timeline,
            RuntimeEventKind.PeerFrameSent,
            port: 9_960);
        var thirdSession = CreateSession(
            CharacterUid + 2,
            OrdinarySourceMapId,
            onMyOwnServer: true,
            dynamicId: 0,
            includeRide: false,
            minHitPoints: 500);
        harness.Sessions.Set(thirdConnection, thirdSession);
        Assert.False(harness.ArenaStates.RegisterWaiting(
            harness.PeerConnection,
            arenaPoints: 4_500,
            out _));
        Assert.True(harness.ArenaStates.RegisterWaiting(
            thirdConnection,
            arenaPoints: 4_300,
            out _));
        Assert.False(harness.ArenaStates.TryAcceptCountdown(harness.PeerConnection, out _));
        Assert.True(harness.ArenaStates.TryAcceptCountdown(thirdConnection, out var pair));
        Assert.True(harness.ArenaStates.TryAddWatcher(harness.Connection, pair, out _));
        harness.Timeline.Clear();

        var outcome = await RunPreflightAsync(
            harness,
            OrdinaryDestinationMapId,
            requestedDynamicId: 77);

        Assert.True(outcome.Allowed);
        Assert.False(outcome.QualifierEndRequested);
        Assert.False(outcome.EndedQualifier);
        Assert.False(outcome.InQualifier);
        Assert.True(outcome.IsWatching);
    }

    [Theory]
    [InlineData(GuardedSourceMapId, 500, true, (int)LegacyTeleportDenialKind.SourceMapGuard)]
    [InlineData(DeadGuardedSourceMapId, 0, true, (int)LegacyTeleportDenialKind.SourceMapGuard)]
    [InlineData(OrdinarySourceMapId, 500, false, (int)LegacyTeleportDenialKind.CrossServerGuard)]
    public async Task Map700RemovesRideBeforeGuardDenial(
        int sourceMapId,
        int minHitPoints,
        bool onMyOwnServer,
        int expectedDenial)
    {
        var harness = CreateHarness(
            sourceMapId,
            onMyOwnServer,
            includeRide: true,
            minHitPoints: minHitPoints);

        var outcome = await RunPreflightAsync(harness, QualifierMapId);

        Assert.False(outcome.Allowed);
        Assert.Equal((LegacyTeleportDenialKind)expectedDenial, outcome.Denial?.Kind);
        Assert.True(outcome.RemovedRide);
        Assert.False(HasRide(outcome.Session.Character));
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(
            outcome.Session.Character.Attributes,
            UnrelatedStatusFlag));
        Assert.True(harness.Sessions.TryGet(harness.Connection, out var stored));
        Assert.Same(outcome.Session, stored);
        Assert.False(HasRide(Assert.IsType<WorldSession>(stored).Character));

        AssertRideClearedStatus(AssertSingleStatusVector(harness.SourceSent));
        AssertRideClearedStatus(AssertSingleStatusVector(harness.PeerSent));
        var persistedIndex = AssertSingleTimelineIndex(
            harness.Timeline,
            entry => entry.Kind == RuntimeEventKind.SourceSessionPersisted);
        var sourceFrameIndex = AssertSingleTimelineIndex(
            harness.Timeline,
            entry => entry.Kind == RuntimeEventKind.SourceFrameSent);
        var peerFrameIndex = AssertSingleTimelineIndex(
            harness.Timeline,
            entry => entry.Kind == RuntimeEventKind.PeerFrameSent);
        Assert.True(persistedIndex < sourceFrameIndex);
        Assert.True(sourceFrameIndex < peerFrameIndex);
    }

    [Fact]
    public async Task RideRemovalRequiresSessionStorePublication()
    {
        var harness = CreateHarness(includeRide: true);
        var context = harness.Context with { Sessions = null };

        var outcome = await WorldPacketHandlers.PrepareLegacyTeleportAsync(
            context,
            harness.SourceSession,
            new LegacyTeleportRequest("missing-store-test", QualifierMapId));

        Assert.False(outcome.Allowed);
        Assert.Equal(
            LegacyTeleportDenialKind.EntrySideEffectPublicationUnavailable,
            outcome.Denial?.Kind);
        Assert.False(outcome.RemovedRide);
        Assert.True(HasRide(outcome.Session.Character));
        Assert.Empty(harness.SourceSent);
        Assert.Empty(harness.PeerSent);
    }

    [Fact]
    public async Task RideRemovalRejectsStaleSourceWithoutOverwritingCurrentSession()
    {
        var harness = CreateHarness(includeRide: true);
        var currentSession = harness.SourceSession with
        {
            Character = harness.SourceSession.Character with { Money = 999 },
        };
        harness.Sessions.Set(harness.Connection, currentSession);
        harness.Timeline.Clear();

        var outcome = await RunPreflightAsync(harness, QualifierMapId);

        Assert.False(outcome.Allowed);
        Assert.Equal(LegacyTeleportDenialKind.StaleSourceSession, outcome.Denial?.Kind);
        Assert.False(outcome.RemovedRide);
        Assert.Same(currentSession, outcome.Session);
        Assert.True(harness.Sessions.TryGet(harness.Connection, out var stored));
        Assert.Same(currentSession, stored);
        Assert.Equal(999, stored!.Character.Money);
        Assert.True(HasRide(stored.Character));
        Assert.Empty(harness.SourceSent);
        Assert.Empty(harness.PeerSent);
        Assert.Empty(harness.Timeline);
    }

    [Fact]
    public async Task CarryHelperPreventsStaleDestinationBuilderFromRestoringRide()
    {
        var harness = CreateHarness(includeRide: true);
        var outcome = await RunPreflightAsync(harness, QualifierMapId);
        Assert.True(outcome.Allowed);
        Assert.True(outcome.RemovedRide);

        var staleDestination = harness.SourceSession with
        {
            Character = harness.SourceSession.Character with
            {
                MapId = QualifierMapId,
                X = 60,
                Y = 60,
            },
        };
        Assert.True(HasRide(staleDestination.Character));

        var carried = WorldPacketHandlers.CarryLegacyTeleportEntrySideEffects(
            outcome,
            staleDestination);

        Assert.False(HasRide(carried.Character));
        Assert.Equal(QualifierMapId, carried.Character.MapId);
        Assert.Equal(60, carried.Character.X);
        Assert.Equal(60, carried.Character.Y);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(
            carried.Character.Attributes,
            UnrelatedStatusFlag));
    }

    private static async Task<LegacyTeleportPreflightOutcome> RunPreflightAsync(
        PreflightHarness harness,
        long requestedMapId,
        long requestedDynamicId = 0)
    {
        return await WorldPacketHandlers.PrepareLegacyTeleportAsync(
            harness.Context,
            harness.SourceSession,
            new LegacyTeleportRequest(
                "store-backed-qualifier-ride-test",
                requestedMapId,
                requestedDynamicId));
    }

    private static void ActivateArenaMatch(
        ArenaSessionStateStore store,
        ConquerTcpConnection first,
        ConquerTcpConnection second)
    {
        Assert.False(store.RegisterWaiting(first, arenaPoints: 4_500, out _));
        Assert.True(store.RegisterWaiting(second, arenaPoints: 4_300, out _));
        Assert.False(store.TryAcceptCountdown(first, out _));
        Assert.True(store.TryAcceptCountdown(second, out _));
    }

    private static TeamArenaMatchPair ActivateTeamArenaMatch(PreflightHarness harness)
    {
        harness.TeamStates.CreateTeam(harness.Connection, CharacterUid);
        harness.TeamStates.CreateTeam(harness.PeerConnection, PeerUid);
        Assert.False(harness.TeamArenaStates.RegisterWaiting(
            harness.Connection,
            arenaPoints: 4_500,
            out _));
        Assert.True(harness.TeamArenaStates.RegisterWaiting(
            harness.PeerConnection,
            arenaPoints: 4_300,
            out _));
        Assert.False(harness.TeamArenaStates.TryAcceptCountdown(harness.Connection, out _));
        Assert.True(harness.TeamArenaStates.TryAcceptCountdown(
            harness.PeerConnection,
            out var pair));
        return pair;
    }

    private static void ActivateTeamPkMatch(PreflightHarness harness)
    {
        harness.TeamStates.CreateTeam(harness.Connection, CharacterUid);
        harness.TeamStates.CreateTeam(harness.PeerConnection, PeerUid);
        harness.TeamPkStates.ActivateMatch(
            harness.Connection,
            harness.PeerConnection,
            dynamicId: 77);
    }

    private static PreflightHarness CreateHarness(
        int sourceMapId = OrdinarySourceMapId,
        bool onMyOwnServer = true,
        bool includeRide = false,
        int minHitPoints = 500)
    {
        var timeline = new List<RuntimeEvent>();
        var sourceSent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        var connection = CreateSendingConnection(
            sourceSent,
            timeline,
            RuntimeEventKind.SourceFrameSent,
            port: 9_958);
        var peerConnection = CreateSendingConnection(
            peerSent,
            timeline,
            RuntimeEventKind.PeerFrameSent,
            port: 9_959);
        var sourceSession = CreateSession(
            CharacterUid,
            sourceMapId,
            onMyOwnServer,
            dynamicId: 0,
            includeRide,
            minHitPoints);
        var peerSession = CreateSession(
            PeerUid,
            sourceMapId,
            onMyOwnServer: true,
            dynamicId: 0,
            includeRide: false,
            minHitPoints: 500);
        var sessions = new WorldSessionStore((changedConnection, _, current) =>
        {
            if (ReferenceEquals(changedConnection, connection))
            {
                timeline.Add(new RuntimeEvent(
                    RuntimeEventKind.SourceSessionPersisted,
                    current,
                    Frame: null));
            }
        });
        sessions.Set(connection, sourceSession);
        sessions.Set(peerConnection, peerSession);
        timeline.Clear();

        var arenaStates = new ArenaSessionStateStore();
        var teamArenaStates = new TeamArenaSessionStateStore();
        var teamStates = new TeamSessionStateStore();
        var elitePkStates = new LegacyElitePkSessionStateStore();
        var teamPkStates = new LegacyTeamPkSessionStateStore();
        var log = new StringWriter(CultureInfo.InvariantCulture);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            sourceSession,
            ArenaStates: arenaStates,
            Sessions: sessions,
            TeamArenaStates: teamArenaStates,
            TeamStates: teamStates,
            ElitePkStates: elitePkStates,
            TeamPkStates: teamPkStates);

        return new PreflightHarness(
            connection,
            peerConnection,
            sourceSession,
            peerSession,
            sessions,
            arenaStates,
            teamArenaStates,
            teamStates,
            elitePkStates,
            teamPkStates,
            context,
            log,
            sourceSent,
            peerSent,
            timeline);
    }

    private static WorldSession CreateSession(
        uint uid,
        long mapId,
        bool onMyOwnServer,
        long dynamicId,
        bool includeRide,
        long minHitPoints)
    {
        return new WorldSession(
            AccountId: uid,
            Username: $"teleport-ordering-{uid}",
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: $"Ordering{uid}",
                Body: 1008,
                Face: 154,
                Hair: 27,
                Class: 7051,
                FirstClass: 0,
                SecondClass: 7050,
                Reborn: 2,
                Level: 140,
                Experience: 0,
                MapId: mapId,
                X: 52,
                Y: 50,
                PreviousMapId: 1_000,
                PreviousMapX: 500,
                PreviousMapY: 650,
                Strength: 0,
                Agility: 36,
                Vitality: 447,
                Spirit: 0,
                AttributePoints: 12,
                MinHitPoints: minHitPoints,
                MinMana: 5_000,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 7,
                SourceFile: "teleport-qualifier-ride-ordering-fixture",
                Attributes: CreateAttributes(
                    onMyOwnServer,
                    dynamicId,
                    includeRide),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static Dictionary<string, string> CreateAttributes(
        bool onMyOwnServer,
        long dynamicId,
        bool includeRide)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = dynamicId.ToString(CultureInfo.InvariantCulture),
            ["MaxHitPoints"] = "500",
            ["FixtureMarker"] = "preserved",
            ["OnMyOwnServer"] = onMyOwnServer.ToString(CultureInfo.InvariantCulture),
            ["StatusFlags"] = includeRide
                ? BuildStatusFlags(RideStatusFlag, UnrelatedStatusFlag)
                : BuildStatusFlags(UnrelatedStatusFlag),
        };

        if (includeRide)
        {
            attributes[WorldPacketHandlers.RuntimeLegacyRideExpiresAtUtcAttribute] =
                DateTimeOffset.UtcNow.AddDays(30).ToString("O", CultureInfo.InvariantCulture);
            attributes[WorldPacketHandlers.RuntimeLegacyRideRemoveOnDeadAttribute] = "true";
            attributes[WorldPacketHandlers.RuntimeLegacyRideStampSecondsAttribute] = "1";
        }

        return attributes;
    }

    private static void AssertNoSyntheticQualifierAttributes(CharacterSnapshot character)
    {
        Assert.All(
            SyntheticQualifierAttributes,
            attribute => Assert.False(character.Attributes.ContainsKey(attribute), attribute));
    }

    private static bool HasRide(CharacterSnapshot character)
    {
        return LegacyPlayerDeathState.HasStatusFlag(character.Attributes, RideStatusFlag);
    }

    private static LegacyMsgUpdatePacket AssertSingleStatusVector(IEnumerable<byte[]> frames)
    {
        return Assert.Single(
            frames.Select(TryParseUpdate)
                .Where(packet => packet is not null)
                .Select(packet => packet!),
            packet => packet.Updates.Any(update =>
                update.Type == LegacyMsgUpdateDataType.StatusFlag &&
                update.Values is not null));
    }

    private static LegacyMsgUpdatePacket? TryParseUpdate(byte[] bytes)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) ? packet : null;
    }

    private static void AssertRideClearedStatus(LegacyMsgUpdatePacket packet)
    {
        Assert.Equal(CharacterUid, packet.Uid);
        var status = Assert.Single(packet.Updates, update =>
            update.Type == LegacyMsgUpdateDataType.StatusFlag &&
            update.Values is not null);
        Assert.NotNull(status.Values);
        Assert.False(IsStatusFlagSet(status.Values, RideStatusFlag));
        Assert.True(IsStatusFlagSet(status.Values, UnrelatedStatusFlag));
    }

    private static bool IsStatusFlagSet(IReadOnlyList<ulong> values, int flag)
    {
        var wordIndex = flag / 32;
        return wordIndex < values.Count &&
            (values[wordIndex] & (1UL << (flag % 32))) != 0;
    }

    private static int AssertSingleTimelineIndex(
        IReadOnlyList<RuntimeEvent> timeline,
        Func<RuntimeEvent, bool> predicate)
    {
        return Assert.Single(
            timeline.Select((entry, index) => new { Entry = entry, Index = index }),
            candidate => predicate(candidate.Entry)).Index;
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[10];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words) + "/";
    }

    private static ConquerTcpConnection CreateSendingConnection(
        List<byte[]> sent,
        List<RuntimeEvent> timeline,
        RuntimeEventKind eventKind,
        int port)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, port),
            sendAsync: (frame, _) =>
            {
                var bytes = frame.ToArray();
                sent.Add(bytes);
                timeline.Add(new RuntimeEvent(eventKind, Session: null, bytes));
                return ValueTask.CompletedTask;
            });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
    }

    private enum RuntimeEventKind
    {
        SourceSessionPersisted,
        SourceFrameSent,
        PeerFrameSent,
    }

    private sealed record RuntimeEvent(
        RuntimeEventKind Kind,
        WorldSession? Session,
        byte[]? Frame);

    private sealed record PreflightHarness(
        ConquerTcpConnection Connection,
        ConquerTcpConnection PeerConnection,
        WorldSession SourceSession,
        WorldSession PeerSession,
        WorldSessionStore Sessions,
        ArenaSessionStateStore ArenaStates,
        TeamArenaSessionStateStore TeamArenaStates,
        TeamSessionStateStore TeamStates,
        LegacyElitePkSessionStateStore ElitePkStates,
        LegacyTeamPkSessionStateStore TeamPkStates,
        WorldPacketHandlerContext Context,
        StringWriter Log,
        List<byte[]> SourceSent,
        List<byte[]> PeerSent,
        List<RuntimeEvent> Timeline);
}
