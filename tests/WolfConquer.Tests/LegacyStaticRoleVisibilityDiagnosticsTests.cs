using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyStaticRoleVisibilityDiagnosticsTests
{
    [Fact]
    public async Task SendVisibleStaticNpcObjectsLogsPositionRangeSourceAndReasonWhenRowsExistButNoneVisible()
    {
        var character = CreateCharacter(mapId: 1015, x: 900, y: 900);
        var npc = NpcParser.ParseLine("20499,2,47496,1015,830,584", sourceLine: 42);
        var sobNpc = CreateSobNpc(id: 25005, name: "Gate", kind: 21, lookface: 1137, mapId: 1015, x: 831, y: 585);
        var worldData = CreateStaticVisibilityCatalog([npc], [sobNpc]);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        using var log = new StringWriter();
        var session = new WorldSession(
            AccountId: 1_293_600,
            Username: "user02",
            Character: character,
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            CombatGear: CombatGearSnapshot.Empty);
        var context = new WorldPacketHandlerContext(connection, log, Session: session, WorldData: worldData);

        await InvokeSendVisibleStaticNpcObjectsAsync(context, character);

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer found no visible LegacyNpcSpawn", output);
        Assert.DoesNotContain("PARITY-GAP no visible LegacyNpcSpawn", output);
        Assert.Contains("map=1015", output);
        Assert.Contains("x=900", output);
        Assert.Contains("y=900", output);
        Assert.Contains("range=19", output);
        Assert.Contains("source=MrRayzo.Role.RoleView.Role(MapObjectType.Npc)", output);
        Assert.Contains("reason=no-visible-row-within-range", output);
        Assert.Contains("loadedNpcCount=1", output);
        Assert.Contains("nearestNpcId=20499", output);
        Assert.Contains("nearestNpcDistance=316", output);
        Assert.Contains("sourceLine=42", output);
        Assert.Contains("rawRow=\"20499,2,47496,1015,830,584\"", output);

        Assert.Contains("WorldServer found no visible LegacySobNpcSpawn", output);
        Assert.DoesNotContain("PARITY-GAP no visible LegacySobNpcSpawn", output);
        Assert.Contains("range=18", output);
        Assert.Contains("source=MrRayzo.Role.RoleView.Role(MapObjectType.SobNpc)", output);
        Assert.Contains("loadedSobNpcCount=1", output);
        Assert.Contains("nearestSobNpcId=25005", output);
        Assert.Contains("nearestSobNpcDistance=315", output);
        Assert.Contains("sourceLine=1", output);
        Assert.Contains("rawRow=\"25005,Gate,21,1137,1015,831,585,0,0,1,0\"", output);
    }

    private static async ValueTask InvokeSendVisibleStaticNpcObjectsAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "SendVisibleStaticNpcObjectsAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [context, character, CancellationToken.None, null]);
        var valueTask = Assert.IsType<ValueTask>(result);
        await valueTask;
    }

    private static WorldDataCatalog CreateStaticVisibilityCatalog(
        IReadOnlyList<Npc> npcs,
        IReadOnlyList<SobNpc> sobNpcs)
    {
        var method = typeof(WorldServerFrameHandlerTests).GetMethod(
            "CreateStaticVisibilityCatalog",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, [npcs, sobNpcs, null, null, null, null, null, null, null]);
        return Assert.IsType<WorldDataCatalog>(result);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static CharacterSnapshot CreateCharacter(long mapId, long x, long y)
    {
        return new CharacterSnapshot(
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
            PreviousMapId: 1000,
            PreviousMapX: 500,
            PreviousMapY: 650,
            Strength: 0,
            Agility: 36,
            Vitality: 447,
            Spirit: 0,
            AttributePoints: 12,
            MinHitPoints: 61_258,
            MinMana: 5000,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 7,
            SourceFile: "fixture://character",
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static SobNpc CreateSobNpc(int id, string name, int kind, int lookface, int mapId, int x, int y)
    {
        var fields = new[]
        {
            id.ToString(),
            name,
            kind.ToString(),
            lookface.ToString(),
            mapId.ToString(),
            x.ToString(),
            y.ToString(),
            "0",
            "0",
            "1",
            "0",
        };
        return new SobNpc(
            Id: id,
            Name: name,
            Kind: kind,
            Lookface: lookface,
            MapId: mapId,
            X: x,
            Y: y,
            Life: 0,
            MaxLife: 0,
            Unknown9: 1,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 1,
            RawLine: string.Join(',', fields),
            Fields: fields);
    }
}
