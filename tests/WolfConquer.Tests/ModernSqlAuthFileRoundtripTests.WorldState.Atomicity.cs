using MySqlConnector;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlWorldSecondWriteTriggerObservesFirstImageThenRollsBackWholePairAndOperation() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        await SeedWorldPairAsync(database, repository);
        var initial = SqlWorldStateTestData.Initial(); var exchanged = SqlWorldStateTestData.Exchanged();
        var request = WorldPair(exchanged.First, exchanged.Second);
        var before = await database.SnapshotWorldStateRowsAsync();
        await database.InstallWorldPairSecondWriteFailureAsync(exchanged.First, 2, exchanged.Second.AccountId);
        var failure = await Assert.ThrowsAsync<MySqlException>(() => repository.SavePairAsync(request).AsTask());
        Assert.Equal(1644, failure.Number);
        Assert.Contains("owned-world-first-image-observed", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("owned-world-first-image-not-observed", failure.Message, StringComparison.Ordinal);
        Assert.Equal(before, await database.SnapshotWorldStateRowsAsync());
        await AssertWorldReadAsync(database, initial.First, 1); await AssertWorldReadAsync(database, initial.Second, 1);

        await database.DropWorldPairSecondWriteFailureAsync();
        AssertWorldCommit(await database.CreateWorldStateRepository().SavePairAsync(request),
            SqlWorldStateCommitStatus.Committed, 2, exchanged.First, exchanged.Second);
        await AssertWorldReadAsync(database, exchanged.First, 2); await AssertWorldReadAsync(database, exchanged.Second, 2);
        var settled = await database.SnapshotWorldStateRowsAsync();
        AssertWorldCommit(await database.CreateWorldStateRepository().SavePairAsync(request),
            SqlWorldStateCommitStatus.Replayed, 2, exchanged.First, exchanged.Second);
        Assert.Equal(settled, await database.SnapshotWorldStateRowsAsync());
        await AssertWorldRefusedAsync(database, settled,
            repository.SavePairAsync(WorldPair(initial.First, initial.Second, id: request.OperationId)), SqlWorldStateFailure.OperationConflict);
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualConcurrentSqlWorldCompareAndSwapCommitsOneRevisionAndPreservesOtherAccount() => RunSqlWorldStateAsync(async (database, _) =>
    {
        var seeder = database.CreateWorldStateRepository(); await SeedWorldPairAsync(database, seeder);
        var initial = SqlWorldStateTestData.Initial();
        var left = SqlWorldStateTestData.Image(initial.First.AccountId, initial.First.CharacterUid, SqlWorldStateTestData.FirstItems(), 1011, 100);
        var right = SqlWorldStateTestData.Image(initial.First.AccountId, initial.First.CharacterUid, SqlWorldStateTestData.FirstItems(), 1022, 100);
        var operations = new[]
        {
            database.CreateWorldStateRepository().SaveAsync(WorldRequest(SqlWorldStateOperationKind.Save, left, 1)).AsTask(),
            database.CreateWorldStateRepository().SaveAsync(WorldRequest(SqlWorldStateOperationKind.Save, right, 1)).AsTask()
        };
        var results = await Task.WhenAll(operations); // Two actual provider connections; no local substitute lock.
        var committed = Assert.Single(results, row => row.Status == SqlWorldStateCommitStatus.Committed);
        var refused = Assert.Single(results, row => row.Status == SqlWorldStateCommitStatus.Refused);
        Assert.Equal(SqlWorldStateFailure.RevisionConflict, refused.Failure); Assert.Empty(refused.Records.ToArray());
        var winner = ReferenceEquals(committed, results[0]) ? left : right;
        AssertWorldCommit(committed, SqlWorldStateCommitStatus.Committed, 2, winner);
        await AssertWorldReadAsync(database, winner, 2); await AssertWorldReadAsync(database, initial.Second, 1);
        var rows = await database.SnapshotWorldStateRowsAsync();
        Assert.Equal(3, rows.Count(row => row.StartsWith("operation|", StringComparison.Ordinal)));
        Assert.Equal(5, rows.Count(row => row.StartsWith("uid|", StringComparison.Ordinal)));
        var untouched = rows.ToArray();
        await AssertWorldReadAsync(database, winner, 2); await AssertWorldReadAsync(database, initial.Second, 1);
        Assert.Equal(untouched, await database.SnapshotWorldStateRowsAsync());
    });
}
