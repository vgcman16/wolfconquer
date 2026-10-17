using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyStrike13050CapabilityTests
{
    [Fact]
    public void ClassifyStrike13050AsDedicatedCasterCenteredAreaRuntimeSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Strike, 13_050);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("Strike(81)", info.SortName);
        Assert.Equal("strike-13050-combat-runtime", info.NextSeam);
        Assert.Contains("caster-centered area runtime", info.Reason);
        Assert.Contains("clicked player/monster animation-coordinate reanchoring", info.Reason);
        Assert.Contains("radius <= 5 mixed visible monster/player/SOB selection from the caster", info.Reason);
        Assert.Contains("monster/SOB-only character and spell progress", info.Reason);
        Assert.Contains("source no-target silence", info.Reason);
        Assert.Contains("Strike.cs case 13050", info.SourceEvidence);
        Assert.Contains("magictype.txt row 1659 defines spell 13050", info.SourceEvidence);
        Assert.Contains("supported=true", info.Diagnostic);
    }
}
