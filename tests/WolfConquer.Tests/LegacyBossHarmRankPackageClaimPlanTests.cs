using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBossHarmRankPackageClaimPlanTests
{
    [Fact]
    public void TryCreateBuildsSuccessfulTrojanPlanWithUniversalAndJobItemStacks()
    {
        var created = LegacyBossHarmRankPackageClaimPlan.TryCreate(
            rankFlag: 1,
            classCode: 1_000,
            availableInventorySlots: 8,
            out var plan);

        Assert.True(created);
        Assert.True(plan.CanClaim);
        Assert.Equal(LegacyBossHarmRankPackageClaimPlanStatus.Success, plan.Status);
        Assert.Equal(1, plan.Rank);
        Assert.Equal(1_000U, plan.ClassExperience);
        Assert.Equal(8, plan.RequiredInventorySlots);
        Assert.Equal(
            new[]
            {
                Item(3_324_226, 1),
                Item(4_050_001, 200),
                Item(4_040_001, 200),
                Item(3_009_003, 5),
                Item(3_316_793, 10),
                Item(3_330_770, 10),
                Item(3_321_098, 200),
                Item(3_321_107, 200)
            },
            plan.ItemGrants);
    }

    [Fact]
    public void TryCreateCountsItemStacksRatherThanItemQuantitiesForInventorySlots()
    {
        var created = LegacyBossHarmRankPackageClaimPlan.TryCreate(
            rankFlag: 6,
            classCode: 4_000,
            availableInventorySlots: 5,
            out var plan);

        Assert.True(created);
        Assert.Equal(5, plan.RequiredInventorySlots);
        Assert.Equal(
            new[]
            {
                Item(3_324_226, 1),
                Item(4_050_001, 50),
                Item(4_040_001, 50),
                Item(3_009_003, 1),
                Item(3_335_128, 1)
            },
            plan.ItemGrants);
    }

    [Fact]
    public void TryCreateDoesNotRequireSlotsForNinjaPoints()
    {
        var created = LegacyBossHarmRankPackageClaimPlan.TryCreate(
            rankFlag: 1,
            classCode: 5_013,
            availableInventorySlots: 6,
            out var plan);

        Assert.True(created);
        Assert.Equal(6, plan.RequiredInventorySlots);
        Assert.Equal(50U, plan.NinjaPoints);
        Assert.Equal(1_000U, plan.ClassExperience);
        Assert.Equal(6, plan.ItemGrants.Count);
    }

    [Fact]
    public void TryCreateDoesNotRequireSlotsForPiratePointsOrPirateExperience()
    {
        var created = LegacyBossHarmRankPackageClaimPlan.TryCreate(
            rankFlag: 1,
            classCode: 7_051,
            availableInventorySlots: 6,
            out var plan);

        Assert.True(created);
        Assert.Equal(6, plan.RequiredInventorySlots);
        Assert.Equal(50U, plan.PirateAirPowerPoints);
        Assert.Equal(50U, plan.PirateExperience);
        Assert.Equal(6, plan.ItemGrants.Count);
    }

    [Fact]
    public void TryCreateFailsBagFullWhenAvailableSlotsAreInsufficient()
    {
        var created = LegacyBossHarmRankPackageClaimPlan.TryCreate(
            rankFlag: 1,
            classCode: 2_000,
            availableInventorySlots: 6,
            out var plan);

        Assert.False(created);
        Assert.False(plan.CanClaim);
        Assert.Equal(LegacyBossHarmRankPackageClaimPlanStatus.InventoryFull, plan.Status);
        Assert.Equal(7, plan.RequiredInventorySlots);
        Assert.Equal(6, plan.AvailableInventorySlots);
        Assert.Equal(7, plan.ItemGrants.Count);
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(10U)]
    public void TryCreateRejectsInvalidRankFlags(uint rankFlag)
    {
        var created = LegacyBossHarmRankPackageClaimPlan.TryCreate(
            rankFlag,
            classCode: 1_000,
            availableInventorySlots: 99,
            out var plan);

        Assert.False(created);
        Assert.False(plan.CanClaim);
        Assert.Equal(LegacyBossHarmRankPackageClaimPlanStatus.InvalidRank, plan.Status);
        Assert.Equal(0, plan.Rank);
        Assert.Empty(plan.ItemGrants);
    }

    private static LegacyBossHarmRankPackageItem Item(uint itemId, uint quantity)
    {
        return new LegacyBossHarmRankPackageItem(
            itemId,
            Plus: 0,
            Quantity: quantity,
            Bound: false,
            SocketProgress: 0);
    }
}
