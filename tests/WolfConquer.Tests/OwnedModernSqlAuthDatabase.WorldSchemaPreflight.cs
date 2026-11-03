using MySqlConnector;

namespace WolfConquer.Tests;

internal enum OwnedWorldPartialSchema { MarkerOnly, MarkerAndState, MarkerStateAndItems, MalformedState }

internal sealed partial class OwnedModernSqlAuthDatabase
{
    internal async Task CreatePartialWorldSchemaAsync(OwnedWorldPartialSchema partial)
    {
        await using var connection = new MySqlConnection(scoped);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "CREATE TABLE wolf_sql_world_schema (singleton TINYINT NOT NULL PRIMARY KEY, version INT NOT NULL) ENGINE=InnoDB");
        await ExecuteAsync(connection, "INSERT INTO wolf_sql_world_schema(singleton,version) VALUES(1,1)");
        if (partial == OwnedWorldPartialSchema.MarkerOnly) return;
        if (partial == OwnedWorldPartialSchema.MalformedState)
        {
            await ExecuteAsync(connection, "CREATE TABLE wolf_sql_world_state (account_id BIGINT NOT NULL PRIMARY KEY, unexpected INT NOT NULL) ENGINE=InnoDB");
            await ExecuteAsync(connection, "INSERT INTO wolf_sql_world_state(account_id,unexpected) VALUES(501,73)");
            return;
        }
        await ExecuteAsync(connection, "CREATE TABLE wolf_sql_world_state (account_id BIGINT NOT NULL PRIMARY KEY, character_uid INT UNSIGNED NOT NULL, revision BIGINT NOT NULL, character_image LONGBLOB NOT NULL, item_image LONGBLOB NOT NULL, UNIQUE KEY ux_world_character (character_uid)) ENGINE=InnoDB");
        await ExecuteAsync(connection, "INSERT INTO wolf_sql_world_state(account_id,character_uid,revision,character_image,item_image) VALUES(501,9001,1,X'1234',X'5678')");
        if (partial == OwnedWorldPartialSchema.MarkerAndState) return;
        if (partial != OwnedWorldPartialSchema.MarkerStateAndItems) throw new ArgumentOutOfRangeException(nameof(partial));
        await ExecuteAsync(connection, "CREATE TABLE wolf_sql_world_item_uid (item_uid INT UNSIGNED NOT NULL PRIMARY KEY, account_id BIGINT NOT NULL, KEY ix_world_item_account (account_id), CONSTRAINT fk_world_item_account FOREIGN KEY (account_id) REFERENCES wolf_sql_world_state(account_id) ON UPDATE RESTRICT ON DELETE RESTRICT) ENGINE=InnoDB");
        await ExecuteAsync(connection, "INSERT INTO wolf_sql_world_item_uid(item_uid,account_id) VALUES(90001,501)");
    }

    internal async Task<string[]> SnapshotPartialWorldSchemaAsync(OwnedWorldPartialSchema partial)
    {
        await using var connection = new MySqlConnection(scoped);
        await connection.OpenAsync();
        var result = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT table_name, table_type, engine FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name IN ('wolf_sql_world_schema','wolf_sql_world_state','wolf_sql_world_item_uid','wolf_sql_world_operation') ORDER BY BINARY table_name";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) result.Add("table|" + reader.GetString(0) + "|" + reader.GetString(1) + "|" + reader.GetString(2));
        }
        var queries = new List<string> { "SHOW CREATE TABLE wolf_sql_world_schema", "SELECT singleton,version FROM wolf_sql_world_schema ORDER BY singleton" };
        if (partial != OwnedWorldPartialSchema.MarkerOnly)
        {
            queries.Add("SHOW CREATE TABLE wolf_sql_world_state");
            queries.Add(partial == OwnedWorldPartialSchema.MalformedState
                ? "SELECT account_id,unexpected FROM wolf_sql_world_state ORDER BY account_id"
                : "SELECT account_id,character_uid,revision,HEX(character_image),HEX(item_image) FROM wolf_sql_world_state ORDER BY account_id");
        }
        if (partial == OwnedWorldPartialSchema.MarkerStateAndItems)
        {
            queries.Add("SHOW CREATE TABLE wolf_sql_world_item_uid");
            queries.Add("SELECT item_uid,account_id FROM wolf_sql_world_item_uid ORDER BY item_uid");
        }
        foreach (string sql in queries)
        {
            await using var command = connection.CreateCommand(); command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var values = new string[reader.FieldCount];
                for (int i = 0; i < values.Length; i++) values[i] = Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture)!;
                result.Add(sql + "|" + string.Join('|', values));
            }
        }
        return result.ToArray();
    }

    internal async Task InstallWorldMarkerUpdateRefusalAsync()
    {
        await using var connection = new MySqlConnection(scoped);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "CREATE TRIGGER __wolf_world_marker_no_update BEFORE UPDATE ON wolf_sql_world_schema FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'owned-world-marker-update-attempted'");
    }
}
