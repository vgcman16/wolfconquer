namespace WolfConquer.Core.World;

public static class VisibleDx8D13AutomaticSkillSelectors
{
    private const int CallerRandomIndexUpperBound = 2;
    private const ushort SelectorStep = 10;

    // FUN_00AC834C supplies each base; FUN_00ACD62B adds the normalized index times ten.
    private static readonly FamilyDefinition[] FamilyDefinitions =
    [
        new(2, 12_740, 3),
        new(6, 12_580, 3),
        new(7, 14_570, 2),
        new(8, 12_210, 3),
        new(9, 15_670, 3),
        new(16, 13_040, 3),
    ];

    private static readonly IReadOnlyList<ushort> ExactSelectorValues = Array.AsReadOnly<ushort>(
    [
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
    ]);

    private static readonly HashSet<ushort> KnownSelectorValues = [.. ExactSelectorValues];

    public static IReadOnlyList<ushort> ExactSelectors => ExactSelectorValues;

    public static bool TryGetSelector(
        int family,
        int callerRandomIndex,
        out ushort selector)
    {
        selector = 0;
        if (callerRandomIndex is < 0 or > CallerRandomIndexUpperBound)
        {
            return false;
        }

        foreach (var definition in FamilyDefinitions)
        {
            if (definition.Family != family)
            {
                continue;
            }

            var normalizedIndex = callerRandomIndex % definition.SelectorCount;
            selector = checked((ushort)(definition.BaseSelector + (normalizedIndex * SelectorStep)));
            return true;
        }

        return false;
    }

    public static bool IsKnownSelector(uint selector)
    {
        return selector <= ushort.MaxValue && KnownSelectorValues.Contains((ushort)selector);
    }

    private readonly record struct FamilyDefinition(
        int Family,
        ushort BaseSelector,
        int SelectorCount);
}
