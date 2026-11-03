using System.Globalization;
using MySqlConnector;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

internal sealed partial class OwnedModernSqlAuthDatabase
{
    // Fixed table/column projections on this object's fresh nonce-marked database only.
    internal Task<string[]> SnapshotWorldBundleRowsAsync() => SnapshotBundleTablesAsync(compactBounds: false);
    internal Task<string[]> SnapshotWorldBundleBoundsAsync() => SnapshotBundleTablesAsync(compactBounds: true);

    private async Task<string[]> SnapshotBundleTablesAsync(bool compactBounds)
    {
        const string ordinaryState = "SELECT account_id,character_uid,revision,record_format,HEX(character_image),HEX(item_image),IFNULL(HEX(spell_image),'<NULL>'),IFNULL(HEX(proficiency_image),'<NULL>'),IFNULL(HEX(quest_image),'<NULL>'),IFNULL(HEX(combat_gear_image),'<NULL>') FROM wolf_sql_world_state ORDER BY account_id";
        const string boundsState = """
            SELECT account_id,character_uid,revision,record_format,HEX(character_image),HEX(item_image),
                OCTET_LENGTH(spell_image),SHA2(spell_image,256),
                IF(OCTET_LENGTH(spell_image)=16777217,CAST((spell_image <=> REPEAT(X'00',16777217)) AS UNSIGNED),0),
                IF(OCTET_LENGTH(spell_image)=16777216,CAST((spell_image <=> REPEAT(X'00',16777216)) AS UNSIGNED),0),
                OCTET_LENGTH(proficiency_image),SHA2(proficiency_image,256),
                IF(OCTET_LENGTH(proficiency_image)=16777216,CAST((proficiency_image <=> REPEAT(X'00',16777216)) AS UNSIGNED),0),
                IFNULL(HEX(quest_image),'<NULL>'),IFNULL(HEX(combat_gear_image),'<NULL>')
            FROM wolf_sql_world_state ORDER BY account_id
            """;
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync();
        var rows = new List<string>();
        foreach (var entry in new[]
        {
            ("schema", "SELECT singleton,version FROM wolf_sql_world_schema ORDER BY singleton"),
            ("state", compactBounds ? boundsState : ordinaryState),
            ("uid", "SELECT item_uid,account_id FROM wolf_sql_world_item_uid ORDER BY item_uid"),
            ("operation", "SELECT HEX(operation_id),HEX(descriptor_hash),HEX(descriptor) FROM wolf_sql_world_operation ORDER BY operation_id")
        })
        {
            await using var command = connection.CreateCommand(); command.CommandText = entry.Item2;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var values = new string[reader.FieldCount];
                for (int index = 0; index < values.Length; index++) values[index] = reader.IsDBNull(index) ? "<NULL>" : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture)!;
                rows.Add(entry.Item1 + "|" + string.Join('|', values));
            }
        }
        return rows.ToArray();
    }

    internal async Task RestoreOwnedBundleStateRowAsync(SqlWorldStateBundle image, long revision)
    {
        if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE wolf_sql_world_state SET character_uid=@uid,revision=@revision,record_format=2,character_image=@character,item_image=@items,spell_image=@spells,proficiency_image=@profs,quest_image=@quests,combat_gear_image=@gear WHERE account_id=@account";
        command.Parameters.AddWithValue("@account", image.AccountId); command.Parameters.AddWithValue("@uid", image.CharacterUid);
        command.Parameters.AddWithValue("@revision", revision); command.Parameters.AddWithValue("@character", image.Core.CopyCharacterBytes());
        command.Parameters.AddWithValue("@items", image.Core.CopyItemBytes());
        string[] names = ["@spells", "@profs", "@quests", "@gear"];
        for (int index = 0; index < 4; index++) command.Parameters.AddWithValue(names[index],
            image.Sidecars[index].Presence == SqlWorldStateSidecarPresence.Missing ? DBNull.Value : image.Sidecars[index].CopyBytes());
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }
}
