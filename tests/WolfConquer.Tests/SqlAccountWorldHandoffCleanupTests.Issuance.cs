using WolfConquer.Core.Accounts;

namespace WolfConquer.Tests;

public sealed partial class SqlAccountWorldHandoffCleanupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IssueCleanupKeepsNestedCommitKindAndRetainsItsRealCleanupHandle(bool committed)
    {
        var primary = CapturePrimaryFailure();
        var witness = new CleanupWitness("inner", []);
        var inner = SqlAccountWorldHandoffException.WithPendingCleanup(primary, [witness], true, committed);
        var original = new AggregateException(new IOException("outsideIssueFailure"), new InvalidOperationException("wrapped", inner));
        var transferred = Assert.IsAssignableFrom<SqlAccountWorldHandoffException>(
            SqlAccountWorldHandoffCleanupTransfer.RetainIssueCleanup(original, null, committed: false));

        Assert.Equal(inner.GetType(), transferred.GetType());
        Assert.Same(original, transferred.InnerException);
        Assert.True(transferred.HasPendingCleanup); Assert.True(inner.HasPendingCleanup);
        await transferred.DisposeAsync();
        Assert.False(transferred.HasPendingCleanup); Assert.False(inner.HasPendingCleanup);
        Assert.Same(original, transferred.InnerException); Assert.Same(primary, inner.InnerException);
        Assert.Equal(1, witness.Calls);
    }

    [Fact]
    public async Task IssueTransferDoesNotDuplicateAnOwnedCleanupReferencedBySeveralFailurePaths()
    {
        var closeFailure = new IOException("innerClose");
        var witness = new CleanupWitness("inner", [], closeFailure);
        var inner = SqlAccountWorldHandoffException.WithPendingCleanup(CapturePrimaryFailure(), [witness], true, false);
        var original = new AggregateException(inner, inner);
        IAsyncDisposable[] additional = [inner, inner];
        var transferred = Assert.IsType<SqlAccountWorldHandoffCommitOutcomeUnknownException>(
            SqlAccountWorldHandoffCleanupTransfer.RetainIssueCleanup(original, additional, committed: false));

        Assert.Same(closeFailure, await Assert.ThrowsAsync<IOException>(() => transferred.DisposeAsync().AsTask()));
        Assert.Equal(1, witness.Calls);
        Assert.True(transferred.HasPendingCleanup); Assert.True(inner.HasPendingCleanup);
        await transferred.DisposeAsync();
        Assert.False(transferred.HasPendingCleanup); Assert.False(inner.HasPendingCleanup);
        Assert.Equal(2, witness.Calls);
        Assert.Same(original, transferred.InnerException);
    }

    [Fact]
    public void IssueFailureWithNoPendingCleanupKeepsTheOriginalFailureObject()
    {
        var primary = CapturePrimaryFailure();
        Exception[] failures = [primary, new SqlAccountWorldHandoffCommitOutcomeUnknownException(primary),
            new SqlAccountWorldHandoffCommittedCleanupException(primary)];
        foreach (var failure in failures)
            Assert.Same(failure, SqlAccountWorldHandoffCleanupTransfer.RetainIssueCleanup(failure, null, committed: false));
    }
}
