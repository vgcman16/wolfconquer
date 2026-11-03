namespace WolfConquer.Tests;

internal sealed partial class OwnedModernSqlAuthDatabase
{
    internal Task ReplaceSqlHandoffWorldIdentityAsync(long accountId, uint characterUid) =>
        WithAuthenticationConnectionAsync(async connection =>
            await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
            {
                command.CommandText = "UPDATE wolf_sql_world_state SET character_uid=@uid,character_image=@character WHERE account_id=@account";
                command.Parameters.AddWithValue("@uid", characterUid);
                command.Parameters.AddWithValue("@character", SqlWorldStateTestData.Character(characterUid));
                command.Parameters.AddWithValue("@account", accountId);
                if (await command.ExecuteNonQueryAsync() != 1)
                    throw new InvalidOperationException("Owned world identity mutation did not address one row.");
                return true;
            }));

    internal Task CorruptSqlHandoffWorldSpellImageAsync(long accountId) => WithAuthenticationConnectionAsync(async connection =>
        await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = "UPDATE wolf_sql_world_state SET spell_image=X'01' WHERE account_id=@account";
            command.Parameters.AddWithValue("@account", accountId);
            if (await command.ExecuteNonQueryAsync() != 1)
                throw new InvalidOperationException("Owned world spell mutation did not address one row.");
            return true;
        }));
}
