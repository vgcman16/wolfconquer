using WolfConquer.Core.World;
using Job = WolfConquer.Core.World.LegacyBossHarmRankPackageJob;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankPackageTargetTests
{
    [Theory]
    [MemberData(nameof(ExtendedRewardStages))]
    public void ActualClaimPlanKeepsJobItemsPointsAndExactCapacityAtEachTargetStage(long classCode, Job job)
    {
        var items = RankOneUniversal().Concat(RankOneExtra(job)).ToArray();

        Assert.True(LegacyBossHarmRankPackageClaimPlan.TryCreate(1, classCode, items.Length, out var plan));

        Assert.True(plan.CanClaim);
        Assert.Equal(LegacyBossHarmRankPackageClaimPlanStatus.Success, plan.Status);
        Assert.Equal(1, plan.Rank);
        Assert.Equal(1000u, plan.ClassExperience);
        Assert.Equal(items, plan.ItemGrants);
        Assert.Equal(items.Length, plan.RequiredInventorySlots);
        Assert.Equal(items.Length, plan.AvailableInventorySlots);
        Assert.Equal(RankOnePoints(job), (plan.NinjaPoints, plan.PirateAirPowerPoints, plan.PirateExperience));

        Assert.False(LegacyBossHarmRankPackageClaimPlan.TryCreate(1, classCode, items.Length - 1, out var full));
        Assert.False(full.CanClaim);
        Assert.Equal(LegacyBossHarmRankPackageClaimPlanStatus.InventoryFull, full.Status);
        Assert.Equal(items.Length, full.RequiredInventorySlots);
        Assert.Equal(items.Length - 1, full.AvailableInventorySlots);
        Assert.Equal(items, full.ItemGrants); // Proposed data is not a committed grant or success.
        Assert.Equal(RankOnePoints(job), (full.NinjaPoints, full.PirateAirPowerPoints, full.PirateExperience));
    }

    [Theory]
    [MemberData(nameof(UniversalOnlyClasses))]
    public void ActualClaimPlanRetainsUniversalOnlyBehaviorForOtherClasses(long classCode)
    {
        Assert.True(LegacyBossHarmRankPackageClaimPlan.TryCreate(1, classCode, 6, out var plan));

        Assert.True(plan.CanClaim);
        Assert.Equal(1000u, plan.ClassExperience);
        Assert.Equal(RankOneUniversal(), plan.ItemGrants);
        Assert.Equal(6, plan.RequiredInventorySlots);
        Assert.Equal((0u, 0u, 0u), (plan.NinjaPoints, plan.PirateAirPowerPoints, plan.PirateExperience));
    }

    [Theory]
    [InlineData(7051L, 8u)] [InlineData(7052L, 8u)] [InlineData(7057L, 8u)]
    [InlineData(7051L, 9u)] [InlineData(7052L, 9u)] [InlineData(7057L, 9u)]
    public void ExtendedPiratesDoNotInventAnAbsentRankEightOrNineJobReward(long classCode, uint rank)
    {
        Assert.True(LegacyBossHarmRankPackageClaimPlan.TryCreate(rank, classCode, 4, out var plan));

        Assert.True(plan.CanClaim);
        Assert.Equal(new[] { Item(3_324_226, 1), Item(4_050_001, 50), Item(4_040_001, 50), Item(3_009_003, 1) }, plan.ItemGrants);
        Assert.Equal(100u, plan.ClassExperience);
        Assert.Equal((0u, 0u, 0u), (plan.NinjaPoints, plan.PirateAirPowerPoints, plan.PirateExperience));
        Assert.Equal(4, plan.RequiredInventorySlots);
    }
}
