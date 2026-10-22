using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Theory]
    [InlineData(0)] // Bound ordinary item.
    [InlineData(1)] // Unbound unabletradeitem entry.
    [InlineData(2)] // Ordinary unbound ground item.
    public async Task DropCommitsDiskSessionAndGroundBeforeFirstOutboundAndReleasesWorldLease(int disposition)
    {
        const uint uid = 1_299_701;
        SeedPersistenceDrop(uid, disposition);
        var owner = NewOwner();
        var barrier = ActualOperationBarrier(owner);
        Player? player = null;
        WorldSession? before = null;
        byte[]? characterBytes = null;
        var armed = false;
        var firstOutbound = 0;
        var leaseAvailable = false;
        Exception? callbackFailure = null;
        Task probeCleanup = Task.CompletedTask;
        player = await Login(owner, uid, bytes =>
        {
            if (!armed || firstOutbound != 0) return;
            firstOutbound++;
            callbackFailure = Record.Exception(() =>
            {
                Assert.NotNull(before);
                Assert.NotNull(characterBytes);
                AssertPersistenceDropCommitted(owner, player!, before, characterBytes, disposition == 2 ? 1 : 0);
                if (disposition == 2)
                {
                    Assert.True(LegacyFloorItemPacket.TryParse(ReadFrame(bytes), out var floor));
                    Assert.Equal(LegacyFloorItemDropType.Visible, floor.DropType);
                }
                else AssertExactDropRemoval(bytes);
            });
            using var cancellation = new CancellationTokenSource();
            var acquisition = barrier.EnterExclusiveAsync(cancellation.Token);
            leaseAvailable = acquisition.IsCompletedSuccessfully;
            if (leaseAvailable) acquisition.Result.Dispose();
            else
            {
                cancellation.Cancel();
                probeCleanup = DisposeCancelledProbe(acquisition);
            }
        });
        before = Current(owner, player);
        characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;
        armed = true;

        await DropPinItem(owner, player, SoldUid);
        await probeCleanup.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, firstOutbound);
        Assert.Null(callbackFailure); // Never let dispatch swallow the ordering oracle.
        Assert.True(leaseAvailable, "Drop/dispatch must release the world lease before the first socket await.");
        AssertPersistenceDropCommitted(owner, player, before, characterBytes, disposition == 2 ? 1 : 0);
        AssertPersistenceDropFrames(player.Sent.Skip(first).ToArray(), disposition == 2);
    }

    [WindowsNpcSaleTheory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task DropCharacterOrItemReplacementFailureLeavesNoPartialStateAndUnlockRetryCommits(int disposition, bool itemTarget)
    {
        const uint uid = 1_299_711;
        SeedPersistenceDrop(uid, disposition);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;
        var path = itemTarget ? ItemPath(uid) : UserPath(uid);

        // Baseline reads remain legal. This exercises the real Windows write /
        // replacement boundary, not only a synthetic failed file-open check.
        using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await DropPinItem(owner, player, SoldUid);
            Assert.Same(before, Current(owner, player));
            Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
            Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
            Assert.Empty(player.Sent.Skip(first));
            Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
        }
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));

        await DropPinItem(owner, player, SoldUid);

        AssertPersistenceDropCommitted(owner, player, before, characterBytes, disposition == 2 ? 1 : 0);
        AssertPersistenceDropFrames(player.Sent.Skip(first).ToArray(), disposition == 2);
        await AssertPersistenceDropReplayIsSilent(owner, player, disposition == 2 ? 1 : 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DropFirstSendFailureDoesNotRollBackOrRecreateCommittedItemAndNewOwnerReadsRemoval(bool groundDrop)
    {
        const uint uid = 1_299_721;
        SeedPersistenceDrop(uid, groundDrop ? 2 : 0);
        var owner = NewOwner();
        Player? player = null;
        WorldSession? before = null;
        byte[]? characterBytes = null;
        var armed = false;
        var attemptedSends = 0;
        Exception? callbackFailure = null;
        player = await Login(owner, uid, bytes =>
        {
            if (!armed) return;
            attemptedSends++;
            callbackFailure ??= Record.Exception(() =>
            {
                Assert.NotNull(before);
                Assert.NotNull(characterBytes);
                AssertPersistenceDropCommitted(owner, player!, before, characterBytes, groundDrop ? 1 : 0);
                if (groundDrop)
                {
                    Assert.True(LegacyFloorItemPacket.TryParse(ReadFrame(bytes), out var floor));
                    Assert.Equal(LegacyFloorItemDropType.Visible, floor.DropType);
                }
                else AssertExactDropRemoval(bytes);
            });
            throw new IOException("Synthetic first committed Drop send failure.");
        });
        before = Current(owner, player);
        characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;
        armed = true;

        await DropPinItem(owner, player, SoldUid);

        Assert.Equal(1, attemptedSends);
        Assert.Null(callbackFailure);
        Assert.Equal(first, player.Sent.Count); // The throwing delegate never queued that frame.
        AssertPersistenceDropCommitted(owner, player, before, characterBytes, groundDrop ? 1 : 0);
        armed = false;
        await AssertPersistenceDropReplayIsSilent(owner, player, groundDrop ? 1 : 0);
        Assert.Equal(1, attemptedSends);
        var expectedBytes = PlayerItemSnapshotLoader.ToLegacyBytes(PersistenceDropItems(before));

        // Independent handler/session ownership models a restart's snapshot read.
        // Only the consumed inventory image is durable here; this does not prove
        // persistence of ground objects across server process restarts.
        var reopenedOwner = NewOwner();
        var reopened = await Login(reopenedOwner, uid);
        Assert.Equal(expectedBytes, PlayerItemSnapshotLoader.ToLegacyBytes(Current(reopenedOwner, reopened).PlayerItems));
        Assert.DoesNotContain(Current(reopenedOwner, reopened).PlayerItems.InventoryItems, item => item.Uid == SoldUid);
        Assert.Equal(before.Character.Money, Current(reopenedOwner, reopened).Character.Money);
        Assert.Equal(before.Character.ConquerPoints, Current(reopenedOwner, reopened).Character.ConquerPoints);
        var restartedFirst = reopened.Sent.Count;
        await DropPinItem(reopenedOwner, reopened, SoldUid);
        Assert.Empty(reopened.Sent.Skip(restartedFirst));
        Assert.Equal(expectedBytes, File.ReadAllBytes(ItemPath(uid)));
    }

    [Theory]
    [InlineData("stale-item-image")]
    [InlineData("inventory-duplicate")]
    [InlineData("warehouse-duplicate")]
    [InlineData("nested-duplicate")]
    public async Task DropRejectsStaleOrGloballyAmbiguousUidBeforeDiskSessionOrFloorPublication(string scenario)
    {
        const uint uid = 1_299_731;
        SeedPersistenceDrop(uid, disposition: 2);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var session = Current(owner, player);
        var dropped = Assert.Single(session.PlayerItems.InventoryItems, item => item.Uid == SoldUid);
        if (scenario == "stale-item-image")
        {
            new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(uid, session.PlayerItems with
            {
                InventoryItems = session.PlayerItems.InventoryItems.Select(item => item.Uid == SoldUid
                    ? item.WithItemId(ItemId + 1) : item).ToArray(),
            });
        }
        else
        {
            var items = scenario switch
            {
                "inventory-duplicate" => session.PlayerItems with { InventoryItems = session.PlayerItems.InventoryItems.Append(dropped).ToArray() },
                "warehouse-duplicate" => session.PlayerItems with { WarehouseItems = session.PlayerItems.WarehouseItems.Append(dropped.WithWarehouseId(1000)).ToArray() },
                _ => session.PlayerItems with
                {
                    WarehouseItems = session.PlayerItems.WarehouseItems.Select(item => item.WithDepositedItems([dropped])).ToArray(),
                },
            };
            owner.Sessions.Set(player.Connection, session with { PlayerItems = items });
        }
        var before = Current(owner, player);
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;

        await DropPinItem(owner, player, SoldUid);

        Assert.Same(before, Current(owner, player));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Empty(player.Sent.Skip(first));
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private void SeedPersistenceDrop(uint uid, int disposition)
    {
        Seed(uid);
        var loader = new PlayerItemSnapshotLoader(ItemsRoot);
        var items = loader.LoadByCharacterUid(uid);
        loader.SaveByCharacterUid(uid, items with
        {
            InventoryItems = items.InventoryItems.Select(item => item.Uid == SoldUid
                ? item.WithItemId(disposition == 1 ? 722_700U : ItemId).WithBound(disposition == 0 ? (byte)1 : (byte)0).WithStackSize(3)
                : item).ToArray(),
        });
    }

    private static PlayerItemSnapshot PersistenceDropItems(WorldSession before) => before.PlayerItems with
    { InventoryItems = before.PlayerItems.InventoryItems.Where(item => item.Uid != SoldUid).ToArray() };

    private void AssertPersistenceDropCommitted(Owner owner, Player player, WorldSession before, byte[] characterBytes, int groundCount)
    {
        var after = Current(owner, player);
        var expected = PersistenceDropItems(before);
        AssertWholeDropItemImage(player.Uid, before, after, expected, characterBytes);
        Assert.Equal(groundCount, DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow).Count);
        if (groundCount == 1)
        {
            var entry = Assert.Single(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
            Assert.NotNull(entry.Item);
            Assert.Same(Assert.Single(before.PlayerItems.InventoryItems, item => item.Uid == SoldUid), entry.Item);
            Assert.Equal(SoldUid, entry.Item.Uid);
            Assert.Equal((ushort)3, entry.Item.StackSize);
            Assert.Equal(before.Character.MapId, entry.MapId);
            Assert.Equal(before.Character.Uid, entry.Packet.ItemOwnerUid);
        }
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private static void AssertPersistenceDropFrames(IReadOnlyList<byte[]> frames, bool ground)
    {
        Assert.Equal(ground ? 2 : 1, frames.Count);
        if (ground)
        {
            Assert.True(LegacyFloorItemPacket.TryParse(ReadFrame(frames[0]), out var floor));
            Assert.Equal(LegacyFloorItemDropType.Visible, floor.DropType);
        }
        AssertExactDropRemoval(frames[^1]);
    }

    private async Task AssertPersistenceDropReplayIsSilent(Owner owner, Player player, int groundCount)
    {
        var before = Current(owner, player);
        var itemBytes = File.ReadAllBytes(ItemPath(player.Uid));
        var characterBytes = File.ReadAllBytes(UserPath(player.Uid));
        var groundBefore = DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow).ToArray();
        var first = player.Sent.Count;
        await DropPinItem(owner, player, SoldUid);
        Assert.Same(before, Current(owner, player));
        Assert.Empty(player.Sent.Skip(first));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(player.Uid)));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(player.Uid)));
        var groundAfter = DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow).ToArray();
        Assert.Equal(groundCount, groundAfter.Length);
        Assert.Equal(groundBefore, groundAfter);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }
}
