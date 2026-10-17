using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyStrike12600CapabilityTests
{
    [Fact]
    public void ClassifyStrike12600AsDedicatedRuntimeSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Strike, 12_600);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("Strike(81)", info.SortName);
        Assert.Equal("strike-12600-combat-runtime", info.NextSeam);
        Assert.Contains("post-soul monster *2", info.Reason);
        Assert.Contains("player *2/3", info.Reason);
        Assert.Contains("SOB *4", info.Reason);
        Assert.Contains("Strike.cs case 12600", info.SourceEvidence);
    }
}
