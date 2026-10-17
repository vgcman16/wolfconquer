using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMountSkillCapabilityTests
{
    [Theory]
    [InlineData(LegacyMagicSort.Riding, 7_001, "Riding(32)")]
    [InlineData(LegacyMagicSort.Spook, 7_002, "Spook(30)")]
    [InlineData(LegacyMagicSort.WarCry, 7_003, "WarCry(31)")]
    public void ClassifyMountControlSpellAsDedicatedRuntimeSlice(
        LegacyMagicSort sort,
        ushort spellId,
        string expectedSortName)
    {
        var info = LegacyMagicSortCapabilities.Classify(sort, spellId);

        Assert.Equal(LegacyMagicSortCapability.StateAndBuffRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal(expectedSortName, info.SortName);
        Assert.Equal("mount-control-runtime", info.NextSeam);
        Assert.Contains("Riding 7001, Spook 7002, and WarCry 7003", info.Reason);
        Assert.Contains("Riding.cs", info.SourceEvidence);
        Assert.Contains("Spook.cs", info.SourceEvidence);
        Assert.Contains("WarCry.cs", info.SourceEvidence);
    }
}
