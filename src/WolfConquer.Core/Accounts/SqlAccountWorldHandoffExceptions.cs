namespace WolfConquer.Core.Accounts;

/// <summary>A COMMIT was attempted, but its outcome was not observed.</summary>
public sealed class SqlAccountWorldHandoffCommitOutcomeUnknownException : Exception
{
    internal SqlAccountWorldHandoffCommitOutcomeUnknownException(Exception inner)
        : base("sqlAccountWorldHandoffCommitOutcomeUnknown", inner) { }
}

/// <summary>A COMMIT was observed, but required cleanup or ownership verification failed.</summary>
public sealed class SqlAccountWorldHandoffCommittedCleanupException : Exception
{
    internal SqlAccountWorldHandoffCommittedCleanupException(Exception inner)
        : base("sqlAccountWorldHandoffCommittedCleanupFailed", inner) { }
}