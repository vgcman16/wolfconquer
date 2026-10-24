using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyTradeRuntimeTests
{
    [Theory]
    [InlineData(1_100_003U, "bound")]
    [InlineData(1_100_003U, "inscribed")]
    [InlineData(1_100_003U, "locked")]
    [InlineData(1_100_003U, "unable")]
    [InlineData(1_100_006U, "bound")]
    [InlineData(1_100_006U, "inscribed")]
    [InlineData(1_100_006U, "locked")]
    [InlineData(1_100_006U, "unable")]
    [InlineData(1_100_009U, "bound")]
    [InlineData(1_100_009U, "inscribed")]
    [InlineData(1_100_009U, "locked")]
    [InlineData(1_100_009U, "unable")]
    public Task ItemClosureRestrictedDepositedChildCannotBeOffered(uint sashItemId, string restriction) =>
        RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_297_001, 1_000, 50,
                new[] { ItemClosureSash(sashItemId), RestrictedItemClosureChild(restriction) });
            var right = AddItemClosurePlayer(1_297_002, 2_000, 60, new[] { Item(20) });
            await DepositItemClosureAsync(left, 10, 11);
            await OpenItemClosureAsync(left, right);
            var own = CaptureItemClosure(left);
            var other = CaptureItemClosure(right);

            await SendItemClosureAsync(left, new(6) { ItemUID = 10 });

            AssertItemClosureUnchanged(left, own);
            AssertItemClosureUnchanged(right, other);
            Assert.Equal(other.SentCount, right.Sent.Count); // No tradable-looking sash preview.
            Assert.True(trades.TryGet(left.Connection, out var party));
            Assert.Empty(party.Items);
            Assert.Contains(left.Sent.Skip(own.SentCount), bytes =>
                LegacyTradePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Action == 10);
            await SendItemClosureAsync(left, new(2));
            AssertItemClosureSettled(left, right, 1_000, 2_000);
        });

    [Theory]
    [InlineData("bound")]
    [InlineData("inscribed")]
    [InlineData("locked")]
    [InlineData("unable")]
    public Task ItemClosureActualDepositAfterOfferCannotTransferChangedSashAtFinalConfirmation(string restriction) =>
        RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_297_011, 1_000, 50,
                new[] { ItemClosureSash(), RestrictedItemClosureChild(restriction) });
            var right = AddItemClosurePlayer(1_297_012, 2_000, 60, new[] { Item(20) });
            await OpenItemClosureAsync(left, right);
            await SendItemClosureAsync(left, new(6) { ItemUID = 10 });
            await SendItemClosureAsync(left, new(7) { Money = 300 });
            await SendItemClosureAsync(left, new(9));
            await DepositItemClosureAsync(left, 10, 11);
            var leftItems = Current(left).PlayerItems;
            var rightItems = Current(right).PlayerItems;
            var beforeLeft = left.Sent.Count;
            var beforeRight = right.Sent.Count;

            await SendItemClosureAsync(right, new(9));

            AssertItemClosureSettled(left, right, 1_000, 2_000);
            Assert.Same(leftItems, Current(left).PlayerItems);
            Assert.Same(rightItems, Current(right).PlayerItems);
            AssertItemClosureNoFinish(left, beforeLeft);
            AssertItemClosureNoFinish(right, beforeRight);
            AssertItemClosureNoIncoming(right, beforeRight, 10);
            var child = Assert.Single(Assert.Single(Current(left).PlayerItems.InventoryItems).DepositedItems);
            AssertItemUnchanged(RestrictedItemClosureChild(restriction), child);
        });

    [Theory]
    [InlineData(1_100_003U)]
    [InlineData(1_100_006U)]
    [InlineData(1_100_009U)]
    public Task ItemClosureLockedChildAndRootKeepTheRealTradePartnerException(uint sashItemId) =>
        RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_297_021, 1_000, 50,
                new[] { ItemClosureSash(sashItemId).WithLocked(1), RestrictedItemClosureChild("locked") });
            var right = AddItemClosurePlayer(1_297_022, 2_000, 60, new[] { Item(20) });
            AddItemClosurePartner(left, right);
            await DepositItemClosureAsync(left, 10, 11);
            var source = Assert.Single(Current(left).PlayerItems.InventoryItems);
            await OpenItemClosureAsync(left, right);
            await SendItemClosureAsync(left, new(6) { ItemUID = 10 });
            Assert.True(trades.TryGet(left.Connection, out var party));
            Assert.Single(party.Items);
            await SendItemClosureAsync(left, new(9));
            await SendItemClosureAsync(right, new(9));

            AssertItemClosureSettled(left, right, 1_000, 2_000);
            AssertInventory(left);
            var received = Assert.Single(Current(right).PlayerItems.InventoryItems, item => item.Uid == 10);
            AssertItemUnchanged(source, received);
            Assert.Equal((byte)1, received.Locked);
            Assert.Equal((byte)2, Assert.Single(received.DepositedItems).Locked);
            Assert.Equal(20991231, Assert.Single(received.DepositedItems).UnlockTimer);
        });

    [Fact]
    public Task ItemClosureRemovedPartnerPermissionIsRecheckedAtFinalConfirmation() =>
        RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_297_031, 1_000, 50,
                new[] { ItemClosureSash(), RestrictedItemClosureChild("locked") });
            var right = AddItemClosurePlayer(1_297_032, 2_000, 60, new[] { Item(20) });
            AddItemClosurePartner(left, right);
            await DepositItemClosureAsync(left, 10, 11);
            await OpenItemClosureAsync(left, right);
            await SendItemClosureAsync(left, new(6) { ItemUID = 10 });
            Assert.True(trades.TryGet(left.Connection, out var party));
            Assert.Single(party.Items);
            await SendItemClosureAsync(left, new(7) { Money = 300 });
            await SendItemClosureAsync(left, new(9));
            closureRelations.Clear(); // Actual current relation source, not a permission callback.
            var leftItems = Current(left).PlayerItems;
            var rightItems = Current(right).PlayerItems;
            var leftCount = left.Sent.Count;
            var rightCount = right.Sent.Count;

            await SendItemClosureAsync(right, new(9));

            AssertItemClosureSettled(left, right, 1_000, 2_000);
            Assert.Same(leftItems, Current(left).PlayerItems);
            Assert.Same(rightItems, Current(right).PlayerItems);
            AssertItemClosureNoFinish(left, leftCount);
            AssertItemClosureNoFinish(right, rightCount);
            AssertItemClosureNoIncoming(right, rightCount, 10);
        });
}
