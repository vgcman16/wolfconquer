using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualBundleCustodyRequiresExistingValidatedCompleteImage() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var images = SqlWorldStateTestData.Initial();
        var complete = CompleteWorldImage(images.Second);
        AssertWorldCommit(await repository.CreateAsync(WorldRequest(SqlWorldStateOperationKind.Create, images.First, 0)), SqlWorldStateCommitStatus.Committed, 1, images.First);
        AssertBundleCommit(await repository.CreateBundleAsync(BundleRequest(SqlWorldStateOperationKind.Create, complete, 0)), SqlWorldStateCommitStatus.Committed, 1, complete);
        var before = await database.SnapshotWorldBundleRowsAsync();
        foreach (long invalidAccount in new long[] { 0, -1 })
        {
            var invalid = await repository.TryAcquireBundleCustodyAsync(invalidAccount);
            Assert.False(invalid.IsAcquired); Assert.Null(invalid.Custody); Assert.Equal(SqlWorldStateFailure.InvalidAccountId, invalid.Failure);
            Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
        }
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel(); SqlWorldBundleCustodyAcquireResult? cancelledResult = null;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { cancelledResult = await repository.TryAcquireBundleCustodyAsync(complete.AccountId, cancelled.Token); });
            Assert.Null(cancelledResult); Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
        }
        var wrongFormat = await repository.TryAcquireBundleCustodyAsync(images.First.AccountId);
        Assert.False(wrongFormat.IsAcquired); Assert.Null(wrongFormat.Custody); Assert.Equal(SqlWorldStateFailure.StateFormatConflict, wrongFormat.Failure);
        var missing = await repository.TryAcquireBundleCustodyAsync(503);
        Assert.False(missing.IsAcquired); Assert.Null(missing.Custody); Assert.Equal(SqlWorldStateFailure.MissingState, missing.Failure);
        var valid = await database.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(complete.AccountId);
        Assert.True(valid.IsAcquired); await using var owner = Assert.IsType<SqlWorldBundleCustody>(valid.Custody);
        var read = await owner.ReadBundleAsync(); Assert.Equal(SqlWorldStateReadStatus.Found, read.Status);
        AssertBundleImage(complete, Assert.IsType<SqlWorldStateBundleRecord>(read.Record).Image);
        Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
    });
}
