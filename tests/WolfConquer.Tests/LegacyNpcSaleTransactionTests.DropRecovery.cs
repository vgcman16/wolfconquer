using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Theory]
    [InlineData(0)] // Published WAL; both exact preimages remain.
    [InlineData(2)] // Both exact postimages exist, but the WAL is still pending.
    public async Task PendingDropJournalFencesLivePublicationThenFreshRecoveryCannotDuplicateRemovedInventoryOrGround(int replacements)
    {
        const uint uid = 1_299_751;
        SeedPersistenceDrop(uid, disposition: 2);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var original = Current(owner, player);
        var updated = original with { PlayerItems = PersistenceDropItems(original) };
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var beforeItems = File.ReadAllBytes(ItemPath(uid));
        var afterItems = PlayerItemSnapshotLoader.ToLegacyBytes(updated.PlayerItems);

        // This component seam deliberately retains a real v2 WAL. It is not a
        // runtime fault-injection setting or a process/power-loss simulation.
        Assert.False(LegacyNpcSalePersistence.TryPersistItemRemoval(original, updated, ItemsRoot,
            count => count == replacements, out var reason));
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(replacements == 0 ? beforeItems : afterItems, File.ReadAllBytes(ItemPath(uid)));
        var journalBytes = File.ReadAllBytes(PendingPath);

        await AssertPinDropDenied(owner, player, expectedReason: "tradeJournalRecoveryRequired:pending");

        Assert.Same(original, Current(owner, player));
        Assert.Equal(journalBytes, File.ReadAllBytes(PendingPath));
        // A different, still-present UID is also blocked by the host-wide latch.
        await AssertPinDropDenied(owner, player, KeptUid, expectedReason: "trade recovery");
        Assert.Equal(journalBytes, File.ReadAllBytes(PendingPath));

        // A new handler owns startup recovery before admission; the old owner is
        // abandoned except for the explicit no-mutation fence probe below.
        var restarted = NewOwner();
        var reopened = await Login(restarted, uid);
        var recovered = Current(restarted, reopened);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(afterItems, File.ReadAllBytes(ItemPath(uid)));
        Assert.Equal(afterItems, PlayerItemSnapshotLoader.ToLegacyBytes(recovered.PlayerItems));
        Assert.DoesNotContain(recovered.PlayerItems.InventoryItems, item => item.Uid == SoldUid);
        Assert.Equal(KeptUid, Assert.Single(recovered.PlayerItems.InventoryItems).Uid);
        Assert.Equal(original.Character.Money, recovered.Character.Money);
        Assert.Equal(original.Character.ConquerPoints, recovered.Character.ConquerPoints);
        Assert.Empty(DropFloor(restarted).SnapshotVisible(DateTimeOffset.UtcNow));

        // V2 recovery rolls the inventory removal forward. It does not restore
        // ground loot: silent replay is a duplication fence, not ground restart
        // durability or proof of the complete native Drop/Map lifecycle.
        await AssertPinDropDenied(restarted, reopened, expectedReason: "inventory item was not found");
        await AssertPinDropDenied(owner, player, KeptUid, expectedReason: "trade recovery");
        Assert.True(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var again, out reason), reason);
        Assert.Equal(0, again);
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(afterItems, File.ReadAllBytes(ItemPath(uid)));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    [Fact]
    public async Task MalformedPendingDropJournalPreservesEvidenceAndFencesLiveDropFreshAdmissionAndRecovery()
    {
        const uint uid = 1_299_761;
        SeedPersistenceDrop(uid, disposition: 2);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var original = Current(owner, player);
        Assert.False(LegacyNpcSalePersistence.TryPersistItemRemoval(original,
            original with { PlayerItems = PersistenceDropItems(original) }, ItemsRoot,
            count => count == 0, out var reason));
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
        File.WriteAllText(PendingPath, "{\"Version\":2,\"Changes\":[");
        var journalBytes = File.ReadAllBytes(PendingPath);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));

        await AssertPinDropDenied(owner, player, expectedReason: "tradeJournalRecoveryRequired:pending");
        await AssertPinDropDenied(owner, player, KeptUid, expectedReason: "trade recovery");
        var fresh = NewOwner();
        var attempted = CreateConnection(uid);

        await SendLogin(fresh, attempted);

        Assert.False(fresh.Sessions.TryGet(attempted.Connection, out _));
        Assert.Empty(attempted.Sent); // No admission, floor-visible, or removal frame.
        Assert.Contains("trade recovery", fresh.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(DropFloor(fresh).SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.False(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var recovered, out reason));
        Assert.Equal(0, recovered);
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
        Assert.Same(original, Current(owner, player));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(original.PlayerItems), itemBytes);
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
        Assert.Equal(journalBytes, File.ReadAllBytes(PendingPath));
        Assert.True(LegacyTradeJournal.HasPending(UsersRoot));
    }
}
