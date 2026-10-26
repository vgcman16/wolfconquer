using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyTradeRuntimeTests
{
    [Theory]
    [InlineData(LegacyWarehouseAction.DepositItem)]
    [InlineData(LegacyWarehouseAction.InventorySashDepositItem)]
    public Task RegisteredWarehouseFamilyOfferedRootDepositRetainsRealEscrowThenFinalConfirmationRefunds(
        LegacyWarehouseAction action) => RunItemClosureAsync(async () =>
    {
        var (left, right, bank) = CreateWarehouseTradePair(action);
        var party = await ConfirmWarehouseTradeEscrowAsync(left, right);
        var offer = party.Items[10];
        var peerOffer = party.Other.Items[20];
        var before = Current(left).PlayerItems;
        var expected = ExpectedWarehouseTradeMove(before, action, bank, 10);
        var files = ReadItemClosureFiles();

        await MoveWarehouseTradeItemAsync(left, action, bank, 10);

        AssertWarehouseTradeStorage(left, expected, files);
        AssertWarehouseTradeEscrow(left, right, party);
        Assert.Same(offer, party.Items[10]);
        Assert.Same(peerOffer, party.Other.Items[20]);
        AssertInventory(left);
        var stored = Assert.Single(Current(left).PlayerItems.WarehouseItems,
            item => item.Uid == 10 && item.WarehouseId == bank);
        Assert.Equal(12U, Assert.Single(stored.DepositedItems).Uid);
        var leftItems = Current(left).PlayerItems;
        var rightItems = Current(right).PlayerItems;
        var leftStart = left.Sent.Count;
        var rightStart = right.Sent.Count;

        await SendItemClosureAsync(right, new(9));

        AssertItemClosureSettled(left, right, 1_000, 2_000);
        Assert.Same(leftItems, Current(left).PlayerItems);
        Assert.Same(rightItems, Current(right).PlayerItems);
        AssertInventory(left);
        AssertInventory(right, 20);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected), File.ReadAllBytes(ItemPath(left)));
        AssertItemClosureNoFinish(left, leftStart);
        AssertItemClosureNoFinish(right, rightStart);
        AssertItemClosureNoIncoming(right, rightStart, 10);
        AssertItemClosureNoIncoming(left, leftStart, 20);
        AssertWarehouseTradeClaimsCleared(left, right);
        var committed = ReadItemClosureFiles();
        await SendItemClosureAsync(left, new(2));
        await SendItemClosureAsync(right, new(9));
        AssertItemClosureFiles(committed);
        AssertWallet(left, 1_000, 50);
        AssertWallet(right, 2_000, 60);
    });

    [Theory]
    [InlineData(LegacyWarehouseAction.WithdrawItem)]
    [InlineData(LegacyWarehouseAction.InventorySashWithdrawItem)]
    public Task RegisteredWarehouseFamilyUnrelatedWithdrawalRetainsRealEscrowThenUnchangedOffersExchange(
        LegacyWarehouseAction action) => RunItemClosureAsync(async () =>
    {
        var (left, right, bank) = CreateWarehouseTradePair(action);
        var party = await ConfirmWarehouseTradeEscrowAsync(left, right);
        var offer = party.Items[10];
        var peerOffer = party.Other.Items[20];
        var before = Current(left).PlayerItems;
        var rightBefore = Current(right).PlayerItems;
        var sash = Assert.Single(before.InventoryItems, item => item.Uid == 10);
        var plain = Assert.Single(rightBefore.InventoryItems, item => item.Uid == 20);
        var expected = ExpectedWarehouseTradeMove(before, action, bank, 11);
        var files = ReadItemClosureFiles();

        await MoveWarehouseTradeItemAsync(left, action, bank, 11);

        AssertWarehouseTradeStorage(left, expected, files);
        AssertWarehouseTradeEscrow(left, right, party);
        Assert.Same(offer, party.Items[10]);
        Assert.Same(peerOffer, party.Other.Items[20]);
        AssertInventory(left, 10, 11);
        var leftBeforeFinal = Current(left).PlayerItems;
        var leftStart = left.Sent.Count;
        var rightStart = right.Sent.Count;

        await SendItemClosureAsync(right, new(9));

        AssertItemClosureSettled(left, right, 700, 2_300);
        AssertInventory(left, 11, 20);
        AssertInventory(right, 10);
        var expectedLeft = expected with
        {
            InventoryItems = expected.InventoryItems.Where(item => item.Uid != 10).Append(plain)
                .OrderBy(item => item.Uid).ToArray(),
        };
        var expectedRight = rightBefore with
        {
            InventoryItems = rightBefore.InventoryItems.Where(item => item.Uid != 20).Append(sash)
                .OrderBy(item => item.Uid).ToArray(),
        };
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedLeft), File.ReadAllBytes(ItemPath(left)));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedRight), File.ReadAllBytes(ItemPath(right)));
        AssertOtherContainers(leftBeforeFinal, Current(left).PlayerItems);
        AssertOtherContainers(rightBefore, Current(right).PlayerItems);
        Assert.Equal(12U, Assert.Single(Assert.Single(Current(right).PlayerItems.InventoryItems).DepositedItems).Uid);
        AssertItemClosureSuccessFrames(left, leftStart, 700, 50, plain, removed: 10, incomingFirst: false);
        AssertItemClosureSuccessFrames(right, rightStart, 2_300, 60, sash, removed: 20, incomingFirst: true);
        AssertWarehouseTradeClaimsCleared(left, right);
        var committed = ReadItemClosureFiles();
        await SendItemClosureAsync(left, new(9));
        await SendItemClosureAsync(right, new(9));
        await SendItemClosureAsync(left, new(2));
        AssertItemClosureFiles(committed);
        AssertWallet(left, 700, 50);
        AssertWallet(right, 2_300, 60);
    });
}
