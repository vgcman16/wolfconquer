using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    // Identity DATA never substitutes for server proof. Each transaction boundary
    // asks the actual server again; an incomplete identity shape cannot use raw mode.
    private static async ValueTask RequireBundleCommitCustodyAsync(MySqlConnection connection,
        MySqlTransaction? transaction, IReadOnlyList<long> accountIds,
        SqlWorldCustodyIdentity? first, SqlWorldCustodyIdentity? second, CancellationToken token)
    {
        if (first is null && second is null)
        {
            if (transaction is null)
                throw new InvalidOperationException("sqlWorldRawCustodyTransactionMissing");
            await RequireAccountLocksAsync(connection, transaction, accountIds, token).ConfigureAwait(false);
            return;
        }
        if (!CustodyParticipantsMatch(accountIds, first, second))
            throw new InvalidOperationException("sqlWorldCustodyProofParticipantsInvalid");
        if (!await VerifyCustodyIdentityAsync(connection, first!, transaction, token).ConfigureAwait(false) ||
            second is not null && (!string.Equals(first!.Database, second.Database, StringComparison.Ordinal) ||
                !await VerifyCustodyClaimsAsync(connection, second, transaction, token).ConfigureAwait(false)))
            throw new InvalidOperationException("sqlWorldSessionCustodyLost");
    }

    private static bool CustodyParticipantsMatch(IReadOnlyList<long> accountIds,
        SqlWorldCustodyIdentity? first, SqlWorldCustodyIdentity? second)
    {
        if (first is null || first.AccountId <= 0) return false;
        if (second is null)
            return accountIds.Count == 1 && accountIds[0] == first.AccountId;
        return accountIds.Count == 2 && second.AccountId > 0 && first.AccountId != second.AccountId &&
            (accountIds[0] == first.AccountId && accountIds[1] == second.AccountId ||
             accountIds[1] == first.AccountId && accountIds[0] == second.AccountId);
    }
}
