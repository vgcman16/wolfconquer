using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicFuseReturnPreparationTests
{
    [Fact]
    public void EqualValuedHeldReplacementCannotUseTheActualSelectedRootWitness()
    {
        var held = new[] { Item(100, 213), Item(101, 214), Item(102, 215) };
        var owner = Owner(held: held); var preparation = Prepare(owner);
        held[0] = Item(100, 213);
        Assert.False(preparation.IsCurrent(owner));
        Assert.False(preparation.TryProject(owner, out var projection, out var reason));
        Assert.Null(projection); Assert.Equal("relicFuseReturnOwnerChanged", reason);
    }

    [Fact]
    public void OwnerReplacementMutablePerfectionAndAnAddedTailCannotRefreshThePrivateRecipe()
    {
        var owner = Owner(); var preparation = Prepare(owner);
        Assert.False(preparation.IsCurrent(owner with { }));
        owner.RelicFuseItems[1].PerfectionProgress = 1;
        Assert.False(preparation.IsCurrent(owner));
        Assert.False(preparation.TryProject(owner, out var projection, out _)); Assert.Null(projection);
        var inventory = new List<PlayerItemSnapshotItem> { Item(1) };
        owner = new(inventory, new[] { Item(100, 213), Item(101, 214), Item(102, 215) }, [], [], [], []);
        preparation = Prepare(owner); inventory.Add(Item(2));
        Assert.False(preparation.TryProject(owner, out projection, out _)); Assert.Null(projection);
    }

    [Fact]
    public void ChangingAnUnrelatedDepositedOwnerFieldInvalidatesTheCompleteGraph()
    {
        var child = Item(501); var owner = Store(Owner(), 6, new[] { Item(500, warehouse: 44, deposits: new[] { child }) });
        var preparation = Prepare(owner);
        child.OwnerName = "changed";
        Assert.False(preparation.IsCurrent(owner));
        Assert.False(preparation.TryProject(owner, out var projection, out _)); Assert.Null(projection);
        Assert.Equal(3, owner.RelicFuseItems.Count);
    }
}
