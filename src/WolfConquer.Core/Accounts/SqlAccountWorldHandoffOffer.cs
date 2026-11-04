using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Accounts;

/// <summary>Retains the actual issuer connection until redemption, expiry, or disposal.</summary>
public sealed class SqlAccountWorldHandoffOffer : IAsyncDisposable
{
    private readonly SqlAccountAuthenticationCustody owner;
    private readonly CancellationTokenSource stop = new();
    private readonly SemaphoreSlim disposeGate = new(1, 1);
    private readonly Task monitor;
    private readonly TimeSpan pollInterval;
    private bool monitorFailureReported;

    internal SqlAccountWorldHandoffOffer(SqlAccountAuthenticationCustody owner, string token,
        ConquerEndpoint endpoint, DateTime issuedUtc, DateTime expiresUtc)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!SqlAccountWorldHandoffToken.IsCanonical(token) || owner.CharacterUid == 0 ||
            issuedUtc.Kind != DateTimeKind.Utc || expiresUtc.Kind != DateTimeKind.Utc || expiresUtc <= issuedUtc)
            throw new ArgumentException("Invalid issued handoff.");
        _ = SqlAccountWorldHandoffEndpoint.Validate(endpoint);
        this.owner = owner;
        Token = token;
        AccountId = owner.AccountId;
        CharacterUid = owner.CharacterUid;
        Username = owner.Username;
        WorldEndpoint = endpoint;
        IssuedAtUtc = new DateTimeOffset(issuedUtc);
        ExpiresAtUtc = new DateTimeOffset(expiresUtc);
        pollInterval = TimeSpan.FromMilliseconds(Math.Min(250, (expiresUtc - issuedUtc).TotalMilliseconds));
        monitor = MonitorAsync();
    }

    public string Token { get; }
    public long AccountId { get; }
    public uint CharacterUid { get; }
    public string Username { get; }
    public ConquerEndpoint WorldEndpoint { get; }
    public DateTimeOffset IssuedAtUtc { get; }
    public DateTimeOffset ExpiresAtUtc { get; }

    private async Task MonitorAsync()
    {
        Exception? failure = null;
        byte[] hash = SqlAccountWorldHandoffToken.Hash(Token);
        try
        {
            while (!await owner.HandoffSettledAsync(hash, IssuedAtUtc.UtcDateTime, ExpiresAtUtc.UtcDateTime, stop.Token).ConfigureAwait(false))
                await Task.Delay(pollInterval, stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception error) { failure = error; }
        finally { CryptographicOperations.ZeroMemory(hash); }
        try { await owner.DisposeAsync().ConfigureAwait(false); }
        catch (Exception cleanup) { failure = failure is null ? cleanup : new AggregateException(failure, cleanup); }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public async ValueTask DisposeAsync()
    {
        await disposeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            Exception? failure = null;
            try { stop.Cancel(); }
            catch (Exception error) { failure = error; }
            try { await monitor.ConfigureAwait(false); }
            catch (Exception error)
            {
                // The first caller receives this exact historical monitor fault.
                // A later disposal retries cleanup, rather than re-reporting it
                // forever after the physical owner has closed successfully.
                if (!monitorFailureReported)
                {
                    monitorFailureReported = true;
                    failure = failure is null ? error : new AggregateException(failure, error);
                }
            }
            // Also retries a failed physical close; the monitor is never awaiting itself.
            try { await owner.DisposeAsync().ConfigureAwait(false); }
            catch (Exception cleanup) { failure = failure is null ? cleanup : new AggregateException(failure, cleanup); }
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
        finally { disposeGate.Release(); }
    }
}