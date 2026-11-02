using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicFuseInputsTests
{
    public static IEnumerable<object[]> CorruptTailCases() =>
        Enumerable.Range(0, 7).SelectMany(group => new[] { "null", "uid", "item", "deposits", "owner", "signature" }
            .Select(kind => new object[] { group, kind }));

    [Theory]
    [MemberData(nameof(CorruptTailCases))]
    public void EveryContainerCorruptTailIsScannedAfterBothInputsWereAlreadyFound(int group, string kind)
    {
        PlayerItemSnapshotItem bad = kind switch
        {
            "null" => null!, "uid" => Item(0), "item" => Item(100, itemId: 0),
            "deposits" => new PlayerItemSnapshotItem { Uid = 100, ItemId = 4_100_001, DepositedItems = null! },
            _ => Item(100),
        };
        if (kind == "owner") bad.OwnerName = null!;
        if (kind == "signature") bad.Signature = null!;
        var owner = Owner();
        var rows = group == 0 ? owner.InventoryItems.Concat([Item(99), bad]).ToArray() : new[] { Item(99), bad };
        Refuses(Store(owner, group, rows), expected: "relicFuseItemGraphInvalid");
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void NullRootContainersAreRequiredState(int group) =>
        Refuses(Store(Owner(), group, null!), expected: "relicFuseItemGraphInvalid");

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void DuplicateUidTailsInEveryContainerCannotBorrowTheInventorySelection(int group)
    {
        var owner = Owner(); var duplicate = Item(1);
        var rows = group == 0 ? owner.InventoryItems.Concat([Item(99), duplicate]).ToArray() : new[] { Item(99), duplicate };
        Refuses(Store(owner, group, rows), expected: "relicFuseItemGraphInvalid");
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void DepositedCorruptTailInEveryContainerIsNotHiddenByValidRoots(int group)
    {
        var owner = Owner(); var parent = Item(99, deposits: [Item(100), Item(0)]);
        var rows = group == 0 ? owner.InventoryItems.Append(parent).ToArray() : new[] { parent };
        Refuses(Store(owner, group, rows), expected: "relicFuseItemGraphInvalid");
    }

    [Fact]
    public void ObjectAliasesAndDepositedCyclesRejectWithoutRecursiveTraversal()
    {
        var shared = Item(100);
        Refuses(Owner([Item(1), Item(2), Item(3, deposits: [shared]), Item(4, deposits: [shared])]),
            expected: "relicFuseItemGraphInvalid");
        var cycle = new List<PlayerItemSnapshotItem>(); var parent = Item(3, deposits: cycle); cycle.Add(parent);
        Refuses(Owner([Item(1), Item(2), parent]), expected: "relicFuseItemGraphInvalid");
        var twoWay = new List<PlayerItemSnapshotItem>(); var otherWay = new List<PlayerItemSnapshotItem>();
        var first = Item(3, deposits: twoWay); var second = Item(4, deposits: otherWay);
        twoWay.Add(second); otherWay.Add(first);
        Refuses(Owner([Item(1), Item(2), first]), expected: "relicFuseItemGraphInvalid");
    }

    [Fact]
    public void ThirtyTwoDescendantLevelsAreFiniteAndTheNextLevelRefuses()
    {
        PlayerItemSnapshotItem Chain(int depth)
        {
            var child = Item((uint)(1000 + depth));
            for (var index = depth - 1; index >= 0; index--) child = Item((uint)(1000 + index), deposits: [child]);
            return child;
        }
        var edge = Owner([Item(1), Item(2), Chain(LegacyRelicFuseInputs.MaximumGraphDepth)]);
        var inputs = Capture(edge);
        Assert.Equal(LegacyRelicFuseInputs.MaximumGraphDepth, inputs.MaximumDepth);
        Assert.True(inputs.IsCurrent(edge));
        Refuses(Owner([Item(1), Item(2), Chain(LegacyRelicFuseInputs.MaximumGraphDepth + 1)]), expected: "relicFuseItemGraphInvalid");
    }

    [Fact]
    public void CompleteTotalRowBoundIncludesItsEdgeAndRejectsAnAdditionalRoot()
    {
        var rows = Enumerable.Range(1, LegacyRelicFuseInputs.MaximumRows).Select(index => Item((uint)index)).ToArray();
        var edge = Owner(rows);
        Assert.True(Capture(edge).IsCurrent(edge));
        Refuses(Owner(rows.Append(Item(100_000)).ToArray()), expected: "relicFuseItemGraphInvalid");
    }
}
