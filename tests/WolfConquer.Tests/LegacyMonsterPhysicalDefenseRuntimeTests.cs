using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterPhysicalDefenseRuntimeTests
{
    private const int LegacyMagicShieldFlag = 0x08;
    private const int LegacyDragonSwingFlag = 0xA0;

    [Fact]
    public void MonsterPhysicalDamageUsesLegacyAjustDefenseRuntimeStateAdjustments()
    {
        var family = new Monster(
            9_001,
            "DefenseProbe",
            new Dictionary<string, string>
            {
                ["life"] = "10000",
                ["lookface"] = "9001",
                ["level"] = "120",
                ["attack_min"] = "6000",
                ["attack_max"] = "6004",
            },
            []);
        var monster = new LegacyMonsterInstance(
            Uid: 400_001,
            MapId: 1_015,
            X: 100,
            Y: 100,
            Mesh: 9_001,
            Level: 120,
            Hitpoints: 10_000,
            MaxLife: 10_000,
            MonsterId: 9_001,
            Name: "DefenseProbe",
            Boss: false,
            GeneratorId: 1,
            SpawnIndex: 0);
        var session = new WorldSession(
            AccountId: 1,
            Username: "defender",
            CreateCharacter(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AjustDefense"] = "999",
                ["StatusFlags"] = BuildStatusFlags(LegacyMagicShieldFlag, LegacyDragonSwingFlag),
                ["DivineGuard"] = "1",
                ["HookMoon"] = "1",
                ["HookMoonAttackedPower"] = "150",
                ["LegacyMonsterPhysicalAttackRoll"] = "3",
            }),
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);

        var result = InvokeMonsterPhysicalDamage(monster, family, session);

        Assert.Equal(5_017U, ReadUIntResult(result, "PlayerAdjustedPhysicalDefense"));
        // Source: ActionHandler.PhysicalAttack adds Program.ServerConfig.PhysicalDamage (100%) before AjustDefense.
        Assert.Equal(6_989U, ReadUIntResult(result, "Damage"));
        Assert.Equal(6_003U, ReadUIntResult(result, "SelectedAttack"));
        Assert.Equal(999U, ReadUIntResult(result, "PlayerPhysicalDefense"));
    }

    private static object InvokeMonsterPhysicalDamage(
        LegacyMonsterInstance monster,
        Monster family,
        WorldSession session)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "CalculateLegacyMonsterPhysicalDamage",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method.Invoke(null, [monster, family, session, null, false, false]);
        Assert.NotNull(result);
        return result;
    }

    private static uint ReadUIntResult(object result, string propertyName)
    {
        var property = result.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        var value = property.GetValue(result);
        Assert.NotNull(value);
        return Assert.IsType<uint>(value);
    }

    private static CharacterSnapshot CreateCharacter(IReadOnlyDictionary<string, string> attributes)
    {
        return new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "Defender",
            Body: 1_003,
            Face: 0,
            Hair: 0,
            Class: 1_005,
            FirstClass: 0,
            SecondClass: 0,
            Reborn: 0,
            Level: 120,
            Experience: 0,
            MapId: 1_015,
            X: 100,
            Y: 100,
            PreviousMapId: 1_015,
            PreviousMapX: 100,
            PreviousMapY: 100,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: 5_000,
            MinMana: 0,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://monster-defense-runtime",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength];
        foreach (var flag in flags)
        {
            var wordIndex = flag / 32;
            var bitIndex = flag % 32;
            words[wordIndex] |= 1UL << bitIndex;
        }

        return string.Join('/', words) + "/";
    }
}
