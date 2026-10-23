using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyLotteryExchangeMutationTests
{
    [Theory]
    [InlineData(LotteryCollectorWongNpcId, (byte)1, DisguiseAmuletItemId, (byte)0, (ushort)0)]
    [InlineData(LotteryCollectorWongNpcId, (byte)1, DisguiseAmuletItemId, (byte)2, (ushort)1)]
    [InlineData(LotteryCollectorWongNpcId, (byte)1, DisguiseAmuletItemId, (byte)7, (ushort)1)]
    [InlineData(MarketLadyLuckNpcId, (byte)2, LotteryTicketItemId, (byte)0, (ushort)0)]
    [InlineData(MarketLadyLuckNpcId, (byte)2, LotteryTicketItemId, (byte)2, (ushort)1)]
    [InlineData(MarketLadyLuckNpcId, (byte)2, LotteryTicketItemId, (byte)7, (ushort)1)]
    public async Task EmptyOrNoncanonicalBoundMaterialCannotAuthorizeExchange(
        uint npcId, byte optionId, uint itemId, byte bound, ushort stack)
    {
        var itemsRoot = CreatePlayerItemsRoot();
        var usersRoot = CreateUsersRoot();
        WriteUserSnapshot(usersRoot, CharacterUid, 25);
        var original = CreatePlayerItems(CreateInventoryItem(10, itemId, stack, bound));
        var originalCharacter = File.ReadAllBytes(GetUserSnapshotPath(usersRoot));
        var result = await InvokeAsync(npcId, optionId, original,
            CreateWorldDataCatalog(CreateItemType(SmallLotteryTicketItemId, "SmallLotteryTicket")),
            itemsRoot, usersRoot, conquerPoints: 25);
        AssertMutationFree(result, original, 25);
        AssertPersistedEquals(itemsRoot, original);
        Assert.Equal(originalCharacter, File.ReadAllBytes(GetUserSnapshotPath(usersRoot)));
        AssertMissingEligibleMaterialDialog(result, npcId);
        Assert.DoesNotContain(result.Sent, frame => ReadFrame(frame).Header.Opcode == LegacyGameItemPacket.Opcode);
    }

    [Theory]
    [InlineData(LotteryCollectorWongNpcId, (byte)1, DisguiseAmuletItemId, (byte)1)]
    [InlineData(LotteryCollectorWongNpcId, (byte)1, DisguiseAmuletItemId, (byte)2)]
    [InlineData(LotteryCollectorWongNpcId, (byte)1, DisguiseAmuletItemId, (byte)7)]
    [InlineData(MarketLadyLuckNpcId, (byte)2, LotteryTicketItemId, (byte)1)]
    [InlineData(MarketLadyLuckNpcId, (byte)2, LotteryTicketItemId, (byte)2)]
    [InlineData(MarketLadyLuckNpcId, (byte)2, LotteryTicketItemId, (byte)7)]
    public async Task DebitUsesFirstPositiveUnboundRowInOriginalEligibleOrder(
        uint npcId, byte optionId, uint itemId, byte bound)
    {
        var itemsRoot = CreatePlayerItemsRoot();
        var usersRoot = CreateUsersRoot();
        WriteUserSnapshot(usersRoot, CharacterUid, 25);
        var original = CreatePlayerItems(CreateInventoryItem(10, itemId, 0),
            CreateInventoryItem(20, itemId, 2, bound),
            CreateInventoryItem(40, itemId, 2), CreateInventoryItem(30, itemId, 1));
        var result = await InvokeAsync(npcId, optionId, original,
            CreateWorldDataCatalog(CreateItemType(SmallLotteryTicketItemId, "SmallLotteryTicket")),
            itemsRoot, usersRoot, conquerPoints: 25);
        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        foreach (var uid in new uint[] { 10, 20, 30 })
            AssertPlayerItemsEqual(CreatePlayerItems(Assert.Single(original.InventoryItems, item => item.Uid == uid)),
                CreatePlayerItems(Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == uid)));
        AssertPlayerItemsEqual(CreatePlayerItems(original.InventoryItems[2].WithStackSize(1)),
            CreatePlayerItems(Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 40)));
        var debit = AssertGameItem(result.Sent[0]);
        Assert.Equal(40U, debit.Uid);
        Assert.Equal(itemId, debit.ItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, debit.Mode);
        Assert.Equal((ushort)1, debit.StackSize);
        Assert.Equal((byte)0, debit.Bound);
        if (npcId == LotteryCollectorWongNpcId)
        {
            Assert.Equal(40, result.UpdatedSession.Character.ConquerPoints);
            Assert.Equal(6, result.Sent.Count);
            AssertConquerPoints(result.Sent[1], 40);
            AssertExactDialog(result.Sent.Skip(2).ToArray(), "You receive 15 ConquerPoints for you item.",
                "Ok~thank~you.", 10);
            Assert.Contains("ConquerPoints=40", ReadUserSnapshot(usersRoot));
        }
        else
        {
            Assert.Equal(25, result.UpdatedSession.Character.ConquerPoints);
            Assert.Equal(2, result.Sent.Count);
            var reward = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems,
                item => item.ItemId == SmallLotteryTicketItemId);
            var grant = AssertGameItem(result.Sent[1]);
            Assert.Equal(reward.Uid, grant.Uid);
            Assert.Equal(SmallLotteryTicketItemId, grant.ItemId);
            Assert.Equal(LegacyGameItemPacket.AddItemMode, grant.Mode);
            Assert.Equal((byte)0, grant.Bound);
            Assert.Equal((ushort)1, grant.StackSize);
            Assert.DoesNotContain(result.Sent, IsNpcReply);
            Assert.Contains("ConquerPoints=25", ReadUserSnapshot(usersRoot));
        }
        AssertPersistedEquals(itemsRoot, result.UpdatedSession.PlayerItems);
        Assert.Contains("removedUid=40 removedBound=0", result.Log);
    }

    private static void AssertMissingEligibleMaterialDialog(InvokeResult result, uint npcId)
    {
        if (npcId == LotteryCollectorWongNpcId)
            AssertExactDialog(result.Sent,
                "You~do~not~have~what~I~want.~Whenever~you~have~one~of~DisguiseAmulets,~LifeFruitBaskets~or~PenitenceAmulets,~just~come~here.",
                "See~you~again.", 10);
        else
            AssertExactDialog(result.Sent, "You don`t have any Lottery Tickets to exchange for the Small Lottery Tickets.",
                "Oops.", 123);
    }
}
