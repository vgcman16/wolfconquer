using System.Reflection;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
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
    public Task RegisteredBoothSharedPredecessorQueuesRealSashThenBuyInFifoCommitOrder() =>
        RunBoothSettlementAsync(async fixture =>
        {
            fixture.Seed(BoothItem(10, 1_100_003), BoothItem(11));
            await fixture.StartAsync();
            await fixture.ListAsync();
            var before = fixture.Capture();
            // Observation-only capture of the ACTUAL transport delegate calls.
            // Two publishers may run concurrently; do not infer ordering from
            // racing reads of the fixture's ordinary List during publication.
            var observed = new System.Collections.Concurrent.ConcurrentQueue<D13VendorSentFrame>();
            fixture.SettlementSendObserver = (recipient, bytes) =>
            {
                observed.Enqueue(new D13VendorSentFrame(recipient, bytes));
                return Task.CompletedTask;
            };
            IDisposable? predecessor = await fixture.SettlementOperations.EnterSharedAsync();
            try
            {
                var deposit = StartRegisteredBoothSashDeposit(fixture);
                await ObserveQueuedBoothWriterAsync(fixture.SettlementOperations, deposit, 1);
                var buy = fixture.BuyAsync();
                await ObserveQueuedBoothWriterAsync(fixture.SettlementOperations, buy, 2);
                Assert.False(deposit.IsCompleted);
                Assert.False(buy.IsCompleted);
                fixture.AssertUnchanged(before);
                predecessor.Dispose();
                predecessor = null;
                await fixture.ObserveSettlementAsync(deposit);
                await fixture.ObserveSettlementAsync(buy);
                fixture.SettlementSendObserver = null;

                // Independent finite oracle for the intermediate committed sash;
                // this is NEVER installed as a live owner or saved by the test.
                var originalSash = Assert.Single(before.Seller.PlayerItems.InventoryItems, item => item.Uid == 10);
                var child = Assert.Single(before.Seller.PlayerItems.InventoryItems, item => item.Uid == 11)
                    .WithWarehouseId(0, position: 0);
                var depositedSash = originalSash.WithDepositedItems(new[] { child });
                var sellerAfterDeposit = before.Seller with
                { PlayerItems = before.Seller.PlayerItems with { InventoryItems = new[] { depositedSash } } };
                var afterDepositFiles = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase)
                {
                    [Path.GetRelativePath(fixture.Root, fixture.ItemPath(before.Seller))] =
                        PlayerItemSnapshotLoader.ToLegacyBytes(sellerAfterDeposit.PlayerItems),
                };
                var afterDeposit = before with
                { Seller = sellerAfterDeposit, SellerValues = JsonSerializer.Serialize(sellerAfterDeposit), Files = afterDepositFiles };
                AssertSettledBoothValues(fixture, afterDeposit); // Exact economics, all stores, full file set and bytes.
                var received = Assert.Single(fixture.Buyer.PlayerItems.InventoryItems, item => item.Uid == 10);
                Assert.Equal(11U, Assert.Single(received.DepositedItems).Uid); // Buy observed Sash's earlier commit.

                var attempted = observed.ToArray();
                static bool IsSashFrame(D13VendorSentFrame frame) => frame.Recipient == "seller" &&
                    ((MsgItemUsagePacket.TryParse(D13VendorTestHarness.ReadFrame(frame.Bytes), out var usage) &&
                        usage.Action == MsgItemUsageAction.RemoveInventory && usage.Id == 11) ||
                     (LegacyWarehousePacket.TryParse(D13VendorTestHarness.ReadFrame(frame.Bytes), out var warehouse) &&
                        warehouse.Action == LegacyWarehouseAction.ShashDepositItem));
                var sashFrames = attempted.Where(IsSashFrame).ToArray();
                Assert.InRange(sashFrames.Length, 0, 2);
                var sashExpected = new[]
                {
                    new MsgItemUsagePacket(11, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(),
                    new LegacyWarehousePacket(10, LegacyWarehouseAction.ShashDepositItem, 0, 0, new[] { child }).ToFrameBytes(),
                };
                for (var index = 0; index < sashFrames.Length; index++) Assert.Equal(sashExpected[index], sashFrames[index].Bytes);
                // FIFO proves commit order, not global send serialization. A
                // current-owner check may race the competing commit before send.
                var frames = attempted.Where(frame => !IsSashFrame(frame)).ToArray();
                Assert.Equal(6, frames.Length);
                var request = BoothFixture.BuyRequest();
                Assert.Equal(new[] { "buyer", "buyer", "seller", "seller", "buyer", "seller" }, frames.Select(frame => frame.Recipient));
                Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(depositedSash).ToFrameBytes(), frames[0].Bytes);
                Assert.Equal(request.ToFrameBytes(), frames[1].Bytes);
                Assert.Equal((request with { Action = MsgItemUsageAction.RemoveVendingItem }).ToFrameBytes(), frames[2].Bytes);
                Assert.Equal(new MsgItemUsagePacket(10, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), frames[3].Bytes);
                Assert.Equal(new LegacyMsgUpdatePacket(D13VendorTestHarness.BuyerCharacterUid,
                    [new(LegacyMsgUpdateDataType.Money, 875)]).ToFrameBytes(), frames[4].Bytes);
                Assert.Equal(new LegacyMsgUpdatePacket(D13VendorTestHarness.SellerCharacterUid,
                    [new(LegacyMsgUpdateDataType.Money, 425)]).ToFrameBytes(), frames[5].Bytes);
            }
            finally { predecessor?.Dispose(); }
        });

    [Fact]
    public Task RegisteredBoothBuyCommitsWhileSashFirstSendWaitsThenSuppressesItsRemainingWarehouseFrame() =>
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
                if (recipient == "seller" && MsgItemUsagePacket.TryParse(D13VendorTestHarness.ReadFrame(bytes), out var packet) &&
                    packet.Action == MsgItemUsageAction.RemoveInventory && packet.Id == 11)
                {
                    fixture.AssertSettlementSaved(); // Sash's actual file commit precedes its FIRST send.
                    Assert.DoesNotContain(fixture.Seller.PlayerItems.InventoryItems, item => item.Uid == 11);
                    Assert.Equal(11U, Assert.Single(Assert.Single(fixture.Seller.PlayerItems.InventoryItems).DepositedItems).Uid);
                    entered.TrySetResult();
                    await release.Task;
                }
            };
            try
            {
                var deposit = StartRegisteredBoothSashDeposit(fixture);
                await entered.Task.WaitAsync(SettlementBound);
                Assert.False(deposit.IsCompleted);
                var beforeBuy = fixture.Capture();
                var buy = fixture.BuyAsync();
                await fixture.ObserveSettlementAsync(buy); // Completes while Sash is STILL paused.
                Assert.False(deposit.IsCompleted);
                AssertOneSettledBoothPurchase(fixture, beforeBuy); // Exact six buy frames, economics and complete files.
                var received = Assert.Single(fixture.Buyer.PlayerItems.InventoryItems, item => item.Uid == 10);
                Assert.Equal(11U, Assert.Single(received.DepositedItems).Uid);
                var committed = fixture.Capture();
                release.TrySetResult();
                await fixture.ObserveSettlementAsync(deposit);
                fixture.SettlementSendObserver = null;
                fixture.AssertUnchanged(committed); // No later Warehouse frame or stale Seller overwrite.
            }
            finally { release.TrySetResult(); } // Also registered with actual fixture cleanup on assertion failure.
        });

    private static Task StartRegisteredBoothSashDeposit(BoothFixture fixture)
    {
        // The sequential DepositAsync helper keeps its own-item postconditions;
        // this overlapping test tracks the real Registry task directly instead.
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var request = new LegacyWarehousePacket(10, LegacyWarehouseAction.ShashDepositItem, 11, 0, []);
        var actual = registry.HandleAsync(fixture.Context(fixture.SellerConnection),
            D13VendorTestHarness.ReadFrame(request.ToFrameBytes())).AsTask();
        fixture.Calls.Add(actual);
        return actual;
    }

    private static async Task ObserveQueuedBoothWriterAsync(WorldOperationBarrier operations, Task actual, int expectedCount)
    {
        var type = typeof(WorldOperationBarrier);
        var gate = type.GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(operations)!;
        var queue = type.GetField("_queue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(operations)!;
        var count = queue.GetType().GetProperty("Count")!;
        using var budget = new CancellationTokenSource(SettlementBound);
        while (true)
        {
            lock (gate)
            {
                var queued = (int)count.GetValue(queue)!;
                if (queued >= expectedCount) { Assert.Equal(expectedCount, queued); return; }
            }
            if (actual.IsCompleted) { await actual; Assert.Fail("Operation completed before its expected actual FIFO queue position."); }
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
