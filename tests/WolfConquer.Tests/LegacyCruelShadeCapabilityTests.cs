using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyCruelShadeCapabilityTests
{
    private const int DecLifeSort = 26;
    private const ushort CruelShadeSpellId = 3_050;
    private const ushort GapingWoundsSpellId = 11_230;

    [Fact]
    public void ClassifyCruelShadeDecLifeAsSourceBackedPartialCombatRuntime()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)DecLifeSort, CruelShadeSpellId);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("DecLife(26)", info.SortName);
        Assert.Equal("cruelshade-combat-runtime", info.NextSeam);
        Assert.Contains("bounded CruelShade 3050 runtime slice", info.Reason);
        Assert.Contains("DecLife sort 26", info.Reason);
        Assert.Contains("single visible monster/player/SOB target", info.Reason);
        Assert.Contains("strict distance < Spell.Range", info.Reason);
        Assert.Contains("monster boss damage = 1", info.Reason);
        Assert.Contains("player HP-based DecLife damage overwritten by ToxinEraser percentage scaling", info.Reason);
        Assert.Contains("SOB Spell.Damage % 100 HP damage", info.Reason);
        Assert.Contains("BreathFocus 11960 fallback exclusion", info.Reason);
        Assert.Contains("Remaining CruelShade parity is exact CanAttack side-effect edges", info.Reason);
        Assert.Contains("CanAttackPlayer relation-gate review", info.Reason);
        Assert.Contains("remaining CanAttackNpc source checks", info.Reason);
        Assert.Contains("GapingWounds 11230", info.Reason);
        Assert.Contains("magictype.txt rows 3050-3053", info.SourceEvidence);
        Assert.Contains("MagicType.cs defines DecLife = 26", info.SourceEvidence);
        Assert.Contains("MagicSort.DecLife to DecLife.Execute", info.SourceEvidence);
        Assert.Contains("excludes spell 3050 from the later Trojan BreathFocus 11960 fallback", info.SourceEvidence);
        Assert.Contains("DecLife.cs calls CanUseSpell.Verified first", info.SourceEvidence);
        Assert.Contains("handles ClientSpell.ID 3050", info.SourceEvidence);
        Assert.Contains("Attack.OpponentUID as one visible monster, player, or SOB NPC", info.SourceEvidence);
        Assert.Contains("CanAttackMonster/CanAttackPlayer/CanAttackNpc gates", info.SourceEvidence);
        Assert.Contains("Spell.Damage % 100 percent of SOB HP", info.SourceEvidence);
        Assert.Contains("MsgSpellAnimation", info.SourceEvidence);
        Assert.Contains("tighten exact CruelShade CanAttack side effects", info.SuggestedNextOwner);
        Assert.Contains("keep GapingWounds 11230 separate", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=DecLife(26) spellId=3050 capability=specialCombatDamage status=partiallyModeled supported=true nextSeam=cruelshade-combat-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyGapingWoundsDecLifeAsSourceBackedPartialCombatRuntime()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)DecLifeSort, GapingWoundsSpellId);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("DecLife(26)", info.SortName);
        Assert.Equal("gapingwounds-combat-runtime", info.NextSeam);
        Assert.Contains("bounded GapingWounds 11230 runtime slice", info.Reason);
        Assert.Contains("DecLife sort 26", info.Reason);
        Assert.Contains("direct Attack.OpponentUID", info.Reason);
        Assert.Contains("strict distance < Spell.Range", info.Reason);
        Assert.Contains("monster boss damage = 1", info.Reason);
        Assert.Contains("non-boss monster HP * (100 - Spell.Damage) / 100 damage", info.Reason);
        Assert.Contains("player requires !ToxinEraser", info.Reason);
        Assert.Contains("source ToxinEraser overwrite math", info.Reason);
        Assert.Contains("SOB HP * (100 - Spell.Damage) / 100 damage", info.Reason);
        Assert.Contains("Remaining GapingWounds parity is exact CanAttack side-effect edges", info.Reason);
        Assert.Contains("separate from CruelShade 3050", info.Reason);
        Assert.Contains("magictype.txt rows 1171-1175", info.SourceEvidence);
        Assert.Contains("type id 11230", info.SourceEvidence);
        Assert.Contains("MagicType.cs defines DecLife = 26", info.SourceEvidence);
        Assert.Contains("MagicSort.DecLife to DecLife.Execute", info.SourceEvidence);
        Assert.Contains("DecLife.cs lines 25-83 handle ClientSpell.ID 11230", info.SourceEvidence);
        Assert.Contains("Attack.OpponentUID as one visible monster, player, or SOB NPC", info.SourceEvidence);
        Assert.Contains("CanAttackMonster/CanAttackPlayer/CanAttackNpc gates", info.SourceEvidence);
        Assert.Contains("!PerfectionEffects.ToxinEraser(player2)", info.SourceEvidence);
        Assert.Contains("Math.Min(99.0, player2.Owner.PerfectionStatus.ToxinEraser)", info.SourceEvidence);
        Assert.Contains("MsgSpellAnimation", info.SourceEvidence);
        Assert.Contains("tighten exact GapingWounds 11230 CanAttack side effects", info.SuggestedNextOwner);
        Assert.Contains("keep CruelShade 3050 and unknown DecLife spell ids separate", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=DecLife(26) spellId=11230 capability=specialCombatDamage status=partiallyModeled supported=true nextSeam=gapingwounds-combat-runtime",
            info.Diagnostic);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11_231)]
    public void ClassifyUnknownDecLifeInputsWithoutSupportedSliceClaim(ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)DecLifeSort, spellId);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.False(info.Supported);
        Assert.Equal("DecLife(26)", info.SortName);
        Assert.Equal("damage-on-monster-special-sort-runtime", info.NextSeam);
        Assert.DoesNotContain("CruelShade 3050", info.Reason);
        Assert.DoesNotContain("GapingWounds 11230", info.Reason);
        Assert.DoesNotContain("cruelshade-combat-runtime", info.Diagnostic);
        Assert.DoesNotContain("gapingwounds-combat-runtime", info.Diagnostic);
    }
}
