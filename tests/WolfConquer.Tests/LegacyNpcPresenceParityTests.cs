using System.Buffers;
using System.Net;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNpcPresenceParityTests
{
    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task SetLocationSendsAllowDynamicStaticNpcInDynamicInstance()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
        var ironTong = worldData.NpcsById[20_499].Single(row => row.MapId == 1_002);
        Assert.Equal(1, ironTong.OptionalFlag);
        var sent = new List<byte[]>();
        var session = CreateSession(
            mapId: ironTong.MapId,
            x: ironTong.X,
            y: ironTong.Y,
            attributes: new Dictionary<string, string> { ["DynamicID"] = "2468" });

        await HandleSetLocationAsync(worldData, session, sent);

        Assert.Contains(sent, bytes =>
            LegacyNpcSpawnPacket.TryParse(ReadFrame(bytes), out var spawn) &&
            spawn.Uid == 20_499);
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task SetLocationSkipsNonAllowDynamicStaticNpcInDynamicInstance()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
        var normalNpc = worldData.NpcsById[3_215].Single(row => row.MapId == 1_002);
        Assert.NotEqual(1, normalNpc.OptionalFlag);
        var sent = new List<byte[]>();
        var session = CreateSession(
            mapId: normalNpc.MapId,
            x: normalNpc.X,
            y: normalNpc.Y,
            attributes: new Dictionary<string, string> { ["DynamicID"] = "2468" });

        await HandleSetLocationAsync(worldData, session, sent);

        Assert.DoesNotContain(sent, bytes =>
            LegacyNpcSpawnPacket.TryParse(ReadFrame(bytes), out var spawn) &&
            spawn.Uid == 3_215);
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task SetLocationSkipsNormalSobNpcInDynamicInstance()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
        var bank = worldData.SobNpcsById[102_177].Single(row => row.MapId == 1_002);
        var sent = new List<byte[]>();
        var session = CreateSession(
            mapId: bank.MapId,
            x: bank.X,
            y: bank.Y,
            attributes: new Dictionary<string, string> { ["DynamicID"] = "2468" });

        await HandleSetLocationAsync(worldData, session, sent);

        Assert.DoesNotContain(sent, bytes =>
            LegacySobNpcSpawnPacket.TryParse(ReadFrame(bytes), out var spawn) &&
            spawn.Uid == 102_177);
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task SetLocationAtTwinCitySpawnSendsVisibleSobNpcAndNoStaticNpc()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
        var gm = worldData.SobNpcsById[45_632].Single(row => row.MapId == 1_002);
        Assert.Equal(7, LegacyTestDistance(410, 354, gm.X, gm.Y));

        var nearestStaticNpc = worldData.NpcsByMapId[1_002]
            .OrderBy(npc => LegacyTestDistance(410, 354, npc.X, npc.Y))
            .First();
        Assert.True(LegacyTestDistance(410, 354, nearestStaticNpc.X, nearestStaticNpc.Y) > 18);

        var sent = new List<byte[]>();
        var session = CreateSession(mapId: 1_002, x: 410, y: 354);

        await HandleSetLocationAsync(worldData, session, sent);

        Assert.Contains(sent, bytes =>
            LegacySobNpcSpawnPacket.TryParse(ReadFrame(bytes), out var spawn) &&
            spawn.Uid == 45_632 &&
            spawn.X == gm.X &&
            spawn.Y == gm.Y);
        Assert.DoesNotContain(sent, bytes =>
            LegacyNpcSpawnPacket.TryParse(ReadFrame(bytes), out var spawn) &&
            spawn.Uid == (uint)nearestStaticNpc.Id);
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task MovementAtTwinCitySpawnResendsVisibleSobNpc()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
        var sent = new List<byte[]>();
        var session = CreateSession(mapId: 1_002, x: 410, y: 354);

        await HandleMovementAsync(worldData, session, sent);

        Assert.Contains(sent, bytes =>
            LegacySobNpcSpawnPacket.TryParse(ReadFrame(bytes), out var spawn) &&
            spawn.Uid == 45_632);
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task JumpAtTwinCitySpawnResendsVisibleSobNpc()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
        var sent = new List<byte[]>();
        var session = CreateSession(mapId: 1_002, x: 410, y: 354);

        await HandleJumpAsync(worldData, session, sent);

        Assert.Contains(sent, bytes =>
            LegacySobNpcSpawnPacket.TryParse(ReadFrame(bytes), out var spawn) &&
            spawn.Uid == 45_632);
    }

    [Fact]
    public async Task UnsupportedNpcScriptClickEmitsStructuredFallbackDiagnostic()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(
                15_641,
                optionId: 99,
                LegacyNpcReplyInteractType.Dialog).ToFrameBytes()));

        var fallbackFrame = Assert.Single(sent);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(fallbackFrame), out var fallback));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, fallback.InteractType);
        Assert.Contains("legacy script is not modeled", fallback.Text);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Equal(15_641U, updatedSession?.ActiveNpcId);

        var output = log.ToString();
        Assert.Contains("PARITY-GAP category=npc severity=warning gap=missing-npc-script", output);
        Assert.Contains("dedupe=npc:missing-npc-script:action=Script:interactType=1:nextSeam=model-npc-script-definition:npcId=15641:opcode=2084:optionId=99:unsupportedReason=MissingOption", output);
        Assert.Contains("npcId=15641", output);
        Assert.Contains("opcode=2084", output);
        Assert.Contains("action=Script", output);
        Assert.Contains("interactType=1", output);
        Assert.Contains("optionId=99", output);
        Assert.Contains("actionCategory=UnknownOption", output);
        Assert.Contains("source=NpcHandler.Teleportall", output);
        Assert.Contains("nextSeam=model-npc-script-definition", output);
        Assert.Contains("unsupportedReason=MissingOption", output);
        Assert.Contains("WorldServer sent LegacyNpcServer fallback dialog", output);
        Assert.DoesNotContain("unknown world opcode", output);
    }

    private static async ValueTask HandleSetLocationAsync(
        WorldDataCatalog worldData,
        WorldSession session,
        List<byte[]> sent)
    {
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: new SobNpcRuntimeStateStore());
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_293_600,
                Timestamp = 1234,
                Type = MsgActionType.SetLocation,
            }.ToFrameBytes()));
    }

    private static async ValueTask HandleMovementAsync(
        WorldDataCatalog worldData,
        WorldSession session,
        List<byte[]> sent)
    {
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: new SobNpcRuntimeStateStore());
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(new MsgMovementPacket
            {
                Direction = 0,
                Running = MsgMovementMode.Walk,
                Timestamp = 456,
            }.ToFrameBytes()));
    }

    private static async ValueTask HandleJumpAsync(
        WorldDataCatalog worldData,
        WorldSession session,
        List<byte[]> sent)
    {
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: new SobNpcRuntimeStateStore());
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_293_600,
                Timestamp = 789,
                Type = MsgActionType.Jump,
                Facing = 6,
                TargetPositionX = 411,
                TargetPositionY = 354,
            }.ToFrameBytes()));
    }

    private static WorldSession CreateSession(
        long mapId = 1002,
        long x = 577,
        long y = 586,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "user02",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "HeroOne",
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
                X: x,
                Y: y,
                PreviousMapId: mapId,
                PreviousMapX: x,
                PreviousMapY: y,
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
                SourceFile: "fixture",
                Attributes: attributes ?? new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
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

    private static long LegacyTestDistance(long x, long y, long x2, long y2)
    {
        return Math.Max(Math.Abs(x - x2), Math.Abs(y - y2));
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        var startPaths = new[]
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
            Path.GetDirectoryName(sourceFilePath) ?? string.Empty,
        };
        foreach (var startPath in startPaths.Where(path => path.Length > 0).Distinct())
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output, working, or source directory.");
    }
}
