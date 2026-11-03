using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    private static ValueTask WriteBundleStateAsync(MySqlConnection connection, MySqlTransaction transaction,
        SqlWorldStateOperationKind kind, SqlWorldStateBundleChange change, CancellationToken token) =>
        UseResourceAsync(connection.CreateCommand(), async command =>
        {
            command.Transaction = transaction;
            command.CommandText = kind == SqlWorldStateOperationKind.Create
                ? "INSERT INTO wolf_sql_world_state(account_id,character_uid,revision,character_image,item_image,record_format,spell_image,proficiency_image,quest_image,combat_gear_image) VALUES(@account,@uid,@revision,@character,@items,2,@spells,@proficiencies,@quests,@gear)"
                : "UPDATE wolf_sql_world_state SET revision=@revision,character_image=@character,item_image=@items,record_format=2,spell_image=@spells,proficiency_image=@proficiencies,quest_image=@quests,combat_gear_image=@gear WHERE account_id=@account AND character_uid=@uid AND revision=@expected AND record_format=2";
            var image = change.Image;
            var sidecars = image.Sidecars.ToArray();
            command.Parameters.AddWithValue("@account", image.AccountId);
            command.Parameters.AddWithValue("@uid", image.CharacterUid);
            command.Parameters.AddWithValue("@revision", change.ExpectedRevision + 1);
            command.Parameters.AddWithValue("@expected", change.ExpectedRevision);
            command.Parameters.AddWithValue("@character", image.Core.CopyCharacterBytes());
            command.Parameters.AddWithValue("@items", image.Core.CopyItemBytes());
            command.Parameters.AddWithValue("@spells", SidecarValue(sidecars[0], SqlWorldStateSidecarKind.Spells));
            command.Parameters.AddWithValue("@proficiencies", SidecarValue(sidecars[1], SqlWorldStateSidecarKind.Proficiencies));
            command.Parameters.AddWithValue("@quests", SidecarValue(sidecars[2], SqlWorldStateSidecarKind.Quests));
            command.Parameters.AddWithValue("@gear", SidecarValue(sidecars[3], SqlWorldStateSidecarKind.CombatGear));
            if (await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new InvalidOperationException("sqlWorldBundleStateWriteLost");
        });

    private static object SidecarValue(SqlWorldStateSidecar sidecar, SqlWorldStateSidecarKind expected)
    {
        if (sidecar.Kind != expected) throw new InvalidOperationException("sqlWorldBundleSidecarOrderInvalid");
        return sidecar.Presence switch
        {
            SqlWorldStateSidecarPresence.Missing => DBNull.Value,
            SqlWorldStateSidecarPresence.Present => sidecar.CopyBytes(),
            _ => throw new InvalidOperationException("sqlWorldBundleSidecarPresenceInvalid")
        };
    }
}
