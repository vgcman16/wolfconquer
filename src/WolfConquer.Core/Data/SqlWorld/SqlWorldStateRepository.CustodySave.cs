using MySqlConnector;
using System.Runtime.ExceptionServices;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    internal async ValueTask<SqlWorldStateBundleCommitResult> SaveCustodyBundleAsync(MySqlConnection connection,
        SqlWorldCustodyIdentity identity, uint characterUid, SqlWorldStateBundleRequest request, CancellationToken token)
    {
        if (!await VerifyCustodyIdentityAsync(connection, identity, null, token).ConfigureAwait(false))
            return RefuseBundle(SqlWorldStateFailure.SessionCustodyRetired);
        if (request.Kind != SqlWorldStateOperationKind.Save) return RefuseBundle(SqlWorldStateFailure.InvalidOperationKind);
        var changes = request.Changes.ToArray();
        if (changes.Length != 1) return RefuseBundle(SqlWorldStateFailure.ParticipantCount);
        if (changes[0].Image.AccountId != identity.AccountId) return RefuseBundle(SqlWorldStateFailure.InvalidAccountId);
        if (changes[0].Image.CharacterUid != characterUid) return RefuseBundle(SqlWorldStateFailure.CharacterIdentityConflict);
        await VerifySchemaAsync(connection, token).ConfigureAwait(false);
        return await CommitCustodyBundleAsync(connection, identity, request, changes, token).ConfigureAwait(false);
    }

    private static async ValueTask<SqlWorldStateBundleCommitResult> CommitCustodyBundleAsync(MySqlConnection connection,
        SqlWorldCustodyIdentity identity, SqlWorldStateBundleRequest request, SqlWorldStateBundleChange[] changes, CancellationToken token)
    {
        var disposition = new CommitDisposition();
        MySqlTransaction? transaction = null;
        SqlWorldStateBundleCommitResult? result = null;
        Exception? primary = null;
        try
        {
            var operation = request.OperationId.ToByteArray();
            var descriptor = request.CopyDescriptorBytes();
            var digest = request.CopyDescriptorHash();
            transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, token).ConfigureAwait(false);
            result = await CommitBundleHeldAsync(connection, transaction, SqlWorldStateOperationKind.Save, changes,
                operation, digest, descriptor, disposition, token, forbidNewItemUids: true, custodyFirst: identity).ConfigureAwait(false);
            if (!await VerifyCustodyIdentityAsync(connection, identity, null, token).ConfigureAwait(false))
                throw new InvalidOperationException("sqlWorldSessionCustodyLost");
        }
        catch (Exception error) { primary = error; }
        Exception? cleanup = null;
        if (transaction is not null)
        {
            try { await transaction.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { cleanup = error; }
        }
        if (primary is not null)
        {
            if (cleanup is not null) primary = new AggregateException(primary, cleanup);
            if (result?.Status is SqlWorldStateCommitStatus.Committed or SqlWorldStateCommitStatus.Replayed)
                throw new SqlWorldCustodyOutcomeException(request, result, primary);
            ExceptionDispatchInfo.Capture(primary).Throw();
        }
        if (cleanup is not null)
        {
            if (result?.Status is SqlWorldStateCommitStatus.Committed or SqlWorldStateCommitStatus.Replayed)
                throw new SqlWorldCustodyOutcomeException(request, result, cleanup);
            ExceptionDispatchInfo.Capture(cleanup).Throw();
        }
        return result!;
    }
}
