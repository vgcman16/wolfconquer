using System.Globalization;
using WolfConquer.Core.Client;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicFusePreparationTests
{
    [Fact]
    public void ChangedUsedCapCannotDiscardActualGenerationOrConsumeSelectedRuleBinding()
    {
        var donor = Caps().Select(row => row with { Values = row.Values.ToArray() }).ToArray();
        var world = World(donor); var gold = PublicationGold(excludeEveryType: false);
        Assert.True(world.TryBindChinese7937XuanbaoAttributes(gold, out var bindReason), bindReason);
        var owner = Owner(); var prepared = Prepare(world, owner);
        var original = prepared.RuleGeneration; var originalWords = Words(Roll(prepared, owner, 51));
        var cap = Assert.IsType<int[]>(donor.Single(row => row.Id == 3).Values);
        cap[^1] = 401; // Still valid/in-range, and selected data exactly matches the mutated raw owner.
        var incompatible = ComposedRuleSource(world);
        Assert.True(Chinese7937XuanbaoRuntimeRules.TryPrepare(world, incompatible, out var acceptedMetadata, out var metadataReason), metadataReason);
        Assert.NotNull(acceptedMetadata);
        Assert.False(world.TryBindChinese7937XuanbaoRules(incompatible, out var reason));
        Assert.Equal("relicFuseRuleSuccessorUnavailable", reason);
        Assert.Null(world.Selected7937XuanbaoRules); Assert.Same(gold, world.Selected7937XuanbaoAttributes);
        Assert.Same(original, world.LegacyRelicFuseRules); Assert.True(original.IsCurrent(world));
        Assert.True(prepared.IsCurrent(owner)); Assert.Equal(originalWords, Words(Roll(prepared, owner, 51)));

        cap[^1] = 400;
        var valid = ComposedRuleSource(world);
        Assert.True(world.TryBindChinese7937XuanbaoRules(valid, out reason), reason);
        Assert.Same(valid, world.Selected7937XuanbaoRules!.Source);
        Assert.Same(gold, world.Selected7937XuanbaoAttributes);
        Assert.NotSame(original, world.LegacyRelicFuseRules); Assert.False(original.IsCurrent(world));
        Assert.True(Prepare(world, owner).RuleGeneration.IsCurrent(world));
    }

    [Fact]
    public void IneligibleSelectedGoldCannotRetireActualGenerationAndValidRetryBinds()
    {
        var world = World(); var owner = Owner(); var preparation = Prepare(world, owner);
        var original = preparation.RuleGeneration;
        var invalid = PublicationGold(excludeEveryType: true);
        _ = LegacyBossSpecialDeathDropCatalog.FromWorldData(world, invalid); // Actual metadata-only binding accepts these source owners.
        Assert.False(world.TryBindChinese7937XuanbaoAttributes(invalid, out var reason));
        Assert.Equal("relicFuseRuleSuccessorUnavailable", reason);
        Assert.Null(world.Selected7937XuanbaoAttributes); Assert.Null(world.Selected7937XuanbaoRules);
        Assert.Same(original, world.LegacyRelicFuseRules); Assert.True(original.IsCurrent(world));
        Assert.True(preparation.IsCurrent(owner)); Assert.NotNull(Roll(preparation, owner));
        var valid = PublicationGold(excludeEveryType: false);
        Assert.True(world.TryBindChinese7937XuanbaoAttributes(valid, out reason), reason);
        Assert.Same(valid, world.Selected7937XuanbaoAttributes);
        Assert.NotSame(original, world.LegacyRelicFuseRules);
        Assert.True(Prepare(world, owner).RuleGeneration.IsCurrent(world));
    }

    [Fact]
    public void NoDefaultGenerationStillPermitsRealMetadataOnlyBindings()
    {
        var world = World(Caps().Where(row => row.Id != 3).ToArray());
        Assert.Null(world.LegacyRelicFuseRules);
        var gold = PublicationGold(excludeEveryType: false);
        Assert.True(world.TryBindChinese7937XuanbaoAttributes(gold, out var reason), reason);
        Assert.Same(gold, world.Selected7937XuanbaoAttributes); Assert.Null(world.LegacyRelicFuseRules);
        var rules = ComposedRuleSource(world);
        Assert.True(Chinese7937XuanbaoRuntimeRules.TryPrepare(world, rules, out var prepared, out var metadataReason), metadataReason);
        Assert.NotNull(prepared);
        Assert.True(world.TryBindChinese7937XuanbaoRules(rules, out reason), reason);
        Assert.Same(rules, world.Selected7937XuanbaoRules!.Source); Assert.Null(world.LegacyRelicFuseRules);
        var owner = Owner(); Assert.True(LegacyRelicFuseInputs.TryCapture(owner, 1, 2, out var inputs, out reason), reason);
        Assert.False(LegacyRelicFusePreparation.TryPrepare(world, inputs!, out _, out reason));
        Assert.Equal("relicFusePublishedRuleOwnerRequired", reason);
    }

    private static Chinese7937XuanbaoAttributeCatalog PublicationGold(bool excludeEveryType)
    {
        var rows = string.Join("\n", LegacyMaxRandomRelicTestData.ItemIds.Select(item =>
            "[" + item.ToString(CultureInfo.InvariantCulture) + "] = { " +
            string.Join(" ", Enumerable.Range(1, 14).Select(type =>
                "[" + type.ToString(CultureInfo.InvariantCulture) + "] = " + (excludeEveryType ? "true" : "false") + ",")) + " },"));
        return Chinese7937XuanbaoAttributeFixture.Parse(Chinese7937XuanbaoAttributeFixture.Module(goldRows: rows));
    }
}
