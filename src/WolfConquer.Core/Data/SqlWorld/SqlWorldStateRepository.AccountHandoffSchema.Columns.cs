using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    private readonly record struct AccountHandoffColumn(string Table, int Ordinal, string Name,
        string Type, bool Unsigned = false, int? ByteLength = null, int? Precision = null, bool Nullable = false);

    private static readonly AccountHandoffColumn[] AccountHandoffColumns =
    [
        new("wolf_sql_auth_handoff", 1, "token_hash", "binary", ByteLength: 32),
        new("wolf_sql_auth_handoff", 2, "account_id", "bigint", Unsigned: true),
        new("wolf_sql_auth_handoff", 3, "character_uid", "int", Unsigned: true),
        new("wolf_sql_auth_handoff", 4, "username", "varbinary", ByteLength: 1024),
        new("wolf_sql_auth_handoff", 5, "endpoint_host", "varbinary", ByteLength: 1024),
        new("wolf_sql_auth_handoff", 6, "endpoint_port", "int", Unsigned: true),
        new("wolf_sql_auth_handoff", 7, "issuer_connection_id", "bigint", Unsigned: true),
        new("wolf_sql_auth_handoff", 8, "handoff_claim", "varbinary", ByteLength: 64),
        new("wolf_sql_auth_handoff", 9, "credential_salt", "binary", ByteLength: 32),
        new("wolf_sql_auth_handoff", 10, "credential_fingerprint", "binary", ByteLength: 32),
        new("wolf_sql_auth_handoff", 11, "issued_utc", "datetime", Precision: 6),
        new("wolf_sql_auth_handoff", 12, "expires_utc", "datetime", Precision: 6),
        new("wolf_sql_auth_handoff", 13, "redeemed_utc", "datetime", Precision: 6, Nullable: true),
        new("wolf_sql_auth_handoff", 14, "redeemed_connection_id", "bigint", Unsigned: true, Nullable: true),
        new("wolf_sql_auth_handoff_schema", 1, "singleton", "tinyint", Unsigned: true),
        new("wolf_sql_auth_handoff_schema", 2, "version", "int")
    ];

    private static ValueTask VerifyAccountHandoffSchemaColumnsAsync(MySqlConnection actual, CancellationToken token) =>
        UseResourceAsync(actual.CreateCommand(), async command =>
        {
            command.CommandText = """
                SELECT table_name,ordinal_position,column_name,data_type,column_type,
                       character_maximum_length,character_octet_length,datetime_precision,
                       is_nullable,extra,character_set_name,collation_name,column_default
                FROM information_schema.columns WHERE BINARY table_schema=BINARY DATABASE() AND LOWER(table_name) IN
                    ('wolf_sql_auth_handoff_schema','wolf_sql_auth_handoff')
                ORDER BY BINARY table_name,ordinal_position LIMIT 17
                """;
            await UseResourceAsync(await command.ExecuteReaderAsync(token).ConfigureAwait(false), async rows =>
            {
                int index = 0;
                while (await rows.ReadAsync(token).ConfigureAwait(false))
                {
                    if (index >= AccountHandoffColumns.Length || !AccountHandoffColumnMatches(rows, AccountHandoffColumns[index]))
                        throw new InvalidOperationException("sqlAccountHandoffSchemaColumnsMismatch");
                    index++;
                }
                if (index != AccountHandoffColumns.Length)
                    throw new InvalidOperationException("sqlAccountHandoffSchemaColumnsMismatch");
            }).ConfigureAwait(false);
        });

    private static bool AccountHandoffColumnMatches(MySqlDataReader row, AccountHandoffColumn expected)
    {
        if (row.IsDBNull(0) || row.IsDBNull(1) || row.IsDBNull(2) || row.IsDBNull(3) ||
            row.IsDBNull(4) || row.IsDBNull(8) || row.IsDBNull(9)) return false;
        string definition = row.GetString(4);
        bool unsigned = definition.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("unsigned", StringComparer.OrdinalIgnoreCase);
        return string.Equals(row.GetString(0), expected.Table, StringComparison.Ordinal) &&
            row.GetInt32(1) == expected.Ordinal && string.Equals(row.GetString(2), expected.Name, StringComparison.Ordinal) &&
            string.Equals(row.GetString(3), expected.Type, StringComparison.OrdinalIgnoreCase) && unsigned == expected.Unsigned &&
            !definition.Contains("zerofill", StringComparison.OrdinalIgnoreCase) &&
            AccountHandoffMetadataNumberMatches(row, 5, expected.ByteLength) &&
            AccountHandoffMetadataNumberMatches(row, 6, expected.ByteLength) &&
            AccountHandoffMetadataNumberMatches(row, 7, expected.Precision) &&
            string.Equals(row.GetString(8), expected.Nullable ? "YES" : "NO", StringComparison.Ordinal) &&
            row.GetString(9).Length == 0 && row.IsDBNull(10) && row.IsDBNull(11) &&
            AccountHandoffDefaultMatches(row, expected.Nullable);
    }

    private static bool AccountHandoffMetadataNumberMatches(MySqlDataReader row, int ordinal, int? expected) =>
        expected is int number ? !row.IsDBNull(ordinal) && row.GetInt64(ordinal) == number : row.IsDBNull(ordinal);

    private static bool AccountHandoffDefaultMatches(MySqlDataReader row, bool nullable) =>
        row.IsDBNull(12) || (nullable && string.Equals(row.GetString(12), "NULL", StringComparison.Ordinal));
}
