using WolfConquer.Core.World;

namespace WolfConquer.Core.Networking;

// Installed by the real legacy TCP producer with its actual encryption writer
// and the same gate used by all ordinary writes on this connection.
internal sealed class RootExpiryWireWriter(
    ConquerTcpConnection connection, SemaphoreSlim gate,
    Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write,
    Func<bool> isClosed, Action markClosed)
{
    internal ConquerTcpConnection Connection { get; } = connection;
    private int waitingForGate;
    private object? activeStart;
    internal bool ConsumeActiveStart(object? permit) => permit is not null &&
        ReferenceEquals(Interlocked.CompareExchange(ref activeStart, null, permit), permit);
    internal int WaitingForGate => Volatile.Read(ref waitingForGate);
    internal ValueTask StartActualWire(ReadOnlyMemory<byte> frame, CancellationToken token) => write(frame, token);

    internal async ValueTask<bool> SendAsync(WorldSessionStore store,
        WorldSessionStore.RootExpiryWritePermit permit, CancellationToken token)
    {
        if (isClosed()) return false;
        // A timed-out or partially written encrypted frame retires the real
        // transport; its stream/cipher cannot safely be used for later frames.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        bool entered = false, started = false, attempting = false;
        try
        {
            Interlocked.Increment(ref waitingForGate);
            try { await gate.WaitAsync(deadline.Token).ConfigureAwait(false); }
            finally { Interlocked.Decrement(ref waitingForGate); }
            entered = true;
            if (isClosed()) return false;
            attempting = true;
            var start = new object();
            if (Interlocked.CompareExchange(ref activeStart, start, null) is not null)
                throw new InvalidOperationException("Root expiry write gate already has an active start.");
            ValueTask pending;
            try
            {
                if (!store.TryStartRootExpiryWrite(permit, Connection, this, start, deadline.Token, out pending))
                { attempting = false; return false; }
            }
            finally { Interlocked.CompareExchange(ref activeStart, null, start); }
            started = true; attempting = false;
            await pending.ConfigureAwait(false);
            return !isClosed();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        { if (started || attempting) markClosed(); throw; }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        { markClosed(); return false; }
        catch (ObjectDisposedException) when (token.IsCancellationRequested || isClosed())
        { if (started || attempting) markClosed(); if (token.IsCancellationRequested) token.ThrowIfCancellationRequested(); return false; }
        catch (Exception error) when (NetworkExceptionClassifier.IsPeerDisconnect(error)) { markClosed(); return false; }
        catch { if (started || attempting) markClosed(); throw; }
        finally { if (entered) gate.Release(); }
    }
}
