using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualD13MailboxFullBagRejectsWithoutClaimRepliesThenRealDepositAllowsClaim(bool claimAll) =>
        RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync(inventoryCount: 40, mixed: true, flag: 0);
        var before = CaptureMailboxFullBag(fixture);
        Assert.Equal(40, before.Owner.PlayerItems.InventoryItems.Count);
        Assert.Equal(AtomicMailItemUid, Assert.Single(before.Rows, row => row.Id == AtomicMailId).ItemId);
        var request = Fixture.AtomicMailRequest(claimAll);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var first = fixture.Sent.Count;
            var logStart = fixture.Log.GetStringBuilder().Length;
            var observed = 0;
            fixture.SendObserver = _ =>
            {
                AssertMailboxFullBagState(fixture, before);
                observed++;
            };

            await fixture.FrameAsync(request);

            fixture.SendObserver = null;
            Assert.Equal(1, observed);
            AssertMailboxFullBagState(fixture, before);
            AssertMailboxFullBagOnlyError(fixture, first, logStart);
        }

        // Free one slot through an actual persisted warehouse request. There is
        // no Store replacement, cache edit or post-login fixture file rewrite.
        await FreeMailboxFullBagSlotThroughWarehouseAsync(fixture, before);
        var retry = CaptureMailboxFullBag(fixture);
        Assert.Equal(39, retry.Owner.PlayerItems.InventoryItems.Count);
        var expected = ExpectedAtomicMailFiles(fixture, retry.Disk, mixed: claimAll);
        var delivered = 0;
        fixture.SendObserver = _ => { fixture.AssertFiles(expected); delivered++; };
        var started = DateTime.Now;

        await fixture.FrameAsync(request);

        fixture.SendObserver = null;
        Assert.Equal(claimAll ? 7 : 2, delivered);
        AssertAtomicMailCommitted(fixture, retry.Disk, mixed: claimAll);
        AssertAtomicMailFrames(fixture, retry.Disk, claimAll, claimAll, started, DateTime.Now, retry.Disk.FrameCount);
        var current = fixture.ReadEnteredMailboxPackageSession();
        Assert.Equal(40, current.PlayerItems.InventoryItems.Count);
        Assert.Single(current.PlayerItems.InventoryItems, item => item.Uid == AtomicMailItemUid);
        Assert.Single(current.PlayerItems.WarehouseItems, item => item.Uid == 10_000 && item.WarehouseId == 8);
        Assert.Equal(before.Json, JsonSerializer.Serialize(before.Owner));
        var rows = fixture.ReadEnteredMailboxFullBagRows();
        var claimed = Assert.Single(rows, row => row.Id == AtomicMailId);
        Assert.Equal(0U, claimed.ItemId);
        Assert.Equal(claimAll ? 0UL : AtomicMailMoney, claimed.Money);
        Assert.Equal(claimAll ? 0U : AtomicMailCp, claimed.EMoney);
        Assert.Same(Assert.Single(before.Rows, row => row.Id == 22), Assert.Single(rows, row => row.Id == 22));

        var replayFirst = fixture.Sent.Count;
        started = DateTime.Now;
        await fixture.FrameAsync(request);
        AssertAtomicMailCommitted(fixture, retry.Disk, mixed: claimAll);
        AssertAtomicMailFrames(fixture, retry.Disk, claimAll, claimAll, started, DateTime.Now, replayFirst, replay: true);
    });
}
