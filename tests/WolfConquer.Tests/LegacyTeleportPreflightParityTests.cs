using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeleportPreflightParityTests
{
    private const uint CharacterUid = 1_293_600;
    private const int DestinationMapId = 1_002;
    private const int SourceX = 52;
    private const int SourceY = 50;
    private const int PortalX = 50;
    private const int PortalY = 50;
    private const int DestinationX = 60;
    private const int DestinationY = 60;
    private const int PrayingFlag = 0x1F;
    private const int CastPrayFlag = 0x1E;
    private const int MagicDefenderFlag = 0x80;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task ChangeMapFrom6001DeniesOrdinaryCharacterBeforeAnyTeleportSideEffects()
    {
        var result = await RunChangeMapAsync(
            sourceMapId: 6_001,
            hitPoints: 500,
            attributes: CreateAttributes(includeCleanupFlags: true));

        AssertDeniedWithoutSideEffects(result);
    }

    [Fact]
    public async Task ChangeMapFrom6001AllowsProjectManager()
    {
        var result = await RunChangeMapAsync(
            sourceMapId: 6_001,
            hitPoints: 500,
            attributes: CreateAttributes(projectManager: true));

        AssertDestinationTransition(result, 6_001, DestinationX, DestinationY);
    }

    [Fact]
    public async Task GuildWarJailOption3Overrides6001TeleportDenial()
    {
        var result = await RunGuildWarJailOption3Async(
            sourceMapId: 6_001,
            hitPoints: 500,
            attributes: CreateAttributes());

        Assert.True(result.NpcHandled);
        Assert.False(result.SourceSession.Character.Attributes.ContainsKey("ProjectManager"));
        AssertDestinationTransition(result, 6_001, expectedX: 410, expectedY: 354);
    }

    [Fact]
    public async Task ChangeMapFrom1038DeniesDeadCharacterBeforeAnyTeleportSideEffects()
    {
        var result = await RunChangeMapAsync(
            sourceMapId: 1_038,
            hitPoints: 0,
            attributes: CreateAttributes(dead: true, includeCleanupFlags: true));

        AssertDeniedWithoutSideEffects(result);
    }

    [Fact]
    public async Task ChangeMapFrom1038AllowsAliveCharacter()
    {
        var result = await RunChangeMapAsync(
            sourceMapId: 1_038,
            hitPoints: 500,
            attributes: CreateAttributes());

        AssertDestinationTransition(result, 1_038, DestinationX, DestinationY);
    }

    [Fact]
    public async Task GuildWarJailOption3OverridesDead1038TeleportDenial()
    {
        var result = await RunGuildWarJailOption3Async(
            sourceMapId: 1_038,
            hitPoints: 500,
            attributes: CreateAttributes(dead: true, includeCleanupFlags: true));

        Assert.True(result.NpcHandled);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(
            result.SourceSession.Character.Attributes,
            LegacyPlayerDeathState.DeadStatusFlag));
        Assert.Equal(500, result.SourceSession.Character.MinHitPoints);
        AssertDestinationTransition(result, 1_038, expectedX: 410, expectedY: 354);
    }

    [Fact]
    public async Task ChangeMapFrom1038StillDeniesDeadProjectManager()
    {
        var result = await RunChangeMapAsync(
            sourceMapId: 1_038,
            hitPoints: 0,
            attributes: CreateAttributes(
                projectManager: true,
                dead: true,
                includeCleanupFlags: true));

        Assert.Equal("1", result.SourceSession.Character.Attributes["ProjectManager"]);
        AssertDeniedWithoutSideEffects(result);
    }

    private static async Task<TransitionResult> RunChangeMapAsync(
        int sourceMapId,
        long hitPoints,
        IReadOnlyDictionary<string, string> attributes)
    {
        var harness = CreateHarness(sourceMapId, hitPoints, attributes);
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
            portals:
            [
                new Portal(
                    sourceMapId,
                    PortalX,
                    PortalY,
                    DestinationMapId,
                    DestinationX,
                    DestinationY),
            ],
            maps:
            [
                CreateMapMetadata(sourceMapId),
                CreateMapMetadata(DestinationMapId),
            ]);
        using var log = new StringWriter();

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                harness.Connection,
                log,
                Session: harness.SourceSession,
                WorldData: worldData,
                Sessions: harness.Sessions),
            ReadFrame(new MsgActionPacket
            {
                ObjId = CharacterUid,
                Timestamp = 4_321,
                Type = MsgActionType.ChangeMap,
            }.ToFrameBytes()));

        return ReadResult(harness);
    }

    private static async Task<TransitionResult> RunGuildWarJailOption3Async(
        int sourceMapId,
        long hitPoints,
        IReadOnlyDictionary<string, string> attributes)
    {
        var harness = CreateHarness(sourceMapId, hitPoints, attributes);
        using var log = new StringWriter();

        var handled = await WorldPacketHandlers.LegacyDisCityBattlefieldServicesTryHandleGwJailAsync(
            new WorldPacketHandlerContext(
                harness.Connection,
                log,
                Session: harness.SourceSession,
                Sessions: harness.Sessions),
            harness.SourceSession,
            optionId: 3,
            new DateTimeOffset(2026, 7, 14, 12, 20, 0, TimeSpan.Zero),
            CancellationToken.None);

        return ReadResult(harness, handled);
    }

    private static TeleportHarness CreateHarness(
        int sourceMapId,
        long hitPoints,
        IReadOnlyDictionary<string, string> attributes)
    {
        var clientSent = new List<byte[]>();
        var connection = CreateSendingConnection(clientSent);
        var sourceSession = CreateSession(
            sourceMapId,
            SourceX,
            SourceY,
            hitPoints,
            attributes,
            CharacterUid);
        var observerSent = new List<byte[]>();
        var observerConnection = CreateSendingConnection(observerSent);
        var observerSession = CreateSession(
            sourceMapId,
            SourceX + 1,
            SourceY,
            hitPoints: 500,
            CreateAttributes(),
            CharacterUid + 1);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, sourceSession);
        sessions.Set(observerConnection, observerSession);

        return new TeleportHarness(connection, sourceSession, sessions, clientSent, observerSent);
    }

    private static TransitionResult ReadResult(
        TeleportHarness harness,
        bool? npcHandled = null)
    {
        Assert.True(harness.Sessions.TryGet(harness.Connection, out var storedSession));
        return new TransitionResult(
            harness.SourceSession,
            Assert.IsType<WorldSession>(storedSession),
            harness.ClientSent,
            harness.ObserverSent,
            npcHandled);
    }

    private static WorldSession CreateSession(
        long mapId,
        long x,
        long y,
        long hitPoints,
        IReadOnlyDictionary<string, string> attributes,
        uint uid)
    {
        return new WorldSession(
            AccountId: uid,
            Username: $"teleport-preflight-{uid}",
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: $"Preflight{uid}",
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
                X: x,
                Y: y,
                PreviousMapId: 1_000,
                PreviousMapX: 500,
                PreviousMapY: 650,
                Strength: 0,
                Agility: 36,
                Vitality: 447,
                Spirit: 0,
                AttributePoints: 12,
                MinHitPoints: hitPoints,
                MinMana: 5_000,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 7,
                SourceFile: "teleport-preflight-fixture",
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static IReadOnlyDictionary<string, string> CreateAttributes(
        bool projectManager = false,
        bool dead = false,
        bool includeCleanupFlags = false)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = "77",
            ["MaxHitPoints"] = "500",
            ["FixtureMarker"] = "preflight-source",
        };
        var flags = new List<int>();
        if (dead)
        {
            flags.Add(LegacyPlayerDeathState.DeadStatusFlag);
        }

        if (includeCleanupFlags)
        {
            flags.AddRange([PrayingFlag, CastPrayFlag, MagicDefenderFlag]);
        }

        if (flags.Count > 0)
        {
            attributes["StatusFlags"] = BuildStatusFlags(flags);
        }

        if (projectManager)
        {
            attributes["ProjectManager"] = "1";
        }

        return attributes;
    }

    private static void AssertDeniedWithoutSideEffects(TransitionResult result)
    {
        Assert.Same(result.SourceSession, result.StoredSession);
        Assert.Equal(result.SourceSession.Character.MapId, result.StoredSession.Character.MapId);
        Assert.Equal(result.SourceSession.Character.X, result.StoredSession.Character.X);
        Assert.Equal(result.SourceSession.Character.Y, result.StoredSession.Character.Y);
        Assert.Equal(result.SourceSession.Character.PreviousMapId, result.StoredSession.Character.PreviousMapId);
        Assert.Equal(result.SourceSession.Character.PreviousMapX, result.StoredSession.Character.PreviousMapX);
        Assert.Equal(result.SourceSession.Character.PreviousMapY, result.StoredSession.Character.PreviousMapY);
        AssertAttributesEqual(
            result.SourceSession.Character.Attributes,
            result.StoredSession.Character.Attributes);

        Assert.True(LegacyPlayerDeathState.HasStatusFlag(
            result.SourceSession.Character.Attributes,
            PrayingFlag));
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(
            result.SourceSession.Character.Attributes,
            CastPrayFlag));
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(
            result.SourceSession.Character.Attributes,
            MagicDefenderFlag));

        Assert.DoesNotContain(result.ClientSent, IsTeleportStatusCleanupFrame);
        Assert.DoesNotContain(result.ObserverSent, IsTeleportStatusCleanupFrame);
        Assert.DoesNotContain(result.ObserverSent, IsRemoveEntityFrame);
        var acknowledgement = Assert.Single(ParseActions(result.ClientSent));
        Assert.Equal(MsgActionType.ChangeMap, acknowledgement.Type);
        Assert.Equal(CharacterUid, acknowledgement.ObjId);
    }

    private static void AssertDestinationTransition(
        TransitionResult result,
        int expectedSourceMapId,
        int expectedX,
        int expectedY)
    {
        Assert.NotSame(result.SourceSession, result.StoredSession);
        Assert.Equal(DestinationMapId, result.StoredSession.Character.MapId);
        Assert.Equal(expectedX, result.StoredSession.Character.X);
        Assert.Equal(expectedY, result.StoredSession.Character.Y);
        Assert.Equal(expectedSourceMapId, result.StoredSession.Character.PreviousMapId);
        Assert.Equal(SourceX, result.StoredSession.Character.PreviousMapX);
        Assert.Equal(SourceY, result.StoredSession.Character.PreviousMapY);

        var teleport = Assert.Single(
            ParseActions(result.ClientSent),
            action => action.Type == MsgActionType.Teleport);
        Assert.Equal((uint)DestinationMapId, teleport.DwParam);
        Assert.Equal((uint)DestinationMapId, teleport.DwParam3);
        Assert.Equal((uint)expectedX, teleport.PositionX);
        Assert.Equal((uint)expectedY, teleport.PositionY);
    }

    private static void AssertAttributesEqual(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (key, value) in expected)
        {
            Assert.True(actual.TryGetValue(key, out var actualValue), $"Missing source attribute '{key}'.");
            Assert.Equal(value, actualValue);
        }
    }

    private static bool IsTeleportStatusCleanupFrame(byte[] bytes)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Updates.Any(update =>
                update.Type == LegacyMsgUpdateDataType.StatusFlag ||
                (update.Type == LegacyMsgUpdateDataType.AzureShield &&
                 update.Flag == MagicDefenderFlag));
    }

    private static bool IsRemoveEntityFrame(byte[] bytes)
    {
        return LegacyRemoveEntityPacket.TryParse(ReadFrame(bytes), out _);
    }

    private static IReadOnlyList<MsgActionPacket> ParseActions(IEnumerable<byte[]> sent)
    {
        var actions = new List<MsgActionPacket>();
        foreach (var bytes in sent)
        {
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                actions.Add(packet);
            }
        }

        return actions;
    }

    private static MapMetadata CreateMapMetadata(int mapId)
    {
        return new MapMetadata(
            mapId,
            mapId.ToString(CultureInfo.InvariantCulture) + ".ini",
            Name: null,
            MapDoc: mapId,
            TypeRaw: "0",
            Type: 0,
            Portal0X: null,
            Portal0Y: null,
            RebornMap: mapId,
            RebornPortal: 0,
            ResLev: 0,
            LinkMap: 0,
            LinkX: 0,
            LinkY: 0,
            ColorRaw: "0",
            Color: 0,
            Attributes: new Dictionary<string, string>());
    }

    private static string BuildStatusFlags(IEnumerable<int> flags)
    {
        var words = new ulong[10];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words) + "/";
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record TeleportHarness(
        ConquerTcpConnection Connection,
        WorldSession SourceSession,
        WorldSessionStore Sessions,
        List<byte[]> ClientSent,
        List<byte[]> ObserverSent);

    private sealed record TransitionResult(
        WorldSession SourceSession,
        WorldSession StoredSession,
        IReadOnlyList<byte[]> ClientSent,
        IReadOnlyList<byte[]> ObserverSent,
        bool? NpcHandled = null);
}
