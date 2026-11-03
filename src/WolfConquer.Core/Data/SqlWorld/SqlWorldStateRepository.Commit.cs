using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

/// <summary>The server may have committed; resolve by retrying the same operation ID and descriptor.</summary>
public sealed class SqlWorldCommitOutcomeUnknownException : Exception
{
    internal SqlWorldCommitOutcomeUnknownException(Exception inner) : base("sqlWorldCommitOutcomeUnknown", inner) { }
}

/// <summary>The transaction committed, but cleanup failed; retry only with the same operation.</summary>
public sealed class SqlWorldCommittedCleanupException : Exception
{
    internal SqlWorldCommittedCleanupException(Exception inner) : base("sqlWorldCommittedCleanupFailed", inner) { }
}

public sealed partial class SqlWorldStateRepository
{
    public ValueTask<SqlWorldStateCommitResult> CreateAsync(SqlWorldStateRequest request, CancellationToken cancellationToken = default) => CommitAsync(request, SqlWorldStateOperationKind.Create, cancellationToken);
    public ValueTask<SqlWorldStateCommitResult> SaveAsync(SqlWorldStateRequest request, CancellationToken cancellationToken = default) => CommitAsync(request, SqlWorldStateOperationKind.Save, cancellationToken);
    public ValueTask<SqlWorldStateCommitResult> SavePairAsync(SqlWorldStateRequest request, CancellationToken cancellationToken = default) => CommitAsync(request, SqlWorldStateOperationKind.SavePair, cancellationToken);

    private async ValueTask<SqlWorldStateCommitResult> CommitAsync(SqlWorldStateRequest request, SqlWorldStateOperationKind kind, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Kind != kind) return Refuse(SqlWorldStateFailure.InvalidOperationKind);
        var changes = request.Changes.ToArray();
        byte[] descriptor = request.CopyDescriptorBytes(), digest = request.CopyDescriptorHash();
        byte[] operation = request.OperationId.ToByteArray();
        var connection = CreateConnection();
        MySqlTransaction? transaction = null;
        var disposition = new CommitDisposition();
        SqlWorldStateCommitResult? result = null;
        Exception? primary = null;
        try
        {
            await connection.OpenAsync(token).ConfigureAwait(false);
            await VerifySchemaAsync(connection, token).ConfigureAwait(false);
            transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, token).ConfigureAwait(false);
            result = await CommitHeldAsync(connection, transaction, kind, changes, operation, digest, descriptor, disposition, token).ConfigureAwait(false);
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
        if (cleanup.Count != 0)
        {
            Exception failure = cleanup.Count == 1 ? cleanup[0] : new AggregateException(cleanup);
            if (disposition.Committed) throw new SqlWorldCommittedCleanupException(failure);
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
        return result!;
    }

    private sealed class CommitDisposition { public bool Committed; }

    private static async ValueTask<SqlWorldStateCommitResult> CommitHeldAsync(MySqlConnection connection, MySqlTransaction transaction, SqlWorldStateOperationKind kind, SqlWorldStateChange[] changes, byte[] operation, byte[] digest, byte[] descriptor, CommitDisposition disposition, CancellationToken token)
    {
        bool commitAttempted = false;
        try
        {
            bool inserted = await TryInsertOperationAsync(connection, transaction, operation, digest, descriptor, token).ConfigureAwait(false);
            if (!inserted)
            {
                var replay = await ReadOperationAsync(connection, transaction, operation, token).ConfigureAwait(false);
                await transaction.RollbackAsync(token).ConfigureAwait(false);
                if (replay is null || !replay.Value.Hash.AsSpan().SequenceEqual(digest) || !replay.Value.Descriptor.AsSpan().SequenceEqual(descriptor))
                    return Refuse(SqlWorldStateFailure.OperationConflict);
                return MakeResult(SqlWorldStateCommitStatus.Replayed, changes);
            }
            foreach (var change in changes)
            {
                var current = await ReadHeldAsync(connection, transaction, change.Image.AccountId, true, token).ConfigureAwait(false);
                SqlWorldStateFailure failure = ValidateCurrent(kind, change, current);
                if (failure != SqlWorldStateFailure.None)
                {
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                    return Refuse(failure);
                }
            }
            await DeleteOwnershipAsync(connection, transaction, changes, token).ConfigureAwait(false);
            foreach (var change in changes)
            {
                try { await WriteStateAsync(connection, transaction, kind, change, token).ConfigureAwait(false); }
                catch (MySqlException error) when (kind == SqlWorldStateOperationKind.Create && error.Number == 1062)
                {
                    var reason = await DiagnoseStateCollisionAsync(connection, transaction, change, token).ConfigureAwait(false);
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                    return Refuse(reason);
                }
            }
            try
            {
                foreach (var change in changes) await WriteOwnershipAsync(connection, transaction, change.Image, token).ConfigureAwait(false);
            }
            catch (ItemUidCollisionException)
            {
                await transaction.RollbackAsync(token).ConfigureAwait(false);
                return Refuse(SqlWorldStateFailure.ItemUidConflict);
            }
            var result = MakeResult(SqlWorldStateCommitStatus.Committed, changes);
            commitAttempted = true;
            await transaction.CommitAsync(token).ConfigureAwait(false);
            disposition.Committed = true;
            return result;
        }
        catch (Exception error)
        {
            if (commitAttempted) throw new SqlWorldCommitOutcomeUnknownException(error);
            try { await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }

    private static SqlWorldStateFailure ValidateCurrent(SqlWorldStateOperationKind kind, SqlWorldStateChange change, SqlWorldStateReadResult current)
    {
        if (current.Status == SqlWorldStateReadStatus.Invalid) return SqlWorldStateFailure.InvalidStoredState;
        if (kind == SqlWorldStateOperationKind.Create) return current.Status == SqlWorldStateReadStatus.Missing ? SqlWorldStateFailure.None : SqlWorldStateFailure.StateExists;
        if (current.Status == SqlWorldStateReadStatus.Missing) return SqlWorldStateFailure.MissingState;
        if (current.Record!.Image.CharacterUid != change.Image.CharacterUid) return SqlWorldStateFailure.CharacterIdentityConflict;
        return current.Record.Revision == change.ExpectedRevision ? SqlWorldStateFailure.None : SqlWorldStateFailure.RevisionConflict;
    }

    private static SqlWorldStateCommitResult Refuse(SqlWorldStateFailure failure) => new(SqlWorldStateCommitStatus.Refused, failure, []);
    private static SqlWorldStateCommitResult MakeResult(SqlWorldStateCommitStatus status, SqlWorldStateChange[] changes)
    {
        var records = new SqlWorldStateRecord[changes.Length];
        for (int i = 0; i < changes.Length; i++)
        {
            if (!SqlWorldStateRecord.TryCreate(changes[i].Image, changes[i].ExpectedRevision + 1, out var record, out _)) throw new InvalidOperationException("sqlWorldPreparedRecordInvalid");
            records[i] = record!;
        }
        return new(status, SqlWorldStateFailure.None, records);
    }
}
