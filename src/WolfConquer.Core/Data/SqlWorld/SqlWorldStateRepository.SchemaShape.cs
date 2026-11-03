using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    private static async ValueTask VerifySchemaColumnsAsync(MySqlConnection connection, CancellationToken token)
    {
        string[] expected =
        [
            "wolf_sql_world_item_uid|1|item_uid|int|unsigned|NO", "wolf_sql_world_item_uid|2|account_id|bigint||NO",
            "wolf_sql_world_operation|1|operation_id|binary|16|NO", "wolf_sql_world_operation|2|descriptor_hash|binary|32|NO", "wolf_sql_world_operation|3|descriptor|longblob||NO",
            "wolf_sql_world_schema|1|singleton|tinyint||NO", "wolf_sql_world_schema|2|version|int||NO",
            "wolf_sql_world_state|1|account_id|bigint||NO", "wolf_sql_world_state|2|character_uid|int|unsigned|NO", "wolf_sql_world_state|3|revision|bigint||NO", "wolf_sql_world_state|4|character_image|longblob||NO", "wolf_sql_world_state|5|item_image|longblob||NO"
        ];
        await UseResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = """
                SELECT table_name, ordinal_position, column_name, data_type, column_type, character_maximum_length, is_nullable, extra
                FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name IN
                ('wolf_sql_world_schema','wolf_sql_world_state','wolf_sql_world_item_uid','wolf_sql_world_operation')
                ORDER BY table_name, ordinal_position
                """;
            var found = new List<string>();
            var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            await UseResourceAsync(reader, async rows =>
            {
                while (await rows.ReadAsync(token).ConfigureAwait(false))
                {
                    string type = rows.GetString(3).ToLowerInvariant();
                    string detail = type == "binary" ? rows.GetInt64(5).ToString(System.Globalization.CultureInfo.InvariantCulture) :
                        rows.GetString(4).Contains("unsigned", StringComparison.OrdinalIgnoreCase) ? "unsigned" : "";
                    if (!string.IsNullOrEmpty(rows.GetString(7))) throw new InvalidOperationException("sqlWorldSchemaColumnExtra");
                    found.Add($"{rows.GetString(0)}|{rows.GetInt32(1)}|{rows.GetString(2)}|{type}|{detail}|{rows.GetString(6)}");
                }
            }).ConfigureAwait(false);
            if (!found.SequenceEqual(expected, StringComparer.Ordinal)) throw new InvalidOperationException("sqlWorldSchemaColumnsMismatch");
        }).ConfigureAwait(false);
    }

    private static async ValueTask VerifySchemaIndexesAsync(MySqlConnection connection, CancellationToken token)
    {
        string[] expected = ["wolf_sql_world_item_uid|PRIMARY|0|1|item_uid", "wolf_sql_world_item_uid|ix_world_item_account|1|1|account_id", "wolf_sql_world_operation|PRIMARY|0|1|operation_id", "wolf_sql_world_schema|PRIMARY|0|1|singleton", "wolf_sql_world_state|PRIMARY|0|1|account_id", "wolf_sql_world_state|ux_world_character|0|1|character_uid"];
        await UseResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = """
                SELECT table_name, index_name, non_unique, seq_in_index, column_name, sub_part
                FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name IN
                ('wolf_sql_world_schema','wolf_sql_world_state','wolf_sql_world_item_uid','wolf_sql_world_operation')
                ORDER BY BINARY table_name, BINARY index_name, seq_in_index
                """;
            var found = new List<string>();
            var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            await UseResourceAsync(reader, async rows =>
            {
                while (await rows.ReadAsync(token).ConfigureAwait(false))
                {
                    if (!rows.IsDBNull(5)) throw new InvalidOperationException("sqlWorldSchemaIndexPrefix");
                    found.Add($"{rows.GetString(0)}|{rows.GetString(1)}|{rows.GetInt32(2)}|{rows.GetInt32(3)}|{rows.GetString(4)}");
                }
            }).ConfigureAwait(false);
            if (!found.SequenceEqual(expected, StringComparer.Ordinal)) throw new InvalidOperationException("sqlWorldSchemaIndexesMismatch");
            command.CommandText = """
            SELECT k.table_name,k.constraint_name,k.column_name,k.referenced_table_schema,
                   k.referenced_table_name,k.referenced_column_name,r.update_rule,r.delete_rule,DATABASE()
            FROM information_schema.key_column_usage k
            JOIN information_schema.referential_constraints r
              ON r.constraint_schema=k.constraint_schema AND r.table_name=k.table_name
                 AND r.constraint_name=k.constraint_name
            WHERE k.table_schema=DATABASE() AND k.table_name IN
            ('wolf_sql_world_schema','wolf_sql_world_state','wolf_sql_world_item_uid','wolf_sql_world_operation')
              AND k.referenced_table_name IS NOT NULL
            """;
            var fk = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            await UseResourceAsync(fk, async actual =>
        {
                if (!await actual.ReadAsync(token).ConfigureAwait(false) || actual.GetString(0) != "wolf_sql_world_item_uid" ||
            actual.GetString(1) != "fk_world_item_account" || actual.GetString(2) != "account_id" ||
            actual.GetString(3) != actual.GetString(8) || actual.GetString(4) != "wolf_sql_world_state" ||
            actual.GetString(5) != "account_id" || actual.GetString(6) != "RESTRICT" || actual.GetString(7) != "RESTRICT" ||
            await actual.ReadAsync(token).ConfigureAwait(false))
            throw new InvalidOperationException("sqlWorldSchemaForeignKeyMismatch");
        }).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}
