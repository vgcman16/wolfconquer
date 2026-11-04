using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Core.Accounts;

/// <summary>Handoff failure with optional retained disposal-only ownership.</summary>
public abstract class SqlAccountWorldHandoffException : Exception, ISqlPendingCleanup
{
    private readonly SqlAccountWorldHandoffCleanupOwner? cleanup;

    internal SqlAccountWorldHandoffException(string message, Exception inner,
        SqlAccountWorldHandoffCleanupOwner? cleanup = null) : base(message, inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        this.cleanup = cleanup;
    }

    public bool HasPendingCleanup => cleanup?.HasPendingCleanup ?? false;

    // Required cleanup is not canceled by an already failed request's token.
    public ValueTask DisposeAsync() => cleanup?.DisposeAsync() ?? ValueTask.CompletedTask;

    internal static SqlAccountWorldHandoffException WithPendingCleanup(Exception primary,
        ReadOnlySpan<IAsyncDisposable> resources, bool commitAttempted, bool committed)
    {
        ArgumentNullException.ThrowIfNull(primary);
        if (committed && !commitAttempted)
            throw new ArgumentException("Observed commitment requires an attempted COMMIT.");
        var owner = new SqlAccountWorldHandoffCleanupOwner(resources);
        return committed ? new SqlAccountWorldHandoffCommittedCleanupException(primary, owner) :
            commitAttempted ? new SqlAccountWorldHandoffCommitOutcomeUnknownException(primary, owner) :
            new SqlAccountWorldHandoffCleanupException(primary, owner);
    }
}
