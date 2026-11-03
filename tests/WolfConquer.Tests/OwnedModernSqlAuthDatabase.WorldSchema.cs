using MySqlConnector;

namespace WolfConquer.Tests;

internal enum OwnedWorldSchemaFault { MarkerVersion, VersionWidth, MarkerEngine, ExtraForeignKey }

internal sealed partial class OwnedModernSqlAuthDatabase
{
    // Fixed changes on the fixture's own nonce-marked SQL schema only.
    internal async Task ApplyWorldSchemaFaultAsync(OwnedWorldSchemaFault fault)
    {
        await using var connection = new MySqlConnection(scoped);
        await connection.OpenAsync();
        switch (fault)
        {
            case OwnedWorldSchemaFault.MarkerVersion:
                await ExecuteAsync(connection, "UPDATE wolf_sql_world_schema SET version=2 WHERE singleton=1");
                break;
            case OwnedWorldSchemaFault.VersionWidth:
                await ExecuteAsync(connection, "ALTER TABLE wolf_sql_world_schema MODIFY version BIGINT NOT NULL");
                break;
            case OwnedWorldSchemaFault.MarkerEngine:
                await ExecuteAsync(connection, "ALTER TABLE wolf_sql_world_schema ENGINE=MyISAM");
                break;
            case OwnedWorldSchemaFault.ExtraForeignKey:
                await ExecuteAsync(connection, "CREATE TABLE wolf_sql_world_aux (operation_id BINARY(16) NOT NULL PRIMARY KEY) ENGINE=InnoDB");
                await ExecuteAsync(connection, "INSERT INTO wolf_sql_world_aux(operation_id) SELECT operation_id FROM wolf_sql_world_operation");
                await ExecuteAsync(connection, "ALTER TABLE wolf_sql_world_operation ADD CONSTRAINT fk_world_extra FOREIGN KEY (operation_id) REFERENCES wolf_sql_world_aux(operation_id) ON UPDATE RESTRICT ON DELETE RESTRICT");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fault));
        }
    }

    internal async Task<string[]> SnapshotWorldSchemaAsync(OwnedWorldSchemaFault fault)
    {
        await using var connection = new MySqlConnection(scoped);
        await connection.OpenAsync();
        var definitions = new List<string>();
        string[] commands = fault == OwnedWorldSchemaFault.ExtraForeignKey
            ? ["SHOW CREATE TABLE wolf_sql_world_schema", "SHOW CREATE TABLE wolf_sql_world_state", "SHOW CREATE TABLE wolf_sql_world_item_uid", "SHOW CREATE TABLE wolf_sql_world_operation", "SHOW CREATE TABLE wolf_sql_world_aux"]
            : ["SHOW CREATE TABLE wolf_sql_world_schema", "SHOW CREATE TABLE wolf_sql_world_state", "SHOW CREATE TABLE wolf_sql_world_item_uid", "SHOW CREATE TABLE wolf_sql_world_operation"];
        foreach (string sql in commands)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) throw new InvalidOperationException("ownedWorldSchemaDefinitionMissing");
            definitions.Add(reader.GetString(0) + "|" + reader.GetString(1));
            if (await reader.ReadAsync()) throw new InvalidOperationException("ownedWorldSchemaDefinitionAmbiguous");
        }
        return definitions.ToArray();
    }
}
