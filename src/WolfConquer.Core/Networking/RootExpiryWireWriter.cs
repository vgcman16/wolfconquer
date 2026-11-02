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
    private int staminaWaitingForGate;
    private int tickerWaitingForGate;
    private int reviverWaitingForGate;
    internal int ReviverWaitingForGate => Volatile.Read(ref reviverWaitingForGate);
    internal int TickerWaitingForGate => Volatile.Read(ref tickerWaitingForGate);
    internal int StaminaWaitingForGate => Volatile.Read(ref staminaWaitingForGate);
    private object? activeStart;
    internal bool ConsumeActiveStart(object? permit) => permit is not null &&
        ReferenceEquals(Interlocked.CompareExchange(ref activeStart, null, permit), permit);
    internal int WaitingForGate => Volatile.Read(ref waitingForGate);
    internal ValueTask StartActualWire(ReadOnlyMemory<byte> frame, CancellationToken token) => write(frame, token);

    // Closed private cases: the common gate lifecycle never accepts a caller
    // validator. Each case reaches its own real producer/Store start check.
    private abstract record WriteRequest;
    private sealed record RootWrite(WorldSessionStore Store, WorldSessionStore.RootExpiryWritePermit Permit) : WriteRequest;
    private sealed record StaminaWrite(WorldSessionStore.StaminaPublication Publication, WorldSessionStore.StaminaPublication.WirePermit Permit) : WriteRequest;
    private sealed record TickerWrite(WorldSessionStore Store, WorldServerFrameHandler Frame,
        WorldServerFrameHandler.PlayerTickXpPermit Permit, object Identity) : WriteRequest;
    private sealed record ReviverWrite(WorldSessionStore Store, WorldSessionStore.ReviverWirePermit Permit) : WriteRequest;

    internal ValueTask<bool> SendAsync(WorldSessionStore store, WorldSessionStore.RootExpiryWritePermit permit, CancellationToken token) =>
        SendOwnedAsync(new RootWrite(store, permit), token);
    internal ValueTask<bool> SendStaminaAsync(WorldSessionStore.StaminaPublication publication, WorldSessionStore.StaminaPublication.WirePermit permit, CancellationToken token) =>
        SendOwnedAsync(new StaminaWrite(publication, permit), token);
    internal ValueTask<bool> SendPlayerTickXpAsync(WorldSessionStore store, WorldServerFrameHandler frame,
        WorldServerFrameHandler.PlayerTickXpPermit permit, object identity, CancellationToken token) =>
        SendOwnedAsync(new TickerWrite(store, frame, permit, identity), token);
    internal ValueTask<bool> SendReviverAsync(WorldSessionStore store, WorldSessionStore.ReviverWirePermit permit, CancellationToken token) =>
        SendOwnedAsync(new ReviverWrite(store, permit), token);

    private void ChangeWaiting(WriteRequest request, int delta)
    {
        switch (request)
        {
            case RootWrite: Interlocked.Add(ref waitingForGate, delta); break;
            case StaminaWrite: Interlocked.Add(ref staminaWaitingForGate, delta); break;
            case TickerWrite: Interlocked.Add(ref tickerWaitingForGate, delta); break;
            case ReviverWrite: Interlocked.Add(ref reviverWaitingForGate, delta); break;
            default: throw new InvalidOperationException("Unknown owned wire request.");
        }
    }

    private bool TryStart(WriteRequest request, object start, CancellationToken token, out ValueTask pending)
    {
        switch (request)
        {
            case RootWrite root:
                return root.Store.TryStartRootExpiryWrite(root.Permit, Connection, this, start, token, out pending);
            case StaminaWrite stamina:
                return stamina.Publication.TryStartWire(stamina.Permit, Connection, this, start, token, out pending);
            case TickerWrite ticker:
                return ticker.Store.TryStartPlayerTickXp(ticker.Frame, ticker.Permit, Connection, ticker.Identity, this, start, token, out pending);
            case ReviverWrite reviver:
                return reviver.Store.TryStartOwnedReviverWire(reviver.Permit, Connection, this, start, token, out pending);
            default: throw new InvalidOperationException("Unknown owned wire request.");
        }
    }

    private async ValueTask<bool> SendOwnedAsync(WriteRequest request, CancellationToken token)
    {
        if (isClosed()) return false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        bool entered = false, started = false, attempting = false;
        try
        {
            ChangeWaiting(request, 1);
            try { await gate.WaitAsync(deadline.Token).ConfigureAwait(false); }
            finally { ChangeWaiting(request, -1); }
            entered = true;
            if (isClosed()) return false;
            attempting = true;
            var start = new object();
            if (Interlocked.CompareExchange(ref activeStart, start, null) is not null)
                throw new InvalidOperationException("Owned wire gate already has an active start.");
            ValueTask pending;
            try
            {
                if (!TryStart(request, start, deadline.Token, out pending))
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
