using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    // Explicit installation only. Ordinary issue/redeem callers use the verifier.
    public async ValueTask EnsureAccountHandoffSchemaAsync(CancellationToken token = default)
    {
        await UseResourceAsync(CreateConnection(), async actual =>
        {
            await actual.OpenAsync(token).ConfigureAwait(false);
            string database = await ReadAccountHandoffSchemaDatabaseAsync(actual, token).ConfigureAwait(false);
            string installerLock = await AcquireAccountHandoffSchemaLockAsync(actual, database, token).ConfigureAwait(false);
            await RequireAccountHandoffSchemaLockAsync(actual, database, installerLock, token).ConfigureAwait(false);

            int present = (await ReadAccountHandoffSchemaTablesAsync(actual, token).ConfigureAwait(false)).Count;
            if (present == 2)
            {
                await VerifyAccountHandoffSchemaAsync(actual, token).ConfigureAwait(false);
                await RequireAccountHandoffSchemaLockAsync(actual, database, installerLock, token).ConfigureAwait(false);
                return;
            }
            if (present != 0) throw new InvalidOperationException("sqlAccountHandoffSchemaPartial");

            // DDL is not transactional. Any interrupted prefix stays partial and
            // is refused by a later invocation; there is no IF NOT EXISTS repair.
            foreach (string ddl in AccountHandoffSchemaStatements)
            {
                await RequireAccountHandoffSchemaLockAsync(actual, database, installerLock, token).ConfigureAwait(false);
                await UseResourceAsync(actual.CreateCommand(), async command =>
                {
                    command.CommandText = ddl;
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
            await VerifyAccountHandoffSchemaShapeAsync(actual, token).ConfigureAwait(false);
            await RequireAccountHandoffSchemaLockAsync(actual, database, installerLock, token).ConfigureAwait(false);
            await UseResourceAsync(actual.CreateCommand(), async command =>
            {
                command.CommandText = "INSERT INTO wolf_sql_auth_handoff_schema (singleton,version) VALUES (1,1)";
                if (await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1)
                    throw new InvalidOperationException("sqlAccountHandoffSchemaVersionMismatch");
            }).ConfigureAwait(false);
            await VerifyAccountHandoffSchemaAsync(actual, token).ConfigureAwait(false);
            await RequireAccountHandoffSchemaLockAsync(actual, database, installerLock, token).ConfigureAwait(false);
            // The repository's nonpooled physical connection owns this lock.
            // Disposal releases it even after a broken connection; no RELEASE query.
        }).ConfigureAwait(false);
    }

    // Called on the real open connection before BeginTransaction. It owns only
    // its commands/readers, never the caller's connection, and performs no writes.
    internal static async ValueTask VerifyAccountHandoffSchemaAsync(MySqlConnection actual, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(actual);
        await ReadAccountHandoffSchemaDatabaseAsync(actual, token).ConfigureAwait(false);
        await VerifyAccountHandoffSchemaShapeAsync(actual, token).ConfigureAwait(false);
        await UseResourceAsync(actual.CreateCommand(), async command =>
        {
            command.CommandText = "SELECT singleton,version FROM wolf_sql_auth_handoff_schema ORDER BY singleton LIMIT 2";
            await UseResourceAsync(await command.ExecuteReaderAsync(token).ConfigureAwait(false), async rows =>
            {
                const string reason = "sqlAccountHandoffSchemaVersionMismatch";
                if (!await rows.ReadAsync(token).ConfigureAwait(false) ||
                    !ReadBinaryLockScalar(rows.GetValue(0), reason) ||
                    !ReadBinaryLockScalar(rows.GetValue(1), reason) ||
                    await rows.ReadAsync(token).ConfigureAwait(false))
                    throw new InvalidOperationException(reason);
            }).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private static readonly string[] AccountHandoffSchemaStatements =
    [
        "CREATE TABLE wolf_sql_auth_handoff_schema (singleton TINYINT UNSIGNED NOT NULL PRIMARY KEY, version INT NOT NULL) ENGINE=InnoDB",
        """
        CREATE TABLE wolf_sql_auth_handoff (
            token_hash BINARY(32) NOT NULL PRIMARY KEY,
            account_id BIGINT UNSIGNED NOT NULL,
            character_uid INT UNSIGNED NOT NULL,
            username VARBINARY(1024) NOT NULL,
            endpoint_host VARBINARY(1024) NOT NULL,
            endpoint_port INT UNSIGNED NOT NULL,
            issuer_connection_id BIGINT UNSIGNED NOT NULL,
            handoff_claim VARBINARY(64) NOT NULL,
            credential_salt BINARY(32) NOT NULL,
            credential_fingerprint BINARY(32) NOT NULL,
            issued_utc DATETIME(6) NOT NULL,
            expires_utc DATETIME(6) NOT NULL,
            redeemed_utc DATETIME(6) NULL,
            redeemed_connection_id BIGINT UNSIGNED NULL
        ) ENGINE=InnoDB
        """
    ];
}
