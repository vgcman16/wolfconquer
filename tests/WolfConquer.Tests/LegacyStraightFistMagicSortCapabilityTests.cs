using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyStraightFistMagicSortCapabilityTests
{
    [Fact]
    public void ClassifyStraightFist12760AsSourceBackedVisibleMonsterRuntime()
    {
        Assert.True(LegacyMagicSortCapabilities.IsSupportedStraightFistVisibleMonsterAttackSpell(
            LegacyMagicSort.StraightFist,
            12_760));

        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.StraightFist, 12_760);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("StraightFist(76)", info.SortName);
        Assert.Equal("straightfist-12760-combat-runtime", info.NextSeam);
        Assert.Contains("bounded StraightFist 12760 runtime", info.Reason);
        Assert.Contains("StraightFist.cs", info.SourceEvidence);
        Assert.DoesNotContain("7770", info.SourceEvidence);
    }

    [Fact]
    public void ClassifyStraightFist12230AsModeledSourceSilentPostResourceBranch()
    {
        Assert.False(LegacyMagicSortCapabilities.IsSupportedStraightFistVisibleMonsterAttackSpell(
            LegacyMagicSort.StraightFist,
            12_230));

        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.StraightFist, 12_230);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Contains("return without damage or animation", info.Reason);
        Assert.Contains("D13 automatic-selector adapter deliberately treats 12230", info.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12_229)]
    [InlineData(12_231)]
    [InlineData(12_759)]
    [InlineData(12_761)]
    [InlineData(ushort.MaxValue)]
    public void ClassifyOtherStraightFistIdsAsUnmodeled(ushort spellId)
    {
        Assert.False(LegacyMagicSortCapabilities.IsSupportedStraightFistVisibleMonsterAttackSpell(
            LegacyMagicSort.StraightFist,
            spellId));

        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.StraightFist, spellId);
        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.False(info.Supported);
    }

    [Fact]
    public void StraightFistIdDoesNotGrantSupportToAnotherSort()
    {
        Assert.False(LegacyMagicSortCapabilities.IsSupportedStraightFistVisibleMonsterAttackSpell(
            LegacyMagicSort.Line,
            12_760));
    }
}
