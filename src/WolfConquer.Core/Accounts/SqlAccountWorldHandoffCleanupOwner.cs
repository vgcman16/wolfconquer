using System.Runtime.ExceptionServices;

namespace WolfConquer.Core.Accounts;

// Cleanup only. These references cannot be recovered as authentication, storage,
// native or publication authority through an exception or a retry operation.
internal sealed class SqlAccountWorldHandoffCleanupOwner : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly IAsyncDisposable?[] pending;
    private int pendingCount;

    internal SqlAccountWorldHandoffCleanupOwner(ReadOnlySpan<IAsyncDisposable> resources)
    {
        if (resources.IsEmpty) throw new ArgumentException("Pending cleanup requires an actual resource.", nameof(resources));
        pending = new IAsyncDisposable?[resources.Length];
        var unique = new HashSet<IAsyncDisposable>(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < resources.Length; index++)
        {
            var resource = resources[index];
            if (resource is null || !unique.Add(resource))
                throw new ArgumentException("Pending cleanup resources must be nonnull and distinct.", nameof(resources));
            pending[index] = resource;
        }
        pendingCount = pending.Length;
    }

    internal bool HasPendingCleanup => Volatile.Read(ref pendingCount) != 0;

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        List<Exception>? failures = null;
        try
        {
            for (int index = 0; index < pending.Length; index++)
            {
                if (pending[index] is not { } resource) continue;
                try
                {
                    await resource.DisposeAsync().ConfigureAwait(false);
                    pending[index] = null;
                    Interlocked.Decrement(ref pendingCount);
                }
                catch (Exception failure) { (failures ??= []).Add(failure); }
            }
        }
        finally { gate.Release(); }
        if (failures is { Count: 1 }) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is { Count: > 1 }) throw new AggregateException(failures);
    }
}
