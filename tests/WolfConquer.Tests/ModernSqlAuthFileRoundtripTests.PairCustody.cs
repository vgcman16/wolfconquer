using MySqlConnector;
using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualTwoSqlCustodiesAtomicallyExchangeCompleteBundlesAndReplay() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var initial = await SeedBundlePairAsync(database, repository);
        var acquired1 = await repository.TryAcquireBundleCustodyAsync(initial.First.AccountId);
        Assert.True(acquired1.IsAcquired); await using var firstOwner = Assert.IsType<SqlWorldBundleCustody>(acquired1.Custody);
        var acquired2 = await database.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(initial.Second.AccountId);
        Assert.True(acquired2.IsAcquired); await using var secondOwner = Assert.IsType<SqlWorldBundleCustody>(acquired2.Custody);
        var core = SqlWorldStateTestData.Exchanged(); var first = CompleteWorldImage(core.First, 1); var second = CompleteWorldImage(core.Second, 2);
        var request = BundlePair(first, second); var descriptor = request.CopyDescriptorBytes(); var digest = request.CopyDescriptorHash();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var forward = firstOwner.SavePairBundleAsync(secondOwner, request, deadline.Token).AsTask();
        var reverse = secondOwner.SavePairBundleAsync(firstOwner, request, deadline.Token).AsTask();
        var results = await Task.WhenAll(forward, reverse);
        AssertBundleCommit(Assert.Single(results, result => result.Status == SqlWorldStateCommitStatus.Committed), SqlWorldStateCommitStatus.Committed, 2, first, second);
        AssertBundleCommit(Assert.Single(results, result => result.Status == SqlWorldStateCommitStatus.Replayed), SqlWorldStateCommitStatus.Replayed, 2, first, second);
        await AssertBundleReadAsync(database, first, 2); await AssertBundleReadAsync(database, second, 2);
        var rows = await database.SnapshotWorldBundleRowsAsync();
        Assert.Contains("uid|71001|502", rows); Assert.Contains("uid|71002|502", rows); Assert.Contains("uid|72001|501", rows);
        Assert.Equal(5, rows.Count(row => row.StartsWith("uid|", StringComparison.Ordinal)));
        string operation = "operation|" + Convert.ToHexString(request.OperationId.ToByteArray()) + "|";
        Assert.Equal(operation + Convert.ToHexString(digest) + "|" + Convert.ToHexString(descriptor), Assert.Single(rows, row => row.StartsWith(operation, StringComparison.Ordinal)));
        Assert.Equal(3, rows.Count(row => row.StartsWith("operation|", StringComparison.Ordinal)));
        AssertBundleCommit(await firstOwner.SavePairBundleAsync(secondOwner, request), SqlWorldStateCommitStatus.Replayed, 2, first, second);
        Assert.Equal(rows, await database.SnapshotWorldBundleRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualTwoSqlCustodiesRollbackObservedFirstWriteAndRetrySamePair() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var initial = await SeedBundlePairAsync(database, repository);
        var acquired1 = await repository.TryAcquireBundleCustodyAsync(initial.First.AccountId);
        Assert.True(acquired1.IsAcquired); await using var firstOwner = Assert.IsType<SqlWorldBundleCustody>(acquired1.Custody);
        var acquired2 = await database.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(initial.Second.AccountId);
        Assert.True(acquired2.IsAcquired); await using var secondOwner = Assert.IsType<SqlWorldBundleCustody>(acquired2.Custody);
        var exchanged = SqlWorldStateTestData.Exchanged(); var first = CompleteWorldImage(exchanged.First, 1); var second = CompleteWorldImage(exchanged.Second, 2);
        var request = BundlePair(first, second); var before = await database.SnapshotWorldBundleRowsAsync();
        await database.InstallCompletePairSecondWriteFailureAsync(first, 2, second.AccountId);
        var failure = await Assert.ThrowsAsync<MySqlException>(() => firstOwner.SavePairBundleAsync(secondOwner, request).AsTask());
        Assert.Equal(1644, failure.Number); Assert.Contains("owned-bundle-first-six-and-uids-observed", failure.Message, StringComparison.Ordinal);
        Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
        await AssertBundleReadAsync(database, initial.First, 1); await AssertBundleReadAsync(database, initial.Second, 1);
        await database.DropCompletePairSecondWriteFailureAsync();
        await firstOwner.DisposeAsync(); await secondOwner.DisposeAsync();
        await using var retry1 = await ReacquireClosedCustodyAsync(database.CreateWorldStateRepository(), initial.First.AccountId);
        await using var retry2 = await ReacquireClosedCustodyAsync(database.CreateWorldStateRepository(), initial.Second.AccountId);
        AssertBundleCommit(await retry1.SavePairBundleAsync(retry2, request), SqlWorldStateCommitStatus.Committed, 2, first, second);
        await AssertBundleReadAsync(database, first, 2); await AssertBundleReadAsync(database, second, 2);
        var settled = await database.SnapshotWorldBundleRowsAsync();
        AssertBundleCommit(await retry2.SavePairBundleAsync(retry1, request), SqlWorldStateCommitStatus.Replayed, 2, first, second);
        Assert.Equal(settled, await database.SnapshotWorldBundleRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualPairHistoricalReplayAfterUidRemovalCannotReissueUid() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var initial = await SeedBundlePairAsync(database, repository);
        var acquired1 = await repository.TryAcquireBundleCustodyAsync(initial.First.AccountId);
        Assert.True(acquired1.IsAcquired); await using var firstOwner = Assert.IsType<SqlWorldBundleCustody>(acquired1.Custody);
        var acquired2 = await database.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(initial.Second.AccountId);
        Assert.True(acquired2.IsAcquired); await using var secondOwner = Assert.IsType<SqlWorldBundleCustody>(acquired2.Custody);
        var exchanged = SqlWorldStateTestData.Exchanged(); var first = CompleteWorldImage(exchanged.First, 1); var second = CompleteWorldImage(exchanged.Second, 2);
        var pair = BundlePair(first, second);
        AssertBundleCommit(await firstOwner.SavePairBundleAsync(secondOwner, pair), SqlWorldStateCommitStatus.Committed, 2, first, second);
        var reduced = CompleteWorldImage(SqlWorldStateTestData.Image(501, 9001, PlayerItemSnapshot.Empty with
        { InventoryItems = [SqlWorldStateTestData.Item(SqlWorldStateTestData.FirstKept)] }, 1025, 103), 3);
        AssertBundleCommit(await firstOwner.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Save, reduced, 2)), SqlWorldStateCommitStatus.Committed, 3, reduced);
        var before = await database.SnapshotWorldBundleRowsAsync(); Assert.DoesNotContain("uid|72001|501", before);
        AssertBundleCommit(await secondOwner.SavePairBundleAsync(firstOwner, pair), SqlWorldStateCommitStatus.Replayed, 2, first, second);
        Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
        await AssertBundleRefusedAsync(database, before, firstOwner.SavePairBundleAsync(secondOwner, BundlePair(first, second, 3, 2)), SqlWorldStateFailure.ItemUidIssuanceRequired);
        await AssertBundleReadAsync(database, reduced, 3); await AssertBundleReadAsync(database, second, 2);
        Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
    });
}
