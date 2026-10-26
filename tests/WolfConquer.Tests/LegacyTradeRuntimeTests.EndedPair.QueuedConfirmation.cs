using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyTradeRuntimeTests
{
    [Theory]
    [InlineData("none")] [InlineData("sender")] [InlineData("partner")]
    public Task QueuedFinalTradeConfirmationRechecksEndedEndpointsBeforeExchangingRealItemsAndEscrow(string retired) =>
        RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_298_011, 1_000, 50, [ItemClosureSash(1_100_009, Item(11))]);
            var right = AddItemClosurePlayer(1_298_012, 2_000, 60, [Item(20)]);
            var leftItems = Current(left).PlayerItems;
            var rightItems = Current(right).PlayerItems;
            await OpenItemClosureAsync(left, right);
            await SendItemClosureAsync(left, new(6) { ItemUID = 10 });
            await SendItemClosureAsync(right, new(6) { ItemUID = 20 });
            await SendItemClosureAsync(left, new(7) { Money = 300 });
            await SendItemClosureAsync(right, new(7) { Money = 700 });
            await SendItemClosureAsync(left, new(9));
            Assert.True(trades.TryGet(left.Connection, out var party));
            Assert.True(party.Confirmed);
            Assert.False(party.Other.Confirmed);
            var beforeLeft = CaptureItemClosure(left);
            var beforeRight = CaptureItemClosure(right);
            var ended = retired switch
            {
                "none" => null,
                "sender" => right,
                "partner" => left,
                _ => throw new ArgumentOutOfRangeException(nameof(retired)),
            };

            await QueueTradeWithEndedEndpointAsync(right, new(9), ended);

            if (ended is null)
            {
                AssertItemClosureSettled(left, right, 1_400, 1_600);
                AssertInventory(left, 20);
                AssertInventory(right, 10);
                AssertItemUnchanged(Assert.Single(leftItems.InventoryItems), Assert.Single(Current(right).PlayerItems.InventoryItems));
                AssertItemUnchanged(Assert.Single(rightItems.InventoryItems), Assert.Single(Current(left).PlayerItems.InventoryItems));
                AssertItemClosureSuccessFrames(left, beforeLeft.SentCount, 1_400, 50,
                    Assert.Single(rightItems.InventoryItems), removed: 10, incomingFirst: false);
                AssertItemClosureSuccessFrames(right, beforeRight.SentCount, 1_600, 60,
                    Assert.Single(leftItems.InventoryItems), removed: 20, incomingFirst: true);
                AssertTradeClaimsConsumed(left, right);
                var committed = ReadItemClosureFiles();
                await SendItemClosureAsync(right, new(9));
                AssertItemClosureFiles(committed);
                return;
            }
            AssertEndedTradeActionUnchanged(left, right, beforeLeft, beforeRight);
            Assert.True(trades.TryGet(left.Connection, out var retained));
            Assert.Same(party, retained);
            Assert.True(party.Confirmed);
            Assert.False(party.Other.Confirmed);
            Assert.Equal(300L, party.Money);
            Assert.Equal(700L, party.Other.Money);
            Assert.Single(party.Items);
            Assert.Single(party.Other.Items);
            var departedCount = ended.Sent.Count;
            var survivor = ReferenceEquals(ended, left) ? right : left;
            Assert.True(await WorldPacketHandlers.CancelLegacyTradeForDisconnectAsync(ItemClosureContext(ended)));
            Assert.Equal(departedCount, ended.Sent.Count);
            AssertItemClosureSettled(left, right, 1_000, 2_000);
            AssertInventory(left, 10);
            AssertInventory(right, 20);
            Assert.Same(leftItems, Current(left).PlayerItems);
            Assert.Same(rightItems, Current(right).PlayerItems);
            AssertTradeClaimsConsumed(left, right);
            var refunded = ReadItemClosureFiles();
            var survivorCount = survivor.Sent.Count;
            Assert.True(await WorldPacketHandlers.CancelLegacyTradeForDisconnectAsync(ItemClosureContext(ended)));
            AssertItemClosureFiles(refunded);
            Assert.Equal(survivorCount, survivor.Sent.Count);
            Assert.Equal(departedCount, ended.Sent.Count);
        });
}
