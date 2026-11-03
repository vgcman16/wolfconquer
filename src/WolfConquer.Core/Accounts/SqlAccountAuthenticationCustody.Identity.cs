using System.Data;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using MySqlConnector;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Core.Accounts;

public sealed partial class SqlAccountAuthenticationCustody
{
    private sealed class Identity
    {
        internal Identity(string database, long connectionId, string accountLock, string claimLock)
        {
            Database = database; ConnectionId = connectionId; AccountLock = accountLock; ClaimLock = claimLock;
        }

        internal string Database { get; }
        internal long ConnectionId { get; }
        internal string AccountLock { get; }
        internal string ClaimLock { get; }
    }

    private static async ValueTask<(string Database, long ConnectionId)> ReadPhysicalIdentityAsync(MySqlConnection connection, CancellationToken token) =>
        await UseSqlAsync<MySqlCommand, (string, long)>(connection.CreateCommand(), async command =>
        {
            command.CommandText = "SELECT DATABASE(),CAST(CONNECTION_ID() AS SIGNED)";
            return await UseSqlAsync<MySqlDataReader, (string, long)>(await command.ExecuteReaderAsync(token).ConfigureAwait(false), async reader =>
            {
                if (!await reader.ReadAsync(token).ConfigureAwait(false) || reader.IsDBNull(0) || reader.IsDBNull(1))
                    throw new InvalidOperationException("sqlAuthenticationPhysicalIdentityMissing");
                string database = reader.GetString(0); long id = reader.GetInt64(1);
                if (database.Length == 0 || id <= 0 || await reader.ReadAsync(token).ConfigureAwait(false))
                    throw new InvalidOperationException("sqlAuthenticationPhysicalIdentityInvalid");
                return (database, id);
            }).ConfigureAwait(false);
        }).ConfigureAwait(false);

    private static string AccountLockName(string database, long accountId)
    {
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(database.ToLowerInvariant())))[..24];
        return "wolf:auth:" + hash + ":a:" + accountId.ToString(CultureInfo.InvariantCulture);
    }

    private static string NewClaimLockName()
    {
        byte[] random = RandomNumberGenerator.GetBytes(24);
        try { return "wolf:auth:c:" + Convert.ToHexString(random); }
        finally { CryptographicOperations.ZeroMemory(random); }
    }

    private static async ValueTask<bool> AcquireClaimAsync(MySqlConnection connection, string name, CancellationToken token)
    {
        object? value = await UseSqlAsync<MySqlCommand, object?>(connection.CreateCommand(), async command =>
        {
            command.CommandText = "SELECT CAST(GET_LOCK(@name,0) AS SIGNED)";
            command.Parameters.AddWithValue("@name", name);
            return await command.ExecuteScalarAsync(token).ConfigureAwait(false);
        }).ConfigureAwait(false);
        return SqlWorldStateRepository.ReadBinaryLockScalar(value, "sqlAuthenticationClaimAcquisitionInvalid");
    }

    private static async ValueTask<bool> ClaimsAreCurrentAsync(MySqlConnection connection, MySqlTransaction? transaction,
        Identity identity, bool requirePhysicalOwner, CancellationToken token)
    {
        if (connection.State != ConnectionState.Open) return false;
        return await UseSqlAsync<MySqlCommand, bool>(connection.CreateCommand(), async command =>
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT DATABASE(),CAST(CONNECTION_ID() AS SIGNED)," +
                "CAST(IS_USED_LOCK(@account) AS SIGNED),CAST(IS_USED_LOCK(@claim) AS SIGNED)";
            command.Parameters.AddWithValue("@account", identity.AccountLock);
            command.Parameters.AddWithValue("@claim", identity.ClaimLock);
            return await UseSqlAsync<MySqlDataReader, bool>(await command.ExecuteReaderAsync(token).ConfigureAwait(false), async reader =>
            {
                if (!await reader.ReadAsync(token).ConfigureAwait(false) || reader.IsDBNull(0) || reader.IsDBNull(1))
                    throw new InvalidOperationException("sqlAuthenticationPhysicalIdentityMissing");
                string database = reader.GetString(0); long current = reader.GetInt64(1);
                if (database.Length == 0 || current <= 0) throw new InvalidOperationException("sqlAuthenticationPhysicalIdentityInvalid");
                bool matches = string.Equals(database, identity.Database, StringComparison.Ordinal) &&
                    (!requirePhysicalOwner || current == identity.ConnectionId) &&
                    !reader.IsDBNull(2) && !reader.IsDBNull(3) &&
                    reader.GetInt64(2) == identity.ConnectionId && reader.GetInt64(3) == identity.ConnectionId;
                if (await reader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("sqlAuthenticationPhysicalIdentityInvalid");
                return matches;
            }).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    // Actual provider resource lifetimes only; cleanup never replaces an I/O failure.
    private static async ValueTask<TResult> UseSqlAsync<TResource, TResult>(TResource resource, Func<TResource, Task<TResult>> use)
        where TResource : IAsyncDisposable
    {
        Exception? primary = null, cleanup = null; TResult result = default!;
        try { result = await use(resource).ConfigureAwait(false); }
        catch (Exception error) { primary = error; }
        try { await resource.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
        return result;
    }
}
