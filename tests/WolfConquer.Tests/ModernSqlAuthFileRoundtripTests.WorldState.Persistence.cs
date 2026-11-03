using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlWorldCreateSingleSaveColdReadAndDurableReplayPreserveExactImages() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var initial = SqlWorldStateTestData.Initial().First;
        Assert.NotEqual(initial.AccountId, (long)initial.CharacterUid);
        var create = WorldRequest(SqlWorldStateOperationKind.Create, initial, 0);
        AssertWorldCommit(await repository.CreateAsync(create), SqlWorldStateCommitStatus.Committed, 1, initial);
        await AssertWorldReadAsync(database, initial, 1);
        var once = await database.SnapshotWorldStateRowsAsync();
        AssertWorldCommit(await database.CreateWorldStateRepository().CreateAsync(create), SqlWorldStateCommitStatus.Replayed, 1, initial);
        Assert.Equal(once, await database.SnapshotWorldStateRowsAsync());

        var updated = SqlWorldStateTestData.Image(initial.AccountId, initial.CharacterUid, SqlWorldStateTestData.FirstItems(), 1007, 101);
        var save = WorldRequest(SqlWorldStateOperationKind.Save, updated, 1);
        AssertWorldCommit(await repository.SaveAsync(save), SqlWorldStateCommitStatus.Committed, 2, updated);
        await AssertWorldReadAsync(database, updated, 2);
        var settled = await database.SnapshotWorldStateRowsAsync();
        AssertWorldCommit(await database.CreateWorldStateRepository().SaveAsync(save), SqlWorldStateCommitStatus.Replayed, 2, updated);
        // A historical replay must neither overwrite the newer state nor invent a new revision.
        AssertWorldCommit(await database.CreateWorldStateRepository().CreateAsync(create), SqlWorldStateCommitStatus.Replayed, 1, initial);
        Assert.Equal(settled, await database.SnapshotWorldStateRowsAsync());
        await AssertWorldReadAsync(database, updated, 2);
        var different = SqlWorldStateTestData.Image(initial.AccountId, initial.CharacterUid, SqlWorldStateTestData.FirstItems(), 1008, 101);
        await AssertWorldRefusedAsync(database, settled,
            repository.SaveAsync(WorldRequest(SqlWorldStateOperationKind.Save, different, 1, save.OperationId)), SqlWorldStateFailure.OperationConflict);
        AssertWorldCommit(await database.CreateWorldStateRepository().SaveAsync(save), SqlWorldStateCommitStatus.Replayed, 2, updated);
        Assert.Equal(settled, await database.SnapshotWorldStateRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlWorldPairTransfersRootChildAndCurrencyImagesTogetherThenColdReplaysOnce() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        await SeedWorldPairAsync(database, repository);
        var exchanged = SqlWorldStateTestData.Exchanged();
        var request = WorldPair(exchanged.First, exchanged.Second);
        AssertWorldCommit(await repository.SavePairAsync(request), SqlWorldStateCommitStatus.Committed, 2, exchanged.First, exchanged.Second);
        await AssertWorldReadAsync(database, exchanged.First, 2); await AssertWorldReadAsync(database, exchanged.Second, 2);
        var rows = await database.SnapshotWorldStateRowsAsync();
        Assert.Contains($"uid|{SqlWorldStateTestData.SashUid}|{SqlWorldStateTestData.SecondAccount}", rows);
        Assert.Contains($"uid|{SqlWorldStateTestData.ChildUid}|{SqlWorldStateTestData.SecondAccount}", rows);
        Assert.Contains($"uid|{SqlWorldStateTestData.OfferedUid}|{SqlWorldStateTestData.FirstAccount}", rows);
        Assert.Equal(5, rows.Count(row => row.StartsWith("uid|", StringComparison.Ordinal)));
        AssertWorldCommit(await database.CreateWorldStateRepository().SavePairAsync(request), SqlWorldStateCommitStatus.Replayed, 2, exchanged.First, exchanged.Second);
        Assert.Equal(rows, await database.SnapshotWorldStateRowsAsync());
        await AssertWorldReadAsync(database, exchanged.First, 2); await AssertWorldReadAsync(database, exchanged.Second, 2);
    });
}
