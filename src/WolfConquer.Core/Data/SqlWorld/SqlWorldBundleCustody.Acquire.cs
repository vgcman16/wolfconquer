using MySqlConnector;
using System.Runtime.ExceptionServices;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldBundleCustody
{
    // The only issuance API accepts a real repository and account, never a
    // caller-supplied connection, record or purported ownership receipt.
    internal static async ValueTask<SqlWorldBundleCustodyAcquireResult> AcquireAsync(SqlWorldStateRepository repository,
        long accountId, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (accountId <= 0) return new(null, SqlWorldStateFailure.InvalidAccountId);
        var owned = repository.CreateBundleCustodyConnection();
        SqlWorldBundleCustodyAcquireResult? result = null;
        Exception? primary = null;
        try
        {
            await owned.OpenAsync(token).ConfigureAwait(false);
            result = await AcquireOpenedAsync(repository, owned, accountId, token).ConfigureAwait(false);
        }
        catch (Exception error) { primary = error; }
        Exception? cleanup = null;
        if (result?.IsAcquired != true)
        {
            try { await owned.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { cleanup = error; }
        }
        if (primary is not null)
        {
            if (cleanup is not null) throw new AggregateException(primary, cleanup);
            ExceptionDispatchInfo.Capture(primary).Throw();
        }
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
        return result!;
    }

    private static async ValueTask<SqlWorldBundleCustodyAcquireResult> AcquireOpenedAsync(SqlWorldStateRepository repository,
        MySqlConnection owned, long accountId, CancellationToken token)
    {
        var identity = await SqlWorldStateRepository.AcquireBundleCustodyIdentityAsync(owned, accountId, token).ConfigureAwait(false);
        if (identity is null)
            return new(null, SqlWorldStateFailure.SessionCustodyConflict);
        var read = await SqlWorldStateRepository.ReadAcquiringBundleCustodyAsync(owned, identity, token).ConfigureAwait(false);
        if (read.Status != SqlWorldStateReadStatus.Found) return new(null, read.Failure);
        var image = read.Record!.Image;
        token.ThrowIfCancellationRequested();
        return new(new SqlWorldBundleCustody(repository, owned, identity, image.CharacterUid), SqlWorldStateFailure.None);
    }
}
