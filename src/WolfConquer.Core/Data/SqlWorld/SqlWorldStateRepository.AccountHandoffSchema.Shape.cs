using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    private static ValueTask<List<(string Name, string? Type, string? Engine)>> ReadAccountHandoffSchemaTablesAsync(
        MySqlConnection actual, CancellationToken token) =>
        UseResourceAsync(actual.CreateCommand(), async command =>
        {
            // Include views and case aliases in presence detection, before DDL.
            command.CommandText = """
                SELECT table_name,table_type,engine FROM information_schema.tables
                WHERE BINARY table_schema=BINARY DATABASE() AND LOWER(table_name) IN
                    ('wolf_sql_auth_handoff_schema','wolf_sql_auth_handoff')
                ORDER BY BINARY table_name LIMIT 3
                """;
            return await UseResourceAsync(await command.ExecuteReaderAsync(token).ConfigureAwait(false), async rows =>
            {
                var result = new List<(string, string?, string?)>();
                while (await rows.ReadAsync(token).ConfigureAwait(false))
                    result.Add((rows.GetString(0), rows.IsDBNull(1) ? null : rows.GetString(1),
                        rows.IsDBNull(2) ? null : rows.GetString(2)));
                return result;
            }).ConfigureAwait(false);
        });

    private static async ValueTask VerifyAccountHandoffSchemaShapeAsync(MySqlConnection actual, CancellationToken token)
    {
        var tables = await ReadAccountHandoffSchemaTablesAsync(actual, token).ConfigureAwait(false);
        if (tables.Count != 2) throw new InvalidOperationException("sqlAccountHandoffSchemaPartial");
        string[] names = ["wolf_sql_auth_handoff", "wolf_sql_auth_handoff_schema"];
        for (int index = 0; index < names.Length; index++)
            if (!string.Equals(tables[index].Name, names[index], StringComparison.Ordinal) ||
                !string.Equals(tables[index].Type, "BASE TABLE", StringComparison.Ordinal) ||
                !string.Equals(tables[index].Engine, "InnoDB", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("sqlAccountHandoffSchemaEngineMismatch");
        await VerifyAccountHandoffSchemaColumnsAsync(actual, token).ConfigureAwait(false);
        await VerifyAccountHandoffSchemaIndexesAsync(actual, token).ConfigureAwait(false);
        await VerifyAccountHandoffSchemaNoForeignKeysAsync(actual, token).ConfigureAwait(false);
    }

    private static ValueTask VerifyAccountHandoffSchemaIndexesAsync(MySqlConnection actual, CancellationToken token) =>
        UseResourceAsync(actual.CreateCommand(), async command =>
        {
            command.CommandText = """
                SELECT table_name,index_name,non_unique,seq_in_index,column_name,
                       sub_part,index_type,`collation`,nullable
                FROM information_schema.statistics WHERE BINARY table_schema=BINARY DATABASE() AND LOWER(table_name) IN
                    ('wolf_sql_auth_handoff_schema','wolf_sql_auth_handoff')
                ORDER BY BINARY table_name,BINARY index_name,seq_in_index LIMIT 3
                """;
            await UseResourceAsync(await command.ExecuteReaderAsync(token).ConfigureAwait(false), async rows =>
            {
                string[] tables = ["wolf_sql_auth_handoff", "wolf_sql_auth_handoff_schema"];
                string[] columns = ["token_hash", "singleton"];
                int index = 0;
                while (await rows.ReadAsync(token).ConfigureAwait(false))
                {
                    const string reason = "sqlAccountHandoffSchemaIndexesMismatch";
                    if (index >= tables.Length || rows.IsDBNull(0) || rows.IsDBNull(1) || rows.IsDBNull(4) ||
                        rows.IsDBNull(6) || rows.IsDBNull(7) || rows.IsDBNull(8) ||
                        !string.Equals(rows.GetString(0), tables[index], StringComparison.Ordinal) ||
                        !string.Equals(rows.GetString(1), "PRIMARY", StringComparison.Ordinal) ||
                        ReadBinaryLockScalar(rows.GetValue(2), reason) || rows.GetInt32(3) != 1 ||
                        !string.Equals(rows.GetString(4), columns[index], StringComparison.Ordinal) ||
                        !rows.IsDBNull(5) || !string.Equals(rows.GetString(6), "BTREE", StringComparison.Ordinal) ||
                        !string.Equals(rows.GetString(7), "A", StringComparison.Ordinal) || rows.GetString(8).Length != 0)
                        throw new InvalidOperationException(reason);
                    index++;
                }
                if (index != tables.Length) throw new InvalidOperationException("sqlAccountHandoffSchemaIndexesMismatch");
            }).ConfigureAwait(false);
        });

    private static ValueTask VerifyAccountHandoffSchemaNoForeignKeysAsync(MySqlConnection actual, CancellationToken token) =>
        UseResourceAsync(actual.CreateCommand(), async command =>
        {
            command.CommandText = """
                SELECT 1 FROM information_schema.key_column_usage
                WHERE BINARY table_schema=BINARY DATABASE() AND LOWER(table_name) IN
                    ('wolf_sql_auth_handoff_schema','wolf_sql_auth_handoff')
                  AND referenced_table_name IS NOT NULL LIMIT 1
                """;
            await UseResourceAsync(await command.ExecuteReaderAsync(token).ConfigureAwait(false), async rows =>
            {
                if (await rows.ReadAsync(token).ConfigureAwait(false))
                    throw new InvalidOperationException("sqlAccountHandoffSchemaForeignKeyMismatch");
            }).ConfigureAwait(false);
        });
}
