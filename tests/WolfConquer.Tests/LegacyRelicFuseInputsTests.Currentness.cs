using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicFuseInputsTests
{
    [Fact]
    public void SameSnapshotListReplacementEqualValuedRowsAndReorderingAreStale()
    {
        var rows = new List<PlayerItemSnapshotItem> { Item(1), Item(2), Item(3), Item(4) };
        var owner = Owner(rows); var inputs = Capture(owner);
        rows[0] = Item(1);
        Assert.False(inputs.IsCurrent(owner));
        var fresh = Capture(owner);
        (rows[2], rows[3]) = (rows[3], rows[2]);
        Assert.False(fresh.IsCurrent(owner));
    }

    [Fact]
    public void AddedMalformedTailOrHeldStateAfterCaptureCannotReuseTheWitness()
    {
        var rows = new List<PlayerItemSnapshotItem> { Item(1), Item(2) };
        var owner = Owner(rows); var inputs = Capture(owner);
        rows.Add(Item(0));
        Assert.False(inputs.IsCurrent(owner));
        var held = new List<PlayerItemSnapshotItem>();
        owner = Store(Owner(), 1, held); inputs = Capture(owner);
        held.Add(Item(3, position: 213));
        Assert.False(inputs.IsCurrent(owner));
    }

    [Fact]
    public void DepositOrderParentsEmptyListContentsAndNewMaximumInvalidateCurrentness()
    {
        var children = new List<PlayerItemSnapshotItem> { Item(100), Item(200) };
        var parent = Item(3, deposits: children);
        var owner = Owner([Item(1), Item(2), parent]); var inputs = Capture(owner);
        (children[0], children[1]) = (children[1], children[0]);
        Assert.False(inputs.IsCurrent(owner));
        var empty = new List<PlayerItemSnapshotItem>(); parent = Item(3, deposits: empty);
        owner = Owner([Item(1), Item(2), parent]); inputs = Capture(owner);
        empty.Add(Item(900));
        Assert.False(inputs.IsCurrent(owner));
        var a = new List<PlayerItemSnapshotItem> { Item(100) }; var b = new List<PlayerItemSnapshotItem>();
        owner = Owner([Item(1), Item(2), Item(3, deposits: a), Item(4, deposits: b)]); inputs = Capture(owner);
        b.Add(a[0]); a.Clear();
        Assert.False(inputs.IsCurrent(owner));
    }

    [Theory]
    [InlineData("level")]
    [InlineData("progress")]
    [InlineData("owner")]
    [InlineData("name")]
    [InlineData("signature")]
    public void MutablePhysicalMetadataOnAnUnselectedTailIsAlsoWitnessed(string change)
    {
        var tail = Item(3); var owner = Owner([Item(1), Item(2), tail]); var inputs = Capture(owner);
        switch (change)
        {
            case "level": tail.PerfectionLevel = 1; break;
            case "progress": tail.PerfectionProgress = 1; break;
            case "owner": tail.OwnerUid = 100; break;
            case "name": tail.OwnerName = "changed"; break;
            default: tail.Signature = "changed"; break;
        }
        Assert.False(inputs.IsCurrent(owner));
    }

    [Fact]
    public void SelectedAttributeOrBoundReplacementCannotBorrowAnEarlierPreparation()
    {
        var rows = new[] { Item(1), Item(2) }; var owner = Owner(rows); var inputs = Capture(owner);
        rows[0] = Item(1, attribute: 999_114);
        Assert.False(inputs.IsCurrent(owner));
        inputs = Capture(owner);
        rows[1] = Item(2, bound: 1);
        Assert.False(inputs.IsCurrent(owner));
        Assert.Equal((byte)0, inputs.ResultBound);
    }
}
