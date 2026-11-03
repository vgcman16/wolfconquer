using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    private static async ValueTask<bool> TryAcquireAccountLocksAsync(MySqlConnection connection,
        IReadOnlyList<long> accountIds, CancellationToken token)
    {
        string[] names = await AccountLockNamesAsync(connection, accountIds, null, token).ConfigureAwait(false);
        foreach (string name in names)
        {
            object? value = await UseResourceAsync(connection.CreateCommand(), async command =>
            {
                command.CommandText = "SELECT CAST(GET_LOCK(@name,0) AS SIGNED)";
                command.Parameters.AddWithValue("@name", name);
                return await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            }).ConfigureAwait(false);
            if (!ReadBinaryLockScalar(value, "sqlWorldAccountLockAcquisitionInvalid")) return false;
        }
        // A server that replaces an earlier named lock cannot authorize a pair.
        if (!await VerifyAccountLocksAsync(connection, accountIds, token).ConfigureAwait(false))
            throw new InvalidOperationException("sqlWorldAccountLocksNotHeld");
        return true;
    }

    private static ValueTask<bool> VerifyAccountLocksAsync(MySqlConnection connection,
        IReadOnlyList<long> accountIds, CancellationToken token) =>
        VerifyAccountLocksAsync(connection, accountIds, null, token);

    private static async ValueTask<bool> VerifyAccountLocksAsync(MySqlConnection connection,
        IReadOnlyList<long> accountIds, MySqlTransaction? transaction, CancellationToken token)
    {
        string[] names = await AccountLockNamesAsync(connection, accountIds, transaction, token).ConfigureAwait(false);
        foreach (string name in names)
        {
            object? value = await UseResourceAsync(connection.CreateCommand(), async command =>
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT CAST(CASE WHEN IS_USED_LOCK(@name)=CONNECTION_ID() THEN 1 ELSE 0 END AS SIGNED)";
                command.Parameters.AddWithValue("@name", name);
                return await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            }).ConfigureAwait(false);
            if (!ReadBinaryLockScalar(value, "sqlWorldAccountLockVerificationInvalid")) return false;
        }
        return true;
    }

    private static async ValueTask RequireAccountLocksAsync(MySqlConnection connection, MySqlTransaction transaction,
        IReadOnlyList<long> accountIds, CancellationToken token)
    {
        if (!await VerifyAccountLocksAsync(connection, accountIds, transaction, token).ConfigureAwait(false))
            throw new InvalidOperationException("sqlWorldSessionCustodyLost");
    }

    // The provider chooses an integer CLR width from server column metadata.
    // Accept only exact binary integer values, never Convert/GetInt64 coercions.
    internal static bool ReadBinaryLockScalar(object? value, string invalidReason) => value switch
    {
        sbyte number when number is 0 or 1 => number == 1,
        byte number when number is 0 or 1 => number == 1,
        short number when number is 0 or 1 => number == 1,
        ushort number when number is 0 or 1 => number == 1,
        int number when number is 0 or 1 => number == 1,
        uint number when number is 0 or 1 => number == 1,
        long number when number is 0 or 1 => number == 1,
        ulong number when number is 0 or 1 => number == 1,
        _ => throw new InvalidOperationException(invalidReason),
    };

    private static async ValueTask<string[]> AccountLockNamesAsync(MySqlConnection connection,
        IReadOnlyList<long> accountIds, MySqlTransaction? transaction, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(accountIds);
        long[] accounts = accountIds.ToArray();
        if (accounts.Length is < 1 or > 2 || accounts.Any(account => account <= 0))
            throw new ArgumentException("SQL account lock participants are invalid.", nameof(accountIds));
        accounts = accounts.Distinct().OrderBy(account => account).ToArray();
        string database = await UseResourceAsync(connection.CreateCommand(), async command =>
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT DATABASE()";
            object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            if (value is not string name || name.Length == 0)
                throw new InvalidOperationException("sqlWorldAccountLockDatabaseMissing");
            return name;
        }).ConfigureAwait(false);
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(database.ToLowerInvariant())))[..24];
        return accounts.Select(account => "wolf:sql:" + digest + ":a:" + account.ToString(CultureInfo.InvariantCulture)).ToArray();
    }
}
