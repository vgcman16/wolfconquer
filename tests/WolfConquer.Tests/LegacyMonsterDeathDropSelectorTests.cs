using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterDeathDropSelectorTests
{
    [Fact]
    public void SelectReturnsNoDropForRollOutsideModeledBands()
    {
        var result = LegacyMonsterDeathDropSelector.Select(
            MonsterInstance(),
            MonsterFamily(),
            new LegacyMonsterDeathDropRolls(DropRoll: 40));

        Assert.Equal(LegacyMonsterDeathDropKind.None, result.Kind);
        Assert.Contains("outside the modeled normal drop bands", result.Reason);
    }

    [Fact]
    public void SelectMapsMoneyRollThroughSourceMoneyItemTable()
    {
        var result = LegacyMonsterDeathDropSelector.Select(
            MonsterInstance(),
            MonsterFamily(),
            new LegacyMonsterDeathDropRolls(
                DropRoll: 43,
                MoneyAmount: 50_000,
                DropCountRateRoll: 99),
            KnownItems(1_091_020));

        Assert.Equal(LegacyMonsterDeathDropKind.Money, result.Kind);
        Assert.Equal(1_091_020U, result.ItemId);
        Assert.Equal(50_000U, result.MoneyAmount);
        Assert.Equal((byte)3, result.DropCount);
        Assert.Contains("41..59", result.Reason);
    }

    [Fact]
    public void SelectMapsGeneratedItemRollToSortLevelQualityItemId()
    {
        var result = LegacyMonsterDeathDropSelector.Select(
            MonsterInstance(),
            MonsterFamily(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["drop_weapon"] = "0000",
            }),
            new LegacyMonsterDeathDropRolls(
                DropRoll: 701,
                GeneratedCategoryRoll: 900,
                GeneratedWeaponRoll: 40,
                GeneratedSortIndexRoll: 0,
                GeneratedLevelAlterRoll: 50,
                GeneratedQuality: 3),
            KnownItems(410_003));

        Assert.Equal(LegacyMonsterDeathDropKind.Item, result.Kind);
        Assert.Equal(410_003U, result.ItemId);
        Assert.Equal((ushort)410, result.ItemSort);
        Assert.Equal(0U, result.ItemLevel);
        Assert.Equal((byte)3, result.ItemQuality);
        Assert.Contains("sort/level/quality", result.Reason);
    }

    [Theory]
    [InlineData(true, 5_000U)]
    [InlineData(false, 200_000U)]
    public void SelectMapsBossFamilyGenerateBossFamilyToFourDeterministicGeneratedItems(
        bool boss,
        uint maxLife)
    {
        var result = LegacyMonsterDeathDropSelector.Select(
            MonsterInstance(boss: boss, maxLife: maxLife),
            MonsterFamily(),
            new LegacyMonsterDeathDropRolls(
                DropRoll: 43,
                GeneratedSort: 410,
                GeneratedBaseLevel: 17,
                GeneratedQuality: 3),
            KnownItems(410_173));

        Assert.Equal(LegacyMonsterDeathDropKind.Item, result.Kind);
        Assert.Equal(410_173U, result.ItemId);
        Assert.Equal((byte)4, result.DropCount);
        Assert.Equal((ushort)410, result.ItemSort);
        Assert.Equal(17U, result.ItemLevel);
        Assert.Equal((byte)3, result.ItemQuality);
        Assert.Contains("GenerateBossFamily", result.Reason);
    }

    [Theory]
    [InlineData(18, "Birdman")]
    [InlineData(77, "BirdmanL88")]
    public void SelectGeneratedBirdmanWeaponDropResolvesExactSourceItemId(
        int monsterId,
        string monsterName)
    {
        var result = LegacyMonsterDeathDropSelector.Select(
            MonsterInstance(monsterId: (uint)monsterId, name: monsterName),
            MonsterFamily(
                attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["drop_weapon"] = "017",
                },
                id: monsterId,
                name: monsterName),
            new LegacyMonsterDeathDropRolls(
                DropRoll: 701,
                GeneratedCategoryRoll: 700,
                GeneratedWeaponRoll: 40,
                GeneratedSortIndexRoll: 0,
                GeneratedLevelAlterRoll: 50,
                GeneratedQuality: 3),
            KnownItems(410_173));

        Assert.Equal(LegacyMonsterDeathDropKind.Item, result.Kind);
        Assert.Equal(410_173U, result.ItemId);
        Assert.Equal((ushort)410, result.ItemSort);
        Assert.Equal(17U, result.ItemLevel);
        Assert.Equal((byte)3, result.ItemQuality);
    }

    [Theory]
    [InlineData(18, "Birdman", 120_173)]
    [InlineData(77, "BirdmanL88", 120_173)]
    public void SelectGeneratedBirdmanMissingExactCandidateReturnsSourceNoDrop(
        int monsterId,
        string monsterName,
        uint missingItemId)
    {
        var result = LegacyMonsterDeathDropSelector.Select(
            MonsterInstance(monsterId: (uint)monsterId, name: monsterName),
            MonsterFamily(
                attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["drop_necklace"] = "017",
                },
                id: monsterId,
                name: monsterName),
            new LegacyMonsterDeathDropRolls(
                DropRoll: 701,
                GeneratedCategoryRoll: 20,
                GeneratedSortIndexRoll: 0,
                GeneratedLevelAlterRoll: 50,
                GeneratedQuality: 3),
            KnownItems(410_173));

        Assert.Equal(LegacyMonsterDeathDropKind.None, result.Kind);
        Assert.Contains(missingItemId.ToString(), result.Reason);
        Assert.Contains("not present", result.Reason);
    }

    [Fact]
    public void SelectMapsSpecialDropWhenSourceWatcherHitIsSupplied()
    {
        var result = LegacyMonsterDeathDropSelector.Select(
            MonsterInstance(),
            MonsterFamily(
                specialDrops:
                [
                    new SpecialDrop(1_088_000, ChanceOrCount: 4),
                ]),
            new LegacyMonsterDeathDropRolls(
                DropRoll: 701,
                SpecialDropRateHit: true),
            KnownItems(1_088_000));

        Assert.Equal(LegacyMonsterDeathDropKind.SpecialItem, result.Kind);
        Assert.Equal(1_088_000U, result.ItemId);
        Assert.Equal((byte)0, result.ItemQuality);
        Assert.Equal(4, result.SpecialDropChanceOrCount);
        Assert.Contains("DropSpecials", result.Reason);
    }

    private static LegacyMonsterInstance MonsterInstance(
        int mapId = 1000,
        bool boss = false,
        uint maxLife = 5_000,
        uint monsterId = 76,
        string name = "BladeGhostL83")
    {
        return new LegacyMonsterInstance(
            Uid: 470_000,
            mapId,
            X: 192,
            Y: 345,
            Mesh: 200,
            Level: 83,
            Hitpoints: maxLife,
            MaxLife: maxLife,
            MonsterId: monsterId,
            Name: name,
            boss,
            GeneratorId: 70,
            SpawnIndex: 0,
            RestSeconds: 1);
    }

    private static Monster MonsterFamily(
        IReadOnlyDictionary<string, string>? attributes = null,
        IReadOnlyList<SpecialDrop>? specialDrops = null,
        int id = 76,
        string name = "BladeGhostL83")
    {
        return new Monster(
            id,
            name,
            attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            specialDrops ?? []);
    }

    private static LegacyMonsterDeathDropContext KnownItems(params uint[] itemIds)
    {
        var known = new HashSet<uint>(itemIds);
        return new LegacyMonsterDeathDropContext(known.Contains);
    }
}
