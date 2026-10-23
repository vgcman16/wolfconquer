using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySacredRefineryPackMutationTests
{
    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    public async Task CostAdmissionSkipsZeroStackAndDebitsThePositiveUnboundRow(byte zeroBound)
    {
        var itemsRoot = CreatePlayerItemsRoot();
        var zero = CreateInventoryItem(10, PackItemId, stackSize: 0, bound: zeroBound);
        var paid = CreateInventoryItem(20, PackItemId, stackSize: 2);
        var unrelated = CreateInventoryItem(30, 720_001);
        unrelated = unrelated.WithMaximumDurabilityAndPlus(unrelated.MaximumDurability, 3).WithSocketProgress(123);
        unrelated.OwnerName = "Retained";
        unrelated.Signature = "No cost";
        var original = CreatePlayerItems(zero, paid, unrelated);
        var result = await InvokeAsync(15, original,
            CreateWorldDataCatalog(CreateItemType(3_004_160, durability: 123)), itemsRoot);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        var rows = result.UpdatedSession.PlayerItems.InventoryItems;
        AssertPlayerItemsEqual(CreatePlayerItems(zero), CreatePlayerItems(Assert.Single(rows, row => row.Uid == 10)));
        AssertPlayerItemsEqual(CreatePlayerItems(paid.WithStackSize(1)), CreatePlayerItems(Assert.Single(rows, row => row.Uid == 20)));
        AssertPlayerItemsEqual(CreatePlayerItems(unrelated), CreatePlayerItems(Assert.Single(rows, row => row.Uid == 30)));
        Assert.Equal(3, result.Sent.Count);
        var debit = AssertGameItem(result.Sent[0]);
        Assert.Equal(20U, debit.Uid);
        Assert.Equal((ushort)1, debit.StackSize);
        Assert.Equal((byte)0, debit.Bound);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, debit.Mode);
        Assert.Equal(SuccessText, AssertNpcReply(result.Sent[1]).Text);
        Assert.Equal(3_004_160U, AssertGameItem(result.Sent[2]).ItemId);
        AssertPersistedEquals(itemsRoot, result.UpdatedSession.PlayerItems);
    }

    [Theory]
    [InlineData((ushort)1)]
    [InlineData((ushort)2)]
    public async Task CostAdmissionUsesFirstEligibleRowInPhysicalInventoryOrder(ushort firstStack)
    {
        var itemsRoot = CreatePlayerItemsRoot();
        var first = CreateInventoryItem(20, PackItemId, firstStack);
        var second = CreateInventoryItem(10, PackItemId);
        second.Signature = "Second eligible";
        var original = CreatePlayerItems(first, second);
        var result = await InvokeAsync(15, original,
            CreateWorldDataCatalog(CreateItemType(3_004_160)), itemsRoot);

        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        var rows = result.UpdatedSession.PlayerItems.InventoryItems;
        AssertPlayerItemsEqual(CreatePlayerItems(second), CreatePlayerItems(Assert.Single(rows, row => row.Uid == 10)));
        if (firstStack == 1)
        {
            Assert.DoesNotContain(rows, row => row.Uid == 20);
            AssertPackRemoval(result.Sent[0], 20);
        }
        else
        {
            AssertPlayerItemsEqual(CreatePlayerItems(first.WithStackSize(1)), CreatePlayerItems(Assert.Single(rows, row => row.Uid == 20)));
            var debit = AssertGameItem(result.Sent[0]);
            Assert.Equal(20U, debit.Uid);
            Assert.Equal((ushort)1, debit.StackSize);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, debit.Mode);
        }
        Assert.Equal(3, result.Sent.Count);
        Assert.Equal(SuccessText, AssertNpcReply(result.Sent[1]).Text);
        Assert.Equal(3_004_160U, AssertGameItem(result.Sent[2]).ItemId);
        AssertPersistedEquals(itemsRoot, result.UpdatedSession.PlayerItems);
    }

    [Fact]
    public async Task ForeignSingletonCannotFalselyFreeCapacityForStackedEligibleCost()
    {
        var itemsRoot = CreatePlayerItemsRoot();
        var rows = new List<PlayerItemSnapshotItem>
        {
            CreateInventoryItem(10, PackItemId, bound: 1),
            CreateInventoryItem(20, PackItemId, stackSize: 2),
        };
        rows.AddRange(Enumerable.Range(100, 38).Select(uid => CreateInventoryItem((uint)uid, (uint)(410_000 + uid))));
        var original = CreatePlayerItems(rows.ToArray());
        var preimage = PlayerItemSnapshotLoader.ToLegacyBytes(original);
        var result = await InvokeAsync(15, original,
            CreateWorldDataCatalog(CreateItemType(3_004_160)), itemsRoot);

        AssertMutationFree(result, original);
        Assert.Same(result.OriginalSession.Character, result.UpdatedSession.Character);
        Assert.Equal(preimage, PlayerItemSnapshotLoader.ToLegacyBytes(result.UpdatedSession.PlayerItems));
        Assert.Empty(result.Sent);
        Assert.Contains("inventory-full-after-planned-pack-removal", result.Log);
        Assert.Equal(preimage, File.ReadAllBytes(Path.Combine(itemsRoot, CharacterUid + ".bin")));
        AssertPersistedEquals(itemsRoot, original);
    }

    [Fact]
    public async Task ZeroUnboundAndPositiveForeignCostCannotAuthorizeAReward()
    {
        var itemsRoot = CreatePlayerItemsRoot();
        var original = CreatePlayerItems(CreateInventoryItem(10, PackItemId, stackSize: 0),
            CreateInventoryItem(20, PackItemId, bound: 1));
        var result = await InvokeAsync(15, original,
            CreateWorldDataCatalog(CreateItemType(3_004_160)), itemsRoot);

        AssertMutationFree(result, original);
        Assert.Same(result.OriginalSession.Character, result.UpdatedSession.Character);
        Assert.Equal("Sorry, you don`t have a SacredRefineryPack.", AssertNpcReply(Assert.Single(result.Sent)).Text);
        AssertPersistedEquals(itemsRoot, original);
    }
}
