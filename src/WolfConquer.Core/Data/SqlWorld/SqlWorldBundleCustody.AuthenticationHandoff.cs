using System.Runtime.ExceptionServices;
using WolfConquer.Core.Accounts;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldBundleCustody
{
    // The real owner gate remains held throughout domain proof and the six-image
    // current read. This method never returns its connection or custody identity.
    internal async ValueTask<(SqlAccountAuthenticationFailure Domain, SqlWorldStateBundleReadResult? Read)>
        ReadForAuthenticationHandoffAsync(SqlAccountAuthenticationCustody auth, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(auth);
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            retainedSaveOutcome?.Outcome.Throw();
            if (connection is null) return (SqlAccountAuthenticationFailure.SessionRetired, null);
            if (auth.AccountId != AccountId || auth.CharacterUid != CharacterUid)
                return (SqlAccountAuthenticationFailure.IdentityConflict, null);
            var domain = await auth.VerifyDomainAsync(connection, null, token).ConfigureAwait(false);
            if (domain != SqlAccountAuthenticationFailure.None) return (domain, null);
            var result = await repository.ReadCustodyBundleAsync(connection, identity, CharacterUid, token).ConfigureAwait(false);
            if (result.Status != SqlWorldStateReadStatus.Found) return (SqlAccountAuthenticationFailure.None, result);
            domain = await auth.VerifyDomainAsync(connection, null, token).ConfigureAwait(false);
            if (domain != SqlAccountAuthenticationFailure.None) return (domain, null);
            if (!await VerifyHeldIdentityAsync(token).ConfigureAwait(false))
                return (SqlAccountAuthenticationFailure.SessionRetired, null);
            return (SqlAccountAuthenticationFailure.None, result);
        }
        catch (Exception error)
        {
            var failure = await RetireHeldAsync(error).ConfigureAwait(false);
            ExceptionDispatchInfo.Capture(failure!).Throw();
            throw;
        }
        finally { gate.Release(); }
    }
    internal async ValueTask<SqlAccountWorldHandoffFailure> VerifyHandoffWorldClaimsAsync(MySqlConnector.MySqlConnection target,
        MySqlConnector.MySqlTransaction? transaction, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (transaction is not null && !ReferenceEquals(transaction.Connection, target))
            throw new InvalidOperationException("sqlHandoffTargetTransactionMismatch");
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            retainedSaveOutcome?.Outcome.Throw();
            if (connection is null) return SqlAccountWorldHandoffFailure.SessionRetired;
            if (ReferenceEquals(connection, target)) return SqlAccountWorldHandoffFailure.DomainConflict;
            if (!await VerifyHeldIdentityAsync(token).ConfigureAwait(false)) return SqlAccountWorldHandoffFailure.SessionRetired;
            bool targetProof = await SqlWorldStateRepository.VerifyCustodyClaimsAsync(target, identity, transaction, token).ConfigureAwait(false);
            if (!await VerifyHeldIdentityAsync(token).ConfigureAwait(false)) return SqlAccountWorldHandoffFailure.SessionRetired;
            return targetProof ? SqlAccountWorldHandoffFailure.None : SqlAccountWorldHandoffFailure.DomainConflict;
        }
        finally { gate.Release(); }
    }
}