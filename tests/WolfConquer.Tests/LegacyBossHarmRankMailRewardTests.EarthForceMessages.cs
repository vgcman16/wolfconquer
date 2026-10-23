using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    [Fact]
    public async Task HandlerMap3998Family40906Quest3646FullBagRejectsWithoutEarthForceReceipt()
    {
        var result = await KillMap3998Family40906Quest3646MonsterAsync(fullInventory: true);

        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId is 3_008_754U or 3_008_745U);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            packet => packet.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                packet.Text == "Please make 1 more space in your inventory.");
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), packet =>
            packet.Text == "You received the Earth Force! Hurry and transform it into Metal Force through the Wheel of Nature!");
        Assert.Contains("inventory-full", result.Log);
    }

    [Fact]
    public async Task RewardMessageEarthForceFullBagPreservesOwnedFilesAndDoesNotAnnounceGrant()
    {
        var scene = await KillRewardMessageMonsterAsync("earth", fullInventory: true);
        Assert.Null(scene.Failure);
        Assert.Same(scene.Original, scene.Current);
        Assert.Equal(scene.OriginalRows, PlayerItemSnapshotLoader.ToLegacyBytes(scene.Current!.PlayerItems));
        Assert.Equal(scene.ItemBefore, scene.ItemAfter);
        Assert.Equal(scene.UserBefore, scene.UserAfter);
        Assert.Contains(ParseNpcReplies(scene.Sent), packet =>
            packet.Text == "Please make 1 more space in your inventory.");
        Assert.DoesNotContain(ParseNpcReplies(scene.Sent), packet => packet.Text.Contains("received the Earth Force", StringComparison.Ordinal));
        Assert.DoesNotContain(ParseGameItems(scene.Sent), packet => packet.ItemId is 3_008_754U or 3_008_745U);
        Assert.Contains("reason=inventory-full", scene.Log);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RewardMessageEarthForceInactiveQuestDoesNotInventGrantCapacityResponse(bool fullInventory)
    {
        var scene = await KillRewardMessageMonsterAsync("earth", fullInventory: fullInventory, accepted: false);
        Assert.Null(scene.Failure);
        Assert.Same(scene.Original, scene.Current);
        Assert.Equal(scene.OriginalRows, PlayerItemSnapshotLoader.ToLegacyBytes(scene.Current!.PlayerItems));
        Assert.Equal(scene.ItemBefore, scene.ItemAfter);
        Assert.Equal(scene.UserBefore, scene.UserAfter);
        Assert.DoesNotContain(ParseNpcReplies(scene.Sent), packet =>
            packet.Text.Contains("received the Earth Force", StringComparison.Ordinal) ||
            packet.Text == "Please make 1 more space in your inventory.");
    }

    [Fact]
    public async Task RewardMessageEarthForceValidFragmentAmountAndWireRemainUnchanged()
    {
        var scene = await KillRewardMessageMonsterAsync("earth");
        Assert.Null(scene.Failure);
        var fragment = Assert.Single(scene.Current!.PlayerItems.InventoryItems, item => item.ItemId == 3_008_754);
        Assert.Equal((ushort)1, fragment.StackSize);
        var itemFrame = Assert.Single(ParseGameItems(scene.Sent), item => item.ItemId == 3_008_754);
        Assert.Equal(fragment.Uid, itemFrame.Uid);
        Assert.Equal((ushort)1, itemFrame.StackSize);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, itemFrame.Mode);
        Assert.DoesNotContain(ParseNpcReplies(scene.Sent), packet => packet.Text.Contains("received the Earth Force", StringComparison.Ordinal));
        Assert.Contains("Earth Force fragment reward", scene.Log);
        Assert.Equal(scene.OriginalRows, PlayerItemSnapshotLoader.ToLegacyBytes(scene.Original.PlayerItems));
    }
}
