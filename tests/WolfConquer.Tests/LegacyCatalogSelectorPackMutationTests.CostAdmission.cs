using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCatalogSelectorPackMutationTests
{
    [Theory]
    [MemberData(nameof(PackedGrantCases))]
    public async Task PackedCatalogPreservesForeignAndZeroRowsBeforeItsEligibleCost(
        uint npcId, uint packItemId, uint rewardItemId, byte expectedBless)
    {
        var foreign = Item(5, packItemId, stackSize: 2, bound: 1)
            .WithMaximumDurabilityAndPlus(4321, 7).WithSocketProgress(123456);
        foreign.OwnerName = "foreign";
        foreign.Signature = "preserve";
        var zero = Item(10, packItemId, stackSize: 0).WithSocketProgress(654321);
        var unrelated = Item(30, 720_001).WithMaximumDurabilityAndPlus(5432, 8);
        var original = Items(foreign, zero, Item(20, packItemId, stackSize: 2), unrelated);
        var preservedRows = new[] { foreign, zero, unrelated }.Select(item =>
            (item.Uid, Bytes: PlayerItemSnapshotLoader.ToLegacyBytes(Items(item)))).ToArray();
        var result = await InvokeAsync(
            npcId, 250, original, SourceCatalog(), selectedItemId: rewardItemId);

        Assert.True(result.Handled);
        Assert.Equal(1, result.CommitCount);
        Assert.Same(result.OriginalSession.Character, result.UpdatedSession.Character);
        foreach (var preserved in preservedRows)
        {
            Assert.Equal(preserved.Bytes, PlayerItemSnapshotLoader.ToLegacyBytes(Items(Assert.Single(
                result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == preserved.Uid))));
        }
        Assert.Equal(1, Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 20).StackSize);
        Assert.Collection(result.Sent,
            frame =>
            {
                Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(frame), out var cost));
                Assert.Equal(20U, cost.Uid);
                Assert.Equal((ushort)1, cost.StackSize);
                Assert.Equal(LegacyGameItemPacket.UpdateItemMode, cost.Mode);
            },
            frame => AssertGrantedItem(frame, rewardItemId, expectedBless),
            frame => AssertMessageBox(frame, "You received 1 "));
        Assert.Equal(0U, result.UpdatedSession.LegacyCatalogSelectedItemId);
        Assert.True(result.PersistenceMatchedAtCommit);
        Assert.True(result.SessionCommittedBeforeFirstSend);
        AssertItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
    }

    [Theory]
    [InlineData(BlackFridayAccessoryNpcId, BlackFridayAccessoryPackItemId, FirstAccessoryItemId, (byte)0, (ushort)1)]
    [InlineData(BlackFridayAccessoryNpcId, BlackFridayAccessoryPackItemId, FirstAccessoryItemId, (byte)0, (ushort)2)]
    [InlineData(BlackFridayMountNpcId, BlackFridayMountPackItemId, FirstMountItemId, (byte)1, (ushort)1)]
    [InlineData(BlackFridayMountNpcId, BlackFridayMountPackItemId, FirstMountItemId, (byte)1, (ushort)2)]
    [InlineData(BlackFridayGarmentNpcId, BlackFridayGarmentPackItemId, FirstGarmentItemId, (byte)1, (ushort)1)]
    [InlineData(BlackFridayGarmentNpcId, BlackFridayGarmentPackItemId, FirstGarmentItemId, (byte)1, (ushort)2)]
    public async Task PackedCatalogDebitsFirstEligiblePhysicalRowRatherThanLowestUid(
        uint npcId, uint packItemId, uint rewardItemId, byte expectedBless, ushort firstStack)
    {
        var laterLowerUid = Item(10, packItemId, stackSize: 3).WithSocketProgress(123456);
        var original = Items(Item(20, packItemId, stackSize: firstStack), laterLowerUid);
        var laterBytes = PlayerItemSnapshotLoader.ToLegacyBytes(Items(laterLowerUid));
        var result = await InvokeAsync(
            npcId, 250, original, SourceCatalog(), selectedItemId: rewardItemId);

        Assert.Equal(laterBytes, PlayerItemSnapshotLoader.ToLegacyBytes(Items(Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 10))));
        Assert.Collection(result.Sent,
            frame =>
            {
                if (firstStack == 1)
                {
                    Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 20);
                    AssertItemRemoval(frame, 20);
                }
                else
                {
                    Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(frame), out var cost));
                    Assert.Equal(20U, cost.Uid);
                    Assert.Equal((ushort)1, cost.StackSize);
                    Assert.Equal(LegacyGameItemPacket.UpdateItemMode, cost.Mode);
                }
            },
            frame => AssertGrantedItem(frame, rewardItemId, expectedBless),
            frame => AssertMessageBox(frame, "You received 1 "));
        AssertItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
    }

    [Theory]
    [MemberData(nameof(PackedGrantCases))]
    public async Task PackedCatalogZeroAndForeignStockCannotAuthorizeAnyReward(
        uint npcId, uint packItemId, uint rewardItemId, byte expectedBless)
    {
        _ = expectedBless;
        var original = Items(Item(10, packItemId, stackSize: 0), Item(20, packItemId, stackSize: 5, bound: 1));
        var before = PlayerItemSnapshotLoader.ToLegacyBytes(original);
        var result = await InvokeAsync(
            npcId, 250, original, SourceCatalog(), selectedItemId: rewardItemId);

        AssertMutationFree(result, original);
        Assert.Empty(result.Sent);
        Assert.Equal(rewardItemId, result.UpdatedSession.LegacyCatalogSelectedItemId);
        Assert.Contains("WorldServer ignored LegacyNpc ItemPackServices no-op", result.Log);
        Assert.Equal(before, PlayerItemSnapshotLoader.ToLegacyBytes(result.UpdatedSession.PlayerItems));
        Assert.Equal(before, File.ReadAllBytes(
            Path.Combine(result.PlayerItemsRoot, CharacterUid + ".bin")));
    }
}
