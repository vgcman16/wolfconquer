using WolfConquer.Core.Accounts;

namespace WolfConquer.Core.Data.SqlWorld;

/// <summary>A pre-owner SQL close failed; this exception owns only its disposal retry.</summary>
public sealed class SqlResourceCleanupException : Exception, ISqlPendingCleanup
{
    private readonly SqlAccountWorldHandoffCleanupOwner cleanup;

    internal SqlResourceCleanupException(Exception? primary, Exception closeFailure, IAsyncDisposable failedResource)
        : base("sqlResourceCleanupFailed", Combine(primary, closeFailure))
    {
        cleanup = new([failedResource]);
    }

    public bool HasPendingCleanup => cleanup.HasPendingCleanup;

    // Reuses the actual serialized disposal-only owner. No failed SQL resource
    // is exposed as a connection, authenticated account or world custody.
    public ValueTask DisposeAsync() => cleanup.DisposeAsync();

    private static Exception Combine(Exception? primary, Exception closeFailure)
    {
        ArgumentNullException.ThrowIfNull(closeFailure);
        return primary is null ? closeFailure : new AggregateException(primary, closeFailure);
    }
}
