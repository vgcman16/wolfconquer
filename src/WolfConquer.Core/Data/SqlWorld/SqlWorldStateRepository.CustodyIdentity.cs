using System.Security.Cryptography;
using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    // Acquire the account before the random claim. Servers that replace the
    // earlier named lock fail the final real ownership check and issue nothing.
    internal static async ValueTask<SqlWorldCustodyIdentity?> AcquireBundleCustodyIdentityAsync(
        MySqlConnection connection, long accountId, CancellationToken token)
    {
        if (!await TryAcquireAccountLocksAsync(connection, new[] { accountId }, token).ConfigureAwait(false))
            return null;
        string accountLock = (await AccountLockNamesAsync(connection, new[] { accountId }, null, token).ConfigureAwait(false))[0];
        string nonceLock = "wolf:sql:claim:" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        object? acquired = await UseResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = "SELECT CAST(GET_LOCK(@name,0) AS SIGNED)";
            command.Parameters.AddWithValue("@name", nonceLock);
            return await command.ExecuteScalarAsync(token).ConfigureAwait(false);
        }).ConfigureAwait(false);
        if (!ReadBinaryLockScalar(acquired, "sqlWorldCustodyClaimAcquisitionInvalid")) return null;
        var identity = await UseResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = "SELECT DATABASE(),CAST(CONNECTION_ID() AS SIGNED)";
            return await UseResourceAsync(await command.ExecuteReaderAsync(token).ConfigureAwait(false), async reader =>
            {
                if (!await reader.ReadAsync(token).ConfigureAwait(false) || reader.IsDBNull(0) || reader.IsDBNull(1))
                    throw new InvalidOperationException("sqlWorldCustodyPhysicalIdentityMissing");
                string database = reader.GetString(0);
                long connectionId = reader.GetInt64(1);
                if (database.Length == 0 || connectionId <= 0)
                    throw new InvalidOperationException("sqlWorldCustodyPhysicalIdentityInvalid");
                return new SqlWorldCustodyIdentity(database, accountId, accountLock, nonceLock, connectionId);
            }).ConfigureAwait(false);
        }).ConfigureAwait(false);
        return await VerifyCustodyIdentityAsync(connection, identity, null, token).ConfigureAwait(false) ? identity : null;
    }

    internal static ValueTask<bool> VerifyCustodyIdentityAsync(MySqlConnection connection,
        SqlWorldCustodyIdentity identity, MySqlTransaction? transaction, CancellationToken token) =>
        VerifyCustodyIdentityHeldAsync(connection, identity, transaction, true, token);

    // The peer's unpredictable claim is checked on the transaction connection.
    // Matching database names or numeric connection IDs alone cannot pass this.
    internal static ValueTask<bool> VerifyCustodyClaimsAsync(MySqlConnection connection,
        SqlWorldCustodyIdentity identity, MySqlTransaction? transaction, CancellationToken token) =>
        VerifyCustodyIdentityHeldAsync(connection, identity, transaction, false, token);

    private static async ValueTask<bool> VerifyCustodyIdentityHeldAsync(MySqlConnection connection,
        SqlWorldCustodyIdentity identity, MySqlTransaction? transaction, bool requirePhysicalOwner, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(identity);
        if (connection.State != System.Data.ConnectionState.Open) return false;
        return await UseResourceAsync(connection.CreateCommand(), async command =>
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT DATABASE(),CAST(CONNECTION_ID() AS SIGNED)," +
                "CAST(IS_USED_LOCK(@accountLock) AS SIGNED),CAST(IS_USED_LOCK(@nonceLock) AS SIGNED)";
            command.Parameters.AddWithValue("@accountLock", identity.AccountLockName);
            command.Parameters.AddWithValue("@nonceLock", identity.NonceLockName);
            return await UseResourceAsync(await command.ExecuteReaderAsync(token).ConfigureAwait(false), async reader =>
            {
                if (!await reader.ReadAsync(token).ConfigureAwait(false) ||
                    reader.IsDBNull(0) || reader.IsDBNull(1) || reader.IsDBNull(2) || reader.IsDBNull(3)) return false;
                return string.Equals(reader.GetString(0), identity.Database, StringComparison.Ordinal) &&
                    (!requirePhysicalOwner || reader.GetInt64(1) == identity.ConnectionId) &&
                    reader.GetInt64(2) == identity.ConnectionId && reader.GetInt64(3) == identity.ConnectionId;
            }).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}
