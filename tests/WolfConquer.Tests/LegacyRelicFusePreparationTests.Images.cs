using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicFusePreparationTests
{
    [Fact]
    public void ProjectionUsesOnlyItsSuppliedUidAndOwnsCompletePrivateImages()
    {
        var unrelated = Item(8, 4_100_005); unrelated.OwnerName = "saved-owner";
        var owner = Owner() with { InventoryItems = new[] { Item(1), Item(2, bound: 1), unrelated } };
        var prepared = Prepare(World(), owner); var roll = Roll(prepared, owner);
        var before = prepared.CopyBeforeImage();
        Assert.True(LegacyRelicFuseProjection.TryCreate(owner, roll, 71, out var result, out var reason), reason);
        var projection = Assert.IsType<LegacyRelicFuseProjection>(result);
        Assert.Equal(71U, projection.ResultUid);
        Assert.Equal(new[] { 8U }, projection.Items.InventoryItems.Select(row => row.Uid));
        Assert.Equal(new[] { 1U, 2U, 71U }, projection.Items.RelicFuseItems.Select(row => row.Uid));
        Assert.Equal(new ushort[] { 213, 214, 215 }, projection.Items.RelicFuseItems.Select(row => row.Position));
        Assert.Equal((byte)1, projection.ResultItem.Bound);
        Assert.Equal(Words(roll), new[] { projection.ResultItem.Attribute1, projection.ResultItem.Attribute2,
            projection.ResultItem.Attribute3, projection.ResultItem.Attribute4, projection.ResultItem.Attribute5 });
        unrelated.OwnerName = "changed-live";
        var returned = projection.Items; returned.InventoryItems[0].OwnerName = "changed-copy";
        var bytes = projection.CopyAfterImage(); bytes[0] ^= 0xff;
        before[0] ^= 0xff;
        Assert.Equal("saved-owner", projection.Items.InventoryItems[0].OwnerName);
        Assert.NotEqual(before, prepared.CopyBeforeImage());
        Assert.NotEqual(bytes, projection.CopyAfterImage());
        Assert.Empty(owner.RelicFuseItems);
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(1U)]
    [InlineData(2U)]
    [InlineData(90U)]
    public void LocalUidCollisionOrZeroRefusesWithEmptyDataOutput(uint supplied)
    {
        var owner = Owner() with { WarehouseItems = new[] {
            Item(80, deposits: [Item(90)]).WithWarehouseId(44) } };
        var roll = Roll(Prepare(World(), owner), owner);
        var before = PlayerItemSnapshotLoader.ToLegacyBytes(owner);
        Assert.False(LegacyRelicFuseProjection.TryCreate(owner, roll, supplied, out var projection, out var reason));
        Assert.Null(projection); Assert.NotEmpty(reason);
        Assert.Equal(before, PlayerItemSnapshotLoader.ToLegacyBytes(owner));
    }

    [Theory]
    [InlineData("grandchild")]
    [InlineData("text")]
    [InlineData("placement")]
    public void CompleteUnrelatedLossyTailRefusesBeforeEntropy(string defect)
    {
        var bad = defect switch
        {
            "grandchild" => Item(3, deposits: [Item(4, deposits: [Item(5)])]).WithWarehouseId(44),
            "placement" => Item(3).WithPosition(1),
            _ => Item(3),
        };
        if (defect == "text") bad.OwnerName = new string('x', 33);
        var owner = defect == "grandchild" ? Owner() with { WarehouseItems = new[] { bad } }
            : Owner() with { InventoryItems = new[] { Item(1), Item(2), bad } };
        Assert.True(LegacyRelicFuseInputs.TryCapture(owner, 1, 2, out var inputs, out _));
        Assert.False(LegacyRelicFusePreparation.TryPrepare(World(), inputs!, out var preparation, out var reason));
        Assert.Null(preparation); Assert.Equal("relicFuseItemGraphNotPersistable", reason);
    }

    [Fact]
    public void ProjectionCannotChangeResultIdentityOrDropInputBinding()
    {
        var owner = Owner(); var roll = Roll(Prepare(World(), owner), owner);
        Assert.True(LegacyRelicFuseRollData.TryFromRecorded(1, 2, 4_100_003, roll.Bound, roll.Durability,
            roll.Attribute1, roll.Attribute2, roll.Attribute3, roll.Attribute4, roll.Attribute5, out var wrongItem));
        Assert.False(LegacyRelicFuseProjection.TryCreate(owner, wrongItem!, 31, out var projection, out _));
        Assert.Null(projection);
        Assert.True(LegacyRelicFuseRollData.TryFromRecorded(1, 2, roll.ItemId, 0, roll.Durability,
            roll.Attribute1, roll.Attribute2, roll.Attribute3, roll.Attribute4, roll.Attribute5, out var wrongBound));
        Assert.False(LegacyRelicFuseProjection.TryCreate(owner, wrongBound!, 31, out projection, out _));
        Assert.Null(projection);
    }
}
