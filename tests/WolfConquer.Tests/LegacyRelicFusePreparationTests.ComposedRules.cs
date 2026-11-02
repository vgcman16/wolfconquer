using WolfConquer.Core.Client;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicFusePreparationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(65_536)]
    [InlineData(int.MaxValue)]
    public void SelectedRulesRetainUnusedLuckyValuesWithoutChangingTheFixedFormula(int unusedMaximum)
    {
        var world = World(Caps(30).Append(ComposedLuckyRow(unusedMaximum)).ToArray());
        var owner = Owner(100_114);
        var baseline = Prepare(world, owner);
        var beforeItems = PlayerItemSnapshotLoader.ToLegacyBytes(owner);
        var expected = Enumerable.Range(0, 32).Select(seed => Roll(baseline, owner, seed)).ToArray();
        var selected = ComposedRuleSource(world);

        Assert.True(world.TryBindChinese7937XuanbaoRules(selected, out var reason), reason);
        Assert.False(baseline.IsCurrent(owner));
        Assert.Same(selected, world.Selected7937XuanbaoRules!.Source);
        Assert.True(world.TryGetEffectiveXuanbaoComposeLimit(14, out var exactRow));
        Assert.Equal(unusedMaximum, exactRow!.Values[^1]);
        var successor = Prepare(world, owner);
        Assert.Same(world.Selected7937XuanbaoRules, successor.RuleGeneration.RuleSource);
        Assert.True(successor.RuleGeneration.IsCurrent(world));
        List<uint> actualWords = [];
        for (var seed = 0; seed < expected.Length; seed++)
        {
            var actual = Roll(successor, owner, seed);
            Assert.Equal(expected[seed].Durability, actual.Durability);
            Assert.Equal(Words(expected[seed]), Words(actual));
            actualWords.AddRange(Words(actual));
        }
        Assert.Contains(100_114U, actualWords);
        Assert.All(actualWords.Where(word => word % 100 == 14), word => Assert.Equal(100_114U, word));
        Assert.Equal(beforeItems, PlayerItemSnapshotLoader.ToLegacyBytes(owner));
        Assert.Empty(owner.RelicFuseItems);
    }

    [Fact]
    public void UnusedLuckyDataStillRequiresAnExactSelectedOwnerMatchBeforePublication()
    {
        var world = World(Caps().Append(ComposedLuckyRow(0)).ToArray());
        var owner = Owner(100_114);
        var preparation = Prepare(world, owner);
        var generation = preparation.RuleGeneration;
        var selected = ComposedRuleSource(world, changedLuckyMaximum: 1);
        Assert.False(world.TryBindChinese7937XuanbaoRules(selected, out var reason));
        Assert.Equal("selected7937ComposeOwnerOrValuesConflict", reason);
        Assert.Null(world.Selected7937XuanbaoRules);
        Assert.Same(generation, world.LegacyRelicFuseRules);
        Assert.True(generation.IsCurrent(world));
        Assert.True(preparation.IsCurrent(owner));
        Assert.NotNull(Roll(preparation, owner));
    }

    [Theory]
    [InlineData(29)]
    [InlineData(65_536)]
    public void SelectedFormulaUsedCapsStillRefuseTheirRealOutOfRangeValues(int invalidMaximum)
    {
        var caps = Caps().Select(row => row.Id == 3
            ? XuanbaoParser.ParseComposeAttributeLimitRow("fixture://composed-cap", 3,
                FormattableString.Invariant($"3@@0@@1@@{invalidMaximum}@@"))
            : row).Append(ComposedLuckyRow(0)).ToArray();
        var world = World(caps);
        Assert.Null(world.LegacyRelicFuseRules);
        var original = world.XuanbaoComposeAttributeLimitsById[3];

        Assert.False(world.TryBindChinese7937XuanbaoRules(ComposedRuleSource(world), out var reason));
        Assert.Equal("selected7937ComposeOwnerOrValuesConflict", reason);
        Assert.Null(world.Selected7937XuanbaoRules);
        Assert.Null(world.LegacyRelicFuseRules);
        Assert.True(world.TryGetEffectiveXuanbaoComposeLimit(3, out var retained));
        Assert.Same(original, retained);
    }

    private static XuanbaoComposeAttributeLimit ComposedLuckyRow(int maximum) =>
        XuanbaoParser.ParseComposeAttributeLimitRow("fixture://composed-lucky", 14,
            FormattableString.Invariant($"14@@0@@1@@{maximum}@@"));

    private static Chinese7937XuanbaoRuleCatalogs ComposedRuleSource(WorldDataCatalog world, int? changedLuckyMaximum = null)
    {
        var compose = string.Concat(world.XuanbaoComposeAttributeLimits.Select(row =>
            FormattableString.Invariant($"{row.Id}@@{row.Values[0]}@@{row.Values[1]}@@{(row.Id == 14 && changedLuckyMaximum is { } changed ? changed : row.Values[2])}@@\r\n")));
        var random = string.Concat(world.XuanbaoRandomAttributes.Reverse().Select(row =>
            FormattableString.Invariant($"{row.ItemTypeId}@@{row.AttributeType}@@{row.MinValue}@@{row.MaxValue}@@\r\n")));
        return Chinese7937XuanbaoRulesFixture.Parse(compose, random);
    }
}
