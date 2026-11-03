using System.Security.Cryptography;
using System.Text;
using MySqlConnector;

namespace WolfConquer.Tests;

internal sealed partial class OwnedModernSqlAuthDatabase
{
    // Test ordering only: no owner/connection/claim is returned to production.
    // OpenOwnedAuthenticationDomainAsync proves the actual selected schema and
    // nonce marker. Its existing resource wrapper retains primary+cleanup errors.
    internal Task WithSqlHandoffInstallerLockHeldAsync(Func<Task> body, CancellationToken token) =>
        WithAuthenticationConnectionAsync(async actual =>
        {
            string name = await SqlHandoffSchemaSafetyLockNameAsync(actual, token);
            var acquired = await ReadSqlHandoffSchemaSafetyLockAsync(actual, name, SchemaSafetyLockRead.Acquire, token);
            if (acquired.ConnectionId <= 0 || acquired.Value != 1)
                throw new InvalidOperationException("Owned handoff installer lock was not acquired.");
            await RequireSqlHandoffSchemaSafetyHolderAsync(actual, name, acquired.ConnectionId, token);
            await body();
            await RequireSqlHandoffSchemaSafetyHolderAsync(actual, name, acquired.ConnectionId, token);
            var released = await ReadSqlHandoffSchemaSafetyLockAsync(actual, name, SchemaSafetyLockRead.Release, token);
            if (released.ConnectionId != acquired.ConnectionId || released.Value != 1)
                throw new InvalidOperationException("Owned handoff installer lock release was not confirmed.");
            if ((await ReadSqlHandoffSchemaSafetyLockAsync(actual, name, SchemaSafetyLockRead.Holder, token)).Value is not null)
                throw new InvalidOperationException("Owned handoff installer lock is still held after release.");
            return true;
        });

    internal Task AssertSqlHandoffInstallerLockFreeAsync(CancellationToken token) =>
        WithAuthenticationConnectionAsync(async actual =>
        {
            string name = await SqlHandoffSchemaSafetyLockNameAsync(actual, token);
            if ((await ReadSqlHandoffSchemaSafetyLockAsync(actual, name, SchemaSafetyLockRead.Holder, token)).Value is not null)
                throw new InvalidOperationException("A real handoff installer lock remains held.");
            return true;
        });

    private async Task<string> SqlHandoffSchemaSafetyLockNameAsync(MySqlConnection actual, CancellationToken token) =>
        await WithAuthenticationResourceAsync(actual.CreateCommand(), async command =>
        {
            command.CommandText = "SELECT DATABASE()";
            object? value = await command.ExecuteScalarAsync(token);
            if (value is not string database || !string.Equals(database, SchemaName, StringComparison.Ordinal))
                throw new InvalidOperationException("Owned handoff installer database changed.");
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(database.ToLowerInvariant())))[..24];
            return "wolf:sql:" + hash + ":handoff-schema:1";
        });

    private static async Task RequireSqlHandoffSchemaSafetyHolderAsync(
        MySqlConnection actual, string name, long connectionId, CancellationToken token)
    {
        var observed = await ReadSqlHandoffSchemaSafetyLockAsync(actual, name, SchemaSafetyLockRead.Holder, token);
        if (observed.ConnectionId != connectionId || observed.Value != connectionId)
            throw new InvalidOperationException("The actual fixture connection lost its installer lock.");
    }

    private enum SchemaSafetyLockRead { Acquire, Holder, Release }

    private static Task<(long ConnectionId, long? Value)> ReadSqlHandoffSchemaSafetyLockAsync(
        MySqlConnection actual, string name, SchemaSafetyLockRead operation, CancellationToken token) =>
        WithAuthenticationResourceAsync(actual.CreateCommand(), async command =>
        {
            command.CommandText = operation switch
            {
                SchemaSafetyLockRead.Acquire => "SELECT CAST(CONNECTION_ID() AS SIGNED),CAST(GET_LOCK(@name,0) AS SIGNED)",
                SchemaSafetyLockRead.Holder => "SELECT CAST(CONNECTION_ID() AS SIGNED),CAST(IS_USED_LOCK(@name) AS SIGNED)",
                SchemaSafetyLockRead.Release => "SELECT CAST(CONNECTION_ID() AS SIGNED),CAST(RELEASE_LOCK(@name) AS SIGNED)",
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
            command.Parameters.AddWithValue("@name", name);
            return await WithAuthenticationResourceAsync(await command.ExecuteReaderAsync(token), async rows =>
            {
                if (!await rows.ReadAsync(token) || rows.IsDBNull(0))
                    throw new InvalidOperationException("Owned installer lock observation is missing.");
                long physical = rows.GetInt64(0);
                long? value = rows.IsDBNull(1) ? null : rows.GetInt64(1);
                if (await rows.ReadAsync(token))
                    throw new InvalidOperationException("Owned installer lock observation is ambiguous.");
                return (physical, value);
            });
        });

    internal Task CreateSqlHandoffMarkerRejectUpdateTriggerAsync(CancellationToken token) =>
        WithAuthenticationConnectionAsync(async actual =>
            await WithAuthenticationResourceAsync(actual.CreateCommand(), async command =>
            {
                command.CommandText = """
                    CREATE TRIGGER __wolf_handoff_marker_no_update
                    BEFORE UPDATE ON wolf_sql_auth_handoff_schema FOR EACH ROW
                    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='owned_handoff_marker_update_forbidden'
                    """;
                await command.ExecuteNonQueryAsync(token);
                return true;
            }));

    internal Task AttemptSqlHandoffMarkerNoOpUpdateAsync(CancellationToken token) =>
        WithAuthenticationConnectionAsync(async actual =>
            await WithAuthenticationResourceAsync(actual.CreateCommand(), async command =>
            {
                command.CommandText = "UPDATE wolf_sql_auth_handoff_schema SET version=version WHERE singleton=1";
                await command.ExecuteNonQueryAsync(token);
                return true;
            }));

    internal Task<string[]> SnapshotSqlHandoffSchemaSafetyObjectsAsync() =>
        WithAuthenticationConnectionAsync(async actual =>
            await WithAuthenticationResourceAsync(actual.CreateCommand(), async command =>
            {
                command.CommandText = """
                    SELECT 'table',table_name,table_type,COALESCE(engine,'<NULL>'),''
                    FROM information_schema.tables WHERE BINARY table_schema=BINARY DATABASE()
                    UNION ALL
                    SELECT 'trigger',trigger_name,event_object_table,action_timing,action_statement
                    FROM information_schema.triggers WHERE BINARY trigger_schema=BINARY DATABASE()
                    ORDER BY 1,2
                    """;
                return await WithAuthenticationResourceAsync(await command.ExecuteReaderAsync(), async rows =>
                {
                    var result = new List<string>();
                    while (await rows.ReadAsync())
                        result.Add(string.Join('|', Enumerable.Range(0, 5).Select(rows.GetString)));
                    return result.ToArray();
                });
            }));
}
