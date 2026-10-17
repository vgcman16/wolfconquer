using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBossHarmRankPackagePolicyTests
{
    [Fact]
    public void TryGetPackageReturnsRank1SourceItemsAndJobRewards()
    {
        var found = LegacyBossHarmRankPackagePolicy.TryGetPackage(1, out var reward);

        Assert.True(found);
        Assert.Equal(1, reward.Rank);
        Assert.Equal(1_000U, reward.ClassExperience);
        Assert.Equal(
            new[]
            {
                Item(3_324_226, 1),
                Item(4_050_001, 200),
                Item(4_040_001, 200),
                Item(3_009_003, 5),
                Item(3_316_793, 10),
                Item(3_330_770, 10)
            },
            reward.Items);

        Assert.True(LegacyBossHarmRankPackagePolicy.TryGetJobReward(
            reward,
            LegacyBossHarmRankPackageJob.Warrior,
            out var warriorReward));
        Assert.Equal(new[] { Item(3_333_403, 5) }, warriorReward.Items);

        Assert.True(LegacyBossHarmRankPackagePolicy.TryGetJobReward(
            reward,
            LegacyBossHarmRankPackageJob.Ninja,
            out var ninjaReward));
        Assert.Equal(50U, ninjaReward.NinjaPoints);

        Assert.True(LegacyBossHarmRankPackagePolicy.TryGetJobReward(
            reward,
            LegacyBossHarmRankPackageJob.Archer,
            out var archerReward));
        Assert.Equal(new[] { Item(3_335_128, 10) }, archerReward.Items);

        Assert.True(LegacyBossHarmRankPackagePolicy.TryGetJobReward(
            reward,
            LegacyBossHarmRankPackageJob.Trojan,
            out var trojanReward));
        Assert.Equal(new[] { Item(3_321_098, 200), Item(3_321_107, 200) }, trojanReward.Items);

        Assert.True(LegacyBossHarmRankPackagePolicy.TryGetJobReward(
            reward,
            LegacyBossHarmRankPackageJob.Pirate,
            out var pirateReward));
        Assert.Equal(50U, pirateReward.PirateAirPowerPoints);
        Assert.Equal(50U, pirateReward.PirateExperience);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    public void TryGetPackagePreservesRank8And9MissingPirateBranch(int rank)
    {
        var found = LegacyBossHarmRankPackagePolicy.TryGetPackage(rank, out var reward);

        Assert.True(found);
        Assert.Equal(
            new[]
            {
                Item(3_324_226, 1),
                Item(4_050_001, 50),
                Item(4_040_001, 50),
                Item(3_009_003, 1)
            },
            reward.Items);
        Assert.False(reward.HasPirateReward);
        Assert.False(LegacyBossHarmRankPackagePolicy.TryGetJobReward(
            reward,
            LegacyBossHarmRankPackageJob.Pirate,
            out var pirateReward));
        Assert.Equal(default, pirateReward);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void TryGetPackageRejectsRanksNotHandledByMailboxFlagProcess(int rank)
    {
        var found = LegacyBossHarmRankPackagePolicy.TryGetPackage(rank, out var reward);

        Assert.False(found);
        Assert.Equal(default, reward);
    }

    [Fact]
    public void TryGetPackagePreservesRank5ClassExperienceQuirk()
    {
        var found = LegacyBossHarmRankPackagePolicy.TryGetPackage(5, out var reward);

        Assert.True(found);
        Assert.Equal(1_000U, reward.ClassExperience);
        Assert.Equal(
            new[]
            {
                Item(3_324_226, 1),
                Item(4_050_001, 80),
                Item(4_040_001, 80),
                Item(3_009_003, 1)
            },
            reward.Items);
    }

    [Fact]
    public void TryGetPackagePreservesTrojanTwoDistinctItemBranches()
    {
        var found = LegacyBossHarmRankPackagePolicy.TryGetPackage(6, out var reward);

        Assert.True(found);
        Assert.True(LegacyBossHarmRankPackagePolicy.TryGetJobReward(
            reward,
            LegacyBossHarmRankPackageJob.Trojan,
            out var trojanReward));
        Assert.Equal(new[] { Item(3_321_098, 50), Item(3_321_107, 50) }, trojanReward.Items);
    }

    [Theory]
    [InlineData(1_000, LegacyBossHarmRankPackageJob.Trojan)]
    [InlineData(2_051, LegacyBossHarmRankPackageJob.Warrior)]
    [InlineData(4_000, LegacyBossHarmRankPackageJob.Archer)]
    [InlineData(5_013, LegacyBossHarmRankPackageJob.Ninja)]
    [InlineData(7_051, LegacyBossHarmRankPackageJob.Pirate)]
    [InlineData(6_000, LegacyBossHarmRankPackageJob.Other)]
    public void GetJobFromClassCodeMapsSourceJobFamilies(
        long classCode,
        LegacyBossHarmRankPackageJob expectedJob)
    {
        Assert.Equal(expectedJob, LegacyBossHarmRankPackagePolicy.GetJobFromClassCode(classCode));
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
