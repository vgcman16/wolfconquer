using System.Runtime.ExceptionServices;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldBundleCustody
{
    public async ValueTask<SqlWorldStateBundleCommitResult> SavePairBundleAsync(SqlWorldBundleCustody other,
        SqlWorldStateBundleRequest request, CancellationToken cancellationToken = default)
    {
        if (other is null || ReferenceEquals(this, other))
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                retainedSaveOutcome?.Outcome.Throw();
                ArgumentNullException.ThrowIfNull(other);
                ArgumentNullException.ThrowIfNull(request);
                return RefusePair(SqlWorldStateFailure.ParticipantCount);
            }
            finally { gate.Release(); }
        }

        var first = AccountId < other.AccountId || AccountId == other.AccountId && gateOrder < other.gateOrder ? this : other;
        var second = ReferenceEquals(first, this) ? other : this;
        await first.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Cancellation while waiting for the second owner still releases
            // the acquired prefix. Neither owner uses a Monitor across SQL.
            await second.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                first.retainedSaveOutcome?.Outcome.Throw();
                second.retainedSaveOutcome?.Outcome.Throw();
                ArgumentNullException.ThrowIfNull(request);
                if (first.AccountId == second.AccountId) return RefusePair(SqlWorldStateFailure.ParticipantCount);
                return await SavePairHeldAsync(first, second, request, cancellationToken).ConfigureAwait(false);
            }
            finally { second.gate.Release(); }
        }
        finally { first.gate.Release(); }
    }

    private static async ValueTask<SqlWorldStateBundleCommitResult> SavePairHeldAsync(SqlWorldBundleCustody first,
        SqlWorldBundleCustody second, SqlWorldStateBundleRequest request, CancellationToken token)
    {
        SqlWorldStateBundleCommitResult? result = null;
        Exception? primary = null;
        try
        {
            if (!await VerifyPairOwnerIdentitiesHeldAsync(first, second, token).ConfigureAwait(false))
                result = RetiredSave();
            else
            {
                var invalid = ValidatePairRequest(first, second, request);
                result = invalid != SqlWorldStateFailure.None ? RefusePair(invalid) :
                    await first.repository.SaveCustodyPairBundleAsync(first.connection!, first.identity, second.identity,
                        request, token).ConfigureAwait(false);
                // A healthy owner on another server is a domain refusal. A
                // peer whose actual connection died must instead be retired.
                if (!await VerifyPairOwnerIdentitiesHeldAsync(first, second, token).ConfigureAwait(false))
                {
                    if (IsKnownSaveResult(result))
                        throw new SqlWorldCustodyOutcomeException(request, result!, new InvalidOperationException("sqlWorldSessionCustodyLost"));
                    result = RetiredSave();
                }
            }
        }
        catch (Exception error)
        {
            primary = IsKnownSaveResult(result) && error is not SqlWorldCustodyOutcomeException
                ? new SqlWorldCustodyOutcomeException(request, result!, error) : error;
        }
        if (primary is not null || result!.Failure == SqlWorldStateFailure.SessionCustodyRetired)
        {
            primary = await first.RetireHeldAsync(primary).ConfigureAwait(false);
            primary = await second.RetireHeldAsync(primary).ConfigureAwait(false);
            if (primary is not null)
            {
                var outcome = ExceptionDispatchInfo.Capture(primary);
                if (MustRetainSaveOutcome(primary))
                {
                    var retained = new RetainedSaveOutcome(request, outcome);
                    first.retainedSaveOutcome = retained;
                    second.retainedSaveOutcome = retained;
                }
                outcome.Throw();
            }
        }
        return result!;
    }

    private static async ValueTask<bool> VerifyPairOwnerIdentitiesHeldAsync(SqlWorldBundleCustody first,
        SqlWorldBundleCustody second, CancellationToken token)
    {
        bool firstHeld = await first.VerifyHeldIdentityAsync(token).ConfigureAwait(false);
        bool secondHeld = await second.VerifyHeldIdentityAsync(token).ConfigureAwait(false);
        return firstHeld && secondHeld;
    }

    private static SqlWorldStateFailure ValidatePairRequest(SqlWorldBundleCustody first,
        SqlWorldBundleCustody second, SqlWorldStateBundleRequest request)
    {
        if (request.Kind != SqlWorldStateOperationKind.SavePair) return SqlWorldStateFailure.InvalidOperationKind;
        var changes = request.Changes;
        if (changes.Length != 2) return SqlWorldStateFailure.ParticipantCount;
        // Immutable requests are sorted by account at their real construction.
        if (changes[0].Image.AccountId != first.AccountId || changes[1].Image.AccountId != second.AccountId)
            return SqlWorldStateFailure.InvalidAccountId;
        if (changes[0].Image.CharacterUid != first.CharacterUid || changes[1].Image.CharacterUid != second.CharacterUid)
            return SqlWorldStateFailure.CharacterIdentityConflict;
        return SqlWorldStateFailure.None;
    }

    private static SqlWorldStateBundleCommitResult RefusePair(SqlWorldStateFailure failure) =>
        new(SqlWorldStateCommitStatus.Refused, failure, []);
}
