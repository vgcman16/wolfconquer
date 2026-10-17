using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFastBladeScentSwordCapabilityTests
{
    [Theory]
    [InlineData(1_045, "FastBlade")]
    [InlineData(1_046, "ScentSword")]
    public void ClassifyFastBladeAndScentSwordManualLineAsSourceBackedPartialRuntimeSlice(
        ushort spellId,
        string spellName)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.ManualLine, spellId);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("ManualLine(96)", info.SortName);
        Assert.Equal("fastblade-scentsword-manual-line-runtime", info.NextSeam);
        Assert.Contains("FastBlade/ScentSword 1045/1046 ManualLine runtime slice", info.Reason);
        Assert.Contains("sort 96", info.Reason);
        Assert.Contains("width-2 ManualLine target shape", info.Reason);
        Assert.Contains("guard-monster exclusion", info.Reason);
        Assert.Contains("source physical monster/player/SOB damage", info.Reason);
        Assert.Contains("source Physical.OnPlayer 110% branch", info.Reason);
        Assert.Contains("player damage scaled by * 15 / 21", info.Reason);
        Assert.Contains("doubled monster animation damage only", info.Reason);
        Assert.Contains("source Send plus JustMe self animation fanout", info.Reason);
        Assert.Contains("monster/SOB-only spell/proficiency progress", info.Reason);
        Assert.Contains("Remaining parity is exact CanAttack side-effect edges", info.Reason);
        Assert.Contains("adjacent non-1045/1046 Line.cs cases", info.Reason);
        Assert.Contains("spell IDs 1045 and 1046 with sort 96 ManualLine", info.Reason);
        Assert.Contains("13450, 13460, and 18003 stay separate", info.Reason);
        Assert.Contains("magictype.txt rows 28-29 and 60-67", info.SourceEvidence);
        Assert.Contains($"{spellId} {spellName}", info.SourceEvidence);
        Assert.Contains("Line.cs case 1045/1046", info.SourceEvidence);
        Assert.Contains("InLine(..., 2)", info.SourceEvidence);
        Assert.Contains("CanAttackMonster/CanAttackPlayer/CanAttackNpc", info.SourceEvidence);
        Assert.Contains("doubles monster animation damage", info.SourceEvidence);
        Assert.Contains("WorldPacketHandlers.PlayerMagic.FastBladeScentSword.cs handles only spell ids 1045/1046 with sort 96", info.SourceEvidence);
        Assert.Contains("LegacyMagicRuntime.GetLegacyLineWidth maps 1045/1046 to width 2", info.SourceEvidence);
        Assert.Contains("focused runtime proof covers mixed monster/player/SOB targets", info.SourceEvidence);
        Assert.Contains("tighten exact CanAttack side effects", info.SuggestedNextOwner);
        Assert.Contains("keep adjacent Line.cs cases plus generic ManualLine and later FastBlade/ScentSword IDs separate", info.SuggestedNextOwner);
        Assert.Contains(
            $"magicSort=ManualLine(96) spellId={spellId} capability=specialCombatDamage status=partiallyModeled supported=true nextSeam=fastblade-scentsword-manual-line-runtime",
            info.Diagnostic);
    }

    [Theory]
    [InlineData(96, 0, "ManualLine(96)")]
    [InlineData(96, 1_000, "ManualLine(96)")]
    [InlineData(96, 13_450, "ManualLine(96)")]
    [InlineData(96, 13_460, "ManualLine(96)")]
    [InlineData(96, 18_003, "ManualLine(96)")]
    [InlineData(14, 1_045, "Line(14)")]
    public void ClassifyOtherLineAndManualLineInputsWithoutFastBladeScentSwordSourceClaim(
        int sort,
        ushort spellId,
        string expectedSortName)
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)sort, spellId);

        Assert.Equal(LegacyMagicSortCapability.VisibleMonsterDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal(expectedSortName, info.SortName);
        Assert.Equal("player-target-and-sob-magic-runtime", info.NextSeam);
        Assert.Contains("visible-monster damage slice", info.Reason);
        Assert.DoesNotContain("FastBlade/ScentSword 1045/1046 ManualLine runtime slice", info.Reason);
        Assert.DoesNotContain("spell IDs 1045 and 1046 with sort 96 ManualLine", info.Reason);
        Assert.DoesNotContain("magictype.txt rows 28-29 and 60-67", info.SourceEvidence);
        Assert.DoesNotContain("Line.cs case 1045/1046", info.SourceEvidence);
        Assert.DoesNotContain("fastblade-scentsword-manual-line-runtime", info.Diagnostic);
    }

    [Theory]
    [InlineData(13_450, "FastBlade")]
    [InlineData(13_460, "ScentSword")]
    [InlineData(18_003, "ScentSword")]
    public void ClassifyLaterLineFastBladeScentSwordRowsAsSourceSilentNoOp(
        ushort spellId,
        string spellName)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Line, spellId);

        Assert.Equal(LegacyMagicSortCapability.SourceSilentNoOp, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.SourceSilentNoOp, info.Status);
        Assert.False(info.Supported);
        Assert.Equal("Line(14)", info.SortName);
        Assert.Equal("source-silent-noop-confirmation", info.NextSeam);
        Assert.Contains("source-silent after CanUseSpell.Verified", info.Reason);
        Assert.Contains("stamina-only source resources", info.Reason);
        Assert.Contains("without spell animation or monster/player/SOB damage", info.Reason);
        Assert.Contains("must not be classified as the generic visible-monster Line damage fallback", info.Reason);
        Assert.Contains("rows around 1712/1713/2951", info.SourceEvidence);
        Assert.Contains($"{spellId} {spellName}", info.SourceEvidence);
        Assert.Contains("stamina 20, mana 0, and ColdTime 0", info.SourceEvidence);
        Assert.Contains("MagicSort.Line and sort 96", info.SourceEvidence);
        Assert.Contains("Line.cs calls CanUseSpell.Verified", info.SourceEvidence);
        Assert.Contains("has no cases for 13450, 13460, or 18003", info.SourceEvidence);
        Assert.Contains("without MsgSpellAnimation or monster/player/SOB damage", info.SourceEvidence);
        Assert.Contains("observable behavior beyond preparation", info.SuggestedNextOwner);
        Assert.DoesNotContain("visible-monster damage slice", info.Reason);
        Assert.DoesNotContain("FastBlade/ScentSword 1045/1046 ManualLine runtime slice", info.Reason);
        Assert.Contains(
            $"magicSort=Line(14) spellId={spellId} capability=sourceSilentNoOp status=sourceSilentNoOp supported=false nextSeam=source-silent-noop-confirmation",
            info.Diagnostic);
    }
}
