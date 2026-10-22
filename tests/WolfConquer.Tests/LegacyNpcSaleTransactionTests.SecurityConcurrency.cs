using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Fact]
    public async Task CancellingQueuedSaleLeavesAssetsUntouchedAndDoesNotBlockNormalFramesOrLaterSale()
    {
        const uint uid = 1_299_161;
        Seed(uid);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var original = Current(owner, player);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;
        var barrier = ActualOperationBarrier(owner);
        using var operationCancellation = new CancellationTokenSource();
        Task? queued = null;
        try
        {
            using (await barrier.EnterSharedAsync())
            {
                // Cancel only this requested operation. This does not model a
                // transport disconnect; the login's registered lifetime is live.
                queued = owner.Handler.HandleFrameAsync(player.Connection,
                    ReadFrame(new MsgItemUsagePacket(NpcUid, SoldUid, MsgItemUsageAction.SellItem, 0, 0, 0, 0).ToFrameBytes()),
                    operationCancellation.Token).AsTask();
                Assert.False(queued.IsCompleted);
                operationCancellation.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                    await queued.WaitAsync(TimeSpan.FromSeconds(5)));

                Assert.False(player.Lifetime.IsCancellationRequested);
                Assert.Same(original, Current(owner, player));
                Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
                Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
                Assert.Equal(first, player.Sent.Count);
                Assert.False(LegacyTradeJournal.HasPending(UsersRoot));

                // A leaked queued writer would block this real ordinary/shared
                // frame even though the original reader is still held.
                var ping = new MsgItemUsagePacket(0, 0, MsgItemUsageAction.Ping, 456, 0, 0, 0).ToFrameBytes();
                using var pingCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await owner.Handler.HandleFrameAsync(player.Connection, ReadFrame(ping), pingCancellation.Token);
                Assert.Equal(ping, Assert.Single(player.Sent.Skip(first)));
                Assert.Same(original, Current(owner, player));
                Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
                Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
            }

            var beforeRetry = player.Sent.Count;
            await Sell(owner, player);

            AssertSaved(uid, SaleResult(original, 1_300, 50));
            Assert.Equal(1_300, Current(owner, player).Character.Money);
            Assert.Single(player.Sent.Skip(beforeRetry), IsRemoval);
            Assert.Equal(2, MoneyUpdateCount(player, beforeRetry));
            await AssertReplayCannotPay(owner, player);
        }
        finally
        {
            operationCancellation.Cancel();
            if (queued is { IsCompleted: false })
            {
                try { await queued.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (OperationCanceledException) { }
            }
        }
    }
}
