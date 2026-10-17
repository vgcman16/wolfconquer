using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyEagleEyeCapabilityTests
{
    [Fact]
    public void ClassifyEagleEyeDirectAttackAsDedicatedRuntimeSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.DirectAttack, 11_030);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("DirectAttack(48)", info.SortName);
        Assert.Equal("eagleeye-direct-attack-runtime", info.NextSeam);
        Assert.Contains("20-second countdown", info.Reason);
        Assert.Contains("width-zero in-line secondary-player", info.Reason);
        Assert.Contains("unscaled secondary damage", info.Reason);
        Assert.Contains("nested-before-shared-animation", info.Reason);
        Assert.Contains("Attack.cs case 11030", info.SourceEvidence);
        Assert.Contains("ThreadPool.cs clears EagleEyeCountDown", info.SourceEvidence);
    }
}
