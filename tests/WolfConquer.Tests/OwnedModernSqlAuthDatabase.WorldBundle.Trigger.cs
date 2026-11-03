using System.Globalization;
using MySqlConnector;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

internal sealed partial class OwnedModernSqlAuthDatabase
{
    internal async Task InstallCompletePairSecondWriteFailureAsync(SqlWorldStateBundle expectedFirst, long expectedRevision, long secondAccount)
    {
        if (expectedFirst.AccountId <= 0 || secondAccount <= expectedFirst.AccountId || expectedRevision < 1)
            throw new ArgumentException("Owned complete-pair fault requires ordered synthetic participants.");
        string first = expectedFirst.AccountId.ToString(CultureInfo.InvariantCulture), second = secondAccount.ToString(CultureInfo.InvariantCulture);
        string revision = expectedRevision.ToString(CultureInfo.InvariantCulture), uid = expectedFirst.CharacterUid.ToString(CultureInfo.InvariantCulture);
        string[] columns = ["spell_image", "proficiency_image", "quest_image", "combat_gear_image"];
        var conditions = expectedFirst.Sidecars.ToArray().Select((row, index) => columns[index] + " <=> " +
            (row.Presence == SqlWorldStateSidecarPresence.Missing ? "NULL" : "X'" + Convert.ToHexString(row.CopyBytes()) + "'"));
        string membership = expectedFirst.ItemUids.Length == 0 ? "1=0" : "item_uid IN (" +
            string.Join(',', expectedFirst.ItemUids.ToArray().Select(value => value.ToString(CultureInfo.InvariantCulture))) + ")";
        string count = expectedFirst.ItemUids.Length.ToString(CultureInfo.InvariantCulture);
        string ddl = $$"""
            CREATE TRIGGER __wolf_bundle_second_write BEFORE UPDATE ON wolf_sql_world_state
            FOR EACH ROW BEGIN
                DECLARE observed INT DEFAULT 0;
                DECLARE owned_count INT DEFAULT 0;
                DECLARE matching_count INT DEFAULT 0;
                IF NEW.account_id = {{second}} THEN
                    SELECT COUNT(*) INTO observed FROM wolf_sql_world_state
                        WHERE account_id={{first}} AND character_uid={{uid}} AND revision={{revision}} AND record_format=2
                        AND character_image=X'{{Convert.ToHexString(expectedFirst.Core.CopyCharacterBytes())}}'
                        AND item_image=X'{{Convert.ToHexString(expectedFirst.Core.CopyItemBytes())}}'
                        AND {{string.Join(" AND ", conditions)}};
                    SELECT COUNT(*) INTO owned_count FROM wolf_sql_world_item_uid WHERE account_id={{first}};
                    SELECT COUNT(*) INTO matching_count FROM wolf_sql_world_item_uid WHERE account_id={{first}} AND {{membership}};
                    IF observed=1 AND owned_count={{count}} AND matching_count={{count}} THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='owned-bundle-first-six-and-uids-observed';
                    ELSE
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='owned-bundle-first-six-and-uids-not-observed';
                    END IF;
                END IF;
            END
            """;
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync(); await ExecuteAsync(connection, ddl);
    }

    internal async Task DropCompletePairSecondWriteFailureAsync()
    {
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync();
        await ExecuteAsync(connection, "DROP TRIGGER __wolf_bundle_second_write");
    }
}
