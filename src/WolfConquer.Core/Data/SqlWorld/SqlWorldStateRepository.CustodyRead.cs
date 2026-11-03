using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateRepository
{
    internal async ValueTask<SqlWorldStateBundleReadResult> ReadCustodyBundleAsync(MySqlConnection connection,
        SqlWorldCustodyIdentity identity, uint characterUid, CancellationToken token)
    {
        var result = await ReadRetainedBundleAsync(connection, identity, token).ConfigureAwait(false);
        if (result.Status == SqlWorldStateReadStatus.Found && result.Record!.Image.CharacterUid != characterUid)
            return InvalidBundleRead(SqlWorldStateFailure.CharacterIdentityConflict);
        return result;
    }

    private static async ValueTask<SqlWorldStateBundleReadResult> ReadRetainedBundleAsync(MySqlConnection connection,
        SqlWorldCustodyIdentity identity, CancellationToken token)
    {
        if (!await VerifyCustodyIdentityAsync(connection, identity, null, token).ConfigureAwait(false))
            return InvalidBundleRead(SqlWorldStateFailure.SessionCustodyRetired);
        await VerifySchemaAsync(connection, token).ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, token).ConfigureAwait(false);
        var result = await UseResourceAsync(transaction, async actual =>
        {
            // A claimant following a lost peer connection waits for an older
            // pair's row lock before receiving current six-image/UID authority.
            var read = await ReadBundleHeldAsync(connection, actual, identity.AccountId, true, token).ConfigureAwait(false);
            if (!await VerifyCustodyIdentityAsync(connection, identity, actual, token).ConfigureAwait(false))
                return InvalidBundleRead(SqlWorldStateFailure.SessionCustodyRetired);
            await actual.CommitAsync(token).ConfigureAwait(false);
            return read;
        }).ConfigureAwait(false);
        // No active transaction remains when the lock helper creates its command.
        if (!await VerifyCustodyIdentityAsync(connection, identity, null, token).ConfigureAwait(false))
            return InvalidBundleRead(SqlWorldStateFailure.SessionCustodyRetired);
        return result;
    }
}
