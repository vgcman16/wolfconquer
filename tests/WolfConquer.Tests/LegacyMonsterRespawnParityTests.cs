using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterRespawnParityTests
{
    [Fact]
    public void RespawnVisibilityRejectsViewerOnDifferentDynamicMap()
    {
        var monster = MonsterOnDefaultDynamicMap();
        var viewerInSourceRangeButDifferentDynamicMap = Character(dynamicId: 123);

        // Source parity:
        // MobCollection.Add defaults dinamicid to 0 and assigns MonsterRole.DynamicID.
        // MonsterView.CanSee rejects obj.DynamicID != role.DynamicID before distance.
        var canSee = InvokeWorldServerCanSeeMonster(viewerInSourceRangeButDifferentDynamicMap, monster);

        Assert.False(canSee);
    }

    private static bool InvokeWorldServerCanSeeMonster(CharacterSnapshot character, LegacyMonsterInstance monster)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod(
            "CanSeeMonster",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return Assert.IsType<bool>(method.Invoke(null, [character, monster]));
    }

    private static CharacterSnapshot Character(uint dynamicId)
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
            MapId: 1000,
            X: 100,
            Y: 100,
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
            SourceFile: "test.ini",
            Attributes: new Dictionary<string, string>
            {
                ["DynamicID"] = dynamicId.ToString()
            },
            PaidNobility: new Dictionary<string, string>());
    }

    private static LegacyMonsterInstance MonsterOnDefaultDynamicMap()
    {
        return new LegacyMonsterInstance(
            Uid: 470_000,
            MapId: 1000,
            X: 100,
            Y: 100,
            Mesh: 200,
            Level: 83,
            Hitpoints: 100,
            MaxLife: 100,
            MonsterId: 2700,
            Name: "BladeGhostL83",
            Boss: false,
            GeneratorId: 70,
            SpawnIndex: 0,
            RestSeconds: 2);
    }
}
