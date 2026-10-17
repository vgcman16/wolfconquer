using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPerimeterMagicSortCapabilityTests
{
    [Theory]
    [InlineData(12_210)]
    [InlineData(12_220)]
    [InlineData(12_580)]
    [InlineData(12_590)]
    [InlineData(12_740)]
    [InlineData(12_750)]
    [InlineData(13_040)]
    [InlineData(14_570)]
    [InlineData(14_580)]
    [InlineData(15_670)]
    [InlineData(15_680)]
    public void ClassifyEvidenceBackedPerimeterSpellAsVisibleMonsterDamage(ushort spellId)
    {
        Assert.True(
            LegacyMagicSortCapabilities.IsSupportedPerimeterVisibleMonsterAttackSpell(
                LegacyMagicSort.Perimeter,
                spellId));

        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Perimeter, spellId);

        Assert.Equal(LegacyMagicSortCapability.VisibleMonsterDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("Perimeter(78)", info.SortName);
        Assert.Equal("perimeter-source-parity-debt", info.NextSeam);
        Assert.Contains("exactly spell IDs 12210, 12220, 12580, 12590, 12740, 12750", info.Reason);
        Assert.Contains("six D13 default-Fan IDs model monster/player/SOB damage", info.Reason);

        var completionEvidence = $"{info.Reason} {info.SourceEvidence}".ToLowerInvariant();
        Assert.Contains("exact bounded mixed monster/player/sob runtime", completionEvidence);
        Assert.Contains("13040", completionEvidence);
        Assert.Contains("15670", completionEvidence);
        Assert.Contains("15680", completionEvidence);
        Assert.Contains("140/7", completionEvidence);
        Assert.Contains("exact decompiled 500/5 compatibility", completionEvidence);
        Assert.Contains("trojan target scaling 10/7", completionEvidence);
        Assert.Contains("non-trojan 10/11", completionEvidence);
        Assert.Contains("monster/sob-only progress", completionEvidence);
        Assert.Contains("no-target silence", completionEvidence);
        Assert.Contains("attack-stamp bypass", completionEvidence);
        Assert.Contains("pending weapon-proc/d13 route", completionEvidence);
        Assert.Contains("onautoattack", completionEvidence);
        Assert.Contains("suppression", completionEvidence);
        Assert.Contains("base.calculatesoul", completionEvidence);
        Assert.Contains("shared canattack", completionEvidence);
        Assert.Contains("status=partiallyModeled", info.Diagnostic);
        Assert.DoesNotContain("7770", info.SourceEvidence);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13_039)]
    [InlineData(13_041)]
    [InlineData(14_569)]
    [InlineData(14_571)]
    [InlineData(14_579)]
    [InlineData(14_581)]
    [InlineData(15_669)]
    [InlineData(15_671)]
    [InlineData(15_679)]
    [InlineData(15_681)]
    [InlineData(ushort.MaxValue)]
    public void ClassifyUnmodeledPerimeterSpellAsUnsupported(ushort spellId)
    {
        Assert.False(
            LegacyMagicSortCapabilities.IsSupportedPerimeterVisibleMonsterAttackSpell(
                LegacyMagicSort.Perimeter,
                spellId));

        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Perimeter, spellId);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.False(info.Supported);
        Assert.Equal("Perimeter(78)", info.SortName);
        Assert.Equal("damage-on-monster-special-sort-runtime", info.NextSeam);
        Assert.DoesNotContain("bounded Perimeter visible-monster physical-damage slice", info.Reason);
        Assert.Contains("status=missingRuntimeNotModeled", info.Diagnostic);
    }

    [Theory]
    [InlineData(12_210)]
    [InlineData(12_220)]
    [InlineData(12_580)]
    [InlineData(12_590)]
    [InlineData(12_740)]
    [InlineData(12_750)]
    [InlineData(13_040)]
    [InlineData(14_570)]
    [InlineData(14_580)]
    [InlineData(15_670)]
    [InlineData(15_680)]
    public void PerimeterSpellIdDoesNotGrantSupportToAnotherSort(ushort spellId)
    {
        Assert.False(
            LegacyMagicSortCapabilities.IsSupportedPerimeterVisibleMonsterAttackSpell(
                LegacyMagicSort.Sector,
                spellId));
    }

    [Fact]
    public void PerimeterSupportPredicateAllowsExactlyReviewedSpellIds()
    {
        HashSet<ushort> expected =
        [
            12_210,
            12_220,
            12_580,
            12_590,
            12_740,
            12_750,
            13_040,
            14_570,
            14_580,
            15_670,
            15_680,
        ];

        for (var candidate = 0; candidate <= ushort.MaxValue; candidate++)
        {
            var spellId = (ushort)candidate;
            var supported = LegacyMagicSortCapabilities.IsSupportedPerimeterVisibleMonsterAttackSpell(
                LegacyMagicSort.Perimeter,
                spellId);

            Assert.True(
                supported == expected.Contains(spellId),
                $"Unexpected Perimeter capability result for spell ID {spellId}.");
        }
    }
}
