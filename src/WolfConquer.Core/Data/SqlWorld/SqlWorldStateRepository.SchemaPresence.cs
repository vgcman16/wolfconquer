using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    private static ValueTask<int> CountOwnedSchemaTablesAsync(MySqlConnection connection, CancellationToken token) =>
        UseResourceAsync(connection.CreateCommand(), async command =>
        {
            // Count any existing table or view with an owned name before issuing DDL.
            command.CommandText = """
                SELECT COUNT(*) FROM information_schema.tables
                WHERE table_schema=DATABASE() AND table_name IN
                ('wolf_sql_world_schema','wolf_sql_world_state','wolf_sql_world_item_uid','wolf_sql_world_operation')
                """;
            return Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
        });
}
