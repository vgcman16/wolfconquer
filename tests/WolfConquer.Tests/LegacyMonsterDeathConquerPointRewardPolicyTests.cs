using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterDeathConquerPointRewardPolicyTests
{
    [Fact]
    public void NormalMonsterDeathRandomConquerPointTrickleIsOneToFiveByDefault()
    {
        var reward = LegacyBossDeathRewardPolicy.GetMonsterDeathRandomConquerPointReward(
            familyId: 85,
            mapId: 1_002,
            boss: false,
            onAutoHunt: false,
            vipLevel: 0);

        Assert.Equal(50, LegacyBossDeathRewardPolicy.TunedMonsterDeathRandomConquerPointRatePercent);
        Assert.Equal(LegacyMonsterDeathRandomConquerPointRewardBranch.NonBossVipLadder, reward.Branch);
        Assert.Equal(1, reward.MinimumInclusive);
        Assert.Equal(6, reward.MaximumExclusive);
        Assert.True(reward.SendsSystemMessage);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(-1)]
    public void MegalithKingNonBossDeathCapsSourceVipRandomConquerPointLadder(int vipLevel)
    {
        var reward = LegacyBossDeathRewardPolicy.GetMonsterDeathRandomConquerPointReward(
            LegacyBossDeathRewardPolicy.MegalithKingFamilyId,
            mapId: 1_002,
            boss: false,
            onAutoHunt: false,
            vipLevel: vipLevel);

        Assert.Equal(LegacyMonsterDeathRandomConquerPointRewardBranch.NonBossVipLadder, reward.Branch);
        Assert.Equal(1, reward.MinimumInclusive);
        Assert.Equal(LegacyBossDeathRewardPolicy.TunedNormalMonsterRandomConquerPointMaximumExclusive, reward.MaximumExclusive);
        Assert.Equal(5, reward.MaximumInclusive);
        Assert.True(reward.SendsSystemMessage);
        Assert.Equal(
            $"You`ve received {reward.MaximumInclusive} Conquer Points.",
            reward.FormatSystemMessage((uint)reward.MaximumInclusive));
    }

    [Theory]
    [InlineData(true, 1_002U, false)]
    [InlineData(false, 2_353U, false)]
    [InlineData(false, 1_002U, true)]
    public void MonsterDeathRandomConquerPointFallbackBranchUsesTunedOneToFiveRange(
        bool boss,
        uint mapId,
        bool onAutoHunt)
    {
        var reward = LegacyBossDeathRewardPolicy.GetMonsterDeathRandomConquerPointReward(
            LegacyBossDeathRewardPolicy.MegalithKingFamilyId,
            mapId,
            boss,
            onAutoHunt,
            vipLevel: 7);

        Assert.Equal(LegacyMonsterDeathRandomConquerPointRewardBranch.Fallback, reward.Branch);
        Assert.Equal(1, reward.MinimumInclusive);
        Assert.Equal(
            LegacyBossDeathRewardPolicy.TunedFallbackMonsterDeathRandomConquerPointMaximumExclusive,
            reward.MaximumExclusive);
        Assert.Equal(5, reward.MaximumInclusive);
        Assert.False(reward.SendsSystemMessage);
        Assert.Null(reward.FormatSystemMessage(5));
    }

    [Fact]
    public void MonsterDeathRandomConquerPointFallbackPreservesWideRangeAsEvidenceOnly()
    {
        Assert.Equal(500, LegacyBossDeathRewardPolicy.MonsterDeathFallbackRandomConquerPointMaximumExclusive);
        Assert.Equal(
            LegacyBossDeathRewardPolicy.TunedNormalMonsterRandomConquerPointMaximumExclusive,
            LegacyBossDeathRewardPolicy.TunedFallbackMonsterDeathRandomConquerPointMaximumExclusive);
    }

    [Theory]
    [InlineData(LegacyBossDeathRewardPolicy.DeityLandBossRewardMapId, 85U, true, 1)]
    [InlineData(LegacyBossDeathRewardPolicy.DragonIslandBossRewardMapId, 4_220U, false, 1)]
    [InlineData(1_015U, 4_171U, true, 1)]
    [InlineData(1_002U, 20_060U, false, 1)]
    [InlineData(1_002U, 8_311U, false, 1)]
    [InlineData(1_002U, 4_212U, true, 1)]
    [InlineData(LegacyBossDeathRewardPolicy.DeityLandBossRewardMapId, 4_171U, true, 2)]
    public void DirectBossAndSpecialConquerPointRewardsStayBounded(
        uint mapId,
        uint familyId,
        bool boss,
        int expectedInvocationCount)
    {
        var hasDirectReward = LegacyBossDeathRewardPolicy.TryGetConquerPointReward(
            familyId,
            mapId,
            boss,
            out var reward);

        Assert.True(hasDirectReward);
        Assert.Equal(expectedInvocationCount, reward.InvocationCount);
        Assert.Equal(LegacyBossDeathRewardPolicy.LastHitterBossConquerPoints, reward.PointsPerInvocation);
        Assert.Equal(expectedInvocationCount * 500, reward.TotalConquerPoints);
        Assert.InRange(reward.TotalConquerPoints, 500, 1_000);
    }

    [Fact]
    public void DragonIslandFamily4220KeepsRandomDeathRewardSeparateFromDirectBossReward()
    {
        var randomReward = LegacyBossDeathRewardPolicy.GetMonsterDeathRandomConquerPointReward(
            familyId: 4_220,
            mapId: LegacyBossDeathRewardPolicy.DragonIslandBossRewardMapId,
            boss: false,
            onAutoHunt: false,
            vipLevel: 4);
        var hasDirectReward = LegacyBossDeathRewardPolicy.TryGetConquerPointReward(
            familyId: 4_220,
            mapId: LegacyBossDeathRewardPolicy.DragonIslandBossRewardMapId,
            boss: false,
            out var directReward);

        Assert.Equal(LegacyMonsterDeathRandomConquerPointRewardBranch.NonBossVipLadder, randomReward.Branch);
        Assert.Equal(1, randomReward.MinimumInclusive);
        Assert.Equal(6, randomReward.MaximumExclusive);
        Assert.Equal(5, randomReward.MaximumInclusive);
        Assert.True(hasDirectReward);
        Assert.Equal(1, directReward.InvocationCount);
        Assert.Equal(500, directReward.TotalConquerPoints);
    }

    [Fact]
    public void BossFamily4171KeepsFallbackRandomDeathRewardSeparateFromDirectBossReward()
    {
        var randomReward = LegacyBossDeathRewardPolicy.GetMonsterDeathRandomConquerPointReward(
            familyId: 4_171,
            mapId: 1_015,
            boss: true,
            onAutoHunt: false,
            vipLevel: 7);
        var hasDirectReward = LegacyBossDeathRewardPolicy.TryGetConquerPointReward(
            familyId: 4_171,
            mapId: 1_015,
            boss: true,
            out var directReward);

        Assert.Equal(LegacyMonsterDeathRandomConquerPointRewardBranch.Fallback, randomReward.Branch);
        Assert.Equal(1, randomReward.MinimumInclusive);
        Assert.Equal(6, randomReward.MaximumExclusive);
        Assert.Equal(5, randomReward.MaximumInclusive);
        Assert.False(randomReward.SendsSystemMessage);
        Assert.True(hasDirectReward);
        Assert.Equal(1, directReward.InvocationCount);
        Assert.Equal(500, directReward.TotalConquerPoints);
    }
}
