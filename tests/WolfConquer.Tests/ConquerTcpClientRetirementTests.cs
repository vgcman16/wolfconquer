using System.Runtime.ExceptionServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

public sealed partial class ConquerTcpClientRetirementTests
{
    [Fact]
    public async Task EofFailureStillCompletesReaderAndRetryClosesOnlyThePendingConnection()
    {
        var events = new List<string>();
        var firstFailure = new IOException("ownedCloseFailure");
        int closes = 0;
        var connection = new ConquerTcpConnection(null, null);
        connection.RegisterTransportCloseCleanup(() =>
        {
            events.Add("close:" + ++closes);
            if (closes == 1) ThrowAtCleanupSite(firstFailure);
            return Task.CompletedTask;
        });
        var reader = new CompletionWitnessReader(events);
        // Failure-only disposal witnesses, never a SQL/native or gameplay owner.
        var retirement = new ConquerTcpClientRetirement(connection, reader);
        var observed = await retirement.TryCompleteAsync();
        Assert.Same(firstFailure, observed);
        Assert.Contains(nameof(ThrowAtCleanupSite), observed!.StackTrace ?? string.Empty);
        Assert.True(connection.HasObservedTransportClose);
        Assert.True(retirement.HasPendingCleanup);
        Assert.Equal(new[] { "close:1", "reader:1" }, events);

        Assert.Null(await retirement.TryCompleteAsync());
        Assert.False(retirement.HasPendingCleanup);
        Assert.Null(await retirement.TryCompleteAsync());
        Assert.Equal(new[] { "close:1", "reader:1", "close:2" }, events);
        Assert.Equal(1, reader.Completions);
    }

    [Fact]
    public async Task ConnectionAndReaderFailuresKeepTheirActualOrderAndBothCanRetry()
    {
        var events = new List<string>();
        var closeFailure = new IOException("ownedClose");
        var readerFailure = new InvalidOperationException("ownedReader");
        int closes = 0;
        var connection = new ConquerTcpConnection(null, null);
        connection.RegisterTransportCloseCleanup(() =>
        {
            events.Add("close:" + ++closes);
            if (closes == 1) ThrowAtCleanupSite(closeFailure);
            return Task.CompletedTask;
        });
        var reader = new CompletionWitnessReader(events, readerFailure);
        var retirement = new ConquerTcpClientRetirement(connection, reader);
        var observed = Assert.IsType<AggregateException>(await retirement.TryCompleteAsync());
        Assert.Collection(observed.InnerExceptions,
            error => Assert.Same(closeFailure, error), error => Assert.Same(readerFailure, error));
        Assert.True(retirement.HasPendingCleanup);
        Assert.Equal(new[] { "close:1", "reader:1" }, events);
        Assert.Null(await retirement.TryCompleteAsync());
        Assert.False(retirement.HasPendingCleanup);
        Assert.Null(await retirement.TryCompleteAsync());
        Assert.Equal(new[] { "close:1", "reader:1", "close:2", "reader:2" }, events);
    }

    [Fact]
    public async Task ConcurrentRetirementWaitsForTheActualCloseAndThenRetriesItsFailure()
    {
        var entered = Signal(); var release = Signal();
        var failure = new IOException("heldClose"); int closes = 0;
        var connection = new ConquerTcpConnection(null, null);
        connection.RegisterTransportCloseCleanup(async () =>
        {
            if (Interlocked.Increment(ref closes) != 1) return;
            entered.TrySetResult();
            await release.Task;
            ThrowAtCleanupSite(failure);
        });
        var reader = new CompletionWitnessReader([]);
        var retirement = new ConquerTcpClientRetirement(connection, reader);
        Task<Exception?> first = retirement.TryCompleteAsync().AsTask();
        Task<Exception?>? second = null;
        bool completedWhileHeld = true; int attemptsWhileHeld = 0;
        Exception? primary = null;
        try
        {
            await entered.Task.WaitAsync(Deadline);
            second = retirement.TryCompleteAsync().AsTask();
            completedWhileHeld = second.IsCompleted;
            attemptsWhileHeld = Volatile.Read(ref closes);
        }
        catch (Exception error) { primary = error; }
        finally { release.TrySetResult(); }
        Exception? cleanup = null; Exception?[] outcomes = [];
        try
        {
            var tasks = second is null ? new[] { first } : new[] { first, second! };
            outcomes = await Task.WhenAll(tasks).WaitAsync(Deadline);
        }
        catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
        Assert.False(completedWhileHeld); Assert.Equal(1, attemptsWhileHeld);
        Assert.Same(failure, outcomes[0]); Assert.Null(outcomes[1]);
        Assert.Equal(2, closes); Assert.Equal(1, reader.Completions);
        Assert.False(retirement.HasPendingCleanup);
    }

    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void ThrowAtCleanupSite(Exception failure) => ExceptionDispatchInfo.Capture(failure).Throw();
}
