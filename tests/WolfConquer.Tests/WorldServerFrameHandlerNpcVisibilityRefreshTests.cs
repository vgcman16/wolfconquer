using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task HandlerSetLocationRefreshSendsVisibleStaticNpcAndSobNpcWithoutRelog()
    {
        var staticNpc = CreateFrameHandlerVisibilityNpc(
            id: 90_001,
            kind: 2,
            lookface: 47_496,
            mapId: 10_123,
            x: 55,
            y: 66);
        var sobNpc = CreateSobNpc(
            id: 90_002,
            name: "RefreshGate",
            kind: 2,
            lookface: 59_850,
            mapId: staticNpc.MapId,
            x: staticNpc.X + 1,
            y: staticNpc.Y + 1);
        var monster = CreateFrameHandlerVisibilityMonster(id: 90_003);
        var monsterSpawn = CreateFrameHandlerVisibilityMonsterSpawn(
            generatorId: 90_004,
            monster,
            staticNpc.MapId,
            staticNpc.X + 2,
            staticNpc.Y + 2);
        var worldData = CreateStaticVisibilityCatalog(
            [staticNpc],
            [sobNpc],
            monsters: [monster],
            monsterSpawns: [monsterSpawn]);

        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1_293_600, uid: 1_293_600, mapId: staticNpc.MapId, x: staticNpc.X, y: staticNpc.Y);
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff, worldData, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        var refreshStart = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_293_600,
                Timestamp = 12_345,
                Type = MsgActionType.SetLocation,
            }.ToFrameBytes()));

        var actionIndex = FindFrameIndexAfter(
            sent,
            refreshStart,
            frame => MsgActionPacket.TryParse(frame, out var action) &&
                action.Type == MsgActionType.SetLocation);
        var mapStatusIndex = FindFrameIndexAfter(
            sent,
            actionIndex + 1,
            frame => LegacyMapStatusPacket.TryParse(frame, out var status) &&
                status.Id == (uint)staticNpc.MapId);
        var npcIndex = FindStaticNpcSpawnIndex(sent, actionIndex + 1, staticNpc.Id);
        var monsterIndex = FindMonsterSpawnIndex(sent, actionIndex + 1, monster.Name, monsterSpawn.BoundX, monsterSpawn.BoundY);
        var sobIndex = FindSobNpcSpawnIndex(sent, actionIndex + 1, sobNpc.Id);

        Assert.InRange(actionIndex, refreshStart, sent.Count - 1);
        Assert.InRange(npcIndex, actionIndex + 1, mapStatusIndex - 1);
        Assert.InRange(monsterIndex, actionIndex + 1, mapStatusIndex - 1);
        Assert.InRange(sobIndex, actionIndex + 1, mapStatusIndex - 1);
        Assert.True(npcIndex < monsterIndex);
        Assert.True(monsterIndex < sobIndex);

        Assert.True(LegacyNpcSpawnPacket.TryParse(ReadFrame(sent[npcIndex]), out var npcSpawn));
        Assert.Equal(new LegacyNpcSpawnPacket(90_001, 90_001, 0, 55, 66, 47_496, 2, 0), npcSpawn);
        Assert.True(LegacyMonsterSpawnPacket.TryParse(ReadFrame(sent[monsterIndex]), out var monsterSpawnPacket));
        Assert.Equal(monster.Name, monsterSpawnPacket.Names[0]);
        Assert.Equal(monsterSpawn.BoundX, monsterSpawnPacket.X);
        Assert.Equal(monsterSpawn.BoundY, monsterSpawnPacket.Y);
        Assert.True(LegacySobNpcSpawnPacket.TryParse(ReadFrame(sent[sobIndex]), out var sobSpawn));
        Assert.Equal((uint)sobNpc.Id, sobSpawn.Uid);
        Assert.Equal(sobNpc.X, sobSpawn.X);
        Assert.Equal(sobNpc.Y, sobSpawn.Y);

        var output = log.ToString();
        Assert.Contains("WorldServer sent LegacyNpcSpawn", output);
        Assert.Contains("npcCount=1", output);
        Assert.Contains("WorldServer sent LegacyMonsterSpawn", output);
        Assert.Contains("monsterCount=1", output);
        Assert.Contains("WorldServer sent LegacySobNpcSpawn", output);
        Assert.Contains("sobNpcCount=1", output);
    }

    [Fact]
    public async Task HandlerChangeMapRefreshSendsVisibleStaticNpcAndSobNpcWithoutRelog()
    {
        var staticNpc = CreateFrameHandlerVisibilityNpc(
            id: 90_101,
            kind: 2,
            lookface: 47_496,
            mapId: 1_002,
            x: 410,
            y: 354);
        var sobNpc = CreateSobNpc(
            id: 90_102,
            name: "FallbackGate",
            kind: 2,
            lookface: 59_850,
            mapId: staticNpc.MapId,
            x: staticNpc.X + 1,
            y: staticNpc.Y);
        var monster = CreateFrameHandlerVisibilityMonster(id: 90_103);
        var monsterSpawn = CreateFrameHandlerVisibilityMonsterSpawn(
            generatorId: 90_104,
            monster,
            staticNpc.MapId,
            staticNpc.X + 2,
            staticNpc.Y);
        var worldData = CreateStaticVisibilityCatalog(
            [staticNpc],
            [sobNpc],
            monsters: [monster],
            monsterSpawns: [monsterSpawn]);

        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1_293_600, uid: 1_293_600, mapId: 9_998, x: 12, y: 34);
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff, worldData, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        var changeMapStart = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_293_600,
                Timestamp = 54_321,
                Type = MsgActionType.ChangeMap,
            }.ToFrameBytes()));

        var teleportIndex = FindFrameIndexAfter(
            sent,
            changeMapStart,
            frame => MsgActionPacket.TryParse(frame, out var action) &&
                action.Type == MsgActionType.Teleport &&
                action.DwParam == 1_002U &&
                action.PositionX == 410U &&
                action.PositionY == 354U);
        var mapStatusIndex = FindFrameIndexAfter(
            sent,
            teleportIndex + 1,
            frame => LegacyMapStatusPacket.TryParse(frame, out var status) &&
                status.Id == 1_002U);
        var npcIndex = FindStaticNpcSpawnIndex(sent, mapStatusIndex + 1, staticNpc.Id);
        var monsterIndex = FindMonsterSpawnIndex(sent, mapStatusIndex + 1, monster.Name, monsterSpawn.BoundX, monsterSpawn.BoundY);
        var sobIndex = FindSobNpcSpawnIndex(sent, mapStatusIndex + 1, sobNpc.Id);

        Assert.InRange(teleportIndex, changeMapStart, sent.Count - 1);
        Assert.InRange(mapStatusIndex, teleportIndex + 1, sent.Count - 1);
        Assert.InRange(npcIndex, mapStatusIndex + 1, sent.Count - 1);
        Assert.InRange(monsterIndex, mapStatusIndex + 1, sent.Count - 1);
        Assert.InRange(sobIndex, mapStatusIndex + 1, sent.Count - 1);
        Assert.True(npcIndex < monsterIndex);
        Assert.True(monsterIndex < sobIndex);

        var updated = GetSession(handler, connection);
        Assert.Equal(1_002, updated.Character.MapId);
        Assert.Equal(staticNpc.X, updated.Character.X);
        Assert.Equal(staticNpc.Y, updated.Character.Y);
        Assert.Equal(9_998, updated.Character.PreviousMapId);

        Assert.True(LegacyNpcSpawnPacket.TryParse(ReadFrame(sent[npcIndex]), out var npcSpawn));
        Assert.Equal((uint)staticNpc.Id, npcSpawn.Uid);
        Assert.True(LegacyMonsterSpawnPacket.TryParse(ReadFrame(sent[monsterIndex]), out var monsterSpawnPacket));
        Assert.Equal(monster.Name, monsterSpawnPacket.Names[0]);
        Assert.Equal(monsterSpawn.BoundX, monsterSpawnPacket.X);
        Assert.Equal(monsterSpawn.BoundY, monsterSpawnPacket.Y);
        Assert.True(LegacySobNpcSpawnPacket.TryParse(ReadFrame(sent[sobIndex]), out var sobSpawn));
        Assert.Equal((uint)sobNpc.Id, sobSpawn.Uid);

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgAction ChangeMap fallback", output);
        Assert.Contains("WorldServer sent LegacyNpcSpawn", output);
        Assert.Contains("WorldServer sent LegacyMonsterSpawn", output);
        Assert.Contains("WorldServer sent LegacySobNpcSpawn", output);
    }

    private static Npc CreateFrameHandlerVisibilityNpc(
        int id,
        int kind,
        int lookface,
        int mapId,
        int x,
        int y)
    {
        var fields = new[]
        {
            id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            kind.ToString(System.Globalization.CultureInfo.InvariantCulture),
            lookface.ToString(System.Globalization.CultureInfo.InvariantCulture),
            mapId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            x.ToString(System.Globalization.CultureInfo.InvariantCulture),
            y.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        return new Npc(
            id,
            kind,
            lookface,
            mapId,
            x,
            y,
            OptionalFlag: null,
            SourceLine: 1,
            RawLine: string.Join(',', fields),
            Fields: fields);
    }

    private static int FindStaticNpcSpawnIndex(List<byte[]> sent, int startIndex, int npcId)
    {
        return FindFrameIndexAfter(
            sent,
            startIndex,
            frame => LegacyNpcSpawnPacket.TryParse(frame, out var spawn) &&
                spawn.Uid == (uint)npcId);
    }

    private static int FindSobNpcSpawnIndex(List<byte[]> sent, int startIndex, int sobNpcId)
    {
        return FindFrameIndexAfter(
            sent,
            startIndex,
            frame => LegacySobNpcSpawnPacket.TryParse(frame, out var spawn) &&
                spawn.Uid == (uint)sobNpcId);
    }

    private static int FindMonsterSpawnIndex(List<byte[]> sent, int startIndex, string name, int x, int y)
    {
        return FindFrameIndexAfter(
            sent,
            startIndex,
            frame => LegacyMonsterSpawnPacket.TryParse(frame, out var spawn) &&
                spawn.Names.Count > 0 &&
                spawn.Names[0] == name &&
                spawn.X == x &&
                spawn.Y == y);
    }

    private static Monster CreateFrameHandlerVisibilityMonster(int id)
    {
        return new Monster(
            id,
            "VisibilityMonster",
            new Dictionary<string, string>
            {
                ["life"] = "50000",
                ["lookface"] = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["level"] = "50",
                ["attack_range"] = "1",
                ["view_range"] = "15",
            },
            []);
    }

    private static MonsterSpawn CreateFrameHandlerVisibilityMonsterSpawn(
        int generatorId,
        Monster monster,
        int mapId,
        int x,
        int y)
    {
        return new MonsterSpawn(
            GeneratorId: generatorId,
            MapId: mapId,
            MonsterTypeId: monster.Id,
            BoundX: x,
            BoundY: y,
            BoundWidth: 1,
            BoundHeight: 1,
            MaxNpc: 1,
            RestSeconds: 10,
            MaxPerGenerator: 1,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: mapId,
            SourceMonsterName: monster.Name,
            SourceRelativePath: System.IO.Path.Combine(
                mapId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                monster.Name,
                "1.ini"),
            Attributes: new Dictionary<string, string>
            {
                ["id"] = generatorId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["mapid"] = mapId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["npctype"] = monster.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
    }
}
