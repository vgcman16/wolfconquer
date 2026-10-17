using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBossSpecialDeathDropPolicyTests
{
    [Fact]
    public void TryCreateFixedDropPlanMapsThrillingSpookNormalBranch()
    {
        var found = LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
            familyId: LegacyBossSpecialDeathDropPolicy.ThrillingSpookFamilyId,
            mapId: 1_015,
            boss: true,
            rolls: new LegacyBossSpecialDeathDropRolls(Family4212RareDrop: false),
            out var plan);

        Assert.True(found);
        Assert.Equal(4_212U, plan.FamilyId);
        Assert.Equal(1_015U, plan.MapId);
        Assert.True(plan.Boss);
        Assert.Equal("MonsterRole.cs Boss > 0 && Family.ID == 4212", plan.SourceBranch);
        Assert.Equal(36, plan.TotalFixedItemCount);
        Assert.Contains(plan.PendingSourceBehaviors, reason => reason.Contains("PurificationItems[7]"));
        AssertDrop(plan, 3_335_128, count: 5);
        AssertDrop(plan, 3_005_127, count: 1);
        AssertDrop(plan, 3_311_633, count: 1);
        AssertDrop(plan, 3_335_125, count: 1);
        AssertDrop(plan, 3_328_253, count: 3);
        AssertDrop(plan, 3_328_254, count: 3);
        AssertDrop(plan, 3_330_770, count: 10);
        AssertDrop(plan, 3_316_793, count: 10);
        AssertDrop(plan, 729_463, count: 1);
        AssertDrop(plan, 3_321_805, count: 1);
        Assert.DoesNotContain(plan.FixedItems, item => item.ItemId == 4_200_010U);
    }

    [Fact]
    public void TryCreateFixedDropPlanMapsThrillingSpookRareBranch()
    {
        var found = LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
            familyId: LegacyBossSpecialDeathDropPolicy.ThrillingSpookFamilyId,
            mapId: 1_015,
            boss: true,
            rolls: new LegacyBossSpecialDeathDropRolls(Family4212RareDrop: true),
            out var plan);

        Assert.True(found);
        Assert.Equal(36, plan.TotalFixedItemCount);
        AssertDrop(plan, 4_200_010, count: 1);
        Assert.DoesNotContain(plan.FixedItems, item => item.ItemId == 3_321_805U);
    }

    [Fact]
    public void TryCreateFixedDropPlanMapsGenerateSoulsItemPoolsFromItemtype()
    {
        var catalog = LegacyBossSpecialDeathDropCatalog.FromItemsById(new Dictionary<int, IReadOnlyList<ItemType>>
        {
            [800_001] = [CreateItemType(800_001, purificationLevel: 7)],
            [800_002] = [CreateItemType(800_002, purificationLevel: 7)],
            [729_305] = [CreateItemType(729_305, purificationLevel: 7)],
            [350_001] = [CreateItemType(350_001)],
            [370_001] = [CreateItemType(370_001)],
        });

        var found = LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
            familyId: LegacyBossSpecialDeathDropPolicy.ThrillingSpookFamilyId,
            mapId: 1_015,
            boss: true,
            rolls: new LegacyBossSpecialDeathDropRolls(
                Family4212RareDrop: false,
                SoulsPurificationRollModulo: 2,
                SoulsPurificationIndexes: [1, 0],
                SoulsAccessoryRollModulo: 2,
                SoulsAccessoryIndexes: [1, 0]),
            catalog,
            out var plan);

        Assert.True(found);
        Assert.Empty(plan.PendingSourceBehaviors);
        Assert.Equal(40, plan.TotalFixedItemCount);
        AssertDrop(plan, 800_002, count: 1);
        AssertDrop(plan, 800_001, count: 1);
        AssertDrop(plan, 370_001, count: 1);
        AssertDrop(plan, 350_001, count: 1);
        AssertDrop(plan, 729_463, count: 1);
        Assert.DoesNotContain(plan.FixedItems, item => item.ItemId == 729_305U);
    }

    [Fact]
    public void TryCreateFixedDropPlanMapsStudyPointBossBranchWithGeneratedSouls()
    {
        var catalog = CreateGenerateSoulsCatalog();

        var found = LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
            familyId: LegacyBossSpecialDeathDropPolicy.StudyPointBossFamilyId,
            mapId: 1_015,
            boss: true,
            rolls: new LegacyBossSpecialDeathDropRolls(
                Family4171RareDrop: false,
                SoulsPurificationRollModulo: 2,
                SoulsPurificationIndexes: [1, 0],
                SoulsAccessoryRollModulo: 2,
                SoulsAccessoryIndexes: [1, 0]),
            catalog,
            out var plan);

        Assert.True(found);
        Assert.Equal(4_171U, plan.FamilyId);
        Assert.Equal("MonsterRole.cs (Boss > 0 && Family.ID == 4171) || Family.ID == 20060 || Family.ID == 8311", plan.SourceBranch);
        Assert.Empty(plan.PendingSourceBehaviors);
        Assert.Equal(39, plan.TotalFixedItemCount);
        AssertDrop(plan, 4_200_007, count: 1);
        AssertDrop(plan, 700_132, count: 1);
        AssertDrop(plan, 3_005_127, count: 3);
        AssertDrop(plan, 3_335_128, count: 3);
        AssertDrop(plan, 3_328_253, count: 3);
        AssertDrop(plan, 3_328_254, count: 3);
        AssertDrop(plan, 800_002, count: 1);
        AssertDrop(plan, 800_001, count: 1);
        AssertDrop(plan, 370_001, count: 1);
        AssertDrop(plan, 350_001, count: 1);
        AssertDrop(plan, 729_463, count: 1);
        AssertDrop(plan, 3_330_770, count: 10);
        AssertDrop(plan, 3_316_793, count: 10);
        Assert.DoesNotContain(plan.FixedItems, item => item.ItemId == 4_200_010U);
    }

    [Theory]
    [InlineData(20_060)]
    [InlineData(8_311)]
    public void TryCreateFixedDropPlanPreservesStudyPointBranchFamilyPrecedenceWithoutBossFlag(uint familyId)
    {
        var found = LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
            familyId,
            mapId: 1_015,
            boss: false,
            rolls: new LegacyBossSpecialDeathDropRolls(Family4171RareDrop: true),
            LegacyBossSpecialDeathDropCatalog.Empty,
            out var plan);

        Assert.True(found);
        Assert.False(plan.Boss);
        AssertDrop(plan, 4_200_010, count: 1);
        Assert.DoesNotContain(plan.FixedItems, item => item.ItemId == 4_200_007U);
    }

    [Fact]
    public void TryCreateFixedDropPlanMapsDragonIslandFamily4220NormalBranchWithoutBossFlag()
    {
        var found = LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
            familyId: LegacyBossSpecialDeathDropPolicy.DragonIslandFamilyId,
            mapId: LegacyBossSpecialDeathDropPolicy.DragonIslandMapId,
            boss: false,
            rolls: new LegacyBossSpecialDeathDropRolls(DragonIslandFamily4220RareDrop: false),
            out var plan);

        Assert.True(found);
        Assert.Equal(4_220U, plan.FamilyId);
        Assert.Equal(10_137U, plan.MapId);
        Assert.False(plan.Boss);
        Assert.Equal("MonsterRole.cs Map == 10137 && Family.ID == 4220", plan.SourceBranch);
        Assert.Equal(48, plan.TotalFixedItemCount);
        AssertDrop(plan, 3_330_609, count: 1, scatterRange: 2);
        AssertDrop(plan, 3_333_402, count: 1, scatterRange: 2);
        AssertDrop(plan, 3_330_770, count: 10);
        AssertDrop(plan, 3_316_793, count: 10);
        AssertDrop(plan, 3_328_253, count: 3);
        AssertDrop(plan, 3_328_254, count: 3);
        AssertDrop(plan, 3_306_368, count: 8);
        AssertDrop(plan, 3_009_104, count: 2, scatterRange: 2);
        AssertDrop(plan, 3_009_002, count: 2);
        AssertDrop(plan, 3_005_360, count: 2);
        AssertDrop(plan, 3_004_181, count: 2, scatterRange: 3);
        AssertDrop(plan, 3_335_128, count: 2);
        AssertDrop(plan, 3_310_995, count: 2);
        Assert.DoesNotContain(plan.FixedItems, item => item.ItemId == 420_009U);
    }

    [Fact]
    public void TryCreateFixedDropPlanMapsDragonIslandFamily4220RareBranch()
    {
        var found = LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
            familyId: LegacyBossSpecialDeathDropPolicy.DragonIslandFamilyId,
            mapId: LegacyBossSpecialDeathDropPolicy.DragonIslandMapId,
            boss: true,
            rolls: new LegacyBossSpecialDeathDropRolls(DragonIslandFamily4220RareDrop: true),
            out var plan);

        Assert.True(found);
        Assert.Equal(47, plan.TotalFixedItemCount);
        AssertDrop(plan, 420_009, count: 1, scatterRange: 2);
        Assert.DoesNotContain(plan.FixedItems, item => item.ItemId == 3_330_609U);
        Assert.DoesNotContain(plan.FixedItems, item => item.ItemId == 3_333_402U);
    }

    [Fact]
    public void TryCreateFixedDropPlanMapsQueenOfEvilRandomDragonBallAndTalismanBranch()
    {
        var catalog = CreateGenerateSoulsCatalog();

        var found = LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
            familyId: LegacyBossSpecialDeathDropPolicy.QueenOfEvilFamilyId,
            mapId: 1_015,
            boss: false,
            rolls: new LegacyBossSpecialDeathDropRolls(
                QueenOfEvilDragonBallCount: 3,
                QueenOfEvilTalismanIndexes: [4, 3, 2, 1, 0]),
            catalog,
            out var plan);

        Assert.True(found);
        Assert.Equal(3_970U, plan.FamilyId);
        Assert.Equal("MonsterRole.cs Name == \"QueenofEvil\" || Family.ID == 3970", plan.SourceBranch);
        Assert.Equal(59, plan.TotalFixedItemCount);
        Assert.Contains(plan.PendingSourceBehaviors, reason => reason.Contains("GenerateBossFamily()"));
        AssertDrop(plan, 3_328_253, count: 3);
        AssertDrop(plan, 3_328_254, count: 3);
        AssertDrop(plan, 3_330_770, count: 20);
        AssertDrop(plan, 3_316_793, count: 20);
        AssertDrop(plan, 1_088_000, count: 3, scatterRange: 3);
        AssertDrop(plan, 3_335_128, count: 5);
        AssertDrop(plan, 4_030_401, count: 1);
        AssertDrop(plan, 4_030_301, count: 1);
        AssertDrop(plan, 4_030_201, count: 1);
        AssertDrop(plan, 4_030_101, count: 1);
        AssertDrop(plan, 4_030_001, count: 1);
        Assert.DoesNotContain(plan.FixedItems, item => item.ItemId == 4_030_002U);
    }

    [Fact]
    public void TryCreateFixedDropPlanMapsQueenOfEvilBySourceName()
    {
        var found = LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
            familyId: 9_999,
            mapId: 1_015,
            boss: true,
            familyName: "QueenofEvil",
            rolls: new LegacyBossSpecialDeathDropRolls(QueenOfEvilDragonBallCount: 1),
            catalog: LegacyBossSpecialDeathDropCatalog.Empty,
            out var plan);

        Assert.True(found);
        Assert.Equal(9_999U, plan.FamilyId);
        Assert.Equal(52, plan.TotalFixedItemCount);
        Assert.Contains(plan.PendingSourceBehaviors, reason => reason.Contains("random talisman"));
    }

    [Fact]
    public void TryCreateFixedDropPlanMapsGenericBossGenerateBossFamilyAsSourceSilentNoDrop()
    {
        var found = LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
            familyId: 9_999,
            mapId: 1_015,
            boss: true,
            rolls: default,
            out var plan);

        Assert.True(found);
        Assert.Equal(9_999U, plan.FamilyId);
        Assert.True(plan.Boss);
        Assert.Equal("MonsterRole.cs GenerateBossFamily()", plan.SourceBranch);
        Assert.Equal(0, plan.TotalFixedItemCount);
        Assert.Empty(plan.FixedItems);
        Assert.Empty(plan.PendingSourceBehaviors);
    }

    [Fact]
    public void TryCreateFixedDropPlanMapsDeityLandBossDictionaryAndFixedBundle()
    {
        var found = LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
            familyId: 9_999,
            mapId: LegacyBossSpecialDeathDropPolicy.DeityLandBossDropMapId,
            boss: true,
            rolls: new LegacyBossSpecialDeathDropRolls(
                DeityLandPrimaryDropIndex: 1,
                DeityLandSecondaryDropIndex: 2),
            out var plan);

        Assert.True(found);
        Assert.Equal(9_999U, plan.FamilyId);
        Assert.Equal(LegacyBossSpecialDeathDropPolicy.DeityLandBossDropMapId, plan.MapId);
        Assert.True(plan.Boss);
        Assert.Equal("MonsterRole.cs killer.Player.Map == 10250 && Boss > 0", plan.SourceBranch);
        Assert.Equal(45, plan.TotalFixedItemCount);
        Assert.Contains(plan.PendingSourceBehaviors, reason => reason.Contains("AddRandomRelic"));
        AssertDrop(plan, 1_088_000, count: 4, scatterRange: 3);
        AssertDrop(plan, 3_303_483, count: 30, scatterRange: 3);
        AssertDrop(plan, 3_005_127, count: 5);
        AssertDrop(plan, 3_004_181, count: 1, scatterRange: 5);
        AssertDrop(plan, 4_200_001, count: 1, scatterRange: 1);
        AssertDropEntry(plan, 4_200_002, count: 1, scatterRange: 1);
        AssertDrop(plan, 4_200_003, count: 1, scatterRange: 1);
        AssertDropEntry(plan, 4_200_002, count: 1, scatterRange: 10);
        AssertDrop(plan, 3_316_793, count: 1, scatterRange: 1);
    }

    [Fact]
    public void TryCreateFixedDropPlanCombinesDeityLandAndStudyPointBranchForFamily8311()
    {
        var catalog = CreateGenerateSoulsCatalog();

        var found = LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
            familyId: LegacyBossSpecialDeathDropPolicy.DeityLandSpecialFamilyId,
            mapId: LegacyBossSpecialDeathDropPolicy.DeityLandBossDropMapId,
            boss: true,
            rolls: new LegacyBossSpecialDeathDropRolls(
                Family4171RareDrop: false,
                DeityLandPrimaryDropIndex: 0,
                DeityLandSecondaryDropIndex: 0,
                SoulsPurificationRollModulo: 2,
                SoulsPurificationIndexes: [1, 0],
                SoulsAccessoryRollModulo: 2,
                SoulsAccessoryIndexes: [1, 0]),
            catalog,
            out var plan);

        Assert.True(found);
        Assert.Equal(8_311U, plan.FamilyId);
        Assert.Equal(59, plan.TotalFixedItemCount);
        Assert.Contains("killer.Player.Map == 10250", plan.SourceBranch);
        Assert.Contains("Family.ID == 8311", plan.SourceBranch);
        Assert.Contains(plan.PendingSourceBehaviors, reason => reason.Contains("AddRandomRelic"));
        AssertDrop(plan, 3_003_124, count: 2, scatterRange: 3);
        AssertDrop(plan, 3_002_030, count: 7, scatterRange: 3);
        AssertDrop(plan, 4_200_007, count: 1);
        AssertDrop(plan, 700_132, count: 1);
        AssertDrop(plan, 800_002, count: 1);
        AssertDrop(plan, 800_001, count: 1);
        AssertDrop(plan, 370_001, count: 1);
        AssertDrop(plan, 350_001, count: 1);
        Assert.Equal(8, plan.FixedItems.Where(item => item.ItemId == 3_005_127U).Sum(static item => item.Count));
    }

    [Fact]
    public void TryCreateFixedDropPlanMapsDeityLandBossGeneratedRandomRelic()
    {
        var catalog = CreateRelicRandomAttributeCatalog();

        var found = LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
            familyId: 9_999,
            mapId: LegacyBossSpecialDeathDropPolicy.DeityLandBossDropMapId,
            boss: true,
            rolls: new LegacyBossSpecialDeathDropRolls(
                DeityLandPrimaryDropIndex: 1,
                DeityLandSecondaryDropIndex: 2,
                DeityLandRelicItemIndex: 0,
                DeityLandRelicDurabilityRoll: 10,
                DeityLandRelicAttributeCount: 3,
                DeityLandRelicAttributeIndexes: [0, 0, 0],
                DeityLandRelicAttributeValues: [200, 100, 900]),
            catalog,
            out var plan);

        Assert.True(found);
        Assert.Empty(plan.PendingSourceBehaviors);
        Assert.Equal(45, plan.TotalFixedItemCount);
        Assert.Equal(1, plan.TotalGeneratedItemCount);
        Assert.Equal(46, plan.TotalItemCount);

        var relic = Assert.Single(plan.GeneratedItems);
        Assert.Equal(4_100_001U, relic.ItemId);
        Assert.Equal(1_099, relic.Durability);
        Assert.Equal(1_099, relic.MaximumDurability);
        Assert.Equal(7, relic.ScatterRange);
        Assert.Equal(200_103U, relic.Attribute1);
        Assert.Equal(100_104U, relic.Attribute2);
        Assert.Equal(900_108U, relic.Attribute3);
        Assert.Equal(0U, relic.Attribute4);
        Assert.Equal(0U, relic.Attribute5);
        Assert.Contains("Inventory.AddRandomRelic", relic.Source);
    }

    [Fact]
    public void TryCreateFixedDropPlanMapsStudyPointTeamRewardFamily20055GenerateSoulsLevel6Branch()
    {
        var catalog = CreateGenerateSoulsCatalog();

        var found = LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
            familyId: LegacyBossSpecialDeathDropPolicy.StudyPointTeamRewardFamilyId,
            mapId: 1_015,
            boss: true,
            rolls: new LegacyBossSpecialDeathDropRolls(
                Family20055Add711679: true,
                Family20055Add711188: true,
                SoulsPurificationRollModulo: 2,
                SoulsPurificationIndexes: [1, 0],
                SoulsAccessoryRollModulo: 2,
                SoulsAccessoryIndexes: [1, 0]),
            catalog,
            out var plan);

        Assert.True(found);
        Assert.Equal(20_055U, plan.FamilyId);
        Assert.Equal("MonsterRole.cs Boss > 0 && Family.ID == 20055", plan.SourceBranch);
        Assert.Empty(plan.PendingSourceBehaviors);
        Assert.Equal(7, plan.TotalFixedItemCount);
        AssertDrop(plan, 700_002, count: 1, scatterRange: 3);
        AssertDrop(plan, 700_001, count: 1, scatterRange: 3);
        AssertDrop(plan, 370_001, count: 1, scatterRange: 3);
        AssertDrop(plan, 350_001, count: 1, scatterRange: 3);
        AssertDrop(plan, 729_463, count: 1, scatterRange: 3);
        AssertDrop(plan, 711_679, count: 1, scatterRange: 3);
        AssertDrop(plan, 711_188, count: 1, scatterRange: 3);
    }

    [Fact]
    public void TryCreateFixedDropPlanMapsSoulBoxFamily6643GenerateSoulsLevel6Branch()
    {
        var catalog = CreateGenerateSoulsCatalog();

        var found = LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
            familyId: LegacyBossSpecialDeathDropPolicy.SoulBoxFamilyId,
            mapId: 1_015,
            boss: true,
            rolls: new LegacyBossSpecialDeathDropRolls(
                SoulsPurificationRollModulo: 2,
                SoulsPurificationIndexes: [1, 0],
                SoulsAccessoryRollModulo: 2,
                SoulsAccessoryIndexes: [1, 0]),
            catalog,
            out var plan);

        Assert.True(found);
        Assert.Equal(6_643U, plan.FamilyId);
        Assert.Equal("MonsterRole.cs Boss > 0 && Family.ID == 6643", plan.SourceBranch);
        Assert.Empty(plan.PendingSourceBehaviors);
        Assert.Equal(5, plan.TotalFixedItemCount);
        AssertDrop(plan, 700_002, count: 1, scatterRange: 3);
        AssertDrop(plan, 700_001, count: 1, scatterRange: 3);
        AssertDrop(plan, 370_001, count: 1, scatterRange: 3);
        AssertDrop(plan, 350_001, count: 1, scatterRange: 3);
        AssertDrop(plan, 729_463, count: 1, scatterRange: 3);
        Assert.DoesNotContain(plan.FixedItems, item => item.ItemId is 711_679U or 711_188U);
    }

    [Theory]
    [InlineData(4_212, false)]
    [InlineData(4_171, false)]
    [InlineData(9_999, false)]
    public void TryCreateFixedDropPlanRejectsUnmappedOrNonBossBranches(uint familyId, bool boss)
    {
        var found = LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
            familyId,
            mapId: 1_015,
            boss,
            rolls: default,
            out var plan);

        Assert.False(found);
        Assert.Null(plan);
    }

    private static void AssertDrop(
        LegacyBossSpecialDeathDropPlan plan,
        uint itemId,
        int count,
        int scatterRange = 10)
    {
        AssertDropEntry(plan, itemId, count, scatterRange);
    }

    private static void AssertDropEntry(
        LegacyBossSpecialDeathDropPlan plan,
        uint itemId,
        int count,
        int scatterRange)
    {
        var item = Assert.Single(
            plan.FixedItems,
            drop => drop.ItemId == itemId && drop.ScatterRange == scatterRange);
        Assert.Equal(count, item.Count);
        Assert.Equal(scatterRange, item.ScatterRange);
        Assert.Contains("MonsterRole.cs", item.Source);
    }

    private static LegacyBossSpecialDeathDropCatalog CreateGenerateSoulsCatalog()
    {
        return LegacyBossSpecialDeathDropCatalog.FromItemsById(new Dictionary<int, IReadOnlyList<ItemType>>
        {
            [800_001] = [CreateItemType(800_001, purificationLevel: 7)],
            [800_002] = [CreateItemType(800_002, purificationLevel: 7)],
            [729_305] = [CreateItemType(729_305, purificationLevel: 7)],
            [700_001] = [CreateItemType(700_001, purificationLevel: 6)],
            [700_002] = [CreateItemType(700_002, purificationLevel: 6)],
            [350_001] = [CreateItemType(350_001)],
            [370_001] = [CreateItemType(370_001)],
            [4_030_001] = [CreateItemType(4_030_001)],
            [4_030_101] = [CreateItemType(4_030_101)],
            [4_030_201] = [CreateItemType(4_030_201)],
            [4_030_301] = [CreateItemType(4_030_301)],
            [4_030_401] = [CreateItemType(4_030_401)],
            [4_030_002] = [CreateItemType(4_030_002)],
        });
    }

    private static LegacyBossSpecialDeathDropCatalog CreateRelicRandomAttributeCatalog()
    {
        return new LegacyBossSpecialDeathDropCatalog(
            new Dictionary<ushort, IReadOnlyList<uint>>(),
            [],
            [],
            new Dictionary<int, IReadOnlyList<XuanbaoRandomAttribute>>
            {
                [4_100_001] =
                [
                    XuanbaoRandomAttribute(4_100_001, LegacyChiAttributeType.Immunity, min: 110, max: 201),
                    XuanbaoRandomAttribute(4_100_001, LegacyChiAttributeType.Breakthrough, min: 30, max: 101),
                    XuanbaoRandomAttribute(4_100_001, LegacyChiAttributeType.AddMagicAttack, min: 600, max: 901),
                ],
            });
    }

    private static ItemType CreateItemType(int id, uint purificationLevel = 0)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = id.ToString();
        fields[1] = FormattableString.Invariant($"Item{id}");
        fields[ItemType.PurificationLevelFieldIndex] = purificationLevel.ToString();
        return new ItemType(id, fields[1], fields);
    }

    private static XuanbaoRandomAttribute XuanbaoRandomAttribute(
        int itemTypeId,
        LegacyChiAttributeType attributeType,
        int min,
        int max)
    {
        var raw = FormattableString.Invariant($"{itemTypeId}@@{(byte)attributeType}@@{min}@@{max}@@");
        return new XuanbaoRandomAttribute(
            itemTypeId,
            (byte)attributeType,
            min,
            max,
            "fixture://xuanbao_rand_attr.txt",
            itemTypeId,
            raw,
            raw.Split("@@"));
    }
}
