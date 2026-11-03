using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    public async ValueTask<SqlWorldStateReadResult> ReadAsync(long accountId, CancellationToken cancellationToken = default)
    {
        if (accountId <= 0) return new(SqlWorldStateReadStatus.Invalid, null, SqlWorldStateFailure.InvalidAccountId);
        var connection = CreateConnection();
        MySqlTransaction? transaction = null;
        SqlWorldStateReadResult? result = null;
        Exception? primary = null;
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await VerifySchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
            result = await ReadHeldAsync(connection, transaction, accountId, false, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) { primary = error; }
        var cleanup = new List<Exception>();
        if (transaction is not null)
        {
            try { await transaction.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { cleanup.Add(error); }
        }
        try { await connection.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { cleanup.Add(error); }
        if (primary is not null)
        {
            if (cleanup.Count != 0) throw new AggregateException(new[] { primary }.Concat(cleanup));
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        }
        if (cleanup.Count != 0) throw new AggregateException(cleanup);
        return result!;
    }

    private static ValueTask<SqlWorldStateReadResult> ReadHeldAsync(MySqlConnection connection, MySqlTransaction transaction, long accountId, bool forUpdate, CancellationToken token) =>
        UseResourceAsync<MySqlCommand, SqlWorldStateReadResult>(connection.CreateCommand(), async command =>
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT character_uid, revision, CASE WHEN OCTET_LENGTH(character_image) BETWEEN 1 AND 16777216 THEN character_image ELSE NULL END, CASE WHEN OCTET_LENGTH(item_image) BETWEEN 1 AND 16777216 THEN item_image ELSE NULL END FROM wolf_sql_world_state WHERE account_id=@account" + (forUpdate ? " FOR UPDATE" : "");
            command.Parameters.AddWithValue("@account", accountId);
            var firstReader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            var captured = await UseResourceAsync<MySqlDataReader, (SqlWorldStateReadResult? Failure, uint Uid, long Revision, byte[]? Character, byte[]? Items)>(firstReader, async reader =>
            {
                if (!await reader.ReadAsync(token).ConfigureAwait(false))
                    return (new(SqlWorldStateReadStatus.Missing, null, SqlWorldStateFailure.MissingState), 0, 0, null, null);
                if (reader.IsDBNull(2) || reader.IsDBNull(3))
                    return (new(SqlWorldStateReadStatus.Invalid, null, SqlWorldStateFailure.InvalidStoredState), 0, 0, null, null);
                return (null, reader.GetUInt32(0), reader.GetInt64(1), reader.GetFieldValue<byte[]>(2), reader.GetFieldValue<byte[]>(3));
            }).ConfigureAwait(false);
            if (captured.Failure is not null) return captured.Failure;
            if (!SqlWorldStateImage.TryCreate(accountId, captured.Uid, captured.Character!, captured.Items!, out var image, out _) ||
                !SqlWorldStateRecord.TryCreate(image!, captured.Revision, out var record, out _))
                return new(SqlWorldStateReadStatus.Invalid, null, SqlWorldStateFailure.InvalidStoredState);
            command.CommandText = "SELECT item_uid FROM wolf_sql_world_item_uid WHERE account_id=@account ORDER BY item_uid" + (forUpdate ? " FOR UPDATE" : "");
            uint[] expectedUids = image!.ItemUids.ToArray();
            var uidReader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            bool matches = await UseResourceAsync(uidReader, async reader =>
            {
                int index = 0;
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                    if (index == expectedUids.Length || reader.GetUInt32(0) != expectedUids[index++]) return false;
                return index == expectedUids.Length;
            }).ConfigureAwait(false);
            if (!matches) return new(SqlWorldStateReadStatus.Invalid, null, SqlWorldStateFailure.InvalidStoredState);
            return new(SqlWorldStateReadStatus.Found, record, SqlWorldStateFailure.None);
        });
}
