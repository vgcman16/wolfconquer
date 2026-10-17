using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterDeathEconomyTests
{
    [Theory]
    [InlineData(1_011, 11_007)]
    [InlineData(1_020, 11)]
    public void Map1011And1020RandomConquerPointTrickleIsTinyByDefault(
        int mapId,
        int familyId)
    {
        var reward = LegacyBossDeathRewardPolicy.GetMonsterDeathRandomConquerPointReward(
            checked((uint)familyId),
            checked((uint)mapId),
            boss: false,
            onAutoHunt: false,
            vipLevel: 7);

        Assert.True(LegacyBossDeathRewardPolicy.IsTunedMap1011Or1020RandomConquerPointMap((uint)mapId));
        Assert.Equal(LegacyMonsterDeathRandomConquerPointRewardBranch.NonBossVipLadder, reward.Branch);
        Assert.Equal(LegacyBossDeathRewardPolicy.MonsterDeathRandomConquerPointMinimumInclusive, reward.MinimumInclusive);
        Assert.Equal(LegacyBossDeathRewardPolicy.TunedNormalMonsterRandomConquerPointMaximumExclusive, reward.MaximumExclusive);
        Assert.Equal(5, reward.MaximumInclusive);
        Assert.Equal("You`ve received 5 Conquer Points.", reward.FormatSystemMessage(5));
    }

    [Fact]
    public void NormalMonsterRandomConquerPointTrickleIsOnByDefault()
    {
        Assert.Equal(50, LegacyBossDeathRewardPolicy.TunedMonsterDeathRandomConquerPointRatePercent);
    }

    [Fact]
    public void NonTunedMapsShareTheSameOneToFiveNormalMonsterCap()
    {
        var reward = LegacyBossDeathRewardPolicy.GetMonsterDeathRandomConquerPointReward(
            familyId: 85,
            mapId: 1_002,
            boss: false,
            onAutoHunt: false,
            vipLevel: 7);

        Assert.False(LegacyBossDeathRewardPolicy.IsTunedMap1011Or1020RandomConquerPointMap(1_002));
        Assert.Equal(LegacyBossDeathRewardPolicy.TunedNormalMonsterRandomConquerPointMaximumExclusive, reward.MaximumExclusive);
        Assert.Equal(5, reward.MaximumInclusive);
    }

    [Theory]
    [InlineData(true, 1_002U, false)]
    [InlineData(false, LegacyBossDeathRewardPolicy.MonsterDeathRandomConquerPointExcludedMapId, false)]
    [InlineData(false, 1_002U, true)]
    public void FallbackMonsterDeathConquerPointBranchStaysUnderTunedTrickleCap(
        bool boss,
        uint mapId,
        bool onAutoHunt)
    {
        var reward = LegacyBossDeathRewardPolicy.GetMonsterDeathRandomConquerPointReward(
            familyId: 85,
            mapId,
            boss,
            onAutoHunt,
            vipLevel: 7);

        Assert.Equal(LegacyMonsterDeathRandomConquerPointRewardBranch.Fallback, reward.Branch);
        Assert.Equal(
            LegacyBossDeathRewardPolicy.TunedFallbackMonsterDeathRandomConquerPointMaximumExclusive,
            reward.MaximumExclusive);
        Assert.InRange(reward.MaximumInclusive, 1, 5);
        Assert.False(reward.SendsSystemMessage);
    }

    [Theory]
    [InlineData(10_250, 8_311, true)]
    [InlineData(10_250, 4_171, true)]
    [InlineData(10_137, 4_220, false)]
    [InlineData(1_015, 4_212, true)]
    public void DirectBossConquerPointRewardsStayUnderTunedPerDeathCap(
        int mapId,
        int familyId,
        bool boss)
    {
        var found = LegacyBossDeathRewardPolicy.TryGetConquerPointReward(
            checked((uint)familyId),
            checked((uint)mapId),
            boss,
            out var reward);

        Assert.True(found);
        Assert.Equal(
            LegacyBossDeathRewardPolicy.GetLastHitterBossConquerPointsPerInvocation(reward.InvocationCount),
            reward.PointsPerInvocation);
        Assert.InRange(
            reward.TotalConquerPoints,
            1,
            LegacyBossDeathRewardPolicy.LastHitterBossConquerPointMaximumPerDeath);
    }

    [Fact]
    public void FutureBossBranchesShareTheSamePerDeathCap()
    {
        Assert.Equal(500, LegacyBossDeathRewardPolicy.GetLastHitterBossConquerPointsPerInvocation(1));
        Assert.Equal(500, LegacyBossDeathRewardPolicy.GetLastHitterBossConquerPointsPerInvocation(2));
        Assert.Equal(333, LegacyBossDeathRewardPolicy.GetLastHitterBossConquerPointsPerInvocation(3));
        Assert.Equal(0, LegacyBossDeathRewardPolicy.GetLastHitterBossConquerPointsPerInvocation(0));
    }

    [Theory]
    [InlineData(1, 1_000)]
    [InlineData(2, 800)]
    [InlineData(3, 600)]
    [InlineData(4, 500)]
    [InlineData(5, 400)]
    [InlineData(6, 300)]
    [InlineData(7, 200)]
    [InlineData(8, 100)]
    [InlineData(9, 100)]
    public void HarmRankMailConquerPointRewardsStayUnderTunedCap(int rank, long expectedConquerPoints)
    {
        var found = LegacyBossDeathRewardPolicy.TryGetHarmRankMailConquerPointReward(rank, out var reward);

        Assert.True(found);
        Assert.Equal(expectedConquerPoints, reward.ConquerPoints);
        Assert.InRange(reward.ConquerPoints, 1, LegacyBossDeathRewardPolicy.HarmRankMailConquerPointMaximum);
    }

    [Fact]
    public void SourceEvidenceAmountsDoNotInflateTunedBossAndMailPayouts()
    {
        var foundBossReward = LegacyBossDeathRewardPolicy.TryGetConquerPointReward(
            familyId: 8_311,
            mapId: LegacyBossDeathRewardPolicy.DeityLandBossRewardMapId,
            boss: true,
            out var bossReward);
        var foundMailReward = LegacyBossDeathRewardPolicy.TryGetHarmRankMailConquerPointReward(
            rank: 1,
            out var mailReward);

        Assert.True(foundBossReward);
        Assert.Equal(500_000, LegacyBossDeathRewardPolicy.SourceLastHitterBossConquerPoints);
        Assert.Equal(LegacyBossDeathRewardPolicy.LastHitterBossConquerPointMaximumPerDeath, bossReward.TotalConquerPoints);
        Assert.True(bossReward.TotalConquerPoints < LegacyBossDeathRewardPolicy.SourceLastHitterBossConquerPoints);

        Assert.True(foundMailReward);
        Assert.Equal(10_000_000, LegacyBossDeathRewardPolicy.SourceHarmRankMailMoney);
        Assert.Equal(LegacyBossDeathRewardPolicy.HarmRankMailMoney, mailReward.Money);
        Assert.True(mailReward.Money < LegacyBossDeathRewardPolicy.SourceHarmRankMailMoney);
        Assert.Equal(0U, mailReward.ItemId);
    }
}
