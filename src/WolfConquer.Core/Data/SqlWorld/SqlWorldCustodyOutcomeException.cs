namespace WolfConquer.Core.Data.SqlWorld;

/// <summary>
/// A known historical SQL result preceded a custody or cleanup failure. Reconcile
/// the same request; this result grants no current gameplay or native ownership.
/// </summary>
public sealed class SqlWorldCustodyOutcomeException : Exception
{
    internal SqlWorldCustodyOutcomeException(SqlWorldStateBundleRequest request,
        SqlWorldStateBundleCommitResult result, Exception inner)
        : base("sqlWorldKnownCustodyOutcomeFailed", inner)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(inner);
        if (result.Status is not (SqlWorldStateCommitStatus.Committed or SqlWorldStateCommitStatus.Replayed))
            throw new ArgumentException("A known SQL custody result is required.", nameof(result));
        Request = request;
        Result = result;
    }

    public SqlWorldStateBundleRequest Request { get; }
    public SqlWorldStateBundleCommitResult Result { get; }
}
