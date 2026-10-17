using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBossDeathRewardPolicyTests
{
    [Fact]
    public void TryGetConquerPointRewardReturnsDirectLastHitterReward()
    {
        var found = LegacyBossDeathRewardPolicy.TryGetConquerPointReward(
            familyId: 4_171,
            mapId: 1_015,
            boss: true,
            out var reward);

        Assert.True(found);
        Assert.Equal(4_171U, reward.FamilyId);
        Assert.Equal(1_015U, reward.MapId);
        Assert.True(reward.Boss);
        Assert.Equal(1, reward.InvocationCount);
        Assert.Equal(500, reward.PointsPerInvocation);
        Assert.Equal(500, reward.TotalConquerPoints);
    }

    [Fact]
    public void TryGetConquerPointRewardPreservesSourceOperatorPrecedence()
    {
        var found = LegacyBossDeathRewardPolicy.TryGetConquerPointReward(
            familyId: 20_060,
            mapId: 1_015,
            boss: false,
            out var reward);

        Assert.True(found);
        Assert.False(reward.Boss);
        Assert.Equal(1, reward.InvocationCount);
        Assert.Equal(500, reward.TotalConquerPoints);
    }

    [Fact]
    public void TryGetConquerPointRewardPreserves8311DoubleInvocationQuirk()
    {
        var found = LegacyBossDeathRewardPolicy.TryGetConquerPointReward(
            familyId: 8_311,
            mapId: 10_250,
            boss: true,
            out var reward);

        Assert.True(found);
        Assert.Equal(2, reward.InvocationCount);
        Assert.Equal(1_000, reward.TotalConquerPoints);
    }

    [Fact]
    public void TryGetConquerPointRewardIncludesDragonIslandFamily4220WithoutBossFlag()
    {
        var found = LegacyBossDeathRewardPolicy.TryGetConquerPointReward(
            familyId: 4_220,
            mapId: 10_137,
            boss: false,
            out var reward);

        Assert.True(found);
        Assert.Equal(1, reward.InvocationCount);
        Assert.Equal(500, reward.TotalConquerPoints);
    }

    [Fact]
    public void TryGetConquerPointRewardIncludesFamily4212BossReward()
    {
        var found = LegacyBossDeathRewardPolicy.TryGetConquerPointReward(
            familyId: 4_212,
            mapId: 1_015,
            boss: true,
            out var reward);

        Assert.True(found);
        Assert.Equal(1, reward.InvocationCount);
        Assert.Equal(500, reward.TotalConquerPoints);
    }

    [Fact]
    public void TryGetConquerPointRewardRejectsUnmappedBosses()
    {
        var found = LegacyBossDeathRewardPolicy.TryGetConquerPointReward(
            familyId: 20_055,
            mapId: 1_015,
            boss: true,
            out var reward);

        Assert.False(found);
        Assert.Equal(default, reward);
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
    public void TryGetHarmRankMailConquerPointRewardReturnsLegacyRankAmounts(int rank, long conquerPoints)
    {
        var found = LegacyBossDeathRewardPolicy.TryGetHarmRankMailConquerPointReward(
            rank,
            out var reward);

        Assert.True(found);
        Assert.Equal(rank, reward.Rank);
        Assert.Equal(conquerPoints, reward.ConquerPoints);
    }

    [Fact]
    public void TryGetHarmRankMailConquerPointRewardReturnsLegacyMailMetadata()
    {
        var found = LegacyBossDeathRewardPolicy.TryGetHarmRankMailConquerPointReward(
            rank: 7,
            out var reward);

        Assert.True(found);
        Assert.Equal(2_592_000, reward.ExpirationDate);
        Assert.Equal(100_000, reward.Money);
        Assert.Equal(0U, reward.ItemId);
        Assert.Equal(1, reward.EmoneyRecordType);
        Assert.Equal("STR_SYSTEM_NAME@@", reward.Title);
        Assert.Equal("STR_SYSTEM_NAME@@", reward.Sender);
        Assert.Equal("STR_SYSTEM_NAME@@", reward.Content);
        Assert.Equal(7, reward.Flag);
    }

    [Fact]
    public void TryGetHarmRankMailConquerPointRewardRejectsRank10()
    {
        var found = LegacyBossDeathRewardPolicy.TryGetHarmRankMailConquerPointReward(
            rank: 10,
            out var reward);

        Assert.False(found);
        Assert.Equal(default, reward);
        Assert.Equal(0, LegacyBossDeathRewardPolicy.GetHarmRankMailConquerPoints(10));
    }

    [Fact]
    public void TryGetStudyPointInnerPowerTeamRewardReturnsBossFamily4171Reward()
    {
        var found = LegacyBossDeathRewardPolicy.TryGetStudyPointInnerPowerTeamReward(
            familyId: 4_171,
            boss: true,
            out var reward);

        Assert.True(found);
        Assert.Equal(4_171U, reward.FamilyId);
        Assert.True(reward.Boss);
        Assert.Equal(1_000, reward.TeammateStudyPoints);
        Assert.Equal(1_000, reward.KillerInnerPowerPotency);
        Assert.Equal(1_000, reward.TeammateInnerPowerPotency);
    }

    [Fact]
    public void TryGetStudyPointInnerPowerTeamRewardRequiresBossOnlyForFamily4171()
    {
        var found4171 = LegacyBossDeathRewardPolicy.TryGetStudyPointInnerPowerTeamReward(
            familyId: 4_171,
            boss: false,
            out var family4171Reward);
        var found20060 = LegacyBossDeathRewardPolicy.TryGetStudyPointInnerPowerTeamReward(
            familyId: 20_060,
            boss: false,
            out var family20060Reward);
        var found8311 = LegacyBossDeathRewardPolicy.TryGetStudyPointInnerPowerTeamReward(
            familyId: 8_311,
            boss: false,
            out var family8311Reward);

        Assert.False(found4171);
        Assert.Equal(default, family4171Reward);
        Assert.True(found20060);
        Assert.False(family20060Reward.Boss);
        Assert.True(found8311);
        Assert.False(family8311Reward.Boss);
    }

    [Theory]
    [InlineData(4_171, true)]
    [InlineData(20_060, false)]
    [InlineData(8_311, false)]
    public void TryGetStudyPointInnerPowerHeroRewardGoalReturnsLegacyGoal709(uint familyId, bool boss)
    {
        var found = LegacyBossDeathRewardPolicy.TryGetStudyPointInnerPowerHeroRewardGoal(
            familyId,
            boss,
            out var goalId);

        Assert.True(found);
        Assert.Equal(709U, goalId);
        Assert.Equal(LegacyBossDeathRewardPolicy.StudyPointInnerPowerHeroRewardGoalId, goalId);
    }

    [Theory]
    [InlineData(4_171, false)]
    [InlineData(4_212, true)]
    [InlineData(20_055, true)]
    [InlineData(8_312, true)]
    public void TryGetStudyPointInnerPowerHeroRewardGoalRejectsNonLegacyBranch(uint familyId, bool boss)
    {
        var found = LegacyBossDeathRewardPolicy.TryGetStudyPointInnerPowerHeroRewardGoal(
            familyId,
            boss,
            out var goalId);

        Assert.False(found);
        Assert.Equal(0U, goalId);
    }

    [Fact]
    public void TryGetStudyPointTeamRewardReturnsFamily20055BossReward()
    {
        var found = LegacyBossDeathRewardPolicy.TryGetStudyPointTeamReward(
            familyId: 20_055,
            boss: true,
            out var reward);

        Assert.True(found);
        Assert.Equal(20_055U, reward.FamilyId);
        Assert.Equal(20, reward.KillerStudyPoints);
        Assert.Equal(20, reward.TeammateStudyPoints);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryGetStudyPointTeamRewardDoesNotIncludeOtherRewardGroups(bool boss)
    {
        var found = LegacyBossDeathRewardPolicy.TryGetStudyPointTeamReward(
            familyId: 4_171,
            boss,
            out var reward);

        Assert.False(found);
        Assert.Equal(default, reward);
    }

    [Fact]
    public void TryGetStudyPointTeamRewardRequiresBossFlag()
    {
        var found = LegacyBossDeathRewardPolicy.TryGetStudyPointTeamReward(
            familyId: 20_055,
            boss: false,
            out var reward);

        Assert.False(found);
        Assert.Equal(default, reward);
    }

    [Fact]
    public void FormatBossSystemMessageMatchesLegacyText()
    {
        var message = LegacyBossDeathRewardPolicy.FormatBossSystemMessage(
            monsterName: "TeratoDragon",
            killerName: "Alice",
            studyPoints: 20);

        Assert.Equal(
            "The TeratoDragon has been destroyed by the team Alice`s ! All team members received 20 Study Points!",
            message);
    }

    [Fact]
    public void FormatLastHitterConquerPointMessageMatchesLegacyText()
    {
        var message = LegacyBossDeathRewardPolicy.FormatLastHitterConquerPointMessage(
            monsterName: "SnowBanshee",
            conquerPoints: 500);

        Assert.Equal(
            "You have received 500 Conquer Points for being the last hitter of SnowBanshee!",
            message);
    }
}
