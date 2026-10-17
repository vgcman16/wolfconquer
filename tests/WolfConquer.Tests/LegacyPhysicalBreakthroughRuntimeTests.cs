using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const string PhysicalBreakthroughRefineryRollAttribute =
        "LegacyPhysicalBreakthroughRefineryRoll";
    private const int PhysicalBreakthroughDeathSighStatusFlag = 0xFD;
    private const uint PhysicalBreakthroughBarrierRuneBaseItemId = 4_039_100;
    private const uint PhysicalBreakthroughTacitStrikeRuneBaseItemId = 4_039_200;

    [Fact]
    public async Task ShurikenVortexSuccessfulLowBattlePowerBreakthroughPenetratesBeforeCallerSixth()
    {
        var result = await RunShurikenVortexDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: false,
            breakthroughFixture: SuccessfulPhysicalBreakthroughFixture(
                targetHasDefensiveStance: false));
        var target = AssertPhysicalBreakthroughTarget(
            result.TargetSent,
            ShurikenVortexSpellId);
        var expectedBeforeCallerSixth = ApplyExpectedPhysicalBreakthroughPenetration(
            result.RawPhysicalDamage);

        Assert.Equal(20U, result.AttackerBreakthroughIndex);
        Assert.Equal(5U, result.TargetCounteractionIndex);
        Assert.Equal(expectedBeforeCallerSixth / 6, target.Damage);
        Assert.NotEqual(
            ApplyExpectedShurikenVortexNoBreakthroughBonus(result.RawPhysicalDamage) / 6,
            target.Damage);
        Assert.Equal(MsgAttackEffect.Penetration, target.Effect);
        Assert.Equal(100_000L - target.Damage, result.TargetRemainingHitpoints);
    }

    [Fact]
    public async Task ManiacDanceSuccessfulBreakthroughDefensiveStanceOverwritesPenetrationWithBlock()
    {
        var result = await RunManiacDanceDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: false,
            breakthroughFixture: SuccessfulPhysicalBreakthroughFixture(
                targetHasDefensiveStance: true));
        var target = AssertPhysicalBreakthroughTarget(
            result.TargetSent,
            ManiacDanceRuntimeSpellId);
        var expectedAfterStance = ApplyExpectedPhysicalBreakthroughPenetration(
            result.RawPhysicalDamage) / 2;

        Assert.Equal(expectedAfterStance, target.Damage);
        Assert.Equal(MsgAttackEffect.Block, target.Effect);
        Assert.False(target.Effect.HasFlag(MsgAttackEffect.Penetration));
        Assert.Equal(100_000L - target.Damage, result.TargetRemainingHitpoints);
    }

    [Theory]
    [InlineData(200U, 200U)]
    [InlineData(201U, 200U)]
    public async Task ManiacDanceEqualOrHigherAttackerBattlePowerUsesNonpenetratingFiftyPercentBranch(
        uint attackerBattlePower,
        uint targetBattlePower)
    {
        var result = await RunManiacDanceDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: false,
            breakthroughFixture: SuccessfulPhysicalBreakthroughFixture(
                targetHasDefensiveStance: false) with
            {
                AttackerBattlePower = attackerBattlePower,
                TargetBattlePower = targetBattlePower,
            });
        var target = AssertPhysicalBreakthroughTarget(
            result.TargetSent,
            ManiacDanceRuntimeSpellId);

        Assert.Equal(
            ApplyExpectedManiacDanceNoBreakthroughBonus(result.RawPhysicalDamage),
            target.Damage);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.False(target.Effect.HasFlag(MsgAttackEffect.Penetration));
    }

    [Theory]
    [InlineData(SacrificeStatusFlag)]
    [InlineData(PhysicalBreakthroughDeathSighStatusFlag)]
    public async Task ShurikenVortexBreakthroughExclusionUsesFiftyPercentBeforeCallerSixth(
        int exclusionStatusFlag)
    {
        var result = await RunShurikenVortexDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: false,
            breakthroughFixture: SuccessfulPhysicalBreakthroughFixture(
                targetHasDefensiveStance: false) with
            {
                AttackerExclusionStatusFlag = exclusionStatusFlag,
            });
        var target = AssertPhysicalBreakthroughTarget(
            result.TargetSent,
            ShurikenVortexSpellId);

        Assert.Equal(
            ApplyExpectedShurikenVortexNoBreakthroughBonus(result.RawPhysicalDamage) / 6,
            target.Damage);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.False(target.Effect.HasFlag(MsgAttackEffect.Penetration));
    }

    [Fact]
    public async Task ManiacDanceSuccessfulBreakthroughAppliesBarrierThenTacitStrikeWithSequentialFloors()
    {
        var result = await RunManiacDanceDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: false,
            breakthroughFixture: SuccessfulPhysicalBreakthroughFixture(
                targetHasDefensiveStance: false) with
            {
                BarrierLevel = 5,
                TacitStrikeLevel = 8,
                RuneEligibilityReborn = 2,
            });
        var target = AssertPhysicalBreakthroughTarget(
            result.TargetSent,
            ManiacDanceRuntimeSpellId);
        var afterPenetration = AddExpectedPercent(result.RawPhysicalDamage, 30);
        var afterBarrier = SubtractExpectedPercent(afterPenetration, 15);
        var afterTacitStrike = AddExpectedPercent(afterBarrier, 25);
        var expectedDamage = ApplyExpectedPhysicalBreakthroughCompensation(afterTacitStrike);

        Assert.Equal(expectedDamage, target.Damage);
        Assert.Equal(MsgAttackEffect.Penetration, target.Effect);
        Assert.NotEqual(
            ApplyExpectedPhysicalBreakthroughCompensation(
                AddExpectedPercent(afterPenetration, 10)),
            target.Damage);
    }

    [Theory]
    [InlineData(0, 8, 2)]
    [InlineData(5, 8, 1)]
    public async Task ManiacDanceTacitWithoutBarrierOrIneligibleRunesDoNotChangeBreakthroughDamage(
        byte barrierLevel,
        byte tacitStrikeLevel,
        long runeEligibilityReborn)
    {
        var result = await RunManiacDanceDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: false,
            breakthroughFixture: SuccessfulPhysicalBreakthroughFixture(
                targetHasDefensiveStance: false) with
            {
                BarrierLevel = barrierLevel,
                TacitStrikeLevel = tacitStrikeLevel,
                RuneEligibilityReborn = runeEligibilityReborn,
            });
        var target = AssertPhysicalBreakthroughTarget(
            result.TargetSent,
            ManiacDanceRuntimeSpellId);

        Assert.Equal(
            ApplyExpectedPhysicalBreakthroughPenetration(result.RawPhysicalDamage),
            target.Damage);
        Assert.Equal(MsgAttackEffect.Penetration, target.Effect);
    }

    [Fact]
    public void PhysicalBreakthroughFallbackBattlePowerIncludesEveryMatchingSourceComponent()
    {
        var worldData = CreatePhysicalBreakthroughBattlePowerCatalog(professionBattlePower: 5);
        var equipment = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem
                {
                    ItemId = 410_009,
                    Position = 4,
                    Plus = 2,
                    SocketOne = 13,
                    SocketTwo = 1,
                },
            ],
            RuneItems: []);
        var attributes = new Dictionary<string, string>
        {
            ["ExtraBattlePower"] = "20",
        };
        var session = CreateSession(
            classCode: 142,
            reborn: 2,
            attributes: attributes,
            playerItems: equipment);
        session = session with
        {
            Character = session.Character with
            {
                PaidNobility = new Dictionary<string, string>
                {
                    ["PaidRank"] = LegacyNobilityRanking.RankDuke.ToString(),
                },
            },
        };
        var equipmentBattlePower = LegacyBattlePowerCalculator.CalculateEquipmentBattlePower(equipment);

        var actual = InvokePhysicalBreakthroughBattlePower(session, worldData);

        Assert.True(equipmentBattlePower > 0);
        Assert.Equal(
            5UL + 120UL + 10UL + equipmentBattlePower + LegacyNobilityRanking.RankDuke + 20UL,
            actual);
    }

    [Theory]
    [InlineData(0, 516UL)]
    [InlineData(LegacyNobilityRanking.RankDuke, 517UL)]
    [InlineData(LegacyNobilityRanking.RankPrince, 519UL)]
    [InlineData(LegacyNobilityRanking.RankKing, 522UL)]
    public void PhysicalBreakthroughFallbackBattlePowerUsesMatchingNobilityCap(
        byte nobilityRank,
        ulong expected)
    {
        var worldData = CreatePhysicalBreakthroughBattlePowerCatalog(professionBattlePower: 5);
        var session = CreateSession(
            classCode: 142,
            reborn: 2,
            attributes: new Dictionary<string, string>
            {
                ["ExtraBattlePower"] = "1000",
            });
        session = session with
        {
            Character = session.Character with
            {
                PaidNobility = new Dictionary<string, string>
                {
                    ["PaidRank"] = nobilityRank.ToString(),
                },
            },
        };

        Assert.Equal(expected, InvokePhysicalBreakthroughBattlePower(session, worldData));
    }

    private static PhysicalBreakthroughRuntimeFixture SuccessfulPhysicalBreakthroughFixture(
        bool targetHasDefensiveStance)
    {
        return new PhysicalBreakthroughRuntimeFixture(
            AttackerBreakthroughIndex: 20,
            TargetCounteractionIndex: 5,
            RefineryRoll: 114,
            AttackerBattlePower: 100,
            TargetBattlePower: 200,
            TargetHasDefensiveStance: targetHasDefensiveStance);
    }

    private static WorldDataCatalog CreatePhysicalBreakthroughBattlePowerCatalog(
        int professionBattlePower)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 1,
            boundX: 832,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        return CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            sobNpcs: [],
            magicRows: [CreateShurikenVortexRow()],
            professionLevelBenefits:
            [
                new ProfessionLevelBenefit(
                    Id: 1,
                    ProfessionCode: 142,
                    Rank: professionBattlePower,
                    BonusValues: [0, 0, 0, 0, 0],
                    SourceFile: "fixture://physical-breakthrough-profession",
                    LineNumber: 1,
                    RawLine: string.Empty,
                    Fields: []),
            ]);
    }

    private static ulong InvokePhysicalBreakthroughBattlePower(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "CalculateLegacyPlayerPlayerPhysicalBattlePower",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<ulong>(method.Invoke(null, [session, worldData]));
    }

    private static uint ApplyExpectedPhysicalBreakthroughPenetration(uint damage)
    {
        return ApplyExpectedPhysicalBreakthroughCompensation(
            AddExpectedPercent(damage, 30));
    }

    private static uint ApplyExpectedPhysicalBreakthroughCompensation(uint damage)
    {
        return (uint)(damage / 0.67);
    }

    private static uint AddExpectedPercent(uint damage, uint percent)
    {
        return damage + (damage * percent / 100);
    }

    private static uint SubtractExpectedPercent(uint damage, uint percent)
    {
        return damage - (damage * percent / 100);
    }

    private static LegacySpellAnimationTarget AssertPhysicalBreakthroughTarget(
        IReadOnlyList<byte[]> frames,
        ushort spellId)
    {
        var incoming = Assert.Single(
            ParseSpellAnimations(frames),
            animation => animation.SpellId == spellId);
        return Assert.Single(incoming.Targets, target => target.Uid == TargetPlayerUid);
    }

    private static IReadOnlyList<ItemType> CreatePhysicalBreakthroughRuneCatalogItems(
        PhysicalBreakthroughRuntimeFixture? fixture)
    {
        var items = new List<ItemType>();
        if (fixture is { BarrierLevel: > 0 })
        {
            var itemId = PhysicalBreakthroughBarrierRuneBaseItemId + fixture.BarrierLevel;
            items.Add(CreateItemType(checked((int)itemId), $"Barrier(+{fixture.BarrierLevel})"));
        }
        if (fixture is { TacitStrikeLevel: > 0 })
        {
            var itemId = PhysicalBreakthroughTacitStrikeRuneBaseItemId + fixture.TacitStrikeLevel;
            items.Add(CreateItemType(checked((int)itemId), $"TacitStrike(+{fixture.TacitStrikeLevel})"));
        }

        return items;
    }

    private static PlayerItemSnapshot CreatePhysicalBreakthroughAttackerRuneItems(
        PhysicalBreakthroughRuntimeFixture? fixture)
    {
        return fixture is { TacitStrikeLevel: > 0 }
            ? CreatePlayerItems(new PlayerItemSnapshotItem
            {
                ItemId = PhysicalBreakthroughTacitStrikeRuneBaseItemId + fixture.TacitStrikeLevel,
                Position = 101,
            })
            : CreatePlayerItems();
    }

    private static PlayerItemSnapshot CreatePhysicalBreakthroughTargetRuneItems(
        PhysicalBreakthroughRuntimeFixture? fixture)
    {
        return fixture is { BarrierLevel: > 0 }
            ? CreatePlayerItems(new PlayerItemSnapshotItem
            {
                ItemId = PhysicalBreakthroughBarrierRuneBaseItemId + fixture.BarrierLevel,
                Position = 101,
            })
            : CreatePlayerItems();
    }

    private static string CreatePhysicalBreakthroughChiPower(
        LegacyChiAttributeType attributeType,
        ushort value)
    {
        return $"1/0/{(byte)attributeType}/{value}/0/0/0/0/0/0/0/0/0/0/";
    }

    private sealed record PhysicalBreakthroughRuntimeFixture(
        ushort AttackerBreakthroughIndex,
        ushort TargetCounteractionIndex = 0,
        uint RefineryRoll = 99,
        uint AttackerBattlePower = 120,
        uint TargetBattlePower = 120,
        bool TargetHasDefensiveStance = true,
        int AttackerExclusionStatusFlag = 0,
        byte BarrierLevel = 0,
        byte TacitStrikeLevel = 0,
        long RuneEligibilityReborn = 2);
}
