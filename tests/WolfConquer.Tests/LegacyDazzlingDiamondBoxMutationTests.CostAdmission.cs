using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDazzlingDiamondBoxMutationTests
{
    [Theory]
    [InlineData((byte)1, 721_533U, "Sun")]
    [InlineData((byte)2, 721_534U, "Moon")]
    [InlineData((byte)3, 721_535U, "Star")]
    [InlineData((byte)4, 721_536U, "Cloud")]
    public async Task DiamondCostPreservesForeignAndZeroPrefixAndDebitsPositiveUnboundBox(
        byte option, uint rewardId, string name)
    {
        var foreign = CreateInventoryItem(5, DazzlingDiamondBoxItemId, stackSize: 2, bound: 1)
            .WithMaximumDurabilityAndPlus(4321, 7).WithSocketProgress(123456);
        foreign.OwnerName = "foreign";
        foreign.Signature = "preserve";
        var zero = CreateInventoryItem(10, DazzlingDiamondBoxItemId, stackSize: 0);
        var original = CreatePlayerItems(foreign, zero, CreateInventoryItem(20, DazzlingDiamondBoxItemId, stackSize: 2));
        var preservedRows = new[] { foreign, zero }.Select(item =>
            (item.Uid, Bytes: PlayerItemSnapshotLoader.ToLegacyBytes(CreatePlayerItems(item)))).ToArray();
        var itemsRoot = CreatePlayerItemsRoot();
        var result = await InvokeAsync(option, original,
            CreateWorldDataCatalog(CreateItemType(rewardId, name + "Diamond", stackLimit: 100)), itemsRoot);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession.Character, result.UpdatedSession.Character);
        foreach (var preserved in preservedRows)
        {
            Assert.Equal(preserved.Bytes, PlayerItemSnapshotLoader.ToLegacyBytes(CreatePlayerItems(Assert.Single(
                result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == preserved.Uid))));
        }
        Assert.Equal(1, Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 20).StackSize);
        Assert.Equal(3, result.Sent.Count);
        var debit = AssertGameItem(result.Sent[0]);
        Assert.Equal(20U, debit.Uid);
        Assert.Equal((ushort)1, debit.StackSize);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, debit.Mode);
        var reward = AssertGameItem(result.Sent[1]);
        Assert.Equal(rewardId, reward.ItemId);
        Assert.Equal((ushort)30, reward.StackSize);
        Assert.Equal((byte)0, reward.Bound);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, reward.Mode);
        Assert.Equal($"You receive 30 pieces of the {name}~Diamond.", AssertMessageBox(result.Sent[2]).Text);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems,
            new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid));
    }

    [Theory]
    [InlineData((ushort)1)]
    [InlineData((ushort)2)]
    public async Task DiamondCostUsesFirstEligiblePhysicalRowNotLowestUid(ushort firstStack)
    {
        var laterLowerUid = CreateInventoryItem(10, DazzlingDiamondBoxItemId, stackSize: 3).WithSocketProgress(123456);
        var original = CreatePlayerItems(CreateInventoryItem(20, DazzlingDiamondBoxItemId, stackSize: firstStack), laterLowerUid);
        var laterBytes = PlayerItemSnapshotLoader.ToLegacyBytes(CreatePlayerItems(laterLowerUid));
        var itemsRoot = CreatePlayerItemsRoot();
        var result = await InvokeAsync(1, original,
            CreateWorldDataCatalog(CreateItemType(721_533, "SunDiamond", stackLimit: 100)), itemsRoot);

        Assert.Equal(laterBytes, PlayerItemSnapshotLoader.ToLegacyBytes(CreatePlayerItems(Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 10))));
        if (firstStack == 1)
        {
            Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 20);
            Assert.Equal(20U, AssertItemUsage(result.Sent[0]).Id);
        }
        else
        {
            var debit = AssertGameItem(result.Sent[0]);
            Assert.Equal(20U, debit.Uid);
            Assert.Equal((ushort)1, debit.StackSize);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, debit.Mode);
        }
        Assert.Equal(3, result.Sent.Count);
        Assert.Equal((ushort)30, AssertGameItem(result.Sent[1]).StackSize);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems,
            new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid));
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    public async Task DiamondZeroUnboundAndPositiveForeignStockCannotAuthorizeReward(byte foreignBound)
    {
        var original = CreatePlayerItems(CreateInventoryItem(10, DazzlingDiamondBoxItemId, stackSize: 0),
            CreateInventoryItem(20, DazzlingDiamondBoxItemId, stackSize: 5, bound: foreignBound));
        var before = PlayerItemSnapshotLoader.ToLegacyBytes(original);
        var itemsRoot = CreatePlayerItemsRoot();
        var result = await InvokeAsync(1, original,
            CreateWorldDataCatalog(CreateItemType(721_533, "SunDiamond", stackLimit: 100)), itemsRoot);

        Assert.Same(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(original, result.UpdatedSession.PlayerItems);
        Assert.Equal("Sorry, you don`t have a DazzlingDiamondBox.", AssertMessageBox(Assert.Single(result.Sent)).Text);
        Assert.Equal(before, PlayerItemSnapshotLoader.ToLegacyBytes(result.UpdatedSession.PlayerItems));
        Assert.Equal(before,
            File.ReadAllBytes(Path.Combine(itemsRoot, CharacterUid + ".bin")));
    }
}
