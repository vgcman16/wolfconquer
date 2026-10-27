using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClassOwnedSpellTargetTests
{
    public static IEnumerable<object[]> TargetLineages()
    {
        for (long stage = 1052; stage <= 1057; stage++)
        {
            yield return [stage, 0L, 0L];
            yield return [2000L, stage, 0L];
            yield return [2000L, 0L, stage];
        }
    }

    [Theory]
    [MemberData(nameof(TargetLineages))]
    public void ConfirmedTrojanStagesSelectOnlyTheExistingCycloneForEachLineageSlot(long current, long first, long second) =>
        Assert.Equal(new ushort[] { 1110 }, LegacyClassOwnedSpellGrantProfile.GetSpellIds(current, first, second));

    [Fact]
    public void SeveralMatchingLineageSlotsStillSelectOneGrantInItsExistingOrder() =>
        Assert.Equal(new ushort[] { 1110 }, LegacyClassOwnedSpellGrantProfile.GetSpellIds(1052, 1053, 1057));

    [Fact]
    public void UnsupportedAndOtherFamiliesCannotAcquireCycloneWithoutAKnownTrojanLineage()
    {
        var inputs = new long[] { long.MinValue, -1, 0, 999, 1058, long.MaxValue }
            .Concat(PrestigeTargetClassFixture.ObservedUnmodeled.Select(id => (long)id))
            .Concat(PrestigeTargetClassFixture.WorldOnly.Select(id => (long)id))
            .Concat(PrestigeTargetClassFixture.Families.Where(row => row.Category != 1).Select(row => (long)row.Last));
        foreach (long job in inputs.Distinct())
        {
            Assert.Empty(LegacyClassOwnedSpellGrantProfile.GetSpellIds(job, 0, 0));
            Assert.Empty(LegacyClassOwnedSpellGrantProfile.GetSpellIds(2000, job, 0));
            Assert.Empty(LegacyClassOwnedSpellGrantProfile.GetSpellIds(2000, 0, job));
        }
    }
}
