using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("LegacySuperGuildWarRuntime")]
public sealed partial class LegacySuperGuildWarClaimSafetyTests
{
    [Theory]
    [InlineData(1UL, "cp-overflow")]
    [InlineData(1UL, "money-overflow")]
    [InlineData(1UL, "durability")]
    [InlineData(1UL, "name")]
    [InlineData(1UL, "guild-name")]
    [InlineData(2UL, "attributes")]
    [InlineData(2UL, "name")]
    [InlineData(2UL, "guild-name")]
    public async Task RegisteredPreparationRefusalPreservesSameUidAndRankBucketForRealRetry(ulong rank, string fault)
    {
        using var fixture = new Fixture(rank);
        fixture.IntroduceFault(fault);
        var refused = await fixture.ClickAsync(0);

        // No global reset/reseed. Repair only the bad fixture precondition, then
        // spend one of the same two entitlements through a different real owner.
        fixture.RepairFault(fault);
        var other = await fixture.ClickAsync(1);
        AssertAward(other, rank);
        var retried = await fixture.ClickAsync(0);
        // On the old body this cannot be a new award: TryClaim already consumed
        // this UID even when its preparation never produced an output prefix.
        AssertAward(retried, rank);
        AssertNoPreparationEffects(refused);
        Assert.Null(refused.Failure);
        Assert.Contains("SuperGuildWar reward preparation rejected", refused.Log);
        Assert.DoesNotContain("clicked NPC is not currently visible", refused.Log);

        var exhausted = await fixture.ClickAsync(2);
        AssertDialogRefusal(exhausted,
            "There are no Super Guild War rewards left for your rank.");
        Assert.Contains("reward exhausted", exhausted.Log);
    }

    [Theory]
    [InlineData(1UL, 38, true)]
    [InlineData(1UL, 39, false)]
    [InlineData(2UL, 36, true)]
    [InlineData(2UL, 37, false)]
    public async Task RegisteredSourceCapacityTwoOrFourRemainsUnchanged(ulong rank, int rows, bool allowed)
    {
        using var fixture = new Fixture(rank, rows);
        var result = await fixture.ClickAsync(0);
        if (allowed) AssertAward(result, rank);
        else
        {
            AssertDialogRefusal(result, rank == 1
                ? "Please make one more space in your inventory."
                : "Please make 4 more spaces in your inventory.");
            Assert.Contains(rank == 1 ? "sourceRequiredSpace=2" : "sourceRequiredSpace=4", result.Log);
        }
    }
}
