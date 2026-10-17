using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClassPkWarRewardSeparationTests
{
    [Fact]
    public void OldMsgClassPkWarRewardMetadataIsSeparateFromClassPoleWarNpcReward()
    {
        Assert.Equal(500_000U, LegacyClassPkWarState.RewardConquerPoints);
        Assert.Equal(4, LegacyClassPkWarState.RewardPowerExpBallStack);
        Assert.Equal(300_000, ReadClassPoleWarNpcTunedConquerPoints());
    }

    [Fact]
    public void ClassPoleWarNpcRewardDoesNotPayOldMsgClassPkWarReward()
    {
        var classPoleWarRewardSource = ReadRepositoryFile(
            "src",
            "WolfConquer.Core",
            "World",
            "WorldPacketHandlers.NpcEntrypoints.WarEvents.ClassPoleRewards.cs");

        Assert.Contains("TunedLegacyClassPoleWarConquerPointsReward = 300_000", classPoleWarRewardSource, StringComparison.Ordinal);
        Assert.Contains("LegacyClassPoleWarSourceConquerPointsReward = 3_000_000", classPoleWarRewardSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RewardConquerPoints = 500_000", classPoleWarRewardSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RewardPowerExpBallStack", classPoleWarRewardSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PowerExpBall", classPoleWarRewardSource, StringComparison.Ordinal);
    }

    private static int ReadClassPoleWarNpcTunedConquerPoints()
    {
        var classPoleWarRewardSource = ReadRepositoryFile(
            "src",
            "WolfConquer.Core",
            "World",
            "WorldPacketHandlers.NpcEntrypoints.WarEvents.ClassPoleRewards.cs");
        return classPoleWarRewardSource.Contains(
            "TunedLegacyClassPoleWarConquerPointsReward = 300_000",
            StringComparison.Ordinal)
            ? 300_000
            : throw new InvalidOperationException("ClassPoleWar NPC tuned reward constant was not found.");
    }

    private static string ReadRepositoryFile(params string[] relativePath)
    {
        var repositoryRoot = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine([repositoryRoot, .. relativePath]));
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src", "WolfConquer.Core")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the WolfConquer repository root.");
    }
}
