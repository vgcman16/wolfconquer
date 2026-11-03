using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    private static ValueTask<bool> TryInsertOperationAsync(MySqlConnection connection, MySqlTransaction transaction, byte[] operation, byte[] hash, byte[] descriptor, CancellationToken token) =>
        UseResourceAsync(connection.CreateCommand(), async command =>
        {
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO wolf_sql_world_operation (operation_id,descriptor_hash,descriptor) VALUES (@id,@hash,@descriptor)";
            command.Parameters.AddWithValue("@id", operation);
            command.Parameters.AddWithValue("@hash", hash);
            command.Parameters.AddWithValue("@descriptor", descriptor);
            try { await command.ExecuteNonQueryAsync(token).ConfigureAwait(false); return true; }
            catch (MySqlException error) when (error.Number == 1062)
            {
                bool present;
                try { present = await ReadOperationAsync(connection, transaction, operation, token).ConfigureAwait(false) is not null; }
                catch (Exception lookupFailure) { throw new AggregateException(error, lookupFailure); }
                if (!present) throw;
                return false;
            }
        });

    private static ValueTask<(byte[] Hash, byte[] Descriptor)?> ReadOperationAsync(MySqlConnection connection, MySqlTransaction transaction, byte[] operation, CancellationToken token) =>
        UseResourceAsync<MySqlCommand, (byte[] Hash, byte[] Descriptor)?>(connection.CreateCommand(), async command =>
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT descriptor_hash, CASE WHEN OCTET_LENGTH(descriptor) BETWEEN 1 AND 68157440 THEN descriptor ELSE NULL END FROM wolf_sql_world_operation WHERE operation_id=@id FOR UPDATE";
            command.Parameters.AddWithValue("@id", operation);
            var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            return await UseResourceAsync<MySqlDataReader, (byte[] Hash, byte[] Descriptor)?>(reader, async actual =>
            {
                if (!await actual.ReadAsync(token).ConfigureAwait(false)) return null;
                if (actual.GetBytes(0, 0, null, 0, 0) != 32 || actual.IsDBNull(1)) throw new InvalidOperationException("sqlWorldInvalidLedger");
                return (actual.GetFieldValue<byte[]>(0), actual.GetFieldValue<byte[]>(1));
            }).ConfigureAwait(false);
        });

    private static async ValueTask DeleteOwnershipAsync(MySqlConnection connection, MySqlTransaction transaction, SqlWorldStateChange[] changes, CancellationToken token)
    {
        foreach (var change in changes)
            await UseResourceAsync(connection.CreateCommand(), async command =>
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM wolf_sql_world_item_uid WHERE account_id=@account";
                command.Parameters.AddWithValue("@account", change.Image.AccountId);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    private static ValueTask WriteStateAsync(MySqlConnection connection, MySqlTransaction transaction, SqlWorldStateOperationKind kind, SqlWorldStateChange change, CancellationToken token) =>
        UseResourceAsync(connection.CreateCommand(), async command =>
        {
            command.Transaction = transaction;
            command.CommandText = kind == SqlWorldStateOperationKind.Create
                ? "INSERT INTO wolf_sql_world_state(account_id,character_uid,revision,character_image,item_image) VALUES(@account,@uid,@revision,@character,@items)"
                : "UPDATE wolf_sql_world_state SET revision=@revision,character_image=@character,item_image=@items WHERE account_id=@account AND character_uid=@uid AND revision=@expected";
            command.Parameters.AddWithValue("@account", change.Image.AccountId);
            command.Parameters.AddWithValue("@uid", change.Image.CharacterUid);
            command.Parameters.AddWithValue("@revision", change.ExpectedRevision + 1);
            command.Parameters.AddWithValue("@expected", change.ExpectedRevision);
            command.Parameters.AddWithValue("@character", change.Image.CopyCharacterBytes());
            command.Parameters.AddWithValue("@items", change.Image.CopyItemBytes());
            if (await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new InvalidOperationException("sqlWorldStateWriteLost");
        });

    private static ValueTask<SqlWorldStateFailure> DiagnoseStateCollisionAsync(MySqlConnection connection, MySqlTransaction transaction, SqlWorldStateChange change, CancellationToken token) =>
        UseResourceAsync(connection.CreateCommand(), async command =>
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT account_id,character_uid FROM wolf_sql_world_state WHERE account_id=@account OR character_uid=@uid FOR UPDATE";
            command.Parameters.AddWithValue("@account", change.Image.AccountId);
            command.Parameters.AddWithValue("@uid", change.Image.CharacterUid);
            var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            return await UseResourceAsync(reader, async actual =>
            {
                bool accountFound = false, characterFound = false;
                while (await actual.ReadAsync(token).ConfigureAwait(false))
                {
                    accountFound |= actual.GetInt64(0) == change.Image.AccountId;
                    characterFound |= actual.GetUInt32(1) == change.Image.CharacterUid;
                }
                if (accountFound) return SqlWorldStateFailure.StateExists;
                if (characterFound) return SqlWorldStateFailure.CharacterIdentityConflict;
                throw new InvalidOperationException("sqlWorldUnattributedUniqueConflict");
            }).ConfigureAwait(false);
        });

    private static async ValueTask WriteOwnershipAsync(MySqlConnection connection, MySqlTransaction transaction, SqlWorldStateImage image, CancellationToken token)
    {
        foreach (uint uid in image.ItemUids.ToArray())
            await UseResourceAsync(connection.CreateCommand(), async command =>
            {
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO wolf_sql_world_item_uid(item_uid,account_id) VALUES(@uid,@account)";
                command.Parameters.AddWithValue("@uid", uid);
                command.Parameters.AddWithValue("@account", image.AccountId);
                try { await command.ExecuteNonQueryAsync(token).ConfigureAwait(false); }
                catch (MySqlException error) when (error.Number == 1062)
                {
                    object? owner;
                    try
                    {
                        owner = await UseResourceAsync(connection.CreateCommand(), async lookup =>
                        {
                            lookup.Transaction = transaction;
                            lookup.CommandText = "SELECT account_id FROM wolf_sql_world_item_uid WHERE item_uid=@uid FOR UPDATE";
                            lookup.Parameters.AddWithValue("@uid", uid);
                            return await lookup.ExecuteScalarAsync(token).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }
                    catch (Exception lookupFailure) { throw new AggregateException(error, lookupFailure); }
                    if (owner is null) throw;
                    throw new ItemUidCollisionException(error);
                }
            }).ConfigureAwait(false);
    }

    private sealed class ItemUidCollisionException(Exception inner) : Exception("sqlWorldItemUidCollision", inner) { }
}
