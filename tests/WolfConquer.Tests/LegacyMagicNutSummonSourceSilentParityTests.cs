using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMagicNutSummonSourceSilentParityTests
{
    [Theory]
    [InlineData(22_060)]
    [InlineData(22_050)]
    [InlineData(22_040)]
    public void ClassifyNutSummonRowsAsSourceSilentNoOpOnlyWhenSourceEvidenceSaysSwitchIsSilent(ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)107, spellId);

        Assert.Equal("NutSummon(107)", info.SortName);
        Assert.Equal(LegacyMagicSortCapability.SourceSilentNoOp, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.SourceSilentNoOp, info.Status);
        Assert.False(info.Supported);
        Assert.Equal("source-silent-noop-confirmation", info.NextSeam);

        Assert.Contains("magictype.txt contains rows for this sort", info.Reason);
        Assert.Contains("MsgAttackPacket.ProcescMagic switch has no dispatch branch", info.Reason);
        Assert.Contains("silent/no-op for normal player magic casts", info.Reason);
        Assert.Contains("magictype.txt", info.SourceEvidence);
        Assert.Contains("MsgAttackPacket.cs ProcescMagic", info.SourceEvidence);
        Assert.Contains("dispatch to AttackHandler classes", info.SourceEvidence);
        Assert.Contains("No gameplay owner until a live capture proves", info.SuggestedNextOwner);

        Assert.Contains("magicSort=NutSummon(107)", info.Diagnostic);
        Assert.Contains($"spellId={spellId}", info.Diagnostic);
        Assert.Contains("capability=sourceSilentNoOp", info.Diagnostic);
        Assert.Contains("status=sourceSilentNoOp", info.Diagnostic);
        Assert.Contains("supported=false", info.Diagnostic);
        Assert.Contains("source-silent-noop-confirmation", info.Diagnostic);
        Assert.Contains("no dispatch branch", info.Diagnostic);
    }
}
