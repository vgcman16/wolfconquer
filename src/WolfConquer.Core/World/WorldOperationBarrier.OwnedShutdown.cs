namespace WolfConquer.Core.World;

internal sealed partial class WorldOperationBarrier
{
    // A real nonwaiting exclusive acquisition. An active or queued operation
    // cannot be mistaken for a drained owner by synchronous resource cleanup.
    internal bool TryEnterIdleExclusive(out IDisposable? lease)
    {
        lock (_gate)
        {
            lease = null;
            if (_writer || _readers != 0 || _queue.Count != 0) return false;
            _writer = true; lease = new Lease(this, exclusive: true); return true;
        }
    }
}
