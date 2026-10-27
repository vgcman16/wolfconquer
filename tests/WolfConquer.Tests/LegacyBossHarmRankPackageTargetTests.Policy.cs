using WolfConquer.Core.World;
using Job = WolfConquer.Core.World.LegacyBossHarmRankPackageJob;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankPackageTargetTests
{
    [Fact]
    public void EveryOldRewardFamilyMemberKeepsItsJobBeforeTheSixNewTargetStages()
    {
        foreach (var family in TargetRewardFamilies)
        for (long job = family.First; job <= family.Last - 6; job++)
            Assert.Equal(family.Job, LegacyBossHarmRankPackagePolicy.GetJobFromClassCode(job));
    }

    [Theory]
    [MemberData(nameof(ExtendedRewardStages))]
    public void AllFiveKnownTargetFamiliesResolveEachStage52Through57AndKeepExactRankOneRewards(long classCode, Job expectedJob)
    {
        Assert.Equal(expectedJob, LegacyBossHarmRankPackagePolicy.GetJobFromClassCode(classCode));
        Assert.True(LegacyBossHarmRankPackagePolicy.TryGetPackage(1, out var package));
        Assert.Equal(RankOneUniversal(), package.Items);
        Assert.Equal(1000u, package.ClassExperience);

        Assert.True(LegacyBossHarmRankPackagePolicy.TryGetJobReward(package,
            LegacyBossHarmRankPackagePolicy.GetJobFromClassCode(classCode), out var reward));

        Assert.Equal(expectedJob, reward.Job);
        Assert.Equal(RankOneExtra(expectedJob), reward.Items);
        Assert.Equal(RankOnePoints(expectedJob), (reward.NinjaPoints, reward.PirateAirPowerPoints, reward.PirateExperience));
    }

    [Theory]
    [MemberData(nameof(UniversalOnlyClasses))]
    public void UnsupportedAndUnconfiguredFamiliesKeepOtherWithoutInventedJobRewards(long classCode)
    {
        Assert.Equal(Job.Other, LegacyBossHarmRankPackagePolicy.GetJobFromClassCode(classCode));
        Assert.True(LegacyBossHarmRankPackagePolicy.TryGetPackage(1, out var package));
        Assert.False(LegacyBossHarmRankPackagePolicy.TryGetJobReward(package, Job.Other, out var reward));
        Assert.Equal(default, reward);
        Assert.Equal(RankOneUniversal(), package.Items);
    }
}
