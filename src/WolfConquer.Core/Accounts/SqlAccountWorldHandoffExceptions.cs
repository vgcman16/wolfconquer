namespace WolfConquer.Core.Accounts;

/// <summary>A COMMIT was attempted, but its outcome was not observed.</summary>
public sealed class SqlAccountWorldHandoffCommitOutcomeUnknownException : SqlAccountWorldHandoffException
{
    internal SqlAccountWorldHandoffCommitOutcomeUnknownException(Exception inner)
        : base("sqlAccountWorldHandoffCommitOutcomeUnknown", inner) { }

    internal SqlAccountWorldHandoffCommitOutcomeUnknownException(Exception inner, SqlAccountWorldHandoffCleanupOwner cleanup)
        : base("sqlAccountWorldHandoffCommitOutcomeUnknown", inner, cleanup) { }
}

/// <summary>A COMMIT was observed, but required cleanup or ownership verification failed.</summary>
public sealed class SqlAccountWorldHandoffCommittedCleanupException : SqlAccountWorldHandoffException
{
    internal SqlAccountWorldHandoffCommittedCleanupException(Exception inner)
        : base("sqlAccountWorldHandoffCommittedCleanupFailed", inner) { }

    internal SqlAccountWorldHandoffCommittedCleanupException(Exception inner, SqlAccountWorldHandoffCleanupOwner cleanup)
        : base("sqlAccountWorldHandoffCommittedCleanupFailed", inner, cleanup) { }
}

/// <summary>COMMIT was never attempted, but required resource disposal failed.</summary>
public sealed class SqlAccountWorldHandoffCleanupException : SqlAccountWorldHandoffException
{
    internal SqlAccountWorldHandoffCleanupException(Exception inner, SqlAccountWorldHandoffCleanupOwner cleanup)
        : base("sqlAccountWorldHandoffCleanupFailed", inner, cleanup) { }
}
