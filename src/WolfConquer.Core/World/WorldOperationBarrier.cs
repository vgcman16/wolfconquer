namespace WolfConquer.Core.World;

// Handler-lifetime operation isolation, not a session/client identity. Never nest
// acquisitions. Every successful lease must be disposed, including after errors.
internal sealed partial class WorldOperationBarrier
{
    private readonly object _gate = new();
    private readonly LinkedList<Waiter> _queue = new();
    private int _readers;
    private bool _writer;

    public ValueTask<IDisposable> EnterSharedAsync(CancellationToken cancellationToken = default) =>
        EnterAsync(exclusive: false, cancellationToken);

    public ValueTask<IDisposable> EnterExclusiveAsync(CancellationToken cancellationToken = default) =>
        EnterAsync(exclusive: true, cancellationToken);

    private ValueTask<IDisposable> EnterAsync(bool exclusive, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Waiter waiter;
        lock (_gate)
        {
            // New readers cannot pass a queued writer. Contiguous readers at the
            // queue head are granted together; writers retain FIFO position.
            if (!_writer && _queue.Count == 0 && (!exclusive || _readers == 0))
            {
                if (exclusive) _writer = true;
                else _readers++;
                return ValueTask.FromResult<IDisposable>(new Lease(this, exclusive));
            }
            waiter = new Waiter(this, exclusive, cancellationToken);
            waiter.Node = _queue.AddLast(waiter);
        }
        return AwaitWaiterAsync(waiter);
    }

    private static async ValueTask<IDisposable> AwaitWaiterAsync(Waiter waiter)
    {
        // Registration happens outside the state lock. If grant won the race,
        // cancellation sees Node=null and the caller receives/disposes its lease.
        using var registration = waiter.CancellationToken.Register(static state =>
        {
            var pending = (Waiter)state!;
            pending.Owner.Cancel(pending);
        }, waiter);
        return await waiter.Completion.Task.ConfigureAwait(false);
    }

    private void Cancel(Waiter waiter)
    {
        lock (_gate)
        {
            if (waiter.Node is null) return;
            _queue.Remove(waiter.Node);
            waiter.Node = null;
            waiter.Completion.TrySetCanceled(waiter.CancellationToken);
            DrainQueue();
        }
    }

    private void Exit(bool exclusive)
    {
        lock (_gate)
        {
            if (exclusive) _writer = false;
            else _readers--;
            DrainQueue();
        }
    }

    // Called only under _gate; completions never run caller continuations inline.
    private void DrainQueue()
    {
        while (!_writer && _queue.First is { } node)
        {
            var waiter = node.Value;
            if (waiter.Exclusive && _readers != 0) return;
            _queue.RemoveFirst();
            waiter.Node = null;
            if (waiter.Exclusive) _writer = true;
            else _readers++;
            waiter.Completion.SetResult(new Lease(this, waiter.Exclusive));
        }
    }

    private sealed class Waiter(WorldOperationBarrier owner, bool exclusive, CancellationToken cancellationToken)
    {
        internal WorldOperationBarrier Owner { get; } = owner;
        internal bool Exclusive { get; } = exclusive;
        internal CancellationToken CancellationToken { get; } = cancellationToken;
        internal LinkedListNode<Waiter>? Node { get; set; }
        internal TaskCompletionSource<IDisposable> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class Lease(WorldOperationBarrier owner, bool exclusive) : IDisposable
    {
        private WorldOperationBarrier? _owner = owner;
        internal bool IsOwnedBy(WorldOperationBarrier candidate) => ReferenceEquals(Volatile.Read(ref _owner), candidate);
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Exit(exclusive);
    }
}
