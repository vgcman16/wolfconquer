using System.Runtime.ExceptionServices;

namespace WolfConquer.Tests;

public sealed partial class ConquerTcpServerRetirementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualPeerEofRetainsFailedRetirementAfterTaskRemovalAndReportsItOnRealRetry(bool cancellationShaped) => RunOwnedAsync(async scope =>
    {
        Exception original = cancellationShaped ? new OperationCanceledException("private-retirement-marker") :
            new IOException("private-retirement-marker");
        scope.OnClose = (_, attempt) =>
        {
            if (attempt == 1) ExceptionDispatchInfo.Capture(original).Throw();
            return Task.CompletedTask;
        };
        var peer = await scope.ConnectAsync();
        await scope.EndPeerSendAndObserveEofAsync(peer, expectedPending: 1);
        Assert.Equal((1, 0), scope.Server.ReadClientRetirementCounts());
        Assert.Equal(1, scope.CloseAttempts[0]);
        Assert.DoesNotContain(original.Message, scope.Output);
        Assert.DoesNotContain(original.Message, scope.Errors);
        Assert.Contains("stage=transport-retirement exceptionType=" + original.GetType().Name, scope.Errors);

        var reported = await Record.ExceptionAsync(() => scope.Server.RetryFailedClientRetirementsAsync().AsTask());
        Assert.Same(original, reported);
        Assert.Equal((0, 0), scope.Server.ReadClientRetirementCounts());
        Assert.Equal(2, scope.CloseAttempts[0]); Assert.True(scope.Server.IsRunning);
        await scope.Server.RetryFailedClientRetirementsAsync();
        Assert.Equal(2, scope.CloseAttempts[0]);
        // The same actual listener still accepts and exchanges a genuine frame.
        scope.OnClose = (_, _) => Task.CompletedTask;
        var next = await scope.ConnectAsync();
        await scope.EndPeerSendAndObserveEofAsync(next, expectedPending: 0);
    });

    [Fact]
    public Task ShutdownAttemptsEveryRealPendingEntryAndRepeatedDisposeDoesNotRecancelDisposedStop() => RunOwnedAsync(async scope =>
    {
        Exception[][] errors =
        [
            [new IOException("firstInitial"), new IOException("firstRetry")],
            [new InvalidOperationException("secondInitial"), new InvalidOperationException("secondRetry")],
        ];
        scope.OnClose = (index, attempt) =>
        {
            if (attempt <= 2) ExceptionDispatchInfo.Capture(errors[index][attempt - 1]).Throw();
            return Task.CompletedTask;
        };
        var first = await scope.ConnectAsync();
        await scope.EndPeerSendAndObserveEofAsync(first, expectedPending: 1);
        var second = await scope.ConnectAsync();
        await scope.EndPeerSendAndObserveEofAsync(second, expectedPending: 2);

        var failed = await Assert.ThrowsAsync<AggregateException>(() => scope.Server.DisposeAsync().AsTask());
        Assert.Collection(failed.InnerExceptions,
            error => AssertEntryErrors(error, errors[0]), error => AssertEntryErrors(error, errors[1]));
        Assert.Equal(new[] { 2, 2 }, scope.CloseAttempts);
        Assert.Equal((2, 0), scope.Server.ReadClientRetirementCounts()); Assert.False(scope.Server.IsRunning);
        await scope.Server.DisposeAsync();
        Assert.Equal(new[] { 3, 3 }, scope.CloseAttempts);
        Assert.Equal((0, 0), scope.Server.ReadClientRetirementCounts());
        await scope.Server.DisposeAsync();
        Assert.Equal(new[] { 3, 3 }, scope.CloseAttempts);
    });

    [Fact]
    public Task ConcurrentServerDisposeWaitsForTheSameActualRetirementRetry() => RunOwnedAsync(async scope =>
    {
        var original = new IOException("initialClose"); var entered = Signal(); var release = Signal();
        scope.ReleaseOnCleanup = () => release.TrySetResult();
        scope.OnClose = async (_, attempt) =>
        {
            if (attempt == 1) ExceptionDispatchInfo.Capture(original).Throw();
            if (attempt != 2) return;
            entered.TrySetResult(); await release.Task;
        };
        var peer = await scope.ConnectAsync();
        await scope.EndPeerSendAndObserveEofAsync(peer, expectedPending: 1);
        var retry = scope.Server.RetryFailedClientRetirementsAsync().AsTask();
        scope.PendingOperations.Add(retry);
        await entered.Task.WaitAsync(Deadline);
        var shutdown = scope.Server.DisposeAsync().AsTask();
        scope.PendingOperations.Add(shutdown);
        bool shutdownCompletedWhileHeld = shutdown.IsCompleted;
        bool listenerStillRunning = scope.Server.IsRunning;
        release.TrySetResult();
        var reported = await Record.ExceptionAsync(() => retry.WaitAsync(Deadline));
        if (retry.IsCompleted) scope.PendingOperations.Remove(retry);
        await shutdown.WaitAsync(Deadline);
        scope.PendingOperations.Remove(shutdown);
        Assert.False(shutdownCompletedWhileHeld); Assert.True(listenerStillRunning);
        Assert.Same(original, reported); Assert.Equal(2, scope.CloseAttempts[0]);
        Assert.Equal((0, 0), scope.Server.ReadClientRetirementCounts()); Assert.False(scope.Server.IsRunning);
    });

    [Fact]
    public Task ActualPipelinePrimaryAndLaterEofFailureRemainOrderedThroughRetirement() => RunOwnedAsync(async scope =>
    {
        var primary = new InvalidOperationException("ownedPipelinePrimary");
        var closeFailure = new IOException("ownedCleanupFailure");
        scope.HandlerFailure = primary;
        scope.OnClose = (_, attempt) =>
        {
            if (attempt == 1) ExceptionDispatchInfo.Capture(closeFailure).Throw();
            return Task.CompletedTask;
        };
        var peer = await scope.ConnectAsync(expectEcho: false);
        await scope.EndPeerSendAndObserveEofAsync(peer, expectedPending: 1);
        var failure = await Assert.ThrowsAsync<AggregateException>(() => scope.Server.RetryFailedClientRetirementsAsync().AsTask());
        Assert.Collection(failure.InnerExceptions,
            error => Assert.Same(primary, error), error => Assert.Same(closeFailure, error));
        Assert.Equal((0, 0), scope.Server.ReadClientRetirementCounts()); Assert.Equal(2, scope.CloseAttempts[0]);
        // The earlier ordinary pipeline diagnostics retain their existing format;
        // the added retirement diagnostic still does not print cleanup values.
        Assert.Contains(primary.Message, scope.Errors);
        Assert.DoesNotContain(closeFailure.Message, scope.Errors);
        await scope.Server.RetryFailedClientRetirementsAsync();
    });

    [Fact]
    public async Task DisposalBeforeStartRefusesLaterStartBeforeCreatingAnyProducer()
    {
        await using var server = new WolfConquer.Core.Networking.ConquerTcpServer(
            new WolfConquer.Core.Networking.ConquerTcpServerOptions(
                WolfConquer.Core.Runtime.ConquerRuntimeDefaults.WorldServer,
                System.Net.IPAddress.Loopback, ListenPort: 0, Log: TextWriter.Null, ErrorLog: TextWriter.Null),
            (_, _, _) => throw new InvalidOperationException("No producer may be created after disposal."));
        await server.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => server.Start());
        Assert.False(server.IsRunning); Assert.Equal((0, 0), server.ReadClientRetirementCounts());
        foreach (string name in new[] { "_acceptTask", "_stop" })
        {
            var field = typeof(WolfConquer.Core.Networking.ConquerTcpServer).GetField(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(field); Assert.Null(field!.GetValue(server));
        }
        await server.DisposeAsync();
        Assert.False(server.IsRunning); Assert.Equal((0, 0), server.ReadClientRetirementCounts());
    }

    private static void AssertEntryErrors(Exception failure, Exception[] expected)
    {
        var aggregate = Assert.IsType<AggregateException>(failure);
        Assert.Collection(aggregate.InnerExceptions,
            error => Assert.Same(expected[0], error), error => Assert.Same(expected[1], error));
    }
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task RunOwnedAsync(Func<RetirementScope, Task> body)
    {
        var scope = new RetirementScope();
        var failures = new List<Exception>();
        try { scope.Server.Start(); await body(scope); }
        catch (Exception failure) { failures.Add(failure); }
        try { scope.ReleaseOnCleanup?.Invoke(); }
        catch (Exception failure) { failures.Add(failure); }
        foreach (var peer in scope.Peers)
            try { peer.Dispose(); }
            catch (Exception failure) { failures.Add(failure); }
        foreach (var pending in scope.PendingOperations)
            try { await pending.WaitAsync(Deadline); }
            catch (Exception failure) { failures.Add(failure); }
        try { await scope.Server.DisposeAsync().AsTask().WaitAsync(Deadline); }
        catch (Exception failure) { failures.Add(failure); }
        try { scope.Dispose(); }
        catch (Exception failure) { failures.Add(failure); }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException(failures);
    }
}
