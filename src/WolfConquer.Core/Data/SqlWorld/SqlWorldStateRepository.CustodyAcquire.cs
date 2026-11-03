using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    public ValueTask<SqlWorldBundleCustodyAcquireResult> TryAcquireBundleCustodyAsync(long accountId,
        CancellationToken cancellationToken = default) =>
        SqlWorldBundleCustody.AcquireAsync(this, accountId, cancellationToken);

    // These bridges perform real repository I/O; none can construct an owner.
    internal MySqlConnection CreateBundleCustodyConnection() => CreateConnection();

    internal static ValueTask<SqlWorldStateBundleReadResult> ReadAcquiringBundleCustodyAsync(MySqlConnection connection,
        SqlWorldCustodyIdentity identity, CancellationToken token) => ReadRetainedBundleAsync(connection, identity, token);
}
