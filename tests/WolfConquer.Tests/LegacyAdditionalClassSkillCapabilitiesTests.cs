using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyAdditionalClassSkillCapabilitiesTests
{
    [Fact]
    public void ClassifyStarArrowAsDedicatedRuntimeSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.StarArrow, 10_313);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("stararrow-combat-runtime", info.NextSeam);
        Assert.Contains("pulled to the caster before range and attack checks", info.Reason);
        Assert.Contains("NatureShield post-pull avoidance roll", info.Reason);
        Assert.Contains("Range.OnPlayer", info.Reason);
        Assert.Contains("zero-base CalculateExtraAttack with attacker PhysicalDamageIncrease and defender PhysicalDamageDecrease", info.Reason);
        Assert.Contains("fixes only WildPhoenix at one damage", info.Reason);
        Assert.DoesNotContain("returns one damage for those defenses", info.Reason);
        Assert.Contains("DefensiveStance's 40 percent reduction", info.Reason);
        Assert.Contains("Heaven's Wonder 16350 BackDmg caller", info.Reason);
        Assert.Contains("StarArrow.cs handles only spell 10313", info.SourceEvidence);
        Assert.Contains("Range.cs applies zero-base CalculateExtraAttack for ShurikenVortex/ManiacDance before WildPhoenix's fixed one", info.SourceEvidence);
    }

    [Theory]
    [InlineData(12_110)]
    [InlineData(12_240)]
    public void ClassifyFatalSpinAsDedicatedRuntimeSlice(ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.FatalSpin, spellId);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("fatalspin-combat-runtime", info.NextSeam);
        Assert.Contains("line geometry", info.Reason);
        Assert.Contains("FatalSpin.cs handles spells 12110 and 12240", info.SourceEvidence);
    }

    [Theory]
    [InlineData(12_830)]
    [InlineData(13_070)]
    [InlineData(13_080)]
    [InlineData(14_280)]
    public void ClassifyRemoveStaminAsDedicatedRuntimeSlice(ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.RemoveStamin, spellId);

        Assert.Equal(LegacyMagicSortCapability.StateAndBuffRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("remove-stamin-state-runtime", info.NextSeam);
        Assert.Contains("FrostArrows flag 0xDB", info.Reason);
        Assert.Contains("Consolidation immunity", info.Reason);
        Assert.Contains("RemoveStamin.cs handles spells 12830, 13070, 13080, and 14280", info.SourceEvidence);
    }

    [Theory]
    [InlineData(LegacyMagicSort.StarArrow, 10_312)]
    [InlineData(LegacyMagicSort.FatalSpin, 12_239)]
    [InlineData(LegacyMagicSort.RemoveStamin, 14_279)]
    public void DoesNotClaimUnprovenSpellIds(LegacyMagicSort sort, ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(sort, spellId);

        Assert.False(info.Supported);
    }
}
