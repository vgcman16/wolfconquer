using MySqlConnector;
using System.Runtime.ExceptionServices;

namespace WolfConquer.Core.Data.SqlWorld;

/// <summary>
/// Retains a real nonpooled SQL connection and its account and random claim locks. It grants no
/// authentication, native/Store publication or new item UID authority.
/// </summary>
public sealed partial class SqlWorldBundleCustody : IAsyncDisposable
{
    // Only a local gate-order tie breaker for invalid equal-account pairs;
    // never database identity or storage authority.
    private static long nextGateOrder;
    private readonly long gateOrder = Interlocked.Increment(ref nextGateOrder);
    private readonly SqlWorldStateRepository repository;
    private readonly SqlWorldCustodyIdentity identity;
    private readonly SemaphoreSlim gate = new(1, 1);
    private MySqlConnection? connection;
    private RetainedSaveOutcome? retainedSaveOutcome;

    // Only the repository's actual Found-format2 acquisition creates this owner.
    private SqlWorldBundleCustody(SqlWorldStateRepository repository, MySqlConnection connection,
        SqlWorldCustodyIdentity identity, uint characterUid)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(identity);
        if (identity.AccountId <= 0 || characterUid == 0 || connection.State != System.Data.ConnectionState.Open)
            throw new ArgumentException("SQL bundle custody requires an open acquired connection and identities.");
        this.repository = repository;
        this.connection = connection;
        this.identity = identity;
        AccountId = identity.AccountId;
        CharacterUid = characterUid;
    }

    public long AccountId { get; }
    public uint CharacterUid { get; }

    public async ValueTask<SqlWorldStateBundleReadResult> ReadBundleAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            retainedSaveOutcome?.Outcome.Throw();
            if (connection is null) return RetiredRead();
            SqlWorldStateBundleReadResult? result = null;
            Exception? primary = null;
            try
            {
                result = await repository.ReadCustodyBundleAsync(connection, identity, CharacterUid, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) { primary = error; }
            if (primary is not null)
            {
                primary = await RetireHeldAsync(primary).ConfigureAwait(false);
                ExceptionDispatchInfo.Capture(primary!).Throw();
            }
            if (result!.Status != SqlWorldStateReadStatus.Found)
            {
                var cleanup = await RetireHeldAsync(null).ConfigureAwait(false);
                if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
            }
            return result!;
        }
        finally { gate.Release(); }
    }

    public async ValueTask<SqlWorldStateBundleCommitResult> SaveBundleAsync(SqlWorldStateBundleRequest request,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            retainedSaveOutcome?.Outcome.Throw();
            ArgumentNullException.ThrowIfNull(request);
            if (connection is null) return RetiredSave();
            SqlWorldStateBundleCommitResult? result = null;
            Exception? primary = null;
            try
            {
                result = await repository.SaveCustodyBundleAsync(connection, identity, CharacterUid, request, cancellationToken).ConfigureAwait(false);
                if (!await VerifyHeldIdentityAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (IsKnownSaveResult(result))
                        throw new SqlWorldCustodyOutcomeException(request, result!, new InvalidOperationException("sqlWorldSessionCustodyLost"));
                    result = RetiredSave();
                }
            }
            catch (Exception error)
            {
                primary = IsKnownSaveResult(result) && error is not SqlWorldCustodyOutcomeException
                    ? new SqlWorldCustodyOutcomeException(request, result!, error) : error;
            }
            if (primary is not null)
            {
                primary = await RetireHeldAsync(primary).ConfigureAwait(false);
                if (MustRetainSaveOutcome(primary!)) retainedSaveOutcome = new(request, ExceptionDispatchInfo.Capture(primary!));
                ExceptionDispatchInfo.Capture(primary!).Throw();
            }
            if (result!.Failure == SqlWorldStateFailure.SessionCustodyRetired)
            {
                var cleanup = await RetireHeldAsync(null).ConfigureAwait(false);
                if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
            }
            return result!;
        }
        finally { gate.Release(); }
    }

    private ValueTask<bool> VerifyHeldIdentityAsync(CancellationToken token) =>
        connection is null ? ValueTask.FromResult(false) :
        SqlWorldStateRepository.VerifyCustodyIdentityAsync(connection, identity, null, token);

    private static bool IsKnownSaveResult(SqlWorldStateBundleCommitResult? result) =>
        result?.Status is SqlWorldStateCommitStatus.Committed or SqlWorldStateCommitStatus.Replayed;

    private static SqlWorldStateBundleReadResult RetiredRead() =>
        new(SqlWorldStateReadStatus.Invalid, null, SqlWorldStateFailure.SessionCustodyRetired);

    private static SqlWorldStateBundleCommitResult RetiredSave() =>
        new(SqlWorldStateCommitStatus.Refused, SqlWorldStateFailure.SessionCustodyRetired, []);
}
