using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class VisibleDx8D13AutomaticSkillSelectorTests
{
    private static readonly int[] ExactFamilies = [2, 6, 7, 8, 9, 16];

    public static TheoryData<int, int, ushort> ExactCallerCases => new()
    {
        { 2, 0, 12_740 },
        { 2, 1, 12_750 },
        { 2, 2, 12_760 },
        { 6, 0, 12_580 },
        { 6, 1, 12_590 },
        { 6, 2, 12_600 },
        { 7, 0, 14_570 },
        { 7, 1, 14_580 },
        { 7, 2, 14_570 },
        { 8, 0, 12_210 },
        { 8, 1, 12_220 },
        { 8, 2, 12_230 },
        { 9, 0, 15_670 },
        { 9, 1, 15_680 },
        { 9, 2, 15_690 },
        { 16, 0, 13_040 },
        { 16, 1, 13_050 },
        { 16, 2, 13_060 },
    };

    public static TheoryData<int> OutOfRangeCallerRandomIndexes => new()
    {
        int.MinValue,
        -1,
        3,
        4,
        int.MaxValue,
    };

    [Theory]
    [MemberData(nameof(ExactCallerCases))]
    public void FamilyLookupMatchesEveryExactCallerOutcome(
        int family,
        int callerRandomIndex,
        ushort expectedSelector)
    {
        Assert.True(VisibleDx8D13AutomaticSkillSelectors.TryGetSelector(
            family,
            callerRandomIndex,
            out var selector));
        Assert.Equal(expectedSelector, selector);
    }

    [Fact]
    public void ExactSelectorListMatchesEveryUniqueBinaryOutcomeInFamilyOrder()
    {
        Assert.Equal(
            new ushort[]
            {
                12_740,
                12_750,
                12_760,
                12_580,
                12_590,
                12_600,
                14_570,
                14_580,
                12_210,
                12_220,
                12_230,
                15_670,
                15_680,
                15_690,
                13_040,
                13_050,
                13_060,
            },
            VisibleDx8D13AutomaticSkillSelectors.ExactSelectors);
        Assert.Equal(
            VisibleDx8D13AutomaticSkillSelectors.ExactSelectors.Count,
            VisibleDx8D13AutomaticSkillSelectors.ExactSelectors.Distinct().Count());
    }

    [Fact]
    public void KnownSelectorPredicateAcceptsExactlyThePublishedSelectorList()
    {
        var exactSelectors = VisibleDx8D13AutomaticSkillSelectors.ExactSelectors.ToHashSet();

        for (uint candidate = ushort.MinValue; candidate <= ushort.MaxValue; candidate++)
        {
            Assert.Equal(
                exactSelectors.Contains((ushort)candidate),
                VisibleDx8D13AutomaticSkillSelectors.IsKnownSelector(candidate));
        }

        Assert.False(VisibleDx8D13AutomaticSkillSelectors.IsKnownSelector(ushort.MaxValue + 1u));
        Assert.False(VisibleDx8D13AutomaticSkillSelectors.IsKnownSelector(uint.MaxValue));
    }

    [Fact]
    public void FamilyLookupRejectsEveryUnknownByteSizedFamily()
    {
        var knownFamilies = ExactFamilies.ToHashSet();

        for (var family = (int)byte.MinValue; family <= byte.MaxValue; family++)
        {
            if (knownFamilies.Contains(family))
            {
                continue;
            }

            for (var callerRandomIndex = 0; callerRandomIndex <= 2; callerRandomIndex++)
            {
                Assert.False(VisibleDx8D13AutomaticSkillSelectors.TryGetSelector(
                    family,
                    callerRandomIndex,
                    out var selector));
                Assert.Equal((ushort)0, selector);
            }
        }

        Assert.False(VisibleDx8D13AutomaticSkillSelectors.TryGetSelector(
            int.MinValue,
            0,
            out var belowRangeSelector));
        Assert.Equal((ushort)0, belowRangeSelector);
        Assert.False(VisibleDx8D13AutomaticSkillSelectors.TryGetSelector(
            int.MaxValue,
            0,
            out var aboveRangeSelector));
        Assert.Equal((ushort)0, aboveRangeSelector);
    }

    [Theory]
    [MemberData(nameof(OutOfRangeCallerRandomIndexes))]
    public void FamilyLookupRejectsRandomValuesOutsideTheCallerZeroToTwoDomain(
        int callerRandomIndex)
    {
        foreach (var family in ExactFamilies)
        {
            Assert.False(VisibleDx8D13AutomaticSkillSelectors.TryGetSelector(
                family,
                callerRandomIndex,
                out var selector));
            Assert.Equal((ushort)0, selector);
        }
    }
}
