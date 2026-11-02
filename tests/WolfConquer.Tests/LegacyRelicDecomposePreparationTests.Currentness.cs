using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicDecomposePreparationTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void CompleteBatchAtExactlyFortySlotsPreparesButOneExtraSlotRefuses(int count)
    {
        var selected = Enumerable.Range(0, count).Select(index => Item(First + (uint)index, 4_100_001U + (uint)index)).ToArray();
        var originalCount = 40 - count * 8;
        var inventory = selected.Concat(Enumerable.Range(0, originalCount - count).Select(index => Item(100U + (uint)index, 1_000_000))).ToArray();
        var owner = PlayerItemSnapshot.Empty with { InventoryItems = inventory };
        var request = Request(selected.Select(row => row.Uid).ToArray());
        var prepared = Prepare(owner, request);
        var before = PlayerItemSnapshotLoader.ToLegacyBytes(owner);
        Assert.True(prepared.Recipe.TryProject(owner, 1_000, count * 9, out var projection, out var reason), reason);
        Assert.Equal(40, Assert.IsType<LegacyRelicDecomposeProjection>(projection).Items.InventoryItems.Count);
        Assert.Equal(before, PlayerItemSnapshotLoader.ToLegacyBytes(owner));
        Refuses(owner with { InventoryItems = inventory.Append(Item(500, 1_000_000)).ToArray() }, request);
    }

    [Fact]
    public void FiveSelectedRelicsCannotFitTheirFortyFiveSeparateRewards()
    {
        var rows = Enumerable.Range(0, 5).Select(index => Item(First + (uint)index, 4_100_001U + (uint)index)).ToArray();
        Refuses(PlayerItemSnapshot.Empty with { InventoryItems = rows }, Request(rows.Select(row => row.Uid).ToArray()));
    }

    [Theory]
    [InlineData("unrelated-scalar")] [InlineData("same-value-replacement")] [InlineData("reordered-list")]
    public void APreparationRequiresTheExactStillCurrentWholeGraph(string change)
    {
        var owner = Owner(); var prepared = Prepare(owner, Request(First));
        Assert.False(prepared.IsCurrent(owner with { }));
        if (change == "unrelated-scalar") owner.InventoryItems[^1].OwnerUid = 44;
        else
        {
            var rows = Assert.IsType<PlayerItemSnapshotItem[]>(owner.InventoryItems);
            if (change == "same-value-replacement") rows[^1] = Item(Kept, 1_000_000);
            else (rows[0], rows[1]) = (rows[1], rows[0]);
        }
        Assert.False(prepared.IsCurrent(owner));
        Assert.True(Prepare(owner, Request(First)).IsCurrent(owner));
    }

    [Theory]
    [InlineData(0U, 9)] [InlineData(90U, 9)] [InlineData(1_000U, 8)]
    [InlineData(1_000U, 10)] [InlineData(1_000U, -1)] [InlineData(4_294_967_288U, 9)]
    public void LocalRangeValidationRefusesZeroCollisionWrongCountAndOverflow(uint first, int count)
    {
        var owner = Owner(); var prepared = Prepare(owner, Request(First));
        var before = PlayerItemSnapshotLoader.ToLegacyBytes(owner);
        Assert.False(prepared.Recipe.TryProject(owner, first, count, out var projection, out var reason));
        Assert.Null(projection); Assert.NotEmpty(reason);
        Assert.Equal(before, PlayerItemSnapshotLoader.ToLegacyBytes(owner));
    }

    [Fact]
    public void ExactUInt32EndIsValidDataWithoutIssuingAnyGlobalUidAuthority()
    {
        var owner = Owner(); var prepared = Prepare(owner, Request(First));
        Assert.True(prepared.Recipe.TryProject(owner, uint.MaxValue - 8, 9, out var projection, out var reason), reason);
        var rewards = Assert.IsType<LegacyRelicDecomposeProjection>(projection).Items.InventoryItems.Where(row => row.ItemId == Crystal).ToArray();
        Assert.Equal(9, rewards.Length); Assert.Equal(uint.MaxValue - 8, rewards[0].Uid); Assert.Equal(uint.MaxValue, rewards[^1].Uid);
        Assert.DoesNotContain(owner.InventoryItems, row => row.ItemId == Crystal);
    }
}
