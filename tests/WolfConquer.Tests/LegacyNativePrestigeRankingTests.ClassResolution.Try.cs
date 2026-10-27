using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

// New-API-dependent tests: omit this file only from Root's tests-only baseline;
// include it in the complete corrected-source selection. No fake API stub.
public sealed partial class LegacyNativePrestigeRankingTests
{
    public static IEnumerable<object[]> ClassResolutionTryKnown() => PrestigeTargetClassFixture.FamilyEdges();
    public static IEnumerable<object[]> ClassResolutionTryUnsupported() => PrestigeTargetClassFixture.StrictRefusals();

    [Theory]
    [MemberData(nameof(ClassResolutionTryKnown))]
    public void ClassResolutionTryReturnsTheKnownTargetFamily(uint characterClass, uint expected)
    {
        Assert.True(Ranking.TryGetIndex(characterClass, out var category));
        Assert.Equal((Ranking.Type)expected, category);
    }

    [Theory]
    [MemberData(nameof(ClassResolutionTryUnsupported))]
    public void ClassResolutionTryDoesNotSupplyAWaterCategoryForWorldOnlyOrUnsupported(uint characterClass)
    {
        Assert.False(Ranking.TryGetIndex(characterClass, out var category));
        Assert.Equal(Ranking.Type.Count, category);
    }
}
