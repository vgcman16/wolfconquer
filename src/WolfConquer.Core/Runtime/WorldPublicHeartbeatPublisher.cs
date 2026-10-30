using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Runtime;

public sealed partial class WorldPublicHeartbeatPublisher : IAsyncDisposable
{
    private readonly ConquerTcpServer server;
    private readonly WorldServerFrameHandler frame;
    private readonly WorldPublicHeartbeatOptions options;
    private readonly TextWriter log;
    private readonly FileStream fileLease;
    private readonly CancellationTokenSource stop = new();
    private readonly SemaphoreSlim publication = new(1, 1);
    private Task worker = Task.CompletedTask;
    private Task? disposal;
    private readonly object disposalGate = new();
    private int failureReported;
    private int closed;

    private WorldPublicHeartbeatPublisher(ConquerTcpServer server, WorldServerFrameHandler frame,
        WorldPublicHeartbeatOptions options, TextWriter log)
    {
        this.server = server; this.frame = frame; this.options = options; this.log = log;
        server.ReadPublicHeartbeatLifecycle(frame); // Exact actual typed owner pairing before any IO.
        fileLease = OpenFileLease(options.FilePath);
    }

    public static WorldPublicHeartbeatPublisher? StartIfEnabled(ConquerTcpServer server, WorldServerFrameHandler frame,
        WorldPublicHeartbeatOptions? options, TextWriter? log = null)
    {
        if (options is null) return null;
        ArgumentNullException.ThrowIfNull(server); ArgumentNullException.ThrowIfNull(frame);
        var publisher = new WorldPublicHeartbeatPublisher(server, frame, options, log ?? TextWriter.Null);
        publisher.worker = publisher.RunAsync();
        return publisher;
    }

    public async ValueTask<bool> PublishOnceAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref closed) != 0, this);
        await publication.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref closed) != 0, this);
            var listener = server.ReadPublicHeartbeatLifecycle(frame);
            var world = await frame.CapturePublicHeartbeatAsync(cancellationToken).ConfigureAwait(false);
            // Do not publish an online snapshot if shutdown/failure crossed the read.
            var latest = server.ReadPublicHeartbeatLifecycle(frame);
            string state = latest.State switch
            {
                PublicListenerState.Created => "starting",
                PublicListenerState.Stopping => "stopping",
                PublicListenerState.Stopped => "offline",
                PublicListenerState.Faulted => "unavailable",
                _ => world.Recovery switch
                {
                    PublicRecoveryState.Ready => "online",
                    PublicRecoveryState.Starting => "starting",
                    PublicRecoveryState.Required => "recovery_required",
                    _ => "unavailable",
                },
            };
            if (listener.StartedAtUtc != latest.StartedAtUtc) state = "unavailable";
            var dto = new WorldPublicHeartbeat(1, "WolfConquer.WorldServer", options.InstanceId, DateTimeOffset.UtcNow,
                latest.StartedAtUtc, latest.UptimeSeconds, world.OnlinePlayers, world.Profile,
                options.ClientBuild, options.Environment, state);
            await WriteFileAsync(dto, cancellationToken).ConfigureAwait(false);
            Interlocked.Exchange(ref failureReported, 0);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            InvalidOperationException or ArgumentException or System.Security.SecurityException)
        {
            if (Interlocked.Exchange(ref failureReported, 1) == 0)
                try { await log.WriteLineAsync("Public game heartbeat unavailable: " + error.GetType().Name +
                    " (0x" + error.HResult.ToString("X8", System.Globalization.CultureInfo.InvariantCulture) + ").").ConfigureAwait(false); }
                catch { /* Status logging cannot manufacture health or stop the game. */ }
            return false;
        }
        finally { publication.Release(); }
    }

    private async Task RunAsync()
    {
        try
        {
            await PublishOnceAsync(stop.Token).ConfigureAwait(false);
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            while (await timer.WaitForNextTickAsync(stop.Token).ConfigureAwait(false))
                await PublishOnceAsync(stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }

    public async ValueTask StopPollingAsync()
    {
        if (Volatile.Read(ref closed) == 0)
            try { await stop.CancelAsync().ConfigureAwait(false); }
            catch (ObjectDisposedException) when (Volatile.Read(ref closed) != 0) { }
        await worker.ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        lock (disposalGate) return new(disposal ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            await StopPollingAsync().ConfigureAwait(false);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try { await PublishOnceAsync(deadline.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
        }
        finally
        {
            await publication.WaitAsync().ConfigureAwait(false);
            try { Interlocked.Exchange(ref closed, 1); fileLease.Dispose(); stop.Dispose(); }
            finally { publication.Release(); }
        }
    }
}
