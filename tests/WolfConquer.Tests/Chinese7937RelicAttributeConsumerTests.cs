using System.Globalization;
using WolfConquer.Core.Client;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937RelicAttributeConsumerTests
{
    private static string GoldRows(bool excludeImmunity, bool omitLast = false) => string.Join("\n",
        LegacyMaxRandomRelicTestData.ItemIds.Take(omitLast ? 4 : 5).Select(item =>
            "[" + item.ToString(CultureInfo.InvariantCulture) + "] = { [1] = false, [3] = " +
            (excludeImmunity ? "true" : "false") + ", },"));

    private static Chinese7937XuanbaoAttributeCatalog Selected(bool excludeImmunity = true, bool omitLast = false) =>
        Chinese7937XuanbaoAttributeFixture.Parse(Chinese7937XuanbaoAttributeFixture.Module(
            goldRows: GoldRows(excludeImmunity, omitLast)));

    private static WorldDataCatalog World(bool addCritical = true)
    {
        var original = LegacyMaxRandomRelicTestData.CreateWorldData();
        var attributes = original.XuanbaoRandomAttributes.ToList();
        if (addCritical)
            foreach (var item in LegacyMaxRandomRelicTestData.ItemIds)
                attributes.Insert(0, XuanbaoParser.ParseRandomAttributeRow("fixture://selected-relic", 1,
                    FormattableString.Invariant($"{item}@@1@@100@@101@@")));
        return LegacyItemWorldDataFixture.Create(original.Items, attributes);
    }

    private static LegacyBossSpecialDeathDropRolls Rolls => new(
        DeityLandRelicItemIndex: 0, DeityLandRelicAttributeCount: 5, DeityLandRelicDurabilityRoll: 10,
        DeityLandRelicAttributeIndexes: [0, 0, 0, 0, 0], DeityLandRelicAttributeValues: [100, 100, 100, 100, 100]);

    private static uint[] Attributes(LegacyBossSpecialGeneratedDropItem item) =>
        [item.Attribute1, item.Attribute2, item.Attribute3, item.Attribute4, item.Attribute5];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndependentlySpecifiedTwentyPairRuleRetainsPublicGeneratorOutputsForAllFiveRelics(bool maximum)
    {
        // Existing source-backed server exclusions, independently matched by the
        // additional7937 table inspection. No client catalog rows are bundled.
        int[][] excluded = [[2, 1, 6, 7], [2, 1, 8, 7], [6, 1, 8, 7], [6, 1, 8, 2], [6, 7, 8, 2]];
        var rows = string.Join("\n", LegacyMaxRandomRelicTestData.ItemIds.Select((item, index) =>
            "[" + item.ToString(CultureInfo.InvariantCulture) + "] = { " +
            string.Join(" ", excluded[index].Select(attribute => "[" + attribute.ToString(CultureInfo.InvariantCulture) + "] = true,")) + " },"));
        var selected = Chinese7937XuanbaoAttributeFixture.Parse(Chinese7937XuanbaoAttributeFixture.Module(goldRows: rows));
        Assert.Equal(20, selected.GoldLimitFlags.Count);
        var world = World();
        var baseline = LegacyBossSpecialDeathDropCatalog.FromWorldData(world);
        var bound = LegacyBossSpecialDeathDropCatalog.FromWorldData(world, selected);
        for (var itemIndex = 0; itemIndex < 5; itemIndex++)
        {
            var rolls = Rolls with { DeityLandRelicItemIndex = itemIndex };
            LegacyBossSpecialGeneratedDropItem expected, actual;
            string expectedReason, actualReason;
            var oldFound = maximum
                ? LegacyBossSpecialDeathDropPolicy.TryCreateMaxRandomRelicItem(baseline, rolls, out expected, out expectedReason)
                : LegacyBossSpecialDeathDropPolicy.TryCreateDeityLandRandomRelicItem(baseline, rolls, out expected, out expectedReason);
            var newFound = maximum
                ? LegacyBossSpecialDeathDropPolicy.TryCreateMaxRandomRelicItem(bound, rolls, out actual, out actualReason)
                : LegacyBossSpecialDeathDropPolicy.TryCreateDeityLandRandomRelicItem(bound, rolls, out actual, out actualReason);
            Assert.True(oldFound, expectedReason);
            Assert.True(newFound, actualReason);
            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingPublicGeneratorsConsumeRealSelectedCatalogInsteadOfTheFallbackSwitch(bool maximum)
    {
        var world = World();
        var legacy = LegacyBossSpecialDeathDropCatalog.FromWorldData(world);
        var selected = Selected();
        Assert.True(world.TryBindChinese7937XuanbaoAttributes(selected, out var reason), reason);
        var bound = LegacyBossSpecialDeathDropCatalog.FromWorldData(world);
        Assert.Same(selected, bound.Selected7937Attributes);
        Assert.Null(legacy.Selected7937Attributes);
        LegacyBossSpecialGeneratedDropItem generated;
        var found = maximum
            ? LegacyBossSpecialDeathDropPolicy.TryCreateMaxRandomRelicItem(bound, Rolls, out generated, out reason)
            : LegacyBossSpecialDeathDropPolicy.TryCreateDeityLandRandomRelicItem(bound, Rolls, out generated, out reason);
        Assert.True(found, reason);
        var types = Attributes(generated).Select(value => value % 1000 % 100).ToArray();
        Assert.Contains(1u, types);
        Assert.DoesNotContain(3u, types);
        Assert.Equal(5, types.Distinct().Count());
        Assert.Equal(4_100_001u, generated.ItemId);
        Assert.True(LegacyBossSpecialDeathDropPolicy.TryCreateMaxRandomRelicItem(legacy, Rolls,
            out var legacyItem, out reason), reason);
        Assert.Equal(LegacyMaxRandomRelicTestData.ExpectedEncodedAttributes, Attributes(legacyItem));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingFifthEligibleAttributeRefusesWithCleanOutput(bool maximum)
    {
        var bound = LegacyBossSpecialDeathDropCatalog.FromWorldData(World(addCritical: false), Selected());
        LegacyBossSpecialGeneratedDropItem generated;
        string reason;
        var found = maximum
            ? LegacyBossSpecialDeathDropPolicy.TryCreateMaxRandomRelicItem(bound, Rolls, out generated, out reason)
            : LegacyBossSpecialDeathDropPolicy.TryCreateDeityLandRandomRelicItem(bound, Rolls, out generated, out reason);
        Assert.False(found);
        Assert.Equal(default, generated);
        Assert.Contains("unique", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void PresentFalseIsRetainedAndDoesNotExcludeAnAttribute()
    {
        var selected = Selected(excludeImmunity: false);
        Assert.True(selected.TryGetGoldLimitFlag(4_100_001, 3, out var excluded));
        Assert.False(excluded);
        var bound = LegacyBossSpecialDeathDropCatalog.FromWorldData(World(addCritical: false), selected);
        Assert.True(LegacyBossSpecialDeathDropPolicy.TryCreateMaxRandomRelicItem(bound, Rolls, out var item, out var reason), reason);
        Assert.Equal(LegacyMaxRandomRelicTestData.ExpectedEncodedAttributes, Attributes(item));
        Assert.False(selected.TryGetGoldLimitFlag(4_100_001, 14, out _));
    }

    [Fact]
    public void IncompleteSelectedItemTableCannotBindOrSilentlyUseLegacyRules()
    {
        var world = World();
        var selected = Selected(omitLast: true);
        Assert.Throws<InvalidDataException>(() => LegacyBossSpecialDeathDropCatalog.FromWorldData(world, selected));
        Assert.False(world.TryBindChinese7937XuanbaoAttributes(selected, out var reason));
        Assert.Equal("selected7937ActualRelicMetadataRequired", reason);
        Assert.Null(world.Selected7937XuanbaoAttributes);
    }

    [Fact]
    public void MissingRealItemOrRandomAttributeOwnersRefuseBeforeWorldPublication()
    {
        var complete = World();
        var missingItem = LegacyItemWorldDataFixture.Create(complete.Items.Take(4).ToArray(), complete.XuanbaoRandomAttributes);
        Assert.False(missingItem.TryBindChinese7937XuanbaoAttributes(Selected(), out _));
        Assert.Null(missingItem.Selected7937XuanbaoAttributes);
        var missingAttributes = LegacyMaxRandomRelicTestData.CreateWorldData(includeRandomAttributes: false);
        Assert.False(missingAttributes.TryBindChinese7937XuanbaoAttributes(Selected(), out _));
        Assert.Null(missingAttributes.Selected7937XuanbaoAttributes);
    }

    [Fact]
    public void OnceBoundReferenceIsIdempotentForItsInstanceAndRefusesAConflictingReplacement()
    {
        var world = World();
        var first = Selected();
        Assert.True(world.TryBindChinese7937XuanbaoAttributes(first, out _));
        Assert.True(world.TryBindChinese7937XuanbaoAttributes(first, out _));
        Assert.False(world.TryBindChinese7937XuanbaoAttributes(Selected(false), out var reason));
        Assert.Equal("selected7937AttributeCatalogAlreadyBound", reason);
        Assert.Same(first, world.Selected7937XuanbaoAttributes);
        Assert.Same(first, LegacyBossSpecialDeathDropCatalog.FromWorldData(world).Selected7937Attributes);
    }
}
