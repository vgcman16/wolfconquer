using System.Reflection;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPlayerBoothItemClosureTests
{
    [Fact]
    public Task RegisteredBoothLongAccountFilenameWithDistinctUintCharacterOwnsActualSettlement() =>
        RunBoothSettlementAsync(async fixture =>
        {
            fixture.SeedSettlementLongSellerAccount((long)uint.MaxValue + 202);
            await fixture.StartAsync();
            await fixture.ListAsync();
            var before = fixture.Capture();

            await fixture.BuyAsync();

            AssertOneSettledBoothPurchase(fixture, before);
            Assert.Equal((long)uint.MaxValue + 202, fixture.Seller.AccountId);
            Assert.Equal(D13VendorTestHarness.SellerCharacterUid, fixture.Seller.Character.Uid);
        });

    [WindowsTradeJournalFailureFact]
    public Task RegisteredBoothSharingFailureKeepsExactPairBookAndFilesThenSamePairRetriesOnce() =>
        RunBoothSettlementAsync(async fixture =>
        {
            fixture.Seed(BoothItem(10));
            await fixture.StartAsync();
            await fixture.ListAsync();
            var before = fixture.Capture();
            // Capture bytes before the genuine exclusive lease. Release it only
            // after the actual handler has terminated, never on an observation timeout.
            using (new FileStream(fixture.ItemPath(before.Seller), FileMode.Open, FileAccess.Read, FileShare.None))
                await fixture.BuyAsync();
            fixture.AssertUnchanged(before);
            Assert.False(fixture.SettlementRecovery.IsBlocked);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Harness.UsersRoot));

            await fixture.BuyAsync();

            AssertOneSettledBoothPurchase(fixture, before);
            var committed = fixture.Capture();
            await fixture.BuyAsync();
            fixture.AssertUnchanged(committed);
        });

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public Task RegisteredBoothStaleWalletOrFullItemImageCannotBeOverwritten(bool buyer, bool itemFile) =>
        RunBoothSettlementAsync(async fixture =>
        {
            fixture.Seed(BoothItem(10));
            await fixture.StartAsync();
            await fixture.ListAsync();
            var session = buyer ? fixture.Buyer : fixture.Seller;
            var path = itemFile ? fixture.ItemPath(session) : fixture.CharacterPath(session);
            var original = File.ReadAllBytes(path);
            if (itemFile)
            {
                var first = session.PlayerItems.InventoryItems[0];
                var changed = session.PlayerItems with
                {
                    InventoryItems = session.PlayerItems.InventoryItems.Select(item =>
                        ReferenceEquals(item, first) ? item.WithDurability(4) : item).ToArray(),
                };
                new PlayerItemSnapshotLoader(fixture.Harness.PlayerItemsRoot)
                    .SaveByCharacterUid(session.Character.Uid, changed);
            }
            else
            {
                Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(path, session.Character.Uid,
                    new Dictionary<string, string> { ["Money"] = "777" }, detailedIoFailures: true,
                    out var reason), reason);
            }
            Assert.NotEqual(original, File.ReadAllBytes(path));
            var before = fixture.Capture(); // The real coherent foreign baseline is now authoritative on disk.

            await fixture.BuyAsync();

            fixture.AssertUnchanged(before);
            Assert.Contains(itemFile ? "vendorItemFileStale" : "tradeEscrowWalletOrClaimStale",
                fixture.Log.ToString()[before.LogLength..], StringComparison.Ordinal);
            Assert.False(fixture.SettlementRecovery.IsBlocked);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Harness.UsersRoot));
            File.WriteAllBytes(path, original); // Repair only this owned fault; no reseed/session reset.
            var repaired = fixture.Capture();
            await fixture.BuyAsync();
            AssertOneSettledBoothPurchase(fixture, repaired);
        });

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public Task RegisteredBoothPendingOrMalformedEscrowIsNeitherSettledNorCleared(bool buyer, bool malformed) =>
        RunBoothSettlementAsync(async fixture =>
        {
            fixture.Seed(BoothItem(10));
            await fixture.StartAsync();
            await fixture.ListAsync();
            fixture.SeedSettlementClaim(buyer, malformed);
            var before = fixture.Capture();

            await fixture.BuyAsync();

            fixture.AssertUnchanged(before);
            Assert.Contains("vendorEscrowActiveOrInvalid", fixture.Log.ToString()[before.LogLength..], StringComparison.Ordinal);
            Assert.False(fixture.SettlementRecovery.IsBlocked);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Harness.UsersRoot));
            var owner = buyer ? fixture.Buyer : fixture.Seller;
            var read = LegacyTradeEscrowClaims.TryRead(owner.Character.Attributes, out var claim, out _);
            Assert.Equal(!malformed, read);
            if (!malformed) { Assert.NotNull(claim); Assert.Equal(25, claim.Money); }
        });

    [Fact]
    public Task RegisteredBoothCommittedSendIOExceptionDoesNotResellRefundOrDoubleCreditOnReplay() =>
        RunBoothSettlementAsync(async fixture =>
        {
            fixture.Seed(BoothItem(10));
            await fixture.StartAsync();
            await fixture.ListAsync();
            var before = fixture.Capture();
            var attempted = 0;
            fixture.SettlementSendObserver = (recipient, _) =>
            {
                if (recipient != "buyer") return Task.CompletedTask;
                attempted++;
                fixture.AssertSettlementSaved(); // Real durable pair exists BEFORE the failing send.
                throw new IOException("Owned booth buyer transport fault after committed settlement.");
            };

            var firstCall = fixture.Calls.Count;
            var failure = await Record.ExceptionAsync(() => fixture.BuyAsync());
            var sendFailure = Assert.IsType<IOException>(failure);
            await fixture.ObserveExpectedSettlementSendFailureAsync(firstCall, sendFailure);

            Assert.Equal(1, attempted);
            fixture.SettlementSendObserver = null;
            AssertSettledBoothValues(fixture, before);
            Assert.Equal(before.FrameCount + 1, fixture.Harness.Sent.Count); // One attempted send, not delivered proof.
            var committed = fixture.Capture();
            await fixture.BuyAsync();
            fixture.AssertUnchanged(committed);
            fixture.AssertSettlementSaved();
        });

    [Fact]
    public Task RegisteredBoothRecipientAndSavedChildDetachRetiredSashMutablePerfection() =>
        RunBoothSettlementAsync(async fixture =>
        {
            fixture.Seed(BoothItem(10, 1_100_003), BoothItem(11));
            await fixture.DepositAsync(11);
            await fixture.StartAsync();
            await fixture.ListAsync();
            var before = fixture.Capture();
            var retiredSash = Assert.Single(before.Seller.PlayerItems.InventoryItems);
            var retiredChild = Assert.Single(retiredSash.DepositedItems);
            await fixture.BuyAsync();
            AssertOneSettledBoothPurchase(fixture, before);
            var receivedSash = Assert.Single(fixture.Buyer.PlayerItems.InventoryItems, item => item.Uid == 10);
            var receivedChild = Assert.Single(receivedSash.DepositedItems);
            Assert.NotSame(retiredSash, receivedSash);
            Assert.NotSame(retiredChild, receivedChild);
            var committed = fixture.Capture();

            retiredChild.OwnerUid = 999;
            retiredChild.OwnerName = "RetiredAlias";
            retiredChild.Signature = "not recipient state";
            retiredChild.PerfectionLevel = 77;
            retiredChild.PerfectionProgress = 88;

            Assert.Equal(committed.BuyerValues, JsonSerializer.Serialize(fixture.Buyer));
            Assert.Equal(committed.SellerValues, JsonSerializer.Serialize(fixture.Seller));
            fixture.AssertFiles(committed.Files);
            fixture.AssertSettlementSaved();
        });

    [Fact]
    public Task RegisteredBoothBuyWaitsForActualAwaitedSashWriterAndItsOwnedSave() =>
        RunBoothSettlementAsync(async fixture =>
        {
            fixture.Seed(BoothItem(10, 1_100_003), BoothItem(11));
            await fixture.StartAsync();
            await fixture.ListAsync();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.SettlementReleases.Add(release);
            fixture.SettlementSendObserver = async (recipient, bytes) =>
            {
                if (recipient == "seller" && LegacyWarehousePacket.TryParse(D13VendorTestHarness.ReadFrame(bytes), out var packet) &&
                    packet.Action == LegacyWarehouseAction.ShashDepositItem)
                {
                    entered.TrySetResult();
                    await release.Task;
                }
            };
            var deposit = fixture.DepositAsync(11);
            await entered.Task.WaitAsync(SettlementBound);
            Assert.False(deposit.IsCompleted);
            var buyerBefore = fixture.Buyer;
            var buyerBytes = File.ReadAllBytes(fixture.ItemPath(buyerBefore));
            var framesBeforeBuy = fixture.Harness.Sent.Count;
            var buy = fixture.BuyAsync();

            await ObserveQueuedBoothWriterAsync(fixture.SettlementOperations, buy);
            Assert.False(buy.IsCompleted);
            Assert.Same(buyerBefore, fixture.Buyer);
            Assert.Equal(buyerBytes, File.ReadAllBytes(fixture.ItemPath(buyerBefore)));
            Assert.Equal(framesBeforeBuy, fixture.Harness.Sent.Count);
            release.TrySetResult();
            await fixture.ObserveSettlementAsync(deposit);
            await fixture.ObserveSettlementAsync(buy);
            fixture.SettlementSendObserver = null;

            Assert.Equal(875, fixture.Buyer.Character.Money);
            Assert.Equal(425, fixture.Seller.Character.Money);
            var received = Assert.Single(fixture.Buyer.PlayerItems.InventoryItems, item => item.Uid == 10);
            Assert.Equal(11U, Assert.Single(received.DepositedItems).Uid);
            Assert.Empty(fixture.Seller.PlayerItems.InventoryItems);
            Assert.Empty(fixture.Seller.LegacyVendorListings!.Listings);
            Assert.Equal(framesBeforeBuy + 6, fixture.Harness.Sent.Count);
            fixture.AssertSettlementSaved();
        });

    private static async Task ObserveQueuedBoothWriterAsync(WorldOperationBarrier operations, Task buy)
    {
        var type = typeof(WorldOperationBarrier);
        var gate = type.GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(operations)!;
        var queue = type.GetField("_queue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(operations)!;
        var count = queue.GetType().GetProperty("Count")!;
        using var budget = new CancellationTokenSource(SettlementBound);
        while (true)
        {
            lock (gate) { if ((int)count.GetValue(queue)! > 0) return; }
            if (buy.IsCompleted) { await buy; Assert.Fail("Buy completed without queuing behind the actual sash reader."); }
            await Task.Delay(10, budget.Token);
        }
    }

    private static void AssertOneSettledBoothPurchase(BoothFixture fixture, Snapshot before)
    {
        AssertSettledBoothValues(fixture, before);
        var frames = fixture.Harness.Sent.Skip(before.FrameCount).ToArray();
        Assert.Equal(6, frames.Length);
        var original = Assert.Single(before.Seller.PlayerItems.InventoryItems, item => item.Uid == 10);
        var request = BoothFixture.BuyRequest();
        Assert.Equal(new[] { "buyer", "buyer", "seller", "seller", "buyer", "seller" }, frames.Select(frame => frame.Recipient));
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(original).ToFrameBytes(), frames[0].Bytes);
        Assert.Equal(request.ToFrameBytes(), frames[1].Bytes);
        Assert.Equal((request with { Action = MsgItemUsageAction.RemoveVendingItem }).ToFrameBytes(), frames[2].Bytes);
        Assert.Equal(new MsgItemUsagePacket(10, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), frames[3].Bytes);
        Assert.Equal(new LegacyMsgUpdatePacket(D13VendorTestHarness.BuyerCharacterUid,
            [new(LegacyMsgUpdateDataType.Money, 875)]).ToFrameBytes(), frames[4].Bytes);
        Assert.Equal(new LegacyMsgUpdatePacket(D13VendorTestHarness.SellerCharacterUid,
            [new(LegacyMsgUpdateDataType.Money, 425)]).ToFrameBytes(), frames[5].Bytes);
    }

    private static void AssertSettledBoothValues(BoothFixture fixture, Snapshot before)
    {
        Assert.Equal(875, fixture.Buyer.Character.Money);
        Assert.Equal(425, fixture.Seller.Character.Money);
        Assert.Equal(200, fixture.Buyer.Character.ConquerPoints);
        Assert.Equal(50, fixture.Seller.Character.ConquerPoints);
        Assert.Empty(fixture.Seller.LegacyVendorListings!.Listings);
        Assert.Single(before.Seller.LegacyVendorListings!.Listings);
        Assert.Empty(fixture.Seller.PlayerItems.InventoryItems);
        var original = Assert.Single(before.Seller.PlayerItems.InventoryItems, item => item.Uid == 10);
        var buyerItems = before.Buyer.PlayerItems with
        { InventoryItems = before.Buyer.PlayerItems.InventoryItems.Append(original).ToArray() };
        var sellerItems = before.Seller.PlayerItems with { InventoryItems = Array.Empty<PlayerItemSnapshotItem>() };
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(buyerItems), PlayerItemSnapshotLoader.ToLegacyBytes(fixture.Buyer.PlayerItems));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(sellerItems), PlayerItemSnapshotLoader.ToLegacyBytes(fixture.Seller.PlayerItems));
        AssertBoothOtherStores(before.Buyer.PlayerItems, fixture.Buyer.PlayerItems);
        AssertBoothOtherStores(before.Seller.PlayerItems, fixture.Seller.PlayerItems);
        var files = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase)
        {
            [Path.GetRelativePath(fixture.Root, fixture.CharacterPath(before.Buyer))] = ProjectBoothWallet(
                before.Files[Path.GetRelativePath(fixture.Root, fixture.CharacterPath(before.Buyer))], 875, 200),
            [Path.GetRelativePath(fixture.Root, fixture.CharacterPath(before.Seller))] = ProjectBoothWallet(
                before.Files[Path.GetRelativePath(fixture.Root, fixture.CharacterPath(before.Seller))], 425, 50),
            [Path.GetRelativePath(fixture.Root, fixture.ItemPath(before.Buyer))] = PlayerItemSnapshotLoader.ToLegacyBytes(buyerItems),
            [Path.GetRelativePath(fixture.Root, fixture.ItemPath(before.Seller))] = PlayerItemSnapshotLoader.ToLegacyBytes(sellerItems),
        };
        fixture.AssertFiles(files);
        fixture.AssertSettlementSaved();
    }
}
