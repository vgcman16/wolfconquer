using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    private static async ValueTask VerifySchemaContractAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await VerifySchemaShapeAsync(connection, cancellationToken).ConfigureAwait(false);
        await UseResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = "SELECT singleton, version FROM wolf_sql_world_schema ORDER BY singleton";
            var version = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await UseResourceAsync(version, async reader =>
            {
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) || reader.GetInt32(0) != 1 || reader.GetInt32(1) != 1 || await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    throw new InvalidOperationException("sqlWorldSchemaVersionMismatch");
            }).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private static async ValueTask VerifySchemaShapeAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        var actual = await UseResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = """
                SELECT table_name, engine FROM information_schema.tables
                WHERE table_schema = DATABASE() AND table_name IN
                ('wolf_sql_world_schema','wolf_sql_world_state','wolf_sql_world_item_uid','wolf_sql_world_operation')
                ORDER BY table_name
                """;
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await UseResourceAsync(reader, async rows =>
            {
                var result = new Dictionary<string, string>(StringComparer.Ordinal);
                while (await rows.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(rows.GetString(0), rows.GetString(1));
                return result;
            }).ConfigureAwait(false);
        }).ConfigureAwait(false);
        string[] names = ["wolf_sql_world_item_uid", "wolf_sql_world_operation", "wolf_sql_world_schema", "wolf_sql_world_state"];
        if (actual.Count != names.Length || names.Any(name => !actual.TryGetValue(name, out var engine) || !string.Equals(engine, "InnoDB", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("sqlWorldSchemaEngineMismatch");
        await VerifySchemaColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        await VerifySchemaIndexesAsync(connection, cancellationToken).ConfigureAwait(false);
    }
}
