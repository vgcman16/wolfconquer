using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRoamerWildwindSourceSilentParityTests
{
    [Fact]
    public void ClassifyRoamerWildwindAsSourceSilentNoOpFromMissingWildwindCase()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Wildwind, spellId: 7_040);

        Assert.Equal("Wildwind(90)", info.SortName);
        Assert.Equal(LegacyMagicSortCapability.SourceSilentNoOp, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.SourceSilentNoOp, info.Status);
        Assert.False(info.Supported);
        Assert.Equal("source-silent-noop-confirmation", info.NextSeam);

        Assert.Contains("Roamer spell 7040 is a Wildwind sort row", info.Reason);
        Assert.Contains("Wildwind.cs switch has no case", info.Reason);
        Assert.Contains("without spell animation or monster/player/SOB damage", info.Reason);
        Assert.Contains("magictype.txt rows 7040-7049", info.SourceEvidence);
        Assert.Contains("dispatches MagicSort.Wildwind", info.SourceEvidence);
        Assert.Contains("has no case for spell ID 7040", info.SourceEvidence);

        Assert.Contains("magicSort=Wildwind(90)", info.Diagnostic);
        Assert.Contains("spellId=7040", info.Diagnostic);
        Assert.Contains("capability=sourceSilentNoOp", info.Diagnostic);
        Assert.Contains("status=sourceSilentNoOp", info.Diagnostic);
        Assert.Contains("supported=false", info.Diagnostic);
    }
}
