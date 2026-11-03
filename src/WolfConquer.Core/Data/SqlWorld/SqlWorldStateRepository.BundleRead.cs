using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    public async ValueTask<SqlWorldStateBundleReadResult> ReadBundleAsync(long accountId, CancellationToken cancellationToken = default)
    {
        if (accountId <= 0) return InvalidBundleRead(SqlWorldStateFailure.InvalidAccountId);
        var connection = CreateConnection();
        MySqlTransaction? transaction = null;
        SqlWorldStateBundleReadResult? result = null;
        Exception? primary = null;
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await VerifySchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
            result = await ReadBundleHeldAsync(connection, transaction, accountId, false, cancellationToken).ConfigureAwait(false);
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

    private static ValueTask<SqlWorldStateBundleReadResult> ReadBundleHeldAsync(MySqlConnection connection,
        MySqlTransaction transaction, long accountId, bool forUpdate, CancellationToken token) =>
        UseResourceAsync<MySqlCommand, SqlWorldStateBundleReadResult>(connection.CreateCommand(), async command =>
        {
            command.Transaction = transaction;
            command.CommandText = BundleReadSql + (forUpdate ? " FOR UPDATE" : "");
            command.Parameters.AddWithValue("@account", accountId);
            var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            var result = await UseResourceAsync(reader, actual => ReadBundleRecordAsync(actual, accountId, token)).ConfigureAwait(false);
            if (result.Status != SqlWorldStateReadStatus.Found) return result;
            if (!await ReadUidOwnershipAsync(command, result.Record!.Image.Core, forUpdate, token).ConfigureAwait(false))
                return InvalidBundleRead(SqlWorldStateFailure.InvalidStoredState);
            return result;
        });

    private static SqlWorldStateBundleReadResult InvalidBundleRead(SqlWorldStateFailure failure) =>
        new(SqlWorldStateReadStatus.Invalid, null, failure);
}
