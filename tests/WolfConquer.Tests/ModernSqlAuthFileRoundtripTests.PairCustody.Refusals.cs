using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualPairCustodyRefusesInvalidRequestsAndForeignSchemaWithoutChangingEitherDatabase() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var initial = await SeedBundlePairAsync(database, repository);
        var acquired1 = await repository.TryAcquireBundleCustodyAsync(initial.First.AccountId);
        Assert.True(acquired1.IsAcquired); await using var first = Assert.IsType<SqlWorldBundleCustody>(acquired1.Custody);
        var acquired2 = await database.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(initial.Second.AccountId);
        Assert.True(acquired2.IsAcquired); await using var second = Assert.IsType<SqlWorldBundleCustody>(acquired2.Custody);
        var before = await database.SnapshotWorldBundleRowsAsync(); var pair = BundlePair(initial.First, initial.Second);
        await Assert.ThrowsAsync<ArgumentNullException>(() => first.SavePairBundleAsync(null!, pair).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => first.SavePairBundleAsync(second, null!).AsTask());
        await AssertBundleRefusedAsync(database, before, first.SavePairBundleAsync(first, pair), SqlWorldStateFailure.ParticipantCount);
        await AssertBundleRefusedAsync(database, before, first.SavePairBundleAsync(second, BundleRequest(SqlWorldStateOperationKind.Save, initial.First, 1)), SqlWorldStateFailure.InvalidOperationKind);
        var third = CompleteWorldImage(SqlWorldStateTestData.Image(503, 9003));
        await AssertBundleRefusedAsync(database, before, first.SavePairBundleAsync(second, BundlePair(initial.First, third)), SqlWorldStateFailure.InvalidAccountId);
        var wrongUid = CompleteWorldImage(SqlWorldStateTestData.Image(501, 9999, SqlWorldStateTestData.FirstItems()));
        await AssertBundleRefusedAsync(database, before, first.SavePairBundleAsync(second, BundlePair(wrongUid, initial.Second)), SqlWorldStateFailure.CharacterIdentityConflict);
        await AssertBundleRefusedAsync(database, before, first.SavePairBundleAsync(second, BundlePair(initial.First, initial.Second, 2, 1)), SqlWorldStateFailure.RevisionConflict);
        var items = SqlWorldStateTestData.FirstItems();
        var fresh = CompleteWorldImage(SqlWorldStateTestData.Image(501, 9001, items with
        { InventoryItems = items.InventoryItems.Append(SqlWorldStateTestData.Item(79999)).ToArray() }));
        await AssertBundleRefusedAsync(database, before, first.SavePairBundleAsync(second, BundlePair(fresh, initial.Second)), SqlWorldStateFailure.ItemUidIssuanceRequired);

        await using var foreignDatabase = await OwnedModernSqlAuthDatabase.CreateAsync(SqlWorldStateTestData.FirstAccount, SqlWorldStateTestData.SecondAccount);
        var foreignRepository = foreignDatabase.CreateWorldStateRepository(); await foreignRepository.InitializeSchemaAsync();
        var foreignImages = await SeedBundlePairAsync(foreignDatabase, foreignRepository);
        var foreignResult = await foreignRepository.TryAcquireBundleCustodyAsync(foreignImages.Second.AccountId);
        Assert.True(foreignResult.IsAcquired); await using var foreign = Assert.IsType<SqlWorldBundleCustody>(foreignResult.Custody);
        var foreignBefore = await foreignDatabase.SnapshotWorldBundleRowsAsync();
        await AssertBundleRefusedAsync(database, before, first.SavePairBundleAsync(foreign, pair), SqlWorldStateFailure.SessionCustodyDomainConflict);
        Assert.Equal(foreignBefore, await foreignDatabase.SnapshotWorldBundleRowsAsync());
        Assert.Equal(SqlWorldStateReadStatus.Found, (await first.ReadBundleAsync()).Status);
        Assert.Equal(SqlWorldStateReadStatus.Found, (await second.ReadBundleAsync()).Status);
        Assert.Equal(SqlWorldStateReadStatus.Found, (await foreign.ReadBundleAsync()).Status);
        Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
        Assert.Equal(foreignBefore, await foreignDatabase.SnapshotWorldBundleRowsAsync());
        AssertBundleCommit(await second.SavePairBundleAsync(first, pair), SqlWorldStateCommitStatus.Committed, 2, initial.First, initial.Second);
        await AssertBundleReadAsync(database, initial.First, 2); await AssertBundleReadAsync(database, initial.Second, 2);
        Assert.Equal(foreignBefore, await foreignDatabase.SnapshotWorldBundleRowsAsync());
    });
}
