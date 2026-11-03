using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    public ValueTask<SqlWorldStateBundleCommitResult> CreateBundleAsync(SqlWorldStateBundleRequest request, CancellationToken cancellationToken = default) => CommitBundleAsync(request, SqlWorldStateOperationKind.Create, cancellationToken);
    public ValueTask<SqlWorldStateBundleCommitResult> SaveBundleAsync(SqlWorldStateBundleRequest request, CancellationToken cancellationToken = default) => CommitBundleAsync(request, SqlWorldStateOperationKind.Save, cancellationToken);
    public ValueTask<SqlWorldStateBundleCommitResult> SavePairBundleAsync(SqlWorldStateBundleRequest request, CancellationToken cancellationToken = default) => CommitBundleAsync(request, SqlWorldStateOperationKind.SavePair, cancellationToken);

    private async ValueTask<SqlWorldStateBundleCommitResult> CommitBundleAsync(SqlWorldStateBundleRequest request, SqlWorldStateOperationKind kind, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Kind != kind) return RefuseBundle(SqlWorldStateFailure.InvalidOperationKind);
        var changes = request.Changes.ToArray();
        byte[] descriptor = request.CopyDescriptorBytes(), digest = request.CopyDescriptorHash();
        byte[] operation = request.OperationId.ToByteArray();
        var connection = CreateConnection();
        MySqlTransaction? transaction = null;
        var disposition = new CommitDisposition();
        SqlWorldStateBundleCommitResult? result = null;
        Exception? primary = null;
        try
        {
            await connection.OpenAsync(token).ConfigureAwait(false);
            await VerifySchemaAsync(connection, token).ConfigureAwait(false);
            transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, token).ConfigureAwait(false);
            result = await CommitBundleHeldAsync(connection, transaction, kind, changes, operation, digest, descriptor, disposition, token).ConfigureAwait(false);
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

    private static async ValueTask<SqlWorldStateBundleCommitResult> CommitBundleHeldAsync(MySqlConnection connection, MySqlTransaction transaction,
        SqlWorldStateOperationKind kind, SqlWorldStateBundleChange[] changes, byte[] operation, byte[] digest,
        byte[] descriptor, CommitDisposition disposition, CancellationToken token)
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
                    return RefuseBundle(SqlWorldStateFailure.OperationConflict);
                return MakeBundleResult(SqlWorldStateCommitStatus.Replayed, changes);
            }
            foreach (var change in changes)
            {
                var current = await ReadBundleHeldAsync(connection, transaction, change.Image.AccountId, true, token).ConfigureAwait(false);
                var failure = ValidateBundleCurrent(kind, change, current);
                if (failure != SqlWorldStateFailure.None)
                {
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                    return RefuseBundle(failure);
                }
            }
            // All old ownership rows are removed before either participant's new set is inserted.
            var coreChanges = changes.Select(change => new SqlWorldStateChange(change.Image.Core, change.ExpectedRevision)).ToArray();
            await DeleteOwnershipAsync(connection, transaction, coreChanges, token).ConfigureAwait(false);
            foreach (var change in changes)
            {
                try { await WriteBundleStateAsync(connection, transaction, kind, change, token).ConfigureAwait(false); }
                catch (MySqlException error) when (kind == SqlWorldStateOperationKind.Create && error.Number == 1062)
                {
                    SqlWorldStateFailure reason;
                    try { reason = await DiagnoseStateCollisionAsync(connection, transaction, new(change.Image.Core, change.ExpectedRevision), token).ConfigureAwait(false); }
                    catch (Exception lookupFailure) { throw new AggregateException(error, lookupFailure); }
                    try { await transaction.RollbackAsync(token).ConfigureAwait(false); }
                    catch (Exception rollbackFailure) { throw new AggregateException(error, rollbackFailure); }
                    return RefuseBundle(reason);
                }
                try { await WriteOwnershipAsync(connection, transaction, change.Image.Core, token).ConfigureAwait(false); }
                catch (ItemUidCollisionException collision)
                {
                    try { await transaction.RollbackAsync(token).ConfigureAwait(false); }
                    catch (Exception rollbackFailure) { throw new AggregateException(collision, rollbackFailure); }
                    return RefuseBundle(SqlWorldStateFailure.ItemUidConflict);
                }
            }
            var result = MakeBundleResult(SqlWorldStateCommitStatus.Committed, changes);
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

    private static SqlWorldStateFailure ValidateBundleCurrent(SqlWorldStateOperationKind kind, SqlWorldStateBundleChange change, SqlWorldStateBundleReadResult current)
    {
        if (current.Status == SqlWorldStateReadStatus.Invalid) return current.Failure;
        if (kind == SqlWorldStateOperationKind.Create) return current.Status == SqlWorldStateReadStatus.Missing ? SqlWorldStateFailure.None : SqlWorldStateFailure.StateExists;
        if (current.Status == SqlWorldStateReadStatus.Missing) return SqlWorldStateFailure.MissingState;
        if (current.Record!.Image.CharacterUid != change.Image.CharacterUid) return SqlWorldStateFailure.CharacterIdentityConflict;
        return current.Record.Revision == change.ExpectedRevision ? SqlWorldStateFailure.None : SqlWorldStateFailure.RevisionConflict;
    }

    private static SqlWorldStateBundleCommitResult RefuseBundle(SqlWorldStateFailure failure) => new(SqlWorldStateCommitStatus.Refused, failure, []);
    private static SqlWorldStateBundleCommitResult MakeBundleResult(SqlWorldStateCommitStatus status, SqlWorldStateBundleChange[] changes)
    {
        var records = new SqlWorldStateBundleRecord[changes.Length];
        for (int i = 0; i < changes.Length; i++)
        {
            if (!SqlWorldStateBundleRecord.TryCreate(changes[i].Image, changes[i].ExpectedRevision + 1, out var record, out _))
                throw new InvalidOperationException("sqlWorldPreparedBundleRecordInvalid");
            records[i] = record!;
        }
        return new(status, SqlWorldStateFailure.None, records);
    }
}
