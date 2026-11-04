using WolfConquer.Core.Accounts;

namespace WolfConquer.Tests;

public sealed partial class SqlAccountWorldHandoffCleanupTests
{
    // These witnesses exercise disposal only. They do not mint SQL/auth/native
    // owners, simulate successful provider replies or establish gameplay authority.
    [Fact]
    public async Task CleanupRetriesOnlyFailedResourcesAndAttemptsAllInOriginalOrder()
    {
        var events = new List<string>();
        var firstError = new IOException("firstClose");
        var secondError = new IOException("secondClose");
        var retryError = new IOException("secondRetry");
        var first = new CleanupWitness("first", events, firstError);
        var second = new CleanupWitness("second", events, secondError, retryError);
        var settled = new CleanupWitness("settled", events);
        var owner = new SqlAccountWorldHandoffCleanupOwner([first, second, settled]);

        var initial = await Assert.ThrowsAsync<AggregateException>(() => owner.DisposeAsync().AsTask());
        Assert.Collection(initial.InnerExceptions,
            error => Assert.Same(firstError, error), error => Assert.Same(secondError, error));
        Assert.True(owner.HasPendingCleanup);
        Assert.Equal(new[] { "first:1", "second:1", "settled:1" }, events);

        Assert.Same(retryError, await Assert.ThrowsAsync<IOException>(() => owner.DisposeAsync().AsTask()));
        Assert.True(owner.HasPendingCleanup);
        Assert.Equal(new[] { "first:1", "second:1", "settled:1", "first:2", "second:2" }, events);

        await owner.DisposeAsync();
        Assert.False(owner.HasPendingCleanup);
        await owner.DisposeAsync();
        Assert.Equal(new[] { "first:1", "second:1", "settled:1", "first:2", "second:2", "second:3" }, events);
        Assert.Equal(2, first.Calls); Assert.Equal(3, second.Calls); Assert.Equal(1, settled.Calls);
    }

    [Fact]
    public async Task SingleCleanupFailureRetainsOriginalExceptionStackAndResource()
    {
        var failure = new IOException("physicalClose");
        var witness = new CleanupWitness("physical", [], failure);
        var owner = new SqlAccountWorldHandoffCleanupOwner([witness]);
        var caught = await Assert.ThrowsAsync<IOException>(() => owner.DisposeAsync().AsTask());
        Assert.Same(failure, caught);
        Assert.Contains(nameof(CleanupWitness.ThrowAtDisposalSite), caught.StackTrace ?? string.Empty);
        Assert.True(owner.HasPendingCleanup);
        await owner.DisposeAsync();
        Assert.False(owner.HasPendingCleanup);
        Assert.Equal(2, witness.Calls);
    }

    [Fact]
    public async Task ConcurrentDisposalWaitsThenRetriesTheStillOwnedFailure()
    {
        var failure = new IOException("blockedClose");
        var witness = new BlockingCleanupWitness(failure);
        var owner = new SqlAccountWorldHandoffCleanupOwner([witness]);
        Task first = owner.DisposeAsync().AsTask();
        await witness.Entered.Task;
        Task second = owner.DisposeAsync().AsTask();
        bool secondCompletedBeforeRelease = second.IsCompleted;
        int callsBeforeRelease = witness.Calls;
        bool pendingBeforeRelease = owner.HasPendingCleanup;
        witness.Release.TrySetResult(true);
        Exception?[] outcomes = await Task.WhenAll(ObserveAsync(first), ObserveAsync(second));

        Assert.False(secondCompletedBeforeRelease);
        Assert.Equal(1, callsBeforeRelease);
        Assert.True(pendingBeforeRelease);
        Assert.Same(failure, outcomes[0]); Assert.Null(outcomes[1]);
        Assert.False(owner.HasPendingCleanup);
        await owner.DisposeAsync();
        Assert.Equal(2, witness.Calls);
    }

    [Theory]
    [InlineData(false, false, typeof(SqlAccountWorldHandoffCleanupException))]
    [InlineData(true, false, typeof(SqlAccountWorldHandoffCommitOutcomeUnknownException))]
    [InlineData(true, true, typeof(SqlAccountWorldHandoffCommittedCleanupException))]
    public async Task PendingCleanupKeepsCommitKindAndOriginalPrimary(bool attempted, bool committed, Type expected)
    {
        var primary = CapturePrimaryFailure();
        string? originalStack = primary.StackTrace;
        var closeFailure = new IOException("closeRetry");
        var witness = new CleanupWitness("close", [], closeFailure);
        var failure = SqlAccountWorldHandoffException.WithPendingCleanup(primary, [witness], attempted, committed);
        Assert.Equal(expected, failure.GetType());
        Assert.Same(primary, failure.InnerException);
        Assert.True(failure.HasPendingCleanup);
        Assert.Same(closeFailure, await Assert.ThrowsAsync<IOException>(() => failure.DisposeAsync().AsTask()));
        Assert.True(failure.HasPendingCleanup);
        Assert.Same(primary, failure.InnerException);
        Assert.Equal(originalStack, primary.StackTrace);
        await failure.DisposeAsync();
        Assert.False(failure.HasPendingCleanup);
        Assert.Same(primary, failure.InnerException);
        Assert.Equal(originalStack, primary.StackTrace);
    }

    [Fact]
    public async Task ExistingKnownAndUnknownConstructorsRetainTheirMessagesWithoutCleanup()
    {
        var primary = CapturePrimaryFailure();
        SqlAccountWorldHandoffException[] failures =
        [new SqlAccountWorldHandoffCommitOutcomeUnknownException(primary),
         new SqlAccountWorldHandoffCommittedCleanupException(primary)];
        Assert.Equal("sqlAccountWorldHandoffCommitOutcomeUnknown", failures[0].Message);
        Assert.Equal("sqlAccountWorldHandoffCommittedCleanupFailed", failures[1].Message);
        foreach (var failure in failures)
        {
            Assert.False(failure.HasPendingCleanup);
            Assert.Same(primary, failure.InnerException);
            await failure.DisposeAsync();
            Assert.False(failure.HasPendingCleanup);
        }
    }
}
