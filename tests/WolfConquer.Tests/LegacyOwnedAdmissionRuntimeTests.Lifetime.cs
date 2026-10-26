using System.Collections.Concurrent;
using System.Net;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedAdmissionRuntimeTests
{
    [Fact]
    public async Task HeldActualWorldLeasePreventsSynchronousCloseAndKeepsRegistryOwnedUntilAsyncDrainCompletes()
    {
        var fixture = new LegacyOwnedAdmissionRuntimeFixture();
        LegacyOwnedAdmissionRuntime? runtime = null;
        WorldServerFrameHandler? handler = null;
        IDisposable? gate = null;
        Task? drain = null;
        Exception? primary = null;
        try
        {
            runtime = Open(fixture);
            handler = CreateHandler(fixture, runtime, TextWriter.Null);
            gate = await Field<WorldOperationBarrier>(handler, "_worldOperations").EnterExclusiveAsync();
            Assert.Throws<InvalidOperationException>(() => runtime.Dispose());
            AssertRegistryLeaseHeld(fixture);

            drain = runtime.DisposeAsync().AsTask();
            Assert.False(drain.IsCompleted);
            AssertRegistryLeaseHeld(fixture);
            gate.Dispose();

            await drain.WaitAsync(TimeSpan.FromSeconds(20));
            using var reopened = Open(fixture);
        }
        catch (Exception error) { primary = error; }

        await FinishOwnedLifetimeAsync(fixture, runtime, handler, primary,
            () => gate?.Dispose(), drain);
    }

    [Fact]
    public async Task ABlockedActualPlayerRuntimeSendRetainsOwnershipUntilTheReturnedWorkersReallyFinish()
    {
        var fixture = new LegacyOwnedAdmissionRuntimeFixture();
        LegacyOwnedAdmissionRuntime? runtime = null;
        WorldServerFrameHandler? handler = null;
        ConquerTcpConnection? connection = null;
        var log = new StringWriter();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sent = new ConcurrentQueue<byte[]>();
        var blocked = 0;
        Task? drain = null;
        Exception? primary = null;
        try
        {
            runtime = Open(fixture);
            var loop = LegacyRuntimeUpdateLoopOptions.EnabledDefault with { TickInterval = TimeSpan.FromMilliseconds(10) };
            handler = new(fixture.Loader, null, LegacyItemWorldDataFixture.Create([], []), loop,
                log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);
            connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958), async (bytes, _) =>
            {
                var copy = bytes.ToArray();
                if (LegacyMsgUpdatePacket.TryParse(Frame(copy), out var update) &&
                    update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.XPCircle && row.Value == 1) &&
                    Interlocked.CompareExchange(ref blocked, 1, 0) == 0)
                {
                    // A genuine returned runtime send delays cancellation until
                    // released. Its real worker must retain namespace ownership.
                    entered.TrySetResult();
                    await release.Task.ConfigureAwait(false);
                }
                sent.Enqueue(copy);
            });
            await handler.HandleFrameAsync(connection, Frame(new LegacyMsgLoginGamePacket(LegacyOwnedAdmissionRuntimeFixture.Uid).ToFrameBytes()));
            Assert.Contains(sent, IsAnswerOk);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            var workers = handler.CaptureLegacyRuntimeWorkerTasks();
            Assert.NotEmpty(workers);
            Assert.Contains(workers, task => !task.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => runtime.Dispose());
            AssertRegistryLeaseHeld(fixture);

            drain = runtime.DisposeAsync().AsTask();
            Assert.False(drain.IsCompleted);
            AssertRegistryLeaseHeld(fixture);
            release.TrySetResult();

            await drain.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.All(workers, task => Assert.True(task.IsCompleted));
            Assert.Equal(0, handler.LegacyRuntimeFaultedWorkerCount);
            using var reopened = Open(fixture);
        }
        catch (Exception error) { primary = error; }

        await FinishOwnedLifetimeAsync(fixture, runtime, handler, primary,
            () => release.TrySetResult(), drain, connection, log);
    }

    private static async Task FinishOwnedLifetimeAsync(LegacyOwnedAdmissionRuntimeFixture fixture,
        LegacyOwnedAdmissionRuntime? runtime, WorldServerFrameHandler? handler, Exception? primary,
        Action release, Task? drain, ConquerTcpConnection? connection = null, TextWriter? log = null)
    {
        var failures = new List<Exception>();
        if (primary is not null) AddOwnedLifetimeFailure(failures, primary);
        try { release(); } catch (Exception error) { AddOwnedLifetimeFailure(failures, error); }

        // Request actual cancellation/drain even when an assertion prevented
        // its initial start. Retain the actual task, including a faulted one.
        try { drain ??= runtime is null ? Task.CompletedTask : runtime.DisposeAsync().AsTask(); }
        catch (Exception error) { AddOwnedLifetimeFailure(failures, error); }

        Task? close = connection is null ? Task.CompletedTask : null;
        try
        {
            close ??= connection!.CompleteTransportCloseAsync();
            await close.WaitAsync(TimeSpan.FromSeconds(20));
        }
        catch (Exception error) { AddOwnedLifetimeFailure(failures, error); }

        try { if (drain is not null) await drain.WaitAsync(TimeSpan.FromSeconds(20)); }
        catch (Exception error) { AddOwnedLifetimeFailure(failures, error); }

        Task[]? workers = null;
        try
        {
            workers = handler?.CaptureLegacyRuntimeWorkerTasks() ?? [];
            await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(20));
        }
        catch (Exception error) { AddOwnedLifetimeFailure(failures, error); }

        // A timeout is not permission to dispose a live worker's log or delete
        // its fixture. Preserve those resources unless actual cleanup returned.
        if (drain?.IsCompletedSuccessfully == true && close?.IsCompletedSuccessfully == true &&
            workers is not null && workers.All(task => task.IsCompleted))
        {
            try { log?.Dispose(); } catch (Exception error) { AddOwnedLifetimeFailure(failures, error); }
            try { fixture.Dispose(); } catch (Exception error) { AddOwnedLifetimeFailure(failures, error); }
        }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Owned lifetime assertion and/or cleanup failed.", failures);
    }

    private static void AddOwnedLifetimeFailure(List<Exception> failures, Exception error)
    {
        if (error is AggregateException aggregate)
        {
            foreach (var inner in aggregate.Flatten().InnerExceptions) AddOwnedLifetimeFailure(failures, inner);
        }
        else if (!failures.Any(previous => ReferenceEquals(previous, error))) failures.Add(error);
    }

    private static void AssertRegistryLeaseHeld(LegacyOwnedAdmissionRuntimeFixture fixture)
    {
        Assert.False(LegacyItemUidRegistry.TryOpen(fixture.RegistryOptions(), out var competing, out var reason));
        Assert.Null(competing);
        Assert.NotEmpty(reason);
    }
}
