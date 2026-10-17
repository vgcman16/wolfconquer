using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClassOwnedSpellGrantProfileTests
{
    public static TheoryData<long, long, long> TrojanLineageCases()
    {
        return new TheoryData<long, long, long>
        {
            { 1_000, 0, 0 },
            { 1_051, 0, 0 },
            { 2_000, 1_000, 0 },
            { 2_000, 1_051, 0 },
            { 2_000, 0, 1_000 },
            { 2_000, 0, 1_051 }
        };
    }

    [Theory]
    [MemberData(nameof(TrojanLineageCases))]
    public void GetSpellIdsReturnsOnlyMatchingLoginCycloneForTrojanLineage(
        long currentClass,
        long firstClass,
        long secondClass)
    {
        var spellIds = LegacyClassOwnedSpellGrantProfile.GetSpellIds(
            currentClass,
            firstClass,
            secondClass);

        Assert.Equal(new ushort[] { LegacyClassOwnedSpellGrantProfile.CycloneSpellId }, spellIds);
    }

    [Theory]
    [InlineData(1_052, 0, 0)]
    [InlineData(1_057, 0, 0)]
    [InlineData(2_000, 1_052, 0)]
    [InlineData(2_000, 0, 1_057)]
    public void GetSpellIdsUsesMatchingTrojanUpperBound(
        long currentClass,
        long firstClass,
        long secondClass)
    {
        var spellIds = LegacyClassOwnedSpellGrantProfile.GetSpellIds(
            currentClass,
            firstClass,
            secondClass);

        Assert.Empty(spellIds);
    }

    [Theory]
    [InlineData(2_000)]
    [InlineData(4_000)]
    [InlineData(5_000)]
    [InlineData(6_000)]
    [InlineData(7_000)]
    [InlineData(8_000)]
    [InlineData(13_002)]
    [InlineData(14_002)]
    [InlineData(16_000)]
    public void GetSpellIdsRejectsUnprovenLevel110ClassGrantFamiliesByDefault(long currentClass)
    {
        var spellIds = LegacyClassOwnedSpellGrantProfile.GetSpellIds(
            currentClass,
            firstClass: 0,
            secondClass: 0);

        Assert.Empty(spellIds);
    }
}
