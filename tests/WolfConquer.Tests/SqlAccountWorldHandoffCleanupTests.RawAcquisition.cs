using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class SqlAccountWorldHandoffCleanupTests
{
    // Failure-only disposal witnesses. These tests do not create authentication,
    // SQL/world custody or successful provider/native replies.
    [Fact]
    public async Task RawAcquisitionCleanupRetainsPrimaryAndExactFailedResourceAcrossRetries()
    {
        var primary = CapturePrimaryFailure();
        string? primaryStack = primary.StackTrace;
        var initialClose = new IOException("initialRawClose");
        var retryClose = new IOException("rawCloseRetry");
        var witness = new CleanupWitness("raw", [], retryClose);
        var failure = new SqlResourceCleanupException(primary, initialClose, witness);
        ISqlPendingCleanup common = failure;
        var original = Assert.IsType<AggregateException>(failure.InnerException);
        Assert.Collection(original.InnerExceptions,
            error => Assert.Same(primary, error), error => Assert.Same(initialClose, error));
        Assert.True(common.HasPendingCleanup);

        Assert.Same(retryClose, await Assert.ThrowsAsync<IOException>(() => common.DisposeAsync().AsTask()));
        Assert.True(failure.HasPendingCleanup); Assert.Equal(1, witness.Calls);
        Assert.Same(original, failure.InnerException); Assert.Equal(primaryStack, primary.StackTrace);
        await failure.DisposeAsync();
        Assert.False(common.HasPendingCleanup); Assert.Equal(2, witness.Calls);
        await common.DisposeAsync();
        Assert.Equal(2, witness.Calls);
        Assert.Same(original, failure.InnerException); Assert.Equal(primaryStack, primary.StackTrace);
    }

    [Fact]
    public async Task RawRefusalCleanupKeepsSoleCloseFailureWithoutInventingAnOperationFailure()
    {
        var close = new IOException("rawRefusalClose");
        var witness = new CleanupWitness("raw", []);
        var failure = new SqlResourceCleanupException(null, close, witness);
        Assert.Same(close, failure.InnerException); Assert.True(failure.HasPendingCleanup);
        await failure.DisposeAsync();
        Assert.False(failure.HasPendingCleanup); Assert.Equal(1, witness.Calls);
        Assert.Same(close, failure.InnerException);
    }

    [Theory]
    [InlineData(false, false, typeof(SqlAccountWorldHandoffCleanupException))]
    [InlineData(true, false, typeof(SqlAccountWorldHandoffCommitOutcomeUnknownException))]
    [InlineData(true, true, typeof(SqlAccountWorldHandoffCommittedCleanupException))]
    public async Task HandoffRetainsNestedRawCleanupWhenEveryLocalCloseSucceeded(
        bool attempted, bool committed, Type expectedType)
    {
        var witness = new CleanupWitness("raw", []);
        var raw = new SqlResourceCleanupException(CapturePrimaryFailure(), new IOException("rawClose"), witness);
        var primary = new InvalidOperationException("handoffAcquireFailed", raw);
        var retained = Assert.IsAssignableFrom<SqlAccountWorldHandoffException>(
            SqlAccountWorldHandoffCleanupTransfer.RetainCleanup(primary, null, attempted, committed));

        Assert.Equal(expectedType, retained.GetType()); Assert.Same(primary, retained.InnerException);
        Assert.True(retained.HasPendingCleanup); Assert.True(raw.HasPendingCleanup);
        await retained.DisposeAsync();
        Assert.False(retained.HasPendingCleanup); Assert.False(raw.HasPendingCleanup);
        Assert.Equal(1, witness.Calls); Assert.Same(primary, retained.InnerException);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IssueTransferFindsSharedRawCleanupOnceAndKeepsNestedCommitKind(bool committed)
    {
        var retryClose = new IOException("sharedRawCloseRetry");
        var witness = new CleanupWitness("raw", [], retryClose);
        var raw = new SqlResourceCleanupException(null, new IOException("initialRawClose"), witness);
        Exception commitFailure = committed
            ? new SqlAccountWorldHandoffCommittedCleanupException(CapturePrimaryFailure())
            : new SqlAccountWorldHandoffCommitOutcomeUnknownException(CapturePrimaryFailure());
        var original = new AggregateException(commitFailure, raw, raw);
        var retained = Assert.IsAssignableFrom<SqlAccountWorldHandoffException>(
            SqlAccountWorldHandoffCleanupTransfer.RetainIssueCleanup(original, null, committed: false));

        Assert.Equal(commitFailure.GetType(), retained.GetType()); Assert.Same(original, retained.InnerException);
        Assert.Same(retryClose, await Assert.ThrowsAsync<IOException>(() => retained.DisposeAsync().AsTask()));
        Assert.Equal(1, witness.Calls); Assert.True(retained.HasPendingCleanup); Assert.True(raw.HasPendingCleanup);
        await retained.DisposeAsync();
        Assert.Equal(2, witness.Calls); Assert.False(retained.HasPendingCleanup); Assert.False(raw.HasPendingCleanup);
        Assert.Same(original, retained.InnerException);
    }
}
