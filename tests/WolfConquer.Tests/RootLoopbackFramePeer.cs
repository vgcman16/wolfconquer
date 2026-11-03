using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

// Test transport only: writes use a real socket and gate; the independent
// peer reads complete frames in wire order. Ordinary sends default to receipt
// only; phase-injection controls may explicitly pause their return after receipt.
internal sealed class RootLoopbackFramePeer : IAsyncDisposable
{
    private const int FillerLength = 16 * 1024 * 1024;
    private static readonly AsyncLocal<bool> InObserver = new();
    private readonly TcpClient writer = new(), reader;
    private readonly NetworkStream writerStream;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource stopping = new();
    private sealed class FrameCompletion
    {
        internal readonly TaskCompletionSource Received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Observed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private readonly ConcurrentDictionary<long, FrameCompletion> completions = new();
    private readonly Channel<(long Number, byte[] Bytes)> callbacks = Channel.CreateUnbounded<(long, byte[])>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly Action<byte[]> record;
    private readonly Func<byte[], ValueTask> observe;
    private readonly bool observeOrdinaryBeforeReturn;
    private readonly Task pump, dispatcher;
    private long written, received;
    private int fillerWriting;
    private int realFrameWriting;
    private int ordinaryClosedAfterGate;
    private int pauseAfterFrame;
    private readonly TaskCompletionSource readerPaused = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource resumeReader = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int closed;
    private int readerAborted;
    private Exception? failure;
    private readonly TaskCompletionSource fillerEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource fillerRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource fillerDrained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal ConquerTcpConnection Connection { get; }
    internal RootExpiryWireWriter ExpiryWriter { get; }
    internal Action<ReadOnlyMemory<byte>>? OrdinaryWriteRequested;
    internal long WireStarts => Volatile.Read(ref written);
    internal int OrdinaryClosedAfterGate => Volatile.Read(ref ordinaryClosedAfterGate);
    internal bool ReaderAborted => Volatile.Read(ref readerAborted) != 0;
    internal bool ActualBackpressure => Volatile.Read(ref fillerWriting) != 0 && gate.CurrentCount == 0;
    internal bool RealFrameWriteInProgress => Volatile.Read(ref realFrameWriting) != 0 && gate.CurrentCount == 0;
    internal bool RealFrameWriteOutstanding => Volatile.Read(ref realFrameWriting) != 0;

    internal RootLoopbackFramePeer(Func<byte[], ValueTask> observe, WolfConquer.Core.World.LegacyNativeGameClientRuntime? native = null,
        bool observeOrdinaryBeforeReturn = false)
        : this(static _ => { }, observe, native, observeOrdinaryBeforeReturn) { }

    internal RootLoopbackFramePeer(Action<byte[]> record, Func<byte[], ValueTask> observe, WolfConquer.Core.World.LegacyNativeGameClientRuntime? native = null,
        bool observeOrdinaryBeforeReturn = false)
    {
        this.record = record; this.observe = observe;
        this.observeOrdinaryBeforeReturn = observeOrdinaryBeforeReturn;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        // Default loopback autotuning can accept the entire filler into kernel
        // buffers. Bound both real socket buffers before any test traffic.
        writer.SendBufferSize = 0;
        writer.NoDelay = true;
        listener.Server.ReceiveBufferSize = 64 * 1024;
        listener.Start();
        writer.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        reader = listener.AcceptTcpClient();
        writerStream = writer.GetStream();
        reader.ReceiveBufferSize = 64 * 1024;
        Assert.InRange(writer.SendBufferSize, 0, 32 * 1024);
        Assert.InRange(reader.ReceiveBufferSize, 1, 128 * 1024);
        Connection = new ConquerTcpConnection(writer.Client.LocalEndPoint, writer.Client.RemoteEndPoint,
            GatedWire, _ => { Close(); return ValueTask.CompletedTask; }, native);
        ExpiryWriter = new RootExpiryWireWriter(Connection, gate, ActualWire, () => Volatile.Read(ref closed) != 0, Close);
        Connection.InstallRootExpiryWire(ExpiryWriter);
        dispatcher = DispatchAsync();
        pump = ReadFramesAsync();
    }
    private void Close()
    {
        if (Interlocked.Exchange(ref closed, 1) != 0) return;
        writer.Close();
        foreach (var pending in completions.Values) { pending.Received.TrySetException(new IOException("Loopback transport closed before receipt.")); pending.Observed.TrySetException(new IOException("Loopback transport closed before observation.")); }
    }
    private async ValueTask ActualWire(ReadOnlyMemory<byte> frame, CancellationToken token)
    {
        var number = Interlocked.Increment(ref written);
        completions.GetOrAdd(number, static _ => new());
        bool filler = frame.Length == FillerLength;
        if (filler) Volatile.Write(ref fillerWriting, 1);
        else Interlocked.Increment(ref realFrameWriting);
        try { await writerStream.WriteAsync(frame, token).ConfigureAwait(false); }
        finally
        {
            if (filler) Volatile.Write(ref fillerWriting, 0);
            else Interlocked.Decrement(ref realFrameWriting);
        }
    }
    private async ValueTask GatedWire(ReadOnlyMemory<byte> frame, CancellationToken token)
    {
        long number;
        if (Volatile.Read(ref closed) != 0) return;
        if (frame.Length != FillerLength) OrdinaryWriteRequested?.Invoke(frame);
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            // Match the production gate's second closed check: an earlier
            // partial/canceled encrypted write poisons the connection before
            // releasing this gate, so an already queued ordinary frame never
            // reaches the real write or advances its cipher/sequence.
            if (Volatile.Read(ref closed) != 0)
            {
                if (frame.Length != FillerLength) Interlocked.Increment(ref ordinaryClosedAfterGate);
                return;
            }
            await ActualWire(frame, token).ConfigureAwait(false); number = Volatile.Read(ref written);
        }
        finally { gate.Release(); }
        // Only explicit callback-interleaving controls pause here. The actual
        // socket gate is already released; recursive observers still wait for
        // receipt only. This is not a socket-backpressure or authority proof.
        await WaitCompletion(number, receiptOnly: !observeOrdinaryBeforeReturn || InObserver.Value, token).ConfigureAwait(false);
    }
    private async ValueTask WaitCompletion(long number, bool receiptOnly, CancellationToken token)
    {
        var completion = completions.GetOrAdd(number, static _ => new());
        await (receiptOnly ? completion.Received.Task : completion.Observed.Task).WaitAsync(token).ConfigureAwait(false);
        ThrowFailure();
    }
    internal void ThrowFailure() { if (Volatile.Read(ref failure) is { } e) throw new InvalidOperationException("Loopback observer failed.", e); }
    internal Task StartBackpressuredWrite() => Connection.SendAsync(new byte[FillerLength]).AsTask();
    internal async Task WaitForBackpressure()
    {
        await fillerEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(200);
        Assert.True(ActualBackpressure,
            $"The filler must still be writing while holding the actual TCP gate. writing={Volatile.Read(ref fillerWriting)} gate={gate.CurrentCount} sendBuffer={writer.SendBufferSize} receiveBuffer={reader.ReceiveBufferSize} available={reader.Available}");
    }
    internal void ReleaseBackpressuredWrite() => fillerRelease.TrySetResult();
    internal async Task DrainBackpressuredWrite() { ReleaseBackpressuredWrite(); await fillerDrained.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
    internal void CloseReader() { fillerRelease.TrySetResult(); Close(); reader.Close(); }
    internal void AbortReaderWithReset()
    {
        Volatile.Write(ref readerAborted, 1);
        reader.Client.LingerState = new LingerOption(true, 0);
        reader.Close();
        ReleaseBackpressuredWrite();
    }
    // Pause the real reader only after it has recorded an ordinary complete
    // wake frame. The observer queue remains independent and is drained.
    internal async Task PauseReaderAfterNextFrame()
    {
        Interlocked.Exchange(ref pauseAfterFrame, 1);
        await Connection.SendAsync(new byte[] { 4, 0, 0, 0 }).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await readerPaused.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await DrainObservations();
    }

    // Test pressure only: write complete framed padding through the real
    // socket while holding its actual semaphore. It grants no publication
    // permit and supplies no completion callback to the relic sender.
    internal async Task PrimeRealSocketUntilBlocked()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await gate.WaitAsync(timeout.Token);
        try
        {
            // If an actual pending send outlives the pressure budget, retire
            // the real socket so its overlapped operation can finish. Never
            // abandon that operation or reuse an ambiguous partial frame.
            Exception? primary = null;
            Exception? closeFailure = null;
            var abort = timeout.Token.Register(() =>
            {
                try { Close(); }
                catch (Exception error) { Interlocked.CompareExchange(ref closeFailure, error, null); }
            });
            try
            {
                var socket = writer.Client;
                // The other real-pressure fixtures keep their original zero
                // buffer. This sole priming path asks Windows for a small real
                // queue; only Poll and the later pending write prove pressure.
                socket.SendBufferSize = 8 * 1024;
                if (socket.SendBufferSize is <= 0 or > 32 * 1024)
                    throw new IOException("The real pressure socket buffer is outside the fixture bound.");
                var packet = new byte[4096];
                BinaryPrimitives.WriteUInt16LittleEndian(packet, checked((ushort)packet.Length));
                var total = 0;
                var stable = Stopwatch.StartNew();
                while (true)
                {
                    timeout.Token.ThrowIfCancellationRequested();
                    if (!socket.Poll(0, SelectMode.SelectWrite))
                    {
                        if (stable.Elapsed >= TimeSpan.FromMilliseconds(250)) return;
                        await Task.Delay(10, timeout.Token).ConfigureAwait(false);
                        continue;
                    }
                    stable.Restart();
                    // Await the real operation, not a timeout wrapper around a
                    // synchronous Send that can remain alive after the test.
                    var count = await socket.SendAsync(packet.AsMemory(), SocketFlags.None, timeout.Token)
                        .ConfigureAwait(false);
                    if (count != packet.Length) throw new IOException("The real pressure frame was only partially accepted.");
                    total = checked(total + count);
                    if (total > 8 * 1024 * 1024) throw new IOException("The real socket did not saturate within the bounded pressure budget.");
                }
            }
            catch (Exception error)
            {
                primary = error;
                try { Close(); }
                catch (Exception closeError) { Interlocked.CompareExchange(ref closeFailure, closeError, null); }
                throw;
            }
            finally
            {
                // Dispose waits for any running cancellation callback before
                // inspecting its failure and before the socket gate is released.
                abort.Dispose();
                if (Volatile.Read(ref closeFailure) is { } closeError)
                {
                    if (primary is not null)
                        throw new AggregateException("Real pressure send and transport close both failed.", primary, closeError);
                    throw new InvalidOperationException("Real pressure transport close failed.", closeError);
                }
            }
        }
        finally { gate.Release(); }
    }

    // Only the remote endpoint is aborted. The writer connection/registered
    // gate is deliberately left open so the next actual socket write observes
    // the peer reset; this is not a synthetic completion callback.
    internal void AbortRemoteOnlyWithReset()
    {
        Volatile.Write(ref readerAborted, 1);
        reader.Client.LingerState = new LingerOption(true, 0);
        reader.Close();
    }

    internal async Task WaitForRemoteSocketClosure()
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < until)
        {
            try
            {
                if (writer.Client.Poll(0, SelectMode.SelectRead) && writer.Client.Available == 0) return;
            }
            catch (SocketException) { return; }
            await Task.Delay(10);
        }
        throw new TimeoutException("The writer socket did not observe the real remote closure.");
    }

    private async Task ReadFramesAsync()
    {
        try
        {
            var stream = reader.GetStream();
            while (!stopping.IsCancellationRequested)
            {
                var header = new byte[4]; await stream.ReadExactlyAsync(header, stopping.Token).ConfigureAwait(false);
                var number = Interlocked.Increment(ref received);
                var completion = completions.GetOrAdd(number, static _ => new());
                int length = BinaryPrimitives.ReadUInt16LittleEndian(header);
                if (length == 0)
                {
                    fillerEntered.TrySetResult(); await fillerRelease.Task.WaitAsync(stopping.Token).ConfigureAwait(false);
                    var buffer = new byte[65536]; var left = FillerLength - header.Length;
                    while (left > 0) { var count = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, left)), stopping.Token).ConfigureAwait(false); if (count == 0) throw new EndOfStreamException(); left -= count; }
                    fillerDrained.TrySetResult(); completion.Received.TrySetResult(); completion.Observed.TrySetResult(); continue;
                }
                if (length < 4) throw new InvalidDataException("Malformed loopback frame.");
                var bytes = new byte[length]; header.CopyTo(bytes, 0);
                await stream.ReadExactlyAsync(bytes.AsMemory(4), stopping.Token).ConfigureAwait(false);
                record(bytes); // The independent reader records before any business callback.
                completion.Received.TrySetResult();
                await callbacks.Writer.WriteAsync((number, bytes), stopping.Token).ConfigureAwait(false);
                if (Interlocked.Exchange(ref pauseAfterFrame, 0) != 0)
                {
                    readerPaused.TrySetResult();
                    await resumeReader.Task.WaitAsync(stopping.Token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception error) when (stopping.IsCancellationRequested || Volatile.Read(ref closed) != 0 ||
            Volatile.Read(ref readerAborted) != 0 && error is IOException or SocketException or ObjectDisposedException or OperationCanceledException) { }
        catch (Exception error) { Fail(error); }
        finally { callbacks.Writer.TryComplete(); }
    }
    private async Task DispatchAsync()
    {
        try
        {
            await foreach (var item in callbacks.Reader.ReadAllAsync(stopping.Token).ConfigureAwait(false))
            {
                var completion = completions.GetOrAdd(item.Number, static _ => new());
                try { InObserver.Value = true; await observe(item.Bytes).ConfigureAwait(false); completion.Observed.TrySetResult(); }
                finally { InObserver.Value = false; }
            }
        }
        catch (Exception) when (stopping.IsCancellationRequested || Volatile.Read(ref closed) != 0) { }
        catch (Exception error) { Fail(error); }
    }
    private void Fail(Exception error)
    {
        Interlocked.CompareExchange(ref failure, error, null);
        foreach (var pending in completions.Values) { pending.Received.TrySetException(error); pending.Observed.TrySetException(error); }
    }
    internal async Task DrainObservations()
    {
        while (true)
        {
            var last = Volatile.Read(ref written);
            if (last > 0)
                await WaitCompletion(last, InObserver.Value, CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            if (Volatile.Read(ref written) == last) return;
            // An earlier observer may have emitted another real frame. Drain
            // that ordered callback too, unless already inside the observer.
        }
    }
    public async ValueTask DisposeAsync()
    {
        stopping.Cancel(); fillerRelease.TrySetResult(); resumeReader.TrySetResult(); Close(); reader.Close();
        await Task.WhenAll(pump, dispatcher).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        gate.Dispose(); stopping.Dispose(); ThrowFailure();
    }
}
