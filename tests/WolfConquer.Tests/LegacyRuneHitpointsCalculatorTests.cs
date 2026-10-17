using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRuneHitpointsCalculatorTests
{
    [Fact]
    public void StorageScoreMatchesLegacyRuneScoreFormula()
    {
        var score = LegacyRuneHitpointsCalculator.CalculateStorageScore(
        [
            new PlayerItemSnapshotItem { ItemId = 4010701, Position = 101 },
            new PlayerItemSnapshotItem { ItemId = 402005, Position = 102 },
            new PlayerItemSnapshotItem { ItemId = 403009, Position = 211 },
            new PlayerItemSnapshotItem { ItemId = 403009, Position = 0 },
        ]);

        Assert.Equal(2_500U, score);
    }

    [Fact]
    public void StorageAttributeHitpointsUseEligibleAttributeTypeOneBonuses()
    {
        var storageAttributes = new[]
        {
            RuneStorageAttribute(id: 1, requiredScore: 500, bonuses:
            [
                new RuneStorageAttributeBonus(1, 100),
                new RuneStorageAttributeBonus(2, 999),
                new RuneStorageAttributeBonus(0, 0),
            ]),
            RuneStorageAttribute(id: 2, requiredScore: 2_000, bonuses:
            [
                new RuneStorageAttributeBonus(11, 300),
                new RuneStorageAttributeBonus(1, 200),
                new RuneStorageAttributeBonus(1, -50),
            ]),
            RuneStorageAttribute(id: 3, requiredScore: 2_001, bonuses:
            [
                new RuneStorageAttributeBonus(1, 777),
                new RuneStorageAttributeBonus(0, 0),
                new RuneStorageAttributeBonus(0, 0),
            ]),
        };

        var hitpoints = LegacyRuneHitpointsCalculator.CalculateStorageAttributeHitpoints(
            storageScore: 2_000,
            storageAttributes);

        Assert.Equal(300U, hitpoints);
    }

    [Fact]
    public void CalculateReturnsSourceBackedStorageHpAndNamesRuntimeStatusFlagGap()
    {
        var runeItems = new[]
        {
            new PlayerItemSnapshotItem { ItemId = 402005, Position = 101 },
        };
        var storageAttributes = new[]
        {
            RuneStorageAttribute(id: 1, requiredScore: 2_000, bonuses:
            [
                new RuneStorageAttributeBonus(1, 123),
                new RuneStorageAttributeBonus(12, 300),
                new RuneStorageAttributeBonus(0, 0),
            ]),
        };

        var breakdown = LegacyRuneHitpointsCalculator.Calculate(runeItems, storageAttributes);

        Assert.Equal(2_000U, breakdown.StorageScore);
        Assert.Equal(123U, breakdown.StorageAttributeHitpoints);
        Assert.Equal(0U, breakdown.StatusFlagHitpoints);
        Assert.Equal(123U, breakdown.Total);
        Assert.Contains(
            "Rune runtime status flag HP inputs (BloodTide, Rampage, Sacrifice, FineRain1, RiseofTaoism)",
            breakdown.MissingInputs);
    }

    [Fact]
    public void CalculateReportsMissingModernInputsWithoutGuessing()
    {
        var breakdown = LegacyRuneHitpointsCalculator.Calculate(
            runeItems: null,
            storageAttributes: null);

        Assert.Equal(0U, breakdown.Total);
        Assert.Contains("PlayerItemSnapshot.RuneItems", breakdown.MissingInputs);
        Assert.Contains("WorldDataCatalog.RuneStorageAttributes", breakdown.MissingInputs);
        Assert.Contains(
            "Rune runtime status flag HP inputs (BloodTide, Rampage, Sacrifice, FineRain1, RiseofTaoism)",
            breakdown.MissingInputs);
    }

    private static RuneStorageAttribute RuneStorageAttribute(
        int id,
        int requiredScore,
        IReadOnlyList<RuneStorageAttributeBonus> bonuses)
    {
        return new RuneStorageAttribute(
            id,
            requiredScore,
            bonuses,
            SourceFile: "rune_storage_attr.txt",
            LineNumber: id,
            RawLine: "",
            Fields: []);
    }
}
