using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyTradeRuntimeTests
{
    [Theory]
    [InlineData(1_100_003U)]
    [InlineData(1_100_006U)]
    [InlineData(1_100_009U)]
    public Task ItemClosureUnboundSashPlainItemAndGoldExchangePreservesFullRowsAndWireOrder(uint sashItemId) =>
        RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_297_071, 1_000, 50,
                new[] { ItemClosureSash(sashItemId), Item(11), Item(12), Item(15) });
            var right = AddItemClosurePlayer(1_297_072, 2_000, 60, new[] { Item(20), Item(21) });
            await DepositItemClosureAsync(left, 10, 11);
            await DepositItemClosureAsync(left, 10, 12);
            var leftBefore = Current(left).PlayerItems;
            var rightBefore = Current(right).PlayerItems;
            var sash = Assert.Single(leftBefore.InventoryItems, item => item.Uid == 10);
            var plain = Assert.Single(rightBefore.InventoryItems, item => item.Uid == 20);
            await OpenItemClosureAsync(left, right);
            await SendItemClosureAsync(left, new(6) { ItemUID = 10 });
            Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(sash, mode: 2).ToFrameBytes(), right.Sent.Last());
            await SendItemClosureAsync(right, new(6) { ItemUID = 20 });
            Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(plain, mode: 2).ToFrameBytes(), left.Sent.Last());
            await SendItemClosureAsync(left, new(7) { Money = 300 });
            await SendItemClosureAsync(right, new(7) { Money = 700 });
            await SendItemClosureAsync(left, new(9));
            var leftCount = left.Sent.Count;
            var rightCount = right.Sent.Count;

            await SendItemClosureAsync(right, new(9));

            AssertItemClosureSettled(left, right, 1_400, 1_600);
            AssertInventory(left, 15, 20);
            AssertInventory(right, 10, 21);
            AssertItemUnchanged(sash, Assert.Single(Current(right).PlayerItems.InventoryItems, item => item.Uid == 10));
            AssertItemUnchanged(plain, Assert.Single(Current(left).PlayerItems.InventoryItems, item => item.Uid == 20));
            AssertOtherContainers(leftBefore, Current(left).PlayerItems);
            AssertOtherContainers(rightBefore, Current(right).PlayerItems);
            Assert.Equal(new uint[] { 11, 12 }, Assert.Single(Current(right).PlayerItems.InventoryItems,
                item => item.Uid == 10).DepositedItems.Select(item => item.Uid));
            AssertItemClosureSuccessFrames(left, leftCount, 1_400, 50, plain, removed: 10, incomingFirst: false);
            AssertItemClosureSuccessFrames(right, rightCount, 1_600, 60, sash, removed: 20, incomingFirst: true);
            var committed = ReadItemClosureFiles();
            await SendItemClosureAsync(left, new(9));
            await SendItemClosureAsync(right, new(9));
            AssertItemClosureFiles(committed); // No second transfer, credit, or UID production.

            // The recipient can access the same physical child through the real
            // registered sash path; its fields/UID are not recreated from a preview.
            var withdraw = new LegacyWarehousePacket(10, LegacyWarehouseAction.ShashWithdrawItem, 11, 0, []);
            using (await closureOperations.EnterSharedAsync())
                await handlers.HandleAsync(ItemClosureContext(right), ReadFrame(withdraw.ToFrameBytes()));
            AssertItemUnchanged(Item(11), Assert.Single(Current(right).PlayerItems.InventoryItems, item => item.Uid == 11));
            Assert.Equal(12U, Assert.Single(Assert.Single(Current(right).PlayerItems.InventoryItems,
                item => item.Uid == 10).DepositedItems).Uid);
            new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(right.Uid, Current(right).PlayerItems);
            AssertPersisted(right);
        });

    [WindowsTradeJournalFailureFact]
    public Task ItemClosureBlockedRealFileRetainsEscrowThenSamePairRetriesExactlyOnce() =>
        RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_297_081, 1_000, 50,
                new[] { ItemClosureSash(1_100_009, Item(11)) });
            var right = AddItemClosurePlayer(1_297_082, 2_000, 60, new[] { Item(20) });
            await OpenItemClosureAsync(left, right);
            await SendItemClosureAsync(left, new(6) { ItemUID = 10 });
            await SendItemClosureAsync(right, new(6) { ItemUID = 20 });
            await SendItemClosureAsync(left, new(7) { Money = 300 });
            await SendItemClosureAsync(left, new(9));
            var leftBefore = CaptureItemClosure(left);
            var rightBefore = CaptureItemClosure(right);
            using (File.Open(ItemPath(right), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                await SendItemClosureAsync(right, new(9));
                Assert.Same(leftBefore.Session, Current(left));
                Assert.Same(rightBefore.Session, Current(right));
                Assert.Equal(leftBefore.Values, System.Text.Json.JsonSerializer.Serialize(Current(left)));
                Assert.Equal(rightBefore.Values, System.Text.Json.JsonSerializer.Serialize(Current(right)));
                AssertWallet(left, 700, 50);
                AssertWallet(right, 2_000, 60);
                AssertItemClosureNoFinish(left, leftBefore.SentCount);
                AssertItemClosureNoFinish(right, rightBefore.SentCount);
                AssertItemClosureNoIncoming(right, rightBefore.SentCount, 10);
            }
            AssertItemClosureFiles(leftBefore.Files); // Read exact bytes only after the actual lease closes.
            await SendItemClosureAsync(left, new(9));
            await SendItemClosureAsync(right, new(9));
            AssertItemClosureSettled(left, right, 700, 2_300);
            AssertInventory(left, 20);
            AssertInventory(right, 10);
            Assert.Equal(11U, Assert.Single(Assert.Single(Current(right).PlayerItems.InventoryItems).DepositedItems).Uid);
            var committed = ReadItemClosureFiles();
            await SendItemClosureAsync(right, new(9));
            AssertItemClosureFiles(committed);
        });

    [Fact]
    public Task ItemClosureDisconnectRefundKeepsNestedRowsAndNeverSendsToDepartingConnection() =>
        RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_297_091, 1_000, 50,
                new[] { ItemClosureSash(1_100_009, Item(11)) });
            var right = AddItemClosurePlayer(1_297_092, 2_000, 60, new[] { Item(20) });
            var leftItems = Current(left).PlayerItems;
            var rightItems = Current(right).PlayerItems;
            var leftBytes = File.ReadAllBytes(ItemPath(left));
            var rightBytes = File.ReadAllBytes(ItemPath(right));
            await OpenItemClosureAsync(left, right);
            await SendItemClosureAsync(left, new(6) { ItemUID = 10 });
            await SendItemClosureAsync(left, new(7) { Money = 300 });
            var count = left.Sent.Count;

            Assert.True(await WorldPacketHandlers.CancelLegacyTradeForDisconnectAsync(ItemClosureContext(left)));
            Assert.True(await WorldPacketHandlers.CancelLegacyTradeForDisconnectAsync(ItemClosureContext(left)));

            Assert.Equal(count, left.Sent.Count);
            AssertItemClosureSettled(left, right, 1_000, 2_000);
            Assert.Same(leftItems, Current(left).PlayerItems);
            Assert.Same(rightItems, Current(right).PlayerItems);
            Assert.Equal(leftBytes, File.ReadAllBytes(ItemPath(left)));
            Assert.Equal(rightBytes, File.ReadAllBytes(ItemPath(right)));
            Assert.Single(TradeFrames(right), packet => packet.Action == 5);
        });

    [Fact]
    public Task ItemClosureRealSendFailureAfterCommitCannotRefundOrRepeatNestedExchange() =>
        RunItemClosureAsync(async () =>
        {
            var failDelivery = false;
            var faults = 0;
            var left = AddItemClosurePlayer(1_297_101, 1_000, 50,
                new[] { ItemClosureSash(1_100_009, Item(11)) });
            Player? recipient = null;
            var right = AddItemClosurePlayer(1_297_102, 2_000, 60, new[] { Item(20) }, bytes =>
            {
                if (!failDelivery || !LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var item) ||
                    item.Uid != 10 || item.Mode != LegacyGameItemPacket.AddItemMode) return;
                faults++;
                AssertPersisted(left);
                AssertWallet(left, 700, 50);
                AssertPersisted(recipient!);
                AssertWallet(recipient!, 2_300, 60);
                throw new IOException("owned committed sash delivery failure");
            });
            recipient = right;
            await OpenItemClosureAsync(left, right);
            await SendItemClosureAsync(left, new(6) { ItemUID = 10 });
            await SendItemClosureAsync(left, new(7) { Money = 300 });
            await SendItemClosureAsync(left, new(9));
            failDelivery = true;

            await SendItemClosureAsync(right, new(9));

            Assert.Equal(1, faults);
            AssertItemClosureSettled(left, right, 700, 2_300);
            AssertInventory(left);
            AssertInventory(right, 10, 20);
            Assert.Equal(11U, Assert.Single(Assert.Single(Current(right).PlayerItems.InventoryItems,
                item => item.Uid == 10).DepositedItems).Uid);
            Assert.Contains("send failed after committed state: IOException", log.ToString(), StringComparison.Ordinal);
            var committed = ReadItemClosureFiles();
            await SendItemClosureAsync(left, new(2));
            await SendItemClosureAsync(right, new(9));
            AssertItemClosureFiles(committed);
            Assert.Equal(1, faults);
        });

    private static void AssertItemClosureSuccessFrames(Player player, int start, ulong money, ulong cps,
        PlayerItemSnapshotItem incoming, uint removed, bool incomingFirst)
    {
        var frames = player.Sent.Skip(start).ToArray();
        Assert.Equal(4, frames.Length);
        Assert.Equal(new LegacyMsgUpdatePacket(player.Uid,
            [new(LegacyMsgUpdateDataType.Money, money), new(LegacyMsgUpdateDataType.ConquerPoints, cps)]).ToFrameBytes(), frames[0]);
        var add = LegacyGameItemPacket.FromSnapshotItem(incoming, mode: LegacyGameItemPacket.AddItemMode).ToFrameBytes();
        var remove = new MsgItemUsagePacket(removed, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes();
        Assert.Equal(incomingFirst ? add : remove, frames[1]);
        Assert.Equal(incomingFirst ? remove : add, frames[2]);
        Assert.Equal(new LegacyTradePacket(4) { UID = player.Uid }.ToNativeFrameBytes(), frames[3]);
    }
}
