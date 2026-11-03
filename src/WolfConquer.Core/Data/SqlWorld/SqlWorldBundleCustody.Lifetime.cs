using MySqlConnector;
using System.Runtime.ExceptionServices;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldBundleCustody
{
    private MySqlConnection? retiringConnection;

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var cleanup = await RetireHeldAsync(null).ConfigureAwait(false);
            if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
        }
        finally { gate.Release(); }
    }

    // The async gate is held by every caller. Detach before awaiting cleanup so
    // neither a failed close nor a queued operation can reuse the provider owner.
    private async ValueTask<Exception?> RetireHeldAsync(Exception? primary)
    {
        var owned = connection ?? retiringConnection;
        connection = null;
        if (owned is null) return primary;
        retiringConnection = owned;
        try
        {
            await owned.DisposeAsync().ConfigureAwait(false);
            retiringConnection = null;
        }
        catch (Exception cleanup)
        {
            // Keep only cleanup custody if close failed. A later DisposeAsync
            // can retry; no operation can regain the retired connection.
            return primary is null ? cleanup : new AggregateException(primary, cleanup);
        }
        return primary;
    }

    private static bool MustRetainSaveOutcome(Exception error) =>
        error is SqlWorldCommitOutcomeUnknownException or SqlWorldCommittedCleanupException or SqlWorldCustodyOutcomeException ||
        error is AggregateException aggregate && aggregate.InnerExceptions.Any(MustRetainSaveOutcome);

    // Retain the exact immutable operation and its actual commit classification.
    // Reuse of this retired owner never generates a new request or hides the
    // committed/unknown outcome behind a refusal. A new owner must query the ledger.
    private sealed record RetainedSaveOutcome(SqlWorldStateBundleRequest Request, ExceptionDispatchInfo Outcome);
}
