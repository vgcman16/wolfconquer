using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyCounterKill16350MagicSortCapabilityTests
{
    private const ushort HeavensWonderSpellId = 16_350;
    private const ushort ExistingCounterKillToggleSpellId = 6_003;
    private static readonly LegacyMagicSort CounterKillSort = (LegacyMagicSort)29;

    [Fact]
    public void CapabilityAcceptsOnlyMatchingDecompileHeavensWonderId()
    {
        var info = LegacyMagicSortCapabilities.Classify(CounterKillSort, HeavensWonderSpellId);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("CounterKill(29)", info.SortName);

        var evidence = $"{info.Reason} {info.SourceEvidence}".ToLowerInvariant();
        Assert.Contains("counterkill.cs", evidence);
        Assert.Contains("16350", evidence);
        Assert.Contains("monster", evidence);
        Assert.Contains("player", evidence);
        Assert.Contains("70", evidence);
        Assert.Contains("radius", evidence);
        Assert.Contains("animation", evidence);
        Assert.Contains("backdmg", evidence);
        Assert.Contains("unscaled secondary in-line", evidence);
        Assert.Contains("stararrow 10313", evidence);
        Assert.Contains("range.onplayer", evidence);
        Assert.Contains("40 percent block", evidence);
        Assert.Contains("omnipotence", evidence);
        Assert.Contains("kineticspark", evidence);
        Assert.DoesNotContain("7770", evidence);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6_003)]
    [InlineData(16_349)]
    [InlineData(16_351)]
    [InlineData(ushort.MaxValue)]
    public void CapabilityRejectsAdjacentAndExistingToggleIds(ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(CounterKillSort, spellId);

        Assert.False(info.Supported);
        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.Equal("CounterKill(29)", info.SortName);
    }

    [Theory]
    [InlineData(28)]
    [InlineData(30)]
    public void CapabilityDoesNotLeakHeavensWonderIntoAdjacentSorts(byte sort)
    {
        Assert.False(
            LegacyMagicSortCapabilities.Classify((LegacyMagicSort)sort, HeavensWonderSpellId).Supported);
    }

    [Fact]
    public void CounterKillSortSupportPredicateIsExactlySpell16350()
    {
        for (var candidate = 0; candidate <= ushort.MaxValue; candidate++)
        {
            var spellId = (ushort)candidate;
            var supported = LegacyMagicSortCapabilities.IsSupportedCounterKill16350RuntimeSpell(
                CounterKillSort,
                spellId);

            Assert.True(
                supported == (spellId == HeavensWonderSpellId),
                $"Unexpected CounterKill(29) capability result for spell ID {spellId}.");
        }

        Assert.False(
            LegacyMagicSortCapabilities.IsSupportedCounterKill16350RuntimeSpell(
                CounterKillSort,
                ExistingCounterKillToggleSpellId));
    }
}
