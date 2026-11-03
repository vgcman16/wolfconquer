using MySqlConnector;

namespace WolfConquer.Tests;

internal enum OwnedBundleStoredFault { Character, Items, Spells, SpellMembership, Proficiencies, ProficiencyMembership, Quests, CombatGear, EmptyPresent, UnknownFormat, CoreFormatWithSidecar, Oversized, AggregateBounds }
internal enum OwnedBundleSchemaFault { SignedFormat, MediumSpellBlob }

internal sealed partial class OwnedModernSqlAuthDatabase
{
    internal async Task AddOwnedWorldUidIndexFaultAsync(uint uid, long account)
    {
        if (uid == 0 || account <= 0) throw new ArgumentOutOfRangeException(nameof(uid));
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO wolf_sql_world_item_uid(item_uid,account_id) VALUES(@uid,@account)";
        command.Parameters.AddWithValue("@uid", uid); command.Parameters.AddWithValue("@account", account);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    internal async Task ApplyOwnedBundleStoredFaultAsync(long account, OwnedBundleStoredFault fault)
    {
        string set = fault switch
        {
            OwnedBundleStoredFault.Character => "character_image=X'000102'",
            OwnedBundleStoredFault.Items => "item_image=X'00000000'",
            OwnedBundleStoredFault.Spells => "spell_image=X'01000000'",
            OwnedBundleStoredFault.SpellMembership => "spell_image=X'01000000000000000000000000000000'",
            OwnedBundleStoredFault.Proficiencies => "proficiency_image=X'01000000'",
            OwnedBundleStoredFault.ProficiencyMembership => "proficiency_image=X'02000000F40100000300000002A1B2C30403020108070605F40100000100000000D4E5F60000000009000000'",
            OwnedBundleStoredFault.Quests => "quest_image=X'01000000'",
            OwnedBundleStoredFault.CombatGear => "combat_gear_image=X'5B436F6D626174476561725D0A436F756E743D300A'",
            OwnedBundleStoredFault.EmptyPresent => "spell_image=X''",
            OwnedBundleStoredFault.UnknownFormat => "record_format=99",
            OwnedBundleStoredFault.CoreFormatWithSidecar => "record_format=1",
            OwnedBundleStoredFault.Oversized => "spell_image=REPEAT(X'00',16777217)",
            OwnedBundleStoredFault.AggregateBounds => "spell_image=REPEAT(X'00',16777216),proficiency_image=REPEAT(X'00',16777216)",
            _ => throw new ArgumentOutOfRangeException(nameof(fault)),
        };
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = "UPDATE wolf_sql_world_state SET " + set + " WHERE account_id=@account";
        command.Parameters.AddWithValue("@account", account); Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    internal async Task RequireOwnedCompleteBoundsCapacityAsync()
    {
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT @@max_allowed_packet";
        long value = Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(value >= 64L * 1024 * 1024, "Owned complete bounds controls require max_allowed_packet>=64MiB on the disposable server; configure its startup explicitly. No existing server setting is changed by this fixture.");
    }

    internal async Task MakeOwnedWorldSchemaOneAsync()
    {
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync();
        await using (var check = connection.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(*) FROM wolf_sql_world_state WHERE record_format<>1 OR spell_image IS NOT NULL OR proficiency_image IS NOT NULL OR quest_image IS NOT NULL OR combat_gear_image IS NOT NULL";
            Assert.Equal(0L, Convert.ToInt64(await check.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
        }
        // Test-only preparation of the historical exact five-column shape, not a provider migration.
        await ExecuteAsync(connection, "ALTER TABLE wolf_sql_world_state DROP COLUMN record_format,DROP COLUMN spell_image,DROP COLUMN proficiency_image,DROP COLUMN quest_image,DROP COLUMN combat_gear_image");
        await ExecuteAsync(connection, "UPDATE wolf_sql_world_schema SET version=1 WHERE singleton=1");
    }

    internal async Task ApplyOwnedCompleteSchemaFaultAsync(OwnedBundleSchemaFault fault)
    {
        string sql = fault switch
        {
            OwnedBundleSchemaFault.SignedFormat => "ALTER TABLE wolf_sql_world_state MODIFY record_format TINYINT NOT NULL",
            OwnedBundleSchemaFault.MediumSpellBlob => "ALTER TABLE wolf_sql_world_state MODIFY spell_image MEDIUMBLOB NULL",
            _ => throw new ArgumentOutOfRangeException(nameof(fault)),
        };
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync(); await ExecuteAsync(connection, sql);
    }
}
