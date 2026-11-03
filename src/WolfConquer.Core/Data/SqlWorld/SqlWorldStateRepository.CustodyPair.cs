using System.Runtime.ExceptionServices;
using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    // Called only while both real owner gates are held. It never issues an owner,
    // opens another connection or acquires the peer's claim recursively.
    internal async ValueTask<SqlWorldStateBundleCommitResult> SaveCustodyPairBundleAsync(
        MySqlConnection primary, SqlWorldCustodyIdentity first, SqlWorldCustodyIdentity second,
        SqlWorldStateBundleRequest request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Kind != SqlWorldStateOperationKind.SavePair)
            return RefuseBundle(SqlWorldStateFailure.InvalidOperationKind);
        var changes = request.Changes.ToArray();
        if (changes.Length != 2) return RefuseBundle(SqlWorldStateFailure.ParticipantCount);
        if (first.AccountId == second.AccountId)
            return RefuseBundle(SqlWorldStateFailure.ParticipantIdentityConflict);
        var accounts = changes.Select(change => change.Image.AccountId).ToArray();
        if (!CustodyParticipantsMatch(accounts, first, second))
            return RefuseBundle(SqlWorldStateFailure.InvalidAccountId);
        if (!string.Equals(first.Database, second.Database, StringComparison.Ordinal))
            return RefuseBundle(SqlWorldStateFailure.SessionCustodyDomainConflict);
        if (primary.State != System.Data.ConnectionState.Open ||
            !await VerifyCustodyIdentityAsync(primary, first, null, token).ConfigureAwait(false))
            return RefuseBundle(SqlWorldStateFailure.SessionCustodyRetired);
        if (!await VerifyCustodyClaimsAsync(primary, second, null, token).ConfigureAwait(false))
            return RefuseBundle(SqlWorldStateFailure.SessionCustodyDomainConflict);
        await VerifySchemaAsync(primary, token).ConfigureAwait(false);
        return await CommitCustodyPairBundleAsync(primary, first, second, request, changes, accounts, token).ConfigureAwait(false);
    }

    private static async ValueTask<SqlWorldStateBundleCommitResult> CommitCustodyPairBundleAsync(
        MySqlConnection primary, SqlWorldCustodyIdentity first, SqlWorldCustodyIdentity second,
        SqlWorldStateBundleRequest request, SqlWorldStateBundleChange[] changes, long[] accounts, CancellationToken token)
    {
        var disposition = new CommitDisposition();
        MySqlTransaction? transaction = null;
        SqlWorldStateBundleCommitResult? result = null;
        Exception? failure = null;
        try
        {
            byte[] operation = request.OperationId.ToByteArray();
            byte[] descriptor = request.CopyDescriptorBytes(), digest = request.CopyDescriptorHash();
            transaction = await primary.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, token).ConfigureAwait(false);
            result = await CommitBundleHeldAsync(primary, transaction, SqlWorldStateOperationKind.SavePair, changes,
                operation, digest, descriptor, disposition, token, forbidNewItemUids: true,
                custodyFirst: first, custodySecond: second).ConfigureAwait(false);
        }
        catch (Exception error) { failure = error; }
        if (transaction is not null)
        {
            try { await transaction.DisposeAsync().ConfigureAwait(false); }
            catch (Exception cleanup) { failure = failure is null ? cleanup : new AggregateException(failure, cleanup); }
        }
        // No active transaction remains when this final actual server proof runs.
        if (failure is null)
        {
            try { await RequireBundleCommitCustodyAsync(primary, null, accounts, first, second, token).ConfigureAwait(false); }
            catch (Exception error) { failure = error; }
        }
        if (failure is not null)
        {
            if (result is { Status: SqlWorldStateCommitStatus.Committed or SqlWorldStateCommitStatus.Replayed })
                throw new SqlWorldCustodyOutcomeException(request, result, failure);
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        return result!;
    }
}
