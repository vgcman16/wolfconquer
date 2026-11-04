using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data.SqlWorld;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Accounts;

public sealed class SqlAccountWorldHandoffIssuer
{
    private readonly MySqlAccountRepository accounts;
    private readonly SqlWorldStateRepository world;
    private readonly ConquerEndpoint endpoint;
    private readonly TimeSpan lifetime;

    public SqlAccountWorldHandoffIssuer(MySqlAccountRepository accounts, SqlWorldStateRepository world,
        ConquerEndpoint worldEndpoint, TimeSpan? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(world);
        _ = SqlAccountWorldHandoffEndpoint.Validate(worldEndpoint);
        TimeSpan selected = lifetime ?? TimeSpan.FromMinutes(2);
        if (selected.Ticks % TimeSpan.TicksPerMillisecond != 0 || selected < TimeSpan.FromMilliseconds(1) || selected > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        this.accounts = accounts; this.world = world; endpoint = worldEndpoint; this.lifetime = selected;
    }

    public async ValueTask<SqlAccountWorldHandoffIssueResult> IssueAsync(AccountLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        SqlAccountAuthenticationAcquireResult authResult;
        try
        {
            authResult = await accounts.TryAcquireAuthenticationAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            // Acquisition may fail before an auth owner exists. Retain only
            // the actual raw cleanup handle; no ticket COMMIT was attempted.
            var acquisitionFailure = SqlAccountWorldHandoffCleanupTransfer.RetainCleanup(error, null,
                commitAttempted: false, committed: false);
            ExceptionDispatchInfo.Capture(acquisitionFailure).Throw();
            throw;
        }
        if (!authResult.IsAcquired) return new(null, Map(authResult.Failure));
        var auth = authResult.Custody!;
        SqlWorldBundleCustody? worldOwner = null;
        SqlAccountWorldHandoffIssueResult? result = null;
        SqlAccountWorldHandoffOffer? pendingOffer = null;
        Exception? failure = null;
        List<IAsyncDisposable>? pendingCleanup = null;
        bool pendingOfferCleanupFailed = false;
        bool committed = false, transferred = false;
        try
        {
            if (auth.CharacterUid == 0) result = new(null, SqlAccountWorldHandoffFailure.IdentityConflict);
            else
            {
                var acquired = await world.TryAcquireBundleCustodyAsync(auth.AccountId, cancellationToken).ConfigureAwait(false);
                if (!acquired.IsAcquired)
                {
                    result = acquired.Failure switch
                    {
                        SqlWorldStateFailure.MissingState => new(null, SqlAccountWorldHandoffFailure.WorldUnavailable),
                        SqlWorldStateFailure.SessionCustodyConflict => new(null, SqlAccountWorldHandoffFailure.SessionConflict),
                        SqlWorldStateFailure.CharacterIdentityConflict => new(null, SqlAccountWorldHandoffFailure.IdentityConflict),
                        _ => throw new InvalidDataException("sqlHandoffWorldStateInvalid"),
                    };
                }
                else
                {
                    worldOwner = acquired.Custody!;
                    if (worldOwner.CharacterUid != auth.CharacterUid) result = new(null, SqlAccountWorldHandoffFailure.IdentityConflict);
                    else
                    {
                        var proof = await worldOwner.ReadForAuthenticationHandoffAsync(auth, cancellationToken).ConfigureAwait(false);
                        if (proof.Domain != SqlAccountAuthenticationFailure.None) result = new(null, Map(proof.Domain));
                        else if (proof.Read?.Status == SqlWorldStateReadStatus.Missing) result = new(null, SqlAccountWorldHandoffFailure.WorldUnavailable);
                        else if (proof.Read?.Status != SqlWorldStateReadStatus.Found) throw new InvalidDataException("sqlHandoffWorldStateInvalid");
                        else
                        {
                            var issued = await auth.IssueHandoffAsync(endpoint, lifetime, cancellationToken).ConfigureAwait(false);
                            if (issued.Failure != SqlAccountWorldHandoffFailure.None) result = new(null, issued.Failure);
                            else
                            {
                                committed = true;
                                var finalProof = await worldOwner.ReadForAuthenticationHandoffAsync(auth, cancellationToken).ConfigureAwait(false);
                                if (finalProof.Domain != SqlAccountAuthenticationFailure.None ||
                                    finalProof.Read?.Status != SqlWorldStateReadStatus.Found ||
                                    finalProof.Read.Record!.Image.CharacterUid != auth.CharacterUid)
                                    throw new InvalidOperationException("sqlHandoffWorldOwnerLost");
                                // Close the temporary world owner before exposing the offer.
                                await worldOwner.DisposeAsync().ConfigureAwait(false);
                                worldOwner = null;
                                pendingOffer = new SqlAccountWorldHandoffOffer(auth, issued.Token!, endpoint, issued.Issued, issued.Expires);
                                result = new(pendingOffer, SqlAccountWorldHandoffFailure.None);
                                transferred = true;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception error)
        {
            failure = committed && error is not SqlAccountWorldHandoffCommittedCleanupException
                ? new SqlAccountWorldHandoffCommittedCleanupException(error) : error;
        }
        if (worldOwner is not null)
        {
            try { await worldOwner.DisposeAsync().ConfigureAwait(false); }
            catch (Exception cleanup)
            {
                (pendingCleanup ??= []).Add(worldOwner);
                failure = failure is null ? cleanup : new AggregateException(failure, cleanup);
            }
        }
        if (!transferred && pendingOffer is not null)
        {
            try { await pendingOffer.DisposeAsync().ConfigureAwait(false); }
            catch (Exception cleanup)
            {
                pendingOfferCleanupFailed = true;
                (pendingCleanup ??= []).Add(pendingOffer);
                failure = failure is null ? cleanup : new AggregateException(failure, cleanup);
            }
        }
        if (!transferred)
        {
            try { await auth.DisposeAsync().ConfigureAwait(false); }
            catch (Exception cleanup)
            {
                // An unpublished offer owns this same auth object and must also
                // join its monitor. Retain that owner once, not a parallel grant.
                if (!pendingOfferCleanupFailed) (pendingCleanup ??= []).Add(auth);
                failure = failure is null ? cleanup : new AggregateException(failure, cleanup);
            }
        }
        if (failure is not null)
        {
            failure = SqlAccountWorldHandoffCleanupTransfer.RetainIssueCleanup(failure, pendingCleanup, committed);
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        return result!;
    }

    private static SqlAccountWorldHandoffFailure Map(SqlAccountAuthenticationFailure failure) => failure switch
    {
        SqlAccountAuthenticationFailure.MissingUsername => SqlAccountWorldHandoffFailure.MissingUsername,
        SqlAccountAuthenticationFailure.WrongPassword => SqlAccountWorldHandoffFailure.WrongPassword,
        SqlAccountAuthenticationFailure.AmbiguousAccount => SqlAccountWorldHandoffFailure.AmbiguousAccount,
        SqlAccountAuthenticationFailure.Banned => SqlAccountWorldHandoffFailure.Banned,
        SqlAccountAuthenticationFailure.AlreadyOnline => SqlAccountWorldHandoffFailure.AlreadyOnline,
        SqlAccountAuthenticationFailure.InvalidAccountState => SqlAccountWorldHandoffFailure.InvalidAccountState,
        SqlAccountAuthenticationFailure.IdentityConflict => SqlAccountWorldHandoffFailure.IdentityConflict,
        SqlAccountAuthenticationFailure.SessionConflict => SqlAccountWorldHandoffFailure.SessionConflict,
        SqlAccountAuthenticationFailure.SessionRetired => SqlAccountWorldHandoffFailure.SessionRetired,
        SqlAccountAuthenticationFailure.DomainConflict => SqlAccountWorldHandoffFailure.DomainConflict,
        _ => throw new ArgumentOutOfRangeException(nameof(failure)),
    };
}