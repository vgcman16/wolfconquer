using System.Collections;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFastBladeScentSwordTargetingTests
{
    private const ushort FastBladeSpellId = 1_045;
    private const ushort ScentSwordSpellId = 1_046;
    private const int MapId = 1_015;
    private const uint OnLineInsideRangeUid = 1_000_001;
    private const uint WidthEdgeUid = 1_000_002;
    private const uint BeyondWidthUid = 1_000_003;
    private const uint AtRangeBoundaryUid = 1_000_004;

    [Theory]
    [InlineData(FastBladeSpellId)]
    [InlineData(ScentSwordSpellId)]
    public void SelectLegacyManualLineMonsterTargetsUsesStrictRangeAndWidthTwo(ushort spellId)
    {
        var monsterFamily = CreateMonsterFamily();
        var monsterStates = new MonsterRuntimeStateStore();
        RegisterMonster(monsterStates, monsterFamily, OnLineInsideRangeUid, x: 109, y: 100);
        RegisterMonster(monsterStates, monsterFamily, WidthEdgeUid, x: 105, y: 102);
        RegisterMonster(monsterStates, monsterFamily, BeyondWidthUid, x: 105, y: 103);
        RegisterMonster(monsterStates, monsterFamily, AtRangeBoundaryUid, x: 110, y: 100);

        var session = CreateSession();
        var context = new WorldPacketHandlerContext(
            new ConquerTcpConnection(null, null),
            TextWriter.Null,
            session,
            MonsterStates: monsterStates);
        var spell = CreateManualLineSpell(spellId);

        var selectedUids = InvokeSelectLegacyLineMonsterTargets(
            context,
            session,
            spell,
            originX: 110,
            originY: 100);

        Assert.Equal([OnLineInsideRangeUid, WidthEdgeUid], selectedUids.Order().ToArray());
    }

    [Theory]
    [InlineData(FastBladeSpellId)]
    [InlineData(ScentSwordSpellId)]
    public void FastBladeAndScentSwordLineWidthMatchesLegacyWidthTwo(ushort spellId)
    {
        Assert.Equal(2, LegacyMagicRuntime.GetLegacyLineWidth(CreateManualLineSpell(spellId)));
    }

    private static IReadOnlyList<uint> InvokeSelectLegacyLineMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "SelectLegacyLineMonsterTargets",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = Assert.IsAssignableFrom<IEnumerable>(
            method.Invoke(null, [context, session, spell, originX, originY]));
        return result.Cast<object>().Select(ReadMonsterUid).ToArray();
    }

    private static uint ReadMonsterUid(object target)
    {
        var monsterProperty = target.GetType().GetProperty(
            "Monster",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(monsterProperty);

        var monster = Assert.IsType<LegacyMonsterInstance>(monsterProperty.GetValue(target));
        return monster.Uid;
    }

    private static WorldSession CreateSession()
    {
        return new WorldSession(
            AccountId: 1,
            Username: "caster",
            CreateCharacter(),
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }

    private static CharacterSnapshot CreateCharacter()
    {
        return new CharacterSnapshot(
            Uid: 1,
            Name: "Caster",
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: 142,
            FirstClass: 142,
            SecondClass: 0,
            Reborn: 0,
            Level: 120,
            Experience: 0,
            MapId,
            X: 100,
            Y: 100,
            PreviousMapId: MapId,
            PreviousMapX: 100,
            PreviousMapY: 100,
            Strength: 300,
            Agility: 0,
            Vitality: 0,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: 5_000,
            MinMana: 500,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://fastblade-targeting",
            Attributes: new Dictionary<string, string>(),
            PaidNobility: new Dictionary<string, string>());
    }

    private static void RegisterMonster(
        MonsterRuntimeStateStore monsterStates,
        Monster monsterFamily,
        uint uid,
        ushort x,
        ushort y)
    {
        monsterStates.RegisterDynamicMonster(
            new LegacyMonsterInstance(
                Uid: uid,
                MapId,
                x,
                y,
                Mesh: 100,
                Level: 100,
                Hitpoints: 5_000,
                MaxLife: 5_000,
                MonsterId: (uint)monsterFamily.Id,
                Name: monsterFamily.Name,
                Boss: false,
                GeneratorId: checked((int)uid),
                SpawnIndex: 0),
            monsterFamily);
    }

    private static Monster CreateMonsterFamily()
    {
        return new Monster(
            9_001,
            "TrainingMonster",
            new Dictionary<string, string>
            {
                ["life"] = "5000",
                ["lookface"] = "100",
                ["level"] = "100",
                ["defense"] = "25",
                ["defense2"] = "10000",
                ["attack_range"] = "0",
            },
            []);
    }

    private static LegacyMagicRuntimeSpell CreateManualLineSpell(ushort spellId)
    {
        return new LegacyMagicRuntimeSpell(
            Id: spellId,
            Sort: LegacyMagicSort.ManualLine,
            Name: spellId == FastBladeSpellId ? "FastBlade" : "ScentSword",
            Level: 0,
            UseMana: 0,
            Damage: 0,
            Experience: 0,
            MaxTargets: 0,
            Range: 10,
            NeedLevel: 0,
            WeaponType: 0,
            UseStamina: 0,
            CoolDown: 0,
            ColdTime: 0);
    }
}
