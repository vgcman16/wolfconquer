using System.Globalization;
using MySqlConnector;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

internal sealed partial class OwnedModernSqlAuthDatabase
{
    internal SqlWorldStateRepository CreateWorldStateRepository() => new(scoped);

    // Fixed queries against this object's freshly created, nonce-marked schema only.
    // No connection string is returned, logged or embedded in public fixtures.
    internal async Task<string[]> SnapshotWorldStateRowsAsync()
    {
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync();
        var rows = new List<string>();
        foreach (var entry in new[]
        {
            ("schema", "SELECT singleton,version FROM wolf_sql_world_schema ORDER BY singleton"),
            ("state", "SELECT account_id,character_uid,revision,HEX(character_image),HEX(item_image) FROM wolf_sql_world_state ORDER BY account_id"),
            ("uid", "SELECT item_uid,account_id FROM wolf_sql_world_item_uid ORDER BY item_uid"),
            ("operation", "SELECT HEX(operation_id),HEX(descriptor_hash),HEX(descriptor) FROM wolf_sql_world_operation ORDER BY operation_id")
        })
        {
            await using var command = connection.CreateCommand(); command.CommandText = entry.Item2;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var values = new string[reader.FieldCount];
                for (int index = 0; index < values.Length; index++)
                    values[index] = Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture)!;
                rows.Add(entry.Item1 + "|" + string.Join('|', values));
            }
        }
        return rows.ToArray();
    }

    internal async Task InstallWorldPairSecondWriteFailureAsync(SqlWorldStateImage expectedFirst, long expectedRevision, long secondAccount)
    {
        if (expectedFirst.AccountId <= 0 || secondAccount <= expectedFirst.AccountId || expectedRevision < 1)
            throw new ArgumentException("Owned pair fault requires distinct ordered synthetic participants.");
        string first = expectedFirst.AccountId.ToString(CultureInfo.InvariantCulture);
        string second = secondAccount.ToString(CultureInfo.InvariantCulture);
        string revision = expectedRevision.ToString(CultureInfo.InvariantCulture);
        string character = Convert.ToHexString(expectedFirst.CopyCharacterBytes());
        string items = Convert.ToHexString(expectedFirst.CopyItemBytes());
        // SELECT INTO observes the genuine prior row UPDATE in this transaction.
        // Only that exact observation produces the assertion's expected SIGNAL.
        string ddl = $$"""
            CREATE TRIGGER __wolf_world_second_write BEFORE UPDATE ON wolf_sql_world_state
            FOR EACH ROW BEGIN
                DECLARE observed INT DEFAULT 0;
                IF NEW.account_id = {{second}} THEN
                    SELECT COUNT(*) INTO observed FROM wolf_sql_world_state
                        WHERE account_id = {{first}} AND revision = {{revision}}
                        AND character_image = X'{{character}}' AND item_image = X'{{items}}';
                    IF observed = 1 THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'owned-world-first-image-observed';
                    ELSE
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'owned-world-first-image-not-observed';
                    END IF;
                END IF;
            END
            """;
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync();
        await ExecuteAsync(connection, ddl);
    }

    internal async Task DropWorldPairSecondWriteFailureAsync()
    {
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync();
        await ExecuteAsync(connection, "DROP TRIGGER __wolf_world_second_write");
    }

    internal async Task CorruptWorldCharacterImageAsync(long account)
    {
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE wolf_sql_world_state SET character_image=@invalid WHERE account_id=@account";
        command.Parameters.AddWithValue("@invalid", new byte[] { 0, 1, 2 });
        command.Parameters.AddWithValue("@account", account);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }
}
