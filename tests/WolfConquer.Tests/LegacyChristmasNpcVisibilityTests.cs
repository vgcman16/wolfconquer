using System.Buffers;
using System.Net;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyChristmasNpcVisibilityTests
{
    [Fact]
    public async Task SetLocationSendsVisibleChristmasNpcFromRealSeed()
    {
        var worldData = await LoadSeedWorldDataAsync();
        if (worldData is null)
        {
            return;
        }

        var christmasNpc = worldData.ChristmasNpcsById[52_301].Single();
        var sent = new List<byte[]>();
        var session = CreateSession(christmasNpc.MapId, christmasNpc.X, christmasNpc.Y);
        using var log = new StringWriter();
        var context = CreateContext(worldData, session, sent, log);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_293_600,
                Timestamp = 1234,
                Type = MsgActionType.SetLocation,
            }.ToFrameBytes()));

        Assert.Contains(sent, bytes =>
            LegacyNpcSpawnPacket.TryParse(ReadFrame(bytes), out var spawn) &&
            spawn == new LegacyNpcSpawnPacket(
                52_301,
                52_301,
                0,
                (ushort)christmasNpc.X,
                (ushort)christmasNpc.Y,
                (ushort)christmasNpc.Lookface,
                (ushort)christmasNpc.Kind,
                0));
        Assert.Contains("WorldServer sent LegacyChristmasNpcProjection", log.ToString());
    }

    [Fact]
    public async Task RequestEntityRefreshesVisibleChristmasNpcSpawn()
    {
        var worldData = await LoadSeedWorldDataAsync();
        if (worldData is null)
        {
            return;
        }

        var christmasNpc = worldData.ChristmasNpcsById[52_301].Single();
        var sent = new List<byte[]>();
        var session = CreateSession(christmasNpc.MapId, christmasNpc.X, christmasNpc.Y);
        using var log = new StringWriter();
        var context = CreateContext(worldData, session, sent, log);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_293_600,
                DwParam = 52_301,
                Timestamp = 1235,
                Type = MsgActionType.RequestEntity,
                PositionX = (ushort)christmasNpc.X,
                PositionY = (ushort)christmasNpc.Y,
            }.ToFrameBytes()));

        var frame = Assert.Single(sent);
        Assert.True(LegacyNpcSpawnPacket.TryParse(ReadFrame(frame), out var spawn));
        Assert.Equal(new LegacyNpcSpawnPacket(
            52_301,
            52_301,
            0,
            (ushort)christmasNpc.X,
            (ushort)christmasNpc.Y,
            (ushort)christmasNpc.Lookface,
            (ushort)christmasNpc.Kind,
            0), spawn);
        Assert.Contains("handled MsgAction RequestEntity Christmas NPC", log.ToString());
        Assert.DoesNotContain("PARITY-GAP MsgAction RequestEntity missing visible entity", log.ToString());
    }

    [Fact]
    public async Task ClickingVisibleChristmasNpcReachesSafeScriptFallback()
    {
        var worldData = await LoadSeedWorldDataAsync();
        if (worldData is null)
        {
            return;
        }

        var christmasNpc = worldData.ChristmasNpcsById[52_301].Single();
        var sent = new List<byte[]>();
        var session = CreateSession(christmasNpc.MapId, christmasNpc.X, christmasNpc.Y);
        using var log = new StringWriter();
        var context = CreateContext(worldData, session, sent, log);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(52_301, optionId: 0, interactType: 1).ToFrameBytes()));

        var frame = Assert.Single(sent);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out var reply));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        Assert.Contains("legacy script is not modeled", reply.Text);

        var output = log.ToString();
        Assert.Contains("npcLookup=static=0/visible=0 sob=0/visible=0 christmas=1/visible=1", output);
        Assert.Contains("WorldServer sent LegacyNpcServer fallback dialog", output);
        Assert.DoesNotContain("clicked NPC is not currently visible", output);
        Assert.DoesNotContain("PARITY-GAP skipped LegacyNpcServer", output);
    }

    private static async Task<WorldDataCatalog?> LoadSeedWorldDataAsync()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return null;
        }

        return await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
    }

    private static WorldPacketHandlerContext CreateContext(
        WorldDataCatalog worldData,
        WorldSession session,
        List<byte[]> sent,
        TextWriter log)
    {
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        return new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: new SobNpcRuntimeStateStore());
    }

    private static WorldSession CreateSession(long mapId, long x, long y)
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "christmas-npc-test",
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
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
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
