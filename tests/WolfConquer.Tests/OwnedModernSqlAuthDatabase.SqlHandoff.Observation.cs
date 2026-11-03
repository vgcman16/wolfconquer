using System.Globalization;

namespace WolfConquer.Tests;

internal sealed partial class OwnedModernSqlAuthDatabase
{
    internal Task<string[]> SnapshotSqlHandoffWorldRowsAsync() => WithAuthenticationConnectionAsync(async connection =>
    {
        var rows = new List<string>();
        foreach (var entry in new[]
        {
            ("schema", "SELECT singleton,version FROM wolf_sql_world_schema ORDER BY singleton"),
            ("state", "SELECT account_id,character_uid,revision,record_format,HEX(character_image),HEX(item_image),HEX(spell_image),HEX(proficiency_image),HEX(quest_image),HEX(combat_gear_image) FROM wolf_sql_world_state ORDER BY account_id"),
            ("uid", "SELECT item_uid,account_id FROM wolf_sql_world_item_uid ORDER BY item_uid"),
            ("operation", "SELECT HEX(operation_id),HEX(descriptor_hash),HEX(descriptor) FROM wolf_sql_world_operation ORDER BY operation_id")
        })
            await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
            {
                command.CommandText = entry.Item2;
                return await WithAuthenticationResourceAsync(await command.ExecuteReaderAsync(), async reader =>
                {
                    while (await reader.ReadAsync())
                    {
                        var values = new string[reader.FieldCount];
                        for (int ordinal = 0; ordinal < values.Length; ordinal++)
                            values[ordinal] = reader.IsDBNull(ordinal) ? "<NULL>" :
                                Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)!;
                        rows.Add(entry.Item1 + "|" + string.Join('|', values));
                    }
                    return true;
                });
            });
        return rows.ToArray();
    });

    internal Task<string[]> SnapshotSqlHandoffTablesAsync() => WithAuthenticationConnectionAsync(async connection =>
    {
        var tables = await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('wolf_sql_auth_handoff_schema','wolf_sql_auth_handoff') ORDER BY TABLE_NAME";
            return await WithAuthenticationResourceAsync(await command.ExecuteReaderAsync(), async reader =>
            {
                var names = new List<string>();
                while (await reader.ReadAsync()) names.Add(reader.GetString(0));
                return names;
            });
        });
        var rows = new List<string>();
        foreach (string table in tables)
        {
            string select = table switch
            {
                "wolf_sql_auth_handoff_schema" => "SELECT singleton,version FROM wolf_sql_auth_handoff_schema ORDER BY singleton",
                // Never return a bearer token, child claim, salt or credential
                // fingerprint. Equality observations expose only their digests.
                "wolf_sql_auth_handoff" => """
                    SELECT SHA2(token_hash,256),account_id,character_uid,HEX(username),HEX(endpoint_host),endpoint_port,
                        issuer_connection_id,SHA2(handoff_claim,256),SHA2(credential_salt,256),SHA2(credential_fingerprint,256),
                        DATE_FORMAT(issued_utc,'%Y-%m-%d %H:%i:%s.%f'),DATE_FORMAT(expires_utc,'%Y-%m-%d %H:%i:%s.%f'),
                        DATE_FORMAT(redeemed_utc,'%Y-%m-%d %H:%i:%s.%f'),redeemed_connection_id
                    FROM wolf_sql_auth_handoff ORDER BY token_hash
                    """,
                _ => throw new InvalidOperationException("Unexpected owned handoff table.")
            };
            await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
            {
                command.CommandText = "SHOW CREATE TABLE `" + table + "`";
                return await WithAuthenticationResourceAsync(await command.ExecuteReaderAsync(), async reader =>
                {
                    if (!await reader.ReadAsync()) throw new InvalidOperationException("Owned handoff definition is missing.");
                    rows.Add("definition|" + reader.GetString(0) + "|" + reader.GetString(1));
                    if (await reader.ReadAsync()) throw new InvalidOperationException("Owned handoff definition is ambiguous.");
                    return true;
                });
            });
            await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
            {
                command.CommandText = select;
                return await WithAuthenticationResourceAsync(await command.ExecuteReaderAsync(), async reader =>
                {
                    while (await reader.ReadAsync())
                    {
                        var values = new string[reader.FieldCount];
                        for (int ordinal = 0; ordinal < values.Length; ordinal++)
                            values[ordinal] = reader.IsDBNull(ordinal) ? "<NULL>" :
                                Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)!;
                        rows.Add(table + "|" + string.Join('|', values));
                    }
                    return true;
                });
            });
        }
        return rows.ToArray();
    });

    internal Task<(long Total, long Unredeemed, long Redeemed)> ObserveSqlHandoffCountsAsync() =>
        WithAuthenticationConnectionAsync(async connection =>
            await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
            {
                command.CommandText = "SELECT CAST(COUNT(*) AS SIGNED),CAST(COALESCE(SUM(redeemed_utc IS NULL),0) AS SIGNED),CAST(COALESCE(SUM(redeemed_utc IS NOT NULL),0) AS SIGNED) FROM wolf_sql_auth_handoff";
                return await WithAuthenticationResourceAsync(await command.ExecuteReaderAsync(), async reader =>
                {
                    if (!await reader.ReadAsync()) throw new InvalidOperationException("Owned handoff count is missing.");
                    var result = (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));
                    if (await reader.ReadAsync()) throw new InvalidOperationException("Owned handoff count is ambiguous.");
                    return result;
                });
            }));
}
