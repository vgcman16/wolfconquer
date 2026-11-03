using System.Data;
using System.Security.Cryptography;
using System.Text;
using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    private static ValueTask<string> ReadAccountHandoffSchemaDatabaseAsync(MySqlConnection actual, CancellationToken token)
    {
        if (actual.State != ConnectionState.Open)
            throw new InvalidOperationException("sqlAccountHandoffSchemaConnectionUnavailable");
        return UseResourceAsync(actual.CreateCommand(), async command =>
        {
            command.CommandText = "SELECT DATABASE()";
            object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            return value is string database && database.Length > 0 ? database :
                throw new InvalidOperationException("sqlAccountHandoffSchemaDatabaseMissing");
        });
    }

    private static async ValueTask<string> AcquireAccountHandoffSchemaLockAsync(
        MySqlConnection actual, string database, CancellationToken token)
    {
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(database.ToLowerInvariant())))[..24];
        string name = "wolf:sql:" + digest + ":handoff-schema:1";
        object? result = await UseResourceAsync(actual.CreateCommand(), async command =>
        {
            command.CommandText = "SELECT CAST(GET_LOCK(@name,0) AS SIGNED)";
            command.Parameters.AddWithValue("@name", name);
            return await command.ExecuteScalarAsync(token).ConfigureAwait(false);
        }).ConfigureAwait(false);
        if (!ReadBinaryLockScalar(result, "sqlAccountHandoffSchemaLockAcquisitionInvalid"))
            throw new InvalidOperationException("sqlAccountHandoffSchemaInstallerBusy");
        return name;
    }

    private static ValueTask RequireAccountHandoffSchemaLockAsync(
        MySqlConnection actual, string database, string installerLock, CancellationToken token) =>
        UseResourceAsync(actual.CreateCommand(), async command =>
        {
            command.CommandText = "SELECT DATABASE(),CAST(CASE WHEN IS_USED_LOCK(@name)=CONNECTION_ID() THEN 1 ELSE 0 END AS SIGNED)";
            command.Parameters.AddWithValue("@name", installerLock);
            await UseResourceAsync(await command.ExecuteReaderAsync(token).ConfigureAwait(false), async rows =>
            {
                const string reason = "sqlAccountHandoffSchemaInstallerLockLost";
                if (!await rows.ReadAsync(token).ConfigureAwait(false) || rows.IsDBNull(0) ||
                    !string.Equals(rows.GetString(0), database, StringComparison.Ordinal) ||
                    !ReadBinaryLockScalar(rows.GetValue(1), "sqlAccountHandoffSchemaLockVerificationInvalid") ||
                    await rows.ReadAsync(token).ConfigureAwait(false))
                    throw new InvalidOperationException(reason);
            }).ConfigureAwait(false);
        });
}
