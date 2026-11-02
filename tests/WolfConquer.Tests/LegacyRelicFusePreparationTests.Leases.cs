using WolfConquer.Core.Client;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicFusePreparationTests
{
    private static Chinese7937XuanbaoRuleCatalogs SelectedRules(WorldDataCatalog world)
    {
        var compose = string.Concat(world.XuanbaoComposeAttributeLimits.Select(row =>
            FormattableString.Invariant($"{row.Id}@@{row.Values[0]}@@{row.Values[1]}@@{row.Values[2]}@@\r\n")));
        var random = string.Concat(world.XuanbaoRandomAttributes.Select(row =>
            FormattableString.Invariant($"{row.ItemTypeId}@@{row.AttributeType}@@{row.MinValue}@@{row.MaxValue}@@\r\n")));
        return Chinese7937XuanbaoRulesFixture.Parse(compose, random);
    }

    [Fact]
    public void ActualCatalogLeasePreventsEvenReentrantSuccessorBindingUntilDispose()
    {
        var world = World(); var original = world.LegacyRelicFuseRules!;
        var selected = SelectedRules(world);
        Assert.True(world.TryAcquireLegacyRelicFuseRuleLease(original, out var acquired));
        var lease = Assert.IsType<WorldDataCatalog.LegacyRelicFuseRuleLease>(acquired);
        try
        {
            Assert.True(lease.IsCurrent);
            Assert.False(world.TryBindChinese7937XuanbaoRules(selected, out var reason));
            Assert.Equal("relicFuseRuleGenerationInUse", reason);
            Assert.False(world.TryBindChinese7937XuanbaoAttributes(null!, out reason));
            Assert.Equal("relicFuseRuleGenerationInUse", reason);
            Assert.Null(world.Selected7937XuanbaoRules); Assert.Null(world.Selected7937XuanbaoAttributes);
            Assert.True(lease.IsCurrent); Assert.Same(original, world.LegacyRelicFuseRules);
        }
        finally { lease.Dispose(); }
        Assert.False(lease.IsCurrent); lease.Dispose();
        Assert.True(world.TryBindChinese7937XuanbaoRules(selected, out var boundReason), boundReason);
        Assert.False(original.IsCurrent(world));
        Assert.False(world.TryAcquireLegacyRelicFuseRuleLease(original, out var stale));
        Assert.Null(stale);
        Assert.True(world.TryAcquireLegacyRelicFuseRuleLease(world.LegacyRelicFuseRules!, out var successor));
        using (successor) Assert.True(successor!.IsCurrent);
    }

    [Fact]
    public void AForeignCatalogGenerationCannotAcquireOrBlockTheActualPublicationOwner()
    {
        var world = World(); var foreign = World().LegacyRelicFuseRules!;
        Assert.False(world.TryAcquireLegacyRelicFuseRuleLease(foreign, out var refused));
        Assert.Null(refused);
        Assert.Throws<InvalidOperationException>(() => WorldDataCatalog.LegacyRelicFuseRuleLease.Issue(
            world, world.LegacyRelicFuseRules!, new object()));
        Assert.True(world.TryBindChinese7937XuanbaoRules(SelectedRules(world), out var reason), reason);
    }
}
