using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualBundleCustodyExcludesRawWritesAndLeavesOtherAccountAvailable() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var images = await SeedBundlePairAsync(database, repository);
        var other = database.CreateWorldStateRepository();
        var acquired = await repository.TryAcquireBundleCustodyAsync(images.First.AccountId);
        Assert.True(acquired.IsAcquired); Assert.Equal(SqlWorldStateFailure.None, acquired.Failure);
        await using var owner = Assert.IsType<SqlWorldBundleCustody>(acquired.Custody);
        Assert.Equal(images.First.AccountId, owner.AccountId); Assert.Equal(images.First.CharacterUid, owner.CharacterUid);
        var read = await owner.ReadBundleAsync();
        Assert.Equal(SqlWorldStateReadStatus.Found, read.Status);
        var record = Assert.IsType<SqlWorldStateBundleRecord>(read.Record);
        Assert.Equal(1, record.Revision); AssertBundleImage(images.First, record.Image);
        var busy = await other.TryAcquireBundleCustodyAsync(images.First.AccountId);
        Assert.False(busy.IsAcquired); Assert.Null(busy.Custody); Assert.Equal(SqlWorldStateFailure.SessionCustodyConflict, busy.Failure);
        var second = await other.TryAcquireBundleCustodyAsync(images.Second.AccountId);
        Assert.True(second.IsAcquired); await using var otherOwner = Assert.IsType<SqlWorldBundleCustody>(second.Custody);
        var before = await database.SnapshotWorldBundleRowsAsync();
        await AssertBundleRefusedAsync(database, before, other.CreateBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, images.First, 0)), SqlWorldStateFailure.SessionCustodyConflict);
        await AssertBundleRefusedAsync(database, before, other.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Save, images.First, 1)), SqlWorldStateFailure.SessionCustodyConflict);
        await AssertBundleRefusedAsync(database, before, other.SavePairBundleAsync(BundlePair(images.First, images.Second)), SqlWorldStateFailure.SessionCustodyConflict);
        await AssertCoreRefusedWithoutBundleChangesAsync(database, before, other.CreateAsync(WorldRequest(SqlWorldStateOperationKind.Create, images.First.Core, 0)), SqlWorldStateFailure.SessionCustodyConflict);
        await AssertCoreRefusedWithoutBundleChangesAsync(database, before, other.SaveAsync(WorldRequest(SqlWorldStateOperationKind.Save, images.First.Core, 1)), SqlWorldStateFailure.SessionCustodyConflict);
        await AssertCoreRefusedWithoutBundleChangesAsync(database, before, other.SavePairAsync(WorldPair(images.First.Core, images.Second.Core)), SqlWorldStateFailure.SessionCustodyConflict);
        await AssertBundleReadAsync(database, images.First, 1); await AssertBundleReadAsync(database, images.Second, 1);
        Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());

        // Release only 501; 502 stays genuinely held. Both pair producers
        // must refuse and release any earlier 501 lock they acquired.
        await owner.DisposeAsync();
        await database.WaitForOwnedWorldPrefixAsync(images.First.AccountId, images.Second.AccountId);
        await AssertCoreRefusedWithoutBundleChangesAsync(database, before, other.SavePairAsync(WorldPair(images.First.Core, images.Second.Core)), SqlWorldStateFailure.SessionCustodyConflict);
        await database.WaitForOwnedWorldPrefixAsync(images.First.AccountId, images.Second.AccountId);
        await AssertBundleRefusedAsync(database, before, other.SavePairBundleAsync(BundlePair(images.First, images.Second)), SqlWorldStateFailure.SessionCustodyConflict);
        await using var reacquired = await ReacquireClosedCustodyAsync(database.CreateWorldStateRepository(), images.First.AccountId);
        var reread = await reacquired.ReadBundleAsync(); Assert.Equal(SqlWorldStateReadStatus.Found, reread.Status);
        AssertBundleImage(images.First, Assert.IsType<SqlWorldStateBundleRecord>(reread.Record).Image);
        Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualBundleCustodyBoundSaveRefusesFreshIdentityOrUidAndReplaysHistoricalReceipt() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var images = await SeedBundlePairAsync(database, repository);
        var acquired = await repository.TryAcquireBundleCustodyAsync(images.First.AccountId);
        Assert.True(acquired.IsAcquired); await using var owner = Assert.IsType<SqlWorldBundleCustody>(acquired.Custody);
        var before = await database.SnapshotWorldBundleRowsAsync();
        var wrongAccount = BundleRequest(SqlWorldStateOperationKind.Save, images.Second, 1);
        await AssertBundleRefusedAsync(database, before, owner.SaveBundleAsync(wrongAccount), SqlWorldStateFailure.InvalidAccountId);
        var wrongCharacter = CompleteWorldImage(SqlWorldStateTestData.Image(501, 9999, SqlWorldStateTestData.FirstItems()));
        await AssertBundleRefusedAsync(database, before, owner.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Save, wrongCharacter, 1)), SqlWorldStateFailure.CharacterIdentityConflict);
        await AssertBundleRefusedAsync(database, before, owner.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, images.First, 0)), SqlWorldStateFailure.InvalidOperationKind);
        await AssertBundleRefusedAsync(database, before, owner.SaveBundleAsync(BundlePair(images.First, images.Second)), SqlWorldStateFailure.InvalidOperationKind);
        await AssertBundleRefusedAsync(database, before, owner.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Save, images.First, 2)), SqlWorldStateFailure.RevisionConflict);
        var originalItems = SqlWorldStateTestData.FirstItems();
        var freshItems = originalItems with { InventoryItems = originalItems.InventoryItems.Append(SqlWorldStateTestData.Item(79999)).ToArray() };
        var fresh = CompleteWorldImage(SqlWorldStateTestData.Image(501, 9001, freshItems));
        await AssertBundleRefusedAsync(database, before, owner.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Save, fresh, 1)), SqlWorldStateFailure.ItemUidIssuanceRequired);
        var first = CompleteWorldImage(SqlWorldStateTestData.Image(501, 9001, SqlWorldStateTestData.FirstItems(), 1007, 101), 1);
        var save = BundleRequest(SqlWorldStateOperationKind.Save, first, 1);
        AssertBundleCommit(await owner.SaveBundleAsync(save), SqlWorldStateCommitStatus.Committed, 2, first);
        var reduced = originalItems with { InventoryItems = [SqlWorldStateTestData.Item(SqlWorldStateTestData.FirstKept)] };
        var next = CompleteWorldImage(SqlWorldStateTestData.Image(501, 9001, reduced, 1010, 102), 2);
        AssertBundleCommit(await owner.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Save, next, 2)), SqlWorldStateCommitStatus.Committed, 3, next);
        var settled = await database.SnapshotWorldBundleRowsAsync();
        AssertBundleCommit(await owner.SaveBundleAsync(save), SqlWorldStateCommitStatus.Replayed, 2, first);
        Assert.Equal(settled, await database.SnapshotWorldBundleRowsAsync());
        await AssertBundleReadAsync(database, next, 3); await AssertBundleReadAsync(database, images.Second, 1);
    });
}
