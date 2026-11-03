using MySqlConnector;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlCompleteSecondWriteObservesFirstSixImagesAndUidSetThenRollsBackAndRetriesSameOperation() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var initial = await SeedBundlePairAsync(database, repository); var core = SqlWorldStateTestData.Exchanged();
        var first = CompleteWorldImage(core.First, 1); var second = CompleteWorldImage(core.Second, 2); var request = BundlePair(first, second);
        var before = await database.SnapshotWorldBundleRowsAsync();
        await database.InstallCompletePairSecondWriteFailureAsync(first, 2, second.AccountId);
        var error = await Assert.ThrowsAsync<MySqlException>(() => repository.SavePairBundleAsync(request).AsTask());
        Assert.Equal(1644, error.Number); Assert.Contains("owned-bundle-first-six-and-uids-observed", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("owned-bundle-first-six-and-uids-not-observed", error.Message, StringComparison.Ordinal);
        Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
        await AssertBundleReadAsync(database, initial.First, 1); await AssertBundleReadAsync(database, initial.Second, 1);
        await database.DropCompletePairSecondWriteFailureAsync();
        AssertBundleCommit(await database.CreateWorldStateRepository().SavePairBundleAsync(request), SqlWorldStateCommitStatus.Committed, 2, first, second);
        await AssertBundleReadAsync(database, first, 2); await AssertBundleReadAsync(database, second, 2);
        var settled = await database.SnapshotWorldBundleRowsAsync();
        AssertBundleCommit(await database.CreateWorldStateRepository().SavePairBundleAsync(request), SqlWorldStateCommitStatus.Replayed, 2, first, second);
        Assert.Equal(settled, await database.SnapshotWorldBundleRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualConcurrentSqlCompleteCompareAndSwapCommitsOneWholeBundleAndRetainsPeer() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var initial = await SeedBundlePairAsync(database, repository);
        var left = CompleteWorldImage(SqlWorldStateTestData.Image(501, 9001, SqlWorldStateTestData.FirstItems(), 1011), 1);
        var right = CompleteWorldImage(SqlWorldStateTestData.Image(501, 9001, SqlWorldStateTestData.FirstItems(), 1022), 2);
        var leftRequest = BundleRequest(SqlWorldStateOperationKind.Save, left, 1);
        var rightRequest = BundleRequest(SqlWorldStateOperationKind.Save, right, 1);
        var operations = new[] { database.CreateWorldStateRepository().SaveBundleAsync(leftRequest).AsTask(),
            database.CreateWorldStateRepository().SaveBundleAsync(rightRequest).AsTask() };
        var results = await Task.WhenAll(operations);
        var committed = Assert.Single(results, row => row.Status == SqlWorldStateCommitStatus.Committed);
        var refused = Assert.Single(results, row => row.Status == SqlWorldStateCommitStatus.Refused);
        Assert.Contains(refused.Failure, new[] { SqlWorldStateFailure.RevisionConflict, SqlWorldStateFailure.SessionCustodyConflict }); Assert.Empty(refused.Records.ToArray());
        var winner = ReferenceEquals(committed, results[0]) ? left : right;
        var winnerRequest = ReferenceEquals(committed, results[0]) ? leftRequest : rightRequest;
        var loserRequest = ReferenceEquals(committed, results[0]) ? rightRequest : leftRequest;
        AssertBundleCommit(committed, SqlWorldStateCommitStatus.Committed, 2, winner);
        await AssertBundleReadAsync(database, winner, 2); await AssertBundleReadAsync(database, initial.Second, 1);
        var rows = await database.SnapshotWorldBundleRowsAsync();
        Assert.Equal(3, rows.Count(row => row.StartsWith("operation|", StringComparison.Ordinal)));
        Assert.Equal(5, rows.Count(row => row.StartsWith("uid|", StringComparison.Ordinal)));
        Assert.DoesNotContain(rows, row => row.StartsWith("operation|" + Convert.ToHexString(loserRequest.OperationId.ToByteArray()) + "|", StringComparison.Ordinal));
        await AssertBundleRefusedAsync(database, rows, database.CreateWorldStateRepository().SaveBundleAsync(loserRequest), SqlWorldStateFailure.RevisionConflict);
        AssertBundleCommit(await database.CreateWorldStateRepository().SaveBundleAsync(winnerRequest), SqlWorldStateCommitStatus.Replayed, 2, winner);
        Assert.Equal(rows, await database.SnapshotWorldBundleRowsAsync());
        await AssertBundleReadAsync(database, winner, 2); Assert.Equal(rows, await database.SnapshotWorldBundleRowsAsync());
    });
}
