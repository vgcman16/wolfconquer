using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class ConquerTcpServerShutdownTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PendingObservation = TimeSpan.FromMilliseconds(150);

    [Fact]
    public Task LateAcceptedClientIsDrainedAfterAcceptLoopIncludingItsRealTransportCleanup() => RunOwnedAsync(async scope =>
    {
        // A dedicated synchronous thread-pool delegate owns the actual Monitor.
        // Accept/Handle can begin, but AcceptLoop cannot register its task yet.
        scope.HoldRegistrationAndScheduleShutdown();
        await scope.GateHeld.Task.WaitAsync(Deadline);
        await scope.ConnectAndSendAsync();
        var received = await scope.HandlerEntered.Task.WaitAsync(Deadline);
        AssertFrame(received);
        var accept = ReadField<Task>(scope.Server, "_acceptTask");
        Assert.False(accept.IsCompleted);

        scope.RequestShutdown.TrySetResult();
        var shutdown = await scope.ShutdownAssigned.Task.WaitAsync(Deadline);
        await scope.GateWorker!.WaitAsync(Deadline); // Monitor is no longer held.
        await accept.WaitAsync(Deadline); // The real late task is now registered.
        await scope.StopObserved.Task.WaitAsync(Deadline);
        await scope.AssertHeldHandlerAndCleanupDrainAsync(shutdown);
    });

    [Fact]
    public Task AlreadyRegisteredClientDrainsItsHandlerAndTransportCleanup() => RunOwnedAsync(async scope =>
    {
        await scope.ConnectAndSendAsync();
        AssertFrame(await scope.HandlerEntered.Task.WaitAsync(Deadline));
        await scope.WaitForActualClientRegistrationAsync();
        var shutdown = scope.BeginShutdown();
        await ReadField<Task>(scope.Server, "_acceptTask").WaitAsync(Deadline);
        await scope.StopObserved.Task.WaitAsync(Deadline);
        await scope.AssertHeldHandlerAndCleanupDrainAsync(shutdown);
    });

    [Fact]
    public Task StartedListenerWithNoClientsStopsAndDrainsAcceptLoop() => RunOwnedAsync(async scope =>
    {
        var accept = ReadField<Task>(scope.Server, "_acceptTask");
        Assert.False(accept.IsCompleted);
        await scope.BeginShutdown().WaitAsync(Deadline);
        Assert.True(accept.IsCompletedSuccessfully);
        Assert.False(scope.Server.IsRunning);
        Assert.False(scope.HandlerEntered.Task.IsCompleted);
        Assert.False(scope.CleanupEntered.Task.IsCompleted);
    });

    [Fact]
    public Task NeverStartedServerDisposesWithoutCreatingAnAcceptTask() => RunOwnedAsync(async scope =>
    {
        Assert.Null(FindField("_acceptTask").GetValue(scope.Server));
        await scope.BeginShutdown().WaitAsync(Deadline);
        Assert.Null(FindField("_acceptTask").GetValue(scope.Server));
        Assert.False(scope.Server.IsRunning);
        Assert.False(scope.HandlerEntered.Task.IsCompleted);
    }, start: false);

    private static void AssertFrame(Received received)
    {
        Assert.Equal((ushort)1010, received.Opcode);
        Assert.Equal(new byte[] { 0x34, 0x12 }, received.Payload);
        Assert.NotNull(received.Connection.LocalEndPoint);
        Assert.NotNull(received.Connection.RemoteEndPoint);
    }

    private static async Task AssertPendingAsync(Task task)
    {
        Assert.False(task.IsCompleted);
        // Only observes noncompletion of a deliberately held gate. This delay
        // never creates the late-registration schedule or releases a producer.
        var observation = Task.Delay(PendingObservation);
        Assert.Same(observation, await Task.WhenAny(task, observation).WaitAsync(Deadline));
        Assert.False(task.IsCompleted);
    }

    private static FieldInfo FindField(string name) =>
        typeof(ConquerTcpServer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Missing real server field: " + name);

    private static T ReadField<T>(ConquerTcpServer server, string name) where T : class =>
        Assert.IsAssignableFrom<T>(FindField(name).GetValue(server));

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task RunOwnedAsync(Func<DrainScope, Task> body, bool start = true)
    {
        var scope = new DrainScope();
        Exception? primary = null;
        try
        {
            if (start) scope.Server.Start();
            await body(scope);
        }
        catch (Exception error)
        {
            primary = error;
            throw;
        }
        finally
        {
            try { await scope.CleanupAsync(); }
            catch (Exception cleanup) when (primary is not null)
            {
                // Retain the assertion/progress failure; do not replace it with
                // a secondary socket/drain cleanup failure in a finally block.
                primary.Data["ConquerTcpServerShutdownCleanupFailure"] = cleanup;
            }
        }
    }

    private sealed record Received(ConquerTcpConnection Connection, ushort Opcode, byte[] Payload);

    private sealed class DrainScope
    {
        internal readonly ConquerTcpServer Server;
        internal readonly TaskCompletionSource GateHeld = Signal();
        internal readonly TaskCompletionSource RequestShutdown = Signal();
        internal readonly TaskCompletionSource<Task> ShutdownAssigned = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<Received> HandlerEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource HandlerRelease = Signal();
        internal readonly TaskCompletionSource HandlerExited = Signal();
        internal readonly TaskCompletionSource StopObserved = Signal();
        internal readonly TaskCompletionSource CleanupEntered = Signal();
        internal readonly TaskCompletionSource CleanupRelease = Signal();
        internal readonly TaskCompletionSource CleanupExited = Signal();
        internal Task? GateWorker;
        private Task? shutdown;
        private TcpClient? peer;

        internal DrainScope()
        {
            Server = new(new ConquerTcpServerOptions(ConquerRuntimeDefaults.WorldServer,
                IPAddress.Loopback, ListenPort: 0, Log: TextWriter.Null, ErrorLog: TextWriter.Null), HandleFrameAsync);
        }

        private async ValueTask HandleFrameAsync(ConquerTcpConnection connection, PacketFrame frame, CancellationToken token)
        {
            // The real TCP producer must invoke/await this actual registered
            // transport-close callback after this frame handler unwinds.
            connection.RegisterTransportCloseCleanup(async () =>
            {
                CleanupEntered.TrySetResult();
                await CleanupRelease.Task.ConfigureAwait(false);
                CleanupExited.TrySetResult();
            });
            using var cancellation = token.Register(() => StopObserved.TrySetResult());
            HandlerEntered.TrySetResult(new(connection, frame.Header.Opcode, frame.Payload.ToArray()));
            await HandlerRelease.Task.ConfigureAwait(false); // Deliberately not canceled by shutdown.
            HandlerExited.TrySetResult();
        }

        internal async Task ConnectAndSendAsync()
        {
            peer = new TcpClient();
            var endpoint = Server.BoundEndPoint;
            await peer.ConnectAsync(endpoint.Address, endpoint.Port).WaitAsync(Deadline);
            var frame = new PacketWriter(1010, payloadLength: 2);
            frame.WriteUInt16(0, 0x1234);
            await peer.GetStream().WriteAsync(frame.ToArray().AsMemory()).AsTask().WaitAsync(Deadline);
        }

        internal void HoldRegistrationAndScheduleShutdown()
        {
            var gate = ReadField<object>(Server, "_gate");
            GateWorker = Task.Run(() =>
            {
                lock (gate)
                {
                    GateHeld.TrySetResult();
                    // No asynchronous await while holding a Monitor. Invoke
                    // Dispose on THIS owner thread, making its initial lock
                    // reentrant, then release before anyone awaits its Task.
                    RequestShutdown.Task.WaitAsync(Deadline).GetAwaiter().GetResult();
                    ShutdownAssigned.TrySetResult(BeginShutdown());
                }
            });
        }

        internal Task BeginShutdown() => shutdown ??= Server.DisposeAsync().AsTask();

        internal async Task WaitForActualClientRegistrationAsync()
        {
            var gate = ReadField<object>(Server, "_gate");
            var tasks = ReadField<List<Task>>(Server, "_clientTasks");
            using var timeout = new CancellationTokenSource(Deadline);
            while (true)
            {
                lock (gate)
                {
                    if (tasks.Count != 0)
                    {
                        Assert.False(Assert.Single(tasks).IsCompleted);
                        return;
                    }
                }
                await Task.Delay(10, timeout.Token);
            }
        }

        internal async Task AssertHeldHandlerAndCleanupDrainAsync(Task disposal)
        {
            Assert.False(HandlerExited.Task.IsCompleted);
            Assert.False(CleanupEntered.Task.IsCompleted);
            await AssertPendingAsync(disposal);
            Assert.False(CleanupEntered.Task.IsCompleted);

            HandlerRelease.TrySetResult();
            await CleanupEntered.Task.WaitAsync(Deadline);
            Assert.True(HandlerExited.Task.IsCompletedSuccessfully);
            Assert.False(CleanupExited.Task.IsCompleted);
            await AssertPendingAsync(disposal);

            // EOF is also withheld by the real TCP finally block until this
            // asynchronous cleanup completes; no simulated connection is used.
            var eof = peer!.GetStream().ReadAsync(new byte[1].AsMemory()).AsTask();
            await AssertPendingAsync(eof);
            CleanupRelease.TrySetResult();
            await disposal.WaitAsync(Deadline);
            Assert.True(CleanupExited.Task.IsCompletedSuccessfully);
            Assert.Equal(0, await eof.WaitAsync(Deadline));
            Assert.False(Server.IsRunning);
        }

        internal async Task CleanupAsync()
        {
            // Release every owned gate on assertion, socket, or timeout failure.
            // Never invoke concurrent DisposeAsync or assume it is idempotent.
            HandlerRelease.TrySetResult();
            CleanupRelease.TrySetResult();
            RequestShutdown.TrySetResult();
            peer?.Dispose();
            try
            {
                if (GateWorker is not null) await GateWorker.WaitAsync(Deadline);
            }
            finally
            {
                // A completed/faulted worker can no longer race this sole
                // disposal owner. An unexpectedly still-running worker keeps
                // ownership; do not introduce a second concurrent Dispose.
                if (GateWorker is null || GateWorker.IsCompleted)
                    await BeginShutdown().WaitAsync(Deadline);
            }
        }
    }
}
