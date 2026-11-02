using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicFusePreparationTests
{
    [Fact]
    public void RealCatalogConstructionPublishesDeepOwnedCapsInsteadOfMutableAliases()
    {
        var rows = Caps().Select(row => row with { Values = row.Values.ToArray() }).ToArray();
        var world = World(rows); var owner = Owner(); var prepared = Prepare(world, owner);
        var generation = Assert.IsType<LegacyRelicFuseRuleGeneration>(world.LegacyRelicFuseRules);
        ((int[])rows[2].Values)[^1] = 70_000;
        ((string[])world.ItemsById[4_100_001][0].Fields)[0] = "7";
        Assert.True(generation.IsCurrent(world));
        Assert.True(prepared.IsCurrent(owner));
        Assert.Same(generation, Prepare(world, owner).RuleGeneration);
        Assert.All(Words(Roll(prepared, owner)), raw => Assert.InRange(raw / 1000, 0U, 400U));
        Assert.True(generation.TryCopyFor(4_100_001, out var types, out var caps));
        types[3] = false; caps[3] = 1;
        Assert.True(generation.TryCopyFor(4_100_001, out types, out caps));
        Assert.True(types[3]); Assert.Equal(400, caps[3]);
    }

    [Fact]
    public void MissingOptionalFeatureMetadataPublishesNoAcceptingRuleOwner()
    {
        var world = World(Caps().Where(row => row.Id != 3).ToArray());
        Assert.Null(world.LegacyRelicFuseRules);
        Assert.NotEmpty(world.Items);
        Assert.True(LegacyRelicFuseInputs.TryCapture(Owner(), 1, 2, out var inputs, out _));
        Assert.False(LegacyRelicFusePreparation.TryPrepare(world, inputs!, out var prepared, out var reason));
        Assert.Null(prepared); Assert.Equal("relicFusePublishedRuleOwnerRequired", reason);
    }

    [Fact]
    public void ExplicitSelectedCatalogBindingPublishesASuccessorAndInvalidatesOldPreparationBeforeEntropy()
    {
        var world = World(); var owner = Owner(); var prepared = Prepare(world, owner);
        var old = prepared.RuleGeneration;
        var compose = string.Concat(world.XuanbaoComposeAttributeLimits.Select(row =>
            FormattableString.Invariant($"{row.Id}@@{row.Values[0]}@@{row.Values[1]}@@{row.Values[2]}@@\r\n")));
        var random = string.Concat(world.XuanbaoRandomAttributes.Reverse().Select(row =>
            FormattableString.Invariant($"{row.ItemTypeId}@@{row.AttributeType}@@{row.MinValue}@@{row.MaxValue}@@\r\n")));
        var selected = Chinese7937XuanbaoRulesFixture.Parse(compose, random);
        Assert.True(world.TryBindChinese7937XuanbaoRules(selected, out var reason), reason);
        Assert.NotSame(old, world.LegacyRelicFuseRules);
        Assert.False(old.IsCurrent(world));
        var actual = new Random(51); var expected = new Random(51);
        Assert.False(prepared.TryRoll(owner, actual, out var roll, out reason));
        Assert.Null(roll); Assert.Equal("relicFusePreparedOwnerChanged", reason);
        Assert.Equal(expected.Next(), actual.Next());
        var successor = Prepare(world, owner);
        Assert.Same(world.Selected7937XuanbaoRules, successor.RuleGeneration.RuleSource);
        Assert.True(successor.RuleGeneration.IsCurrent(world));
        Assert.NotNull(Roll(successor, owner));
        var published = successor.RuleGeneration;
        Assert.True(world.TryBindChinese7937XuanbaoRules(selected, out reason), reason);
        Assert.Same(published, world.LegacyRelicFuseRules);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void MissingMalformedOrDuplicateComposeTailCannotCreateAnAcceptingGeneration(int defect)
    {
        var caps = Caps().ToList();
        if (defect == 0) caps.Add(caps[0] with { Id = 15 });
        else if (defect == 1) caps.Add(caps[0] with { Id = 14, Values = new[] { 0, 1 } });
        else caps.Add(caps[0] with { Values = new[] { 0, 1, 401 } });
        WorldDataCatalog world;
        try { world = World(caps); }
        catch (Exception error) when (error is InvalidDataException or ArgumentException) { return; }
        Assert.Null(world.LegacyRelicFuseRules);
    }
}
