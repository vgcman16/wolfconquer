namespace WolfConquer.Core.Data.SqlWorld;

// Private server observations retained by an actual acquired owner. This data
// cannot issue custody, reopen a connection or authorize native publication.
internal sealed class SqlWorldCustodyIdentity
{
    internal SqlWorldCustodyIdentity(string database, long accountId, string accountLockName,
        string nonceLockName, long connectionId)
    {
        Database = database;
        AccountId = accountId;
        AccountLockName = accountLockName;
        NonceLockName = nonceLockName;
        ConnectionId = connectionId;
    }

    internal string Database { get; }
    internal long AccountId { get; }
    internal string AccountLockName { get; }
    internal string NonceLockName { get; }
    internal long ConnectionId { get; }
}
