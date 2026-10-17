using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeleportDestinationExecutorTests
{
    [Fact]
    public async Task StaleSourceIsRejectedBeforeLeaveOrDestinationPublication()
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var source = CreateSession(mapId: 1_002, x: 430, y: 378);
        var publicationCount = 0;
        var sessions = new WorldSessionStore((_, _, _) => publicationCount++);
        sessions.Set(connection, source);
        var current = source with
        {
            Character = source.Character with { Money = 123_456 },
        };
        sessions.Set(connection, current);
        var publicationCountBeforeTeleport = publicationCount;
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: source,
            Sessions: sessions,
            TeleportTerrain: CreatePrisonTerrain());

        var result = await WorldPacketHandlers.ExecuteLegacyTeleportDestinationAsync(
            context,
            source,
            new LegacyTeleportDestination(6_000, 32, 73),
            timestamp: 4_321,
            CancellationToken.None);

        Assert.Null(result);
        Assert.True(sessions.TryGet(connection, out var stored));
        var storedSession = Assert.IsType<WorldSession>(stored);
        Assert.Same(current, storedSession);
        Assert.Equal(123_456, storedSession.Character.Money);
        Assert.Equal(publicationCountBeforeTeleport, publicationCount);
        Assert.Empty(sent);
        Assert.Contains("skipped stale legacy GameClient.Teleport", log.ToString());
    }

    [Fact]
    public async Task InvalidPrisonLocationPublishesBothSourceTransitions()
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var source = CreateSession(mapId: 1_002, x: 430, y: 378);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, source);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: source,
            Sessions: sessions,
            TeleportTerrain: CreatePrisonTerrain());

        var result = await WorldPacketHandlers.ExecuteLegacyTeleportDestinationAsync(
            context,
            source,
            new LegacyTeleportDestination(6_000, 80, 90),
            timestamp: 4_321,
            CancellationToken.None);

        var finalSession = Assert.IsType<WorldSession>(result);
        Assert.Equal(6_000, finalSession.Character.MapId);
        Assert.Equal(32, finalSession.Character.X);
        Assert.Equal(73, finalSession.Character.Y);
        Assert.Equal(1_002, finalSession.Character.PreviousMapId);
        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.Same(finalSession, stored);

        var teleports = ParseActions(sent)
            .Where(packet => packet.Type == MsgActionType.Teleport)
            .ToArray();
        Assert.Equal(2, teleports.Length);
        Assert.Equal((80U, 90U), (teleports[0].PositionX, teleports[0].PositionY));
        Assert.Equal((32U, 73U), (teleports[1].PositionX, teleports[1].PositionY));
        Assert.All(teleports, packet => Assert.Equal(6_000U, packet.DwParam3));
    }

    [Fact]
    public async Task UnloadableRequestedMapPublishesTwinCityFallbackWithRequestedDynamic()
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var source = CreateSession(mapId: 1_015, x: 830, y: 584, dynamicId: 44);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, source);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: source,
            Sessions: sessions,
            TeleportTerrain: LegacyGameMapTerrainQuery.Empty);

        var result = await WorldPacketHandlers.ExecuteLegacyTeleportDestinationAsync(
            context,
            source,
            new LegacyTeleportDestination(99_999, 12, 13, DynamicId: 88),
            timestamp: 7_654,
            CancellationToken.None);

        var finalSession = Assert.IsType<WorldSession>(result);
        Assert.Equal(1_002, finalSession.Character.MapId);
        Assert.Equal(410, finalSession.Character.X);
        Assert.Equal(354, finalSession.Character.Y);
        Assert.Equal("88", finalSession.Character.Attributes["DynamicID"]);
        Assert.Equal("0", finalSession.Character.Attributes["PDinamycID"]);
        var teleport = Assert.Single(
            ParseActions(sent),
            packet => packet.Type == MsgActionType.Teleport);
        Assert.Equal(1_002U, teleport.DwParam3);
        Assert.Equal(410U, teleport.PositionX);
        Assert.Equal(354U, teleport.PositionY);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlockedPrisonNpcTileIsAccepted(bool useSobNpc)
    {
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var source = CreateSession(mapId: 1_002, x: 430, y: 378);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, source);
        using var log = new StringWriter();
        var worldData = CreateWorldDataWithPrisonNpc(useSobNpc);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: source,
            Sessions: sessions,
            WorldData: worldData,
            TeleportTerrain: CreatePrisonTerrain());

        var result = await WorldPacketHandlers.ExecuteLegacyTeleportDestinationAsync(
            context,
            source,
            new LegacyTeleportDestination(6_000, 80, 90),
            timestamp: 4_321,
            CancellationToken.None);

        var finalSession = Assert.IsType<WorldSession>(result);
        Assert.Equal((6_000L, 80L, 90L),
            (finalSession.Character.MapId, finalSession.Character.X, finalSession.Character.Y));
        var teleport = Assert.Single(
            ParseActions(sent),
            packet => packet.Type == MsgActionType.Teleport);
        Assert.Equal((80U, 90U), (teleport.PositionX, teleport.PositionY));
    }

    private static LegacyGameMapTerrainQuery CreatePrisonTerrain()
    {
        const int width = 100;
        const int height = 100;
        var cells = new LegacyDMapCell[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                cells[(y * width) + x] = new LegacyDMapCell(
                    x,
                    y,
                    TerrainFlag: 1,
                    Surface: 0,
                    Altitude: 0);
            }
        }

        cells[(73 * width) + 32] = new LegacyDMapCell(
            32,
            73,
            TerrainFlag: 0,
            Surface: 0,
            Altitude: 0);
        return new LegacyGameMapTerrainQuery(
        [
            new LegacyDMapTerrain(6_000, "prison.dmap", width, height, cells),
        ]);
    }

    private static WorldDataCatalog CreateWorldDataWithPrisonNpc(bool useSobNpc)
    {
        var npcs = useSobNpc
            ? Array.Empty<Npc>()
            : [new Npc(1, 0, 0, 6_000, 80, 90, OptionalFlag: null)];
        var sobNpcs = useSobNpc
            ?
            [
                new SobNpc(
                    2,
                    "PrisonStructure",
                    0,
                    0,
                    6_000,
                    80,
                    90,
                    1,
                    1,
                    0,
                    null,
                    null,
                    0,
                    string.Empty,
                    []),
            ]
            : Array.Empty<SobNpc>();
        return LegacyItemWorldDataFixture.Create([], [], npcs, sobNpcs);
    }

    private static WorldSession CreateSession(
        long mapId,
        long x,
        long y,
        ulong dynamicId = 0)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = dynamicId.ToString(CultureInfo.InvariantCulture),
            ["PDinamycID"] = "777",
        };
        return new WorldSession(
            AccountId: 2,
            Username: "HeroOne",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "HeroOne",
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
                PreviousMapId: 900,
                PreviousMapX: 90,
                PreviousMapY: 91,
                Strength: 0,
                Agility: 36,
                Vitality: 447,
                Spirit: 0,
                AttributePoints: 12,
                MinHitPoints: 61_258,
                MinMana: 5_000,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 7,
                SourceFile: "",
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static IReadOnlyList<MsgActionPacket> ParseActions(IEnumerable<byte[]> sent)
    {
        var actions = new List<MsgActionPacket>();
        foreach (var bytes in sent)
        {
            Assert.True(PacketFrameDecoder.TryRead(
                new ReadOnlySequence<byte>(bytes),
                out var frame,
                out _,
                out _));
            if (MsgActionPacket.TryParse(frame, out var packet))
            {
                actions.Add(packet);
            }
        }

        return actions;
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9958),
            (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }
}
