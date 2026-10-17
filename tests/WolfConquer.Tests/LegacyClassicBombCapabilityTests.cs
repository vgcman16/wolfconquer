using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClassicBombCapabilityTests
{
    [Theory]
    [InlineData(5_010)]
    [InlineData(7_020)]
    [InlineData(8_030)]
    public void ClassifyExactClassicBombSpellAsDedicatedRangeRuntime(ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Bomb, spellId);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("Bomb(5)", info.SortName);
        Assert.Equal("classic-bomb-range-runtime", info.NextSeam);
        Assert.Contains("caster-centered strict distance under Spell.Range", info.Reason);
        Assert.Contains("Range.OnPlayer ShurikenVortex and ManiacDance", info.Reason);
        Assert.Contains("without weapon proficiency", info.Reason);
        Assert.Contains("Bomb.cs cases 5010", info.SourceEvidence);
        Assert.Contains("exact-D13 wire", info.SourceEvidence);
        Assert.Contains("no Source7770", info.SourceEvidence);

        if (spellId == 8_030)
        {
            Assert.Contains("removes XPList", info.Reason);
        }
    }

    [Fact]
    public void KeepAdjacentBombSpellOnGenericCapability()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Bomb, 5_040);

        Assert.Equal(LegacyMagicSortCapability.VisibleMonsterDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.DoesNotContain("classic-bomb-range-runtime", info.Diagnostic);
        Assert.DoesNotContain("Snow 5010", info.Reason);
    }
}
