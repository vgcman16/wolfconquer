using MySqlConnector;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualBundleCustodyCancellationAndDisposalAllowSafeReacquisition() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var images = await SeedBundlePairAsync(database, repository);
        var updated = CompleteWorldImage(SqlWorldStateTestData.Image(501, 9001, SqlWorldStateTestData.FirstItems(), 1007, 101), 1);
        var request = BundleRequest(SqlWorldStateOperationKind.Save, updated, 1);
        var descriptor = request.CopyDescriptorBytes(); var digest = request.CopyDescriptorHash();
        var acquired = await repository.TryAcquireBundleCustodyAsync(images.First.AccountId);
        Assert.True(acquired.IsAcquired); var owner = Assert.IsType<SqlWorldBundleCustody>(acquired.Custody);
        try
        {
            var before = await database.SnapshotWorldBundleRowsAsync();
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await owner.ReadBundleAsync(cancelled.Token));
            SqlWorldStateBundleCommitResult? cancelledSave = null;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { cancelledSave = await owner.SaveBundleAsync(request, cancelled.Token); });
            Assert.Null(cancelledSave); Assert.Equal(descriptor, request.CopyDescriptorBytes()); Assert.Equal(digest, request.CopyDescriptorHash());
            Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
            var stillCurrent = await owner.ReadBundleAsync();
            Assert.Equal(SqlWorldStateReadStatus.Found, stillCurrent.Status);
            AssertBundleImage(images.First, Assert.IsType<SqlWorldStateBundleRecord>(stillCurrent.Record).Image);
            var busy = await database.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(images.First.AccountId);
            Assert.False(busy.IsAcquired); Assert.Null(busy.Custody); Assert.Equal(SqlWorldStateFailure.SessionCustodyConflict, busy.Failure);
            Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
            AssertBundleCommit(await owner.SaveBundleAsync(request), SqlWorldStateCommitStatus.Committed, 2, updated);
            await AssertBundleReadAsync(database, updated, 2); await AssertBundleReadAsync(database, images.Second, 1);
        }
        finally { await owner.DisposeAsync(); }
        var retired = await owner.ReadBundleAsync();
        Assert.Equal(SqlWorldStateReadStatus.Invalid, retired.Status); Assert.Equal(SqlWorldStateFailure.SessionCustodyRetired, retired.Failure);
        await using var next = await ReacquireClosedCustodyAsync(database.CreateWorldStateRepository(), images.First.AccountId);
        var read = await next.ReadBundleAsync(); Assert.Equal(SqlWorldStateReadStatus.Found, read.Status);
        var reopened = Assert.IsType<SqlWorldStateBundleRecord>(read.Record);
        Assert.Equal(2, reopened.Revision); AssertBundleImage(updated, reopened.Image);
        await AssertBundleReadAsync(database, images.Second, 1);
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualOwnedServerConnectionLossRetiresBundleCustodyWithoutWriting() => RunSqlWorldStateAsync(async (database, repository) =>
    {
        var images = await SeedBundlePairAsync(database, repository);
        var acquired = await repository.TryAcquireBundleCustodyAsync(images.First.AccountId);
        Assert.True(acquired.IsAcquired); await using var owner = Assert.IsType<SqlWorldBundleCustody>(acquired.Custody);
        var before = await database.SnapshotWorldBundleRowsAsync();
        await database.KillOwnedWorldCustodyConnectionAsync(images.First.AccountId);
        // The provider may already report closed, or discover loss during I/O;
        // either outcome must retire ownership before later use.
        SqlWorldStateBundleReadResult? first = null;
        var failure = await Record.ExceptionAsync(async () => { first = await owner.ReadBundleAsync(); });
        if (failure is null)
        {
            Assert.NotNull(first); Assert.Equal(SqlWorldStateReadStatus.Invalid, first.Status);
            Assert.Equal(SqlWorldStateFailure.SessionCustodyRetired, first.Failure); Assert.Null(first.Record);
        }
        else if (failure is AggregateException aggregate)
            Assert.Contains(aggregate.Flatten().InnerExceptions, error => error is MySqlException);
        else Assert.IsAssignableFrom<MySqlException>(failure);
        var retired = await owner.ReadBundleAsync();
        Assert.Equal(SqlWorldStateReadStatus.Invalid, retired.Status); Assert.Equal(SqlWorldStateFailure.SessionCustodyRetired, retired.Failure);
        var save = await owner.SaveBundleAsync(BundleRequest(SqlWorldStateOperationKind.Save, images.First, 1));
        Assert.Equal(SqlWorldStateCommitStatus.Refused, save.Status); Assert.Equal(SqlWorldStateFailure.SessionCustodyRetired, save.Failure);
        Assert.Empty(save.Records.ToArray()); Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
        await using var next = await ReacquireClosedCustodyAsync(database.CreateWorldStateRepository(), images.First.AccountId);
        await AssertBundleReadAsync(database, images.First, 1); await AssertBundleReadAsync(database, images.Second, 1);
        Assert.Equal(before, await database.SnapshotWorldBundleRowsAsync());
    });
}
