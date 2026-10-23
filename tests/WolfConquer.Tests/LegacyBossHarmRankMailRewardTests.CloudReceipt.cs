using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    [Fact]
    public async Task HandlerMap1020Family13Quest1337NoOwnedScrollDoesNotAnnounceGrantAndFullBagRefuses()
    {
        var noOwnedScroll = await KillMap1020Family13SnakemanMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_337, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceQuest1341Rate: false,
            forceQuest1333Rate: false);

        Assert.DoesNotContain(noOwnedScroll.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 720_856U);
        Assert.DoesNotContain(ParseGameItems(noOwnedScroll.Sent), packet => packet.ItemId == 720_856U);
        Assert.DoesNotContain(ParseNpcReplies(noOwnedScroll.Sent), packet =>
            packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            packet.Text == "You~received~the~Cloud~Riding~Scroll!");
        Assert.Contains("Cloud Riding Scroll no-add branch", noOwnedScroll.Log);

        var fullInventory = await KillMap1020Family13SnakemanMonsterAsync(
            [
                new PlayerQuestSnapshotEntry(1_337, PlayerQuestStatus.Accepted, 0, [0U]),
            ],
            forceQuest1341Rate: false,
            forceQuest1333Rate: false,
            inventoryItems: Enumerable
                .Range(1, 40)
                .Select(index => CreateInventoryItem((uint)index, (uint)(940_000 + index)))
                .ToArray());

        Assert.Equal(40, fullInventory.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(fullInventory.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 720_856U);
        Assert.DoesNotContain(ParseGameItems(fullInventory.Sent), packet => packet.ItemId == 720_856U);
        Assert.DoesNotContain(ParseNpcReplies(fullInventory.Sent), packet =>
            packet.Text == "You~received~the~Cloud~Riding~Scroll!");
        Assert.Contains(ParseMessages(fullInventory.Sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content[3] == "Please make 1 more space in your inventory.");
        Assert.Contains("questId=1337", fullInventory.Log);
        Assert.Contains("inventory", fullInventory.Log);
    }

    [Fact]
    public async Task RewardMessageCloudReceiptAndItemBytesFollowRealOwnedGrantCommit()
    {
        var scene = await KillRewardMessageMonsterAsync("cloud");
        Assert.Null(scene.Failure);
        Assert.NotSame(scene.Original, scene.Current);
        Assert.Same(scene.Original.Character, scene.Current!.Character);
        Assert.Equal(3, scene.Current.PlayerItems.InventoryItems.Where(item => item.ItemId == 720_856).Sum(item => item.StackSize));
        Assert.Equal(new[] { true }, scene.ReceiptOwnership);
        Assert.Equal(new[] { true }, scene.ItemOwnership);
        var receiptIndex = scene.Sent.ToList().FindIndex(bytes => LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply) &&
            reply.Text == "You~received~the~Cloud~Riding~Scroll!");
        var itemIndex = scene.Sent.ToList().FindIndex(bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var item) &&
            item.ItemId == 720_856 && item.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.True(receiptIndex >= 0 && itemIndex > receiptIndex);
        Assert.Equal(LegacyNpcReplyPacket.ForMessageBox("You~received~the~Cloud~Riding~Scroll!").ToFrameBytes(), scene.Sent[receiptIndex]);
        Assert.Equal(scene.OriginalRows, PlayerItemSnapshotLoader.ToLegacyBytes(scene.Original.PlayerItems));
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("unregistered")]
    [InlineData("missing")]
    [InlineData("replace-on-attack")]
    [InlineData("metadata-retire")]
    public async Task RewardMessageCloudRejectsMissingOrRetiredOwnerWithoutGrantOrSuccess(string ownership)
    {
        var scene = await KillRewardMessageMonsterAsync("cloud", ownership);
        Assert.Null(scene.Failure);
        var expected = ownership == "unregistered" ? null :
            ownership is "stale" or "replace-on-attack" or "metadata-retire" ? scene.Replacement : scene.Original;
        Assert.Same(expected, scene.Current);
        if (scene.Current is not null)
            Assert.Equal(scene.OriginalRows, PlayerItemSnapshotLoader.ToLegacyBytes(scene.Current.PlayerItems));
        Assert.Equal(scene.OriginalRows, PlayerItemSnapshotLoader.ToLegacyBytes(scene.Original.PlayerItems));
        Assert.Equal(scene.ItemBefore, scene.ItemAfter);
        Assert.Equal(scene.UserBefore, scene.UserAfter);
        Assert.Empty(scene.ReceiptOwnership);
        Assert.Empty(scene.ItemOwnership);
        if (ownership == "metadata-retire")
        {
            Assert.True(scene.MetadataRetired);
            Assert.Contains("reason=session-changed-before-commit", scene.Log);
        }
        else if (ownership == "unregistered")
        {
            // The real ArrowBlades caller's initial session refresh consumes
            // this attack before damage or reward dispatch, without a gap log.
            Assert.Contains("WorldServer handled MsgAttack", scene.Log);
            Assert.DoesNotContain("Cloud Riding Scroll", scene.Log);
            Assert.Empty(scene.Sent);
        }
        else Assert.Contains("reason=session-registration-missing-or-stale", scene.Log);
    }

    [Theory]
    [InlineData("missing-type")]
    [InlineData("durability")]
    [InlineData("uid-exhausted")]
    public async Task RewardMessageCloudPreparationFailureCannotPublishEarlyReceipt(string failure)
    {
        var scene = await KillRewardMessageMonsterAsync("cloud",
            includeScrollType: failure != "missing-type",
            scrollDurability: failure == "durability" ? 65_536U : 0U,
            highestItemUid: failure == "uid-exhausted" ? uint.MaxValue : null);
        Assert.Null(scene.Failure);
        Assert.Same(scene.Original, scene.Current);
        Assert.Equal(scene.OriginalRows, PlayerItemSnapshotLoader.ToLegacyBytes(scene.Current!.PlayerItems));
        Assert.Equal(scene.ItemBefore, scene.ItemAfter);
        Assert.Equal(scene.UserBefore, scene.UserAfter);
        Assert.Empty(scene.ReceiptOwnership);
        Assert.Empty(scene.ItemOwnership);
        Assert.Contains(failure == "missing-type" ? "map1020-family13-cloud-riding-scroll" : "reward-preparation-overflow", scene.Log);
    }

    [Fact]
    public async Task RewardMessageCloudNoOwnedScrollPreservesNoAddBranchWithoutReceipt()
    {
        var scene = await KillRewardMessageMonsterAsync("cloud", ownsScroll: false);
        Assert.Null(scene.Failure);
        Assert.Same(scene.Original, scene.Current);
        Assert.Empty(scene.ReceiptOwnership);
        Assert.Empty(scene.ItemOwnership);
        Assert.Equal(scene.ItemBefore, scene.ItemAfter);
        Assert.Equal(scene.UserBefore, scene.UserAfter);
        Assert.Contains("Cloud Riding Scroll no-add branch", scene.Log);
    }

    [Fact]
    public async Task RewardMessageCloudFullBagPreservesItemsAndOnlyReportsCapacity()
    {
        var scene = await KillRewardMessageMonsterAsync("cloud", fullInventory: true);
        Assert.Null(scene.Failure);
        Assert.Same(scene.Original, scene.Current);
        Assert.Equal(scene.OriginalRows, PlayerItemSnapshotLoader.ToLegacyBytes(scene.Current!.PlayerItems));
        Assert.Equal(scene.ItemBefore, scene.ItemAfter);
        Assert.Empty(scene.ReceiptOwnership);
        Assert.Empty(scene.ItemOwnership);
        Assert.Contains(ParseMessages(scene.Sent), packet => packet.Content.Contains("Please make 1 more space in your inventory."));
    }

    [Fact]
    public async Task RewardMessageCloudObserverRetirementAfterCasSuppressesBothOutputsWithoutRollback()
    {
        var scene = await KillRewardMessageMonsterAsync("cloud", "retire-after-commit");
        Assert.Null(scene.Failure);
        Assert.True(scene.ObserverRetired);
        Assert.Same(scene.Replacement, scene.Current);
        Assert.Empty(scene.ReceiptOwnership);
        Assert.Empty(scene.ItemOwnership);
        Assert.Equal(scene.OriginalRows, PlayerItemSnapshotLoader.ToLegacyBytes(scene.Original.PlayerItems));
        Assert.Equal(scene.ItemBefore, scene.ItemAfter);
    }

    [Fact]
    public async Task RewardMessageCloudReceiptBoundaryRetirementSuppressesStaleItemFrame()
    {
        var scene = await KillRewardMessageMonsterAsync("cloud", "retire-after-receipt");
        Assert.Null(scene.Failure);
        Assert.Same(scene.Replacement, scene.Current);
        Assert.Equal(new[] { true }, scene.ReceiptOwnership);
        Assert.Empty(scene.ItemOwnership);
        Assert.Equal(scene.OriginalRows, PlayerItemSnapshotLoader.ToLegacyBytes(scene.Original.PlayerItems));
        Assert.Equal(scene.ItemBefore, scene.ItemAfter);
    }
}
