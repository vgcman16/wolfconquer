using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyEventUtilityExchangeMutationTests
{
    [Theory]
    [InlineData((byte)0, (ushort)0)]
    [InlineData((byte)2, (ushort)1)]
    [InlineData((byte)7, (ushort)1)]
    public async Task IneligibleStardustCannotAuthorizeOrDebitExchange(byte bound, ushort stack)
    {
        var original = Items(Item(10, StardustItemId, stack, bound));
        var result = await InvokeAsync(ElementalPoolNpcId, 10, original,
            Catalog(ItemType(ShadowScrollItemId, "ShadowScroll")));
        AssertMutationFree(result, original);
        AssertExactDialog(result.Sent, "Sorry, you not have the Stardust in your inventory.", "Okay.", 203);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)7)]
    public async Task StardustSkipsEmptyAndBoundRowsBeforeFirstEligibleCost(byte bound)
    {
        var original = Items(Item(10, StardustItemId, 2, bound),
            Item(15, StardustItemId, 0), Item(20, StardustItemId, 2));
        var result = await InvokeAsync(ElementalPoolNpcId, 10, original,
            Catalog(ItemType(ShadowScrollItemId, "ShadowScroll", 222)));
        Assert.True(result.Handled);
        Assert.Equal(1, result.CommitCount);
        Assert.Same(original.InventoryItems[0], Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 10));
        Assert.Same(original.InventoryItems[1], Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 15));
        AssertPlayerItemsEqual(Items(original.InventoryItems[2].WithStackSize(1)),
            Items(Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 20)));
        var reward = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == ShadowScrollItemId);
        Assert.Collection(result.Sent,
            frame => AssertItemUpdate(frame, 20, StardustItemId, 1, 0),
            frame => AssertGrant(frame, reward.Uid, ShadowScrollItemId, 222));
        Assert.True(result.PersistenceMatchedAtCommit);
        Assert.True(result.SessionCommittedBeforeFirstSend);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)7)]
    public async Task FlowerDebitSkipsEmptyAndBoundRowsInBothCostClasses(byte bound)
    {
        var original = Items(Item(10, RedFlowerItemId, 4, bound), Item(15, RedFlowerItemId, 0),
            Item(20, RedFlowerItemId, 4), Item(30, WhiteFlowerItemId, 4, bound),
            Item(35, WhiteFlowerItemId, 0), Item(40, WhiteFlowerItemId, 4));
        var result = await InvokeAsync(SwapperStarryNpcId, 3, original,
            Catalog(ItemType(YinYangSealItemId, "YinYangSeal")), rateRoll: 29);
        Assert.True(result.Handled);
        Assert.Equal(1, result.CommitCount);
        foreach (var uid in new uint[] { 10, 15, 30, 35 })
            Assert.Same(Assert.Single(original.InventoryItems, item => item.Uid == uid),
                Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == uid));
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid is 20 or 40);
        var reward = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == YinYangSealItemId);
        Assert.Collection(result.Sent,
            frame => AssertItemUpdate(frame, 20, RedFlowerItemId, 3, 0),
            frame => AssertItemUpdate(frame, 20, RedFlowerItemId, 2, 0),
            frame => AssertItemUpdate(frame, 20, RedFlowerItemId, 1, 0),
            frame => AssertItemRemoval(frame, 20),
            frame => AssertItemUpdate(frame, 40, WhiteFlowerItemId, 3, 0),
            frame => AssertItemUpdate(frame, 40, WhiteFlowerItemId, 2, 0),
            frame => AssertItemUpdate(frame, 40, WhiteFlowerItemId, 1, 0),
            frame => AssertItemRemoval(frame, 40),
            frame => AssertGrant(frame, reward.Uid, YinYangSealItemId, 1));
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
    }

    [Fact]
    public async Task EmptyUnboundNecklaceCannotAuthorizeBuddhaExchange()
    {
        const uint necklace = 3_007_563;
        var original = Items(Item(10, necklace, 0));
        var result = await InvokeAsync(25_003, 2, original,
            Catalog(ItemType(3_007_564, "BoundlessHeart"), ItemType(3_007_565, "RosewoodIncense")));
        AssertMutationFree(result, original);
        AssertExactDialog(result.Sent, "Please come back when you will have 1 Buddha`sNecklace in your inventory.", "Oh.", 226);
        Assert.Contains("reason=unbound-necklace-missing", result.Log);
    }

    [Theory]
    [InlineData(29U, 3_007_564U)]
    [InlineData(30U, 3_007_565U)]
    public async Task BuddhaSkipsEmptyNecklaceAndDebitsFirstPositiveUnboundRow(uint roll, uint rewardId)
    {
        const uint necklace = 3_007_563;
        var original = Items(Item(10, necklace, 0), Item(20, necklace, 2));
        var result = await InvokeAsync(25_003, 2, original,
            Catalog(ItemType(3_007_564, "BoundlessHeart"), ItemType(3_007_565, "RosewoodIncense")),
            rateRoll: roll);
        Assert.True(result.Handled);
        Assert.Equal(1, result.CommitCount);
        Assert.Same(original.InventoryItems[0], Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 10));
        Assert.Equal((ushort)1, Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 20).StackSize);
        var reward = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == rewardId);
        Assert.Collection(result.Sent,
            frame => AssertItemUpdate(frame, 20, necklace, 1, 0),
            frame =>
            {
                var reply = AssertNpcReply(frame, LegacyNpcReplyInteractType.MessageBox);
                Assert.Equal(roll == 29 ? "You received BoundlessHeart." : "You received RosewoodIncense.", reply.Text);
            },
            frame => AssertGrant(frame, reward.Uid, rewardId, 1));
        Assert.True(result.PersistenceMatchedAtCommit);
        Assert.True(result.SessionCommittedBeforeFirstSend);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
    }
}
