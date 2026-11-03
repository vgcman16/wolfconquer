using System.Data;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using MySqlConnector;

namespace WolfConquer.Core.Accounts;

/// <summary>Retained authentication SQL ownership; not a ticket, native owner or publication grant.</summary>
public sealed partial class SqlAccountAuthenticationCustody : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Identity identity;
    private readonly byte[] salt, passwordFingerprint;
    private MySqlConnection? connection, retiringConnection;

    private SqlAccountAuthenticationCustody(MySqlConnection connection, Identity identity, long accountId,
        string username, uint characterUid, byte[] salt, byte[] passwordFingerprint)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(passwordFingerprint);
        if (connection.State != ConnectionState.Open || accountId <= 0 || salt.Length != 32 || passwordFingerprint.Length != 32)
            throw new ArgumentException("Invalid authentication custody initialization.");
        this.connection = connection; this.identity = identity;
        AccountId = accountId; Username = username; CharacterUid = characterUid;
        this.salt = salt; this.passwordFingerprint = passwordFingerprint;
    }

    public long AccountId { get; }
    public string Username { get; }
    public uint CharacterUid { get; }

    public async ValueTask<SqlAccountAuthenticationFailure> VerifyAsync(CancellationToken token = default)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try { return await VerifyAndRetireHeldAsync(token).ConfigureAwait(false); }
        finally { gate.Release(); }
    }

    internal async ValueTask<SqlAccountAuthenticationFailure> VerifyDomainAsync(MySqlConnection target,
        MySqlTransaction? transaction, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(target);
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (connection is null) return SqlAccountAuthenticationFailure.SessionRetired;
            if (ReferenceEquals(target, connection)) return SqlAccountAuthenticationFailure.DomainConflict;
            if (transaction is not null && !ReferenceEquals(transaction.Connection, target))
                throw new InvalidOperationException("sqlAuthenticationTargetTransactionMismatch");
            var before = await VerifyAndRetireHeldAsync(token).ConfigureAwait(false);
            if (before != SqlAccountAuthenticationFailure.None) return before;
            if (target.State != ConnectionState.Open) throw new InvalidOperationException("sqlAuthenticationTargetNotOpen");
            bool sameDomain = await ClaimsAreCurrentAsync(target, transaction, identity, false, token).ConfigureAwait(false);
            var after = await VerifyAndRetireHeldAsync(token).ConfigureAwait(false);
            if (after != SqlAccountAuthenticationFailure.None) return after;
            return sameDomain ? SqlAccountAuthenticationFailure.None : SqlAccountAuthenticationFailure.DomainConflict;
        }
        finally { gate.Release(); }
    }

    private async ValueTask<SqlAccountAuthenticationFailure> VerifyAndRetireHeldAsync(CancellationToken token)
    {
        if (connection is null) return SqlAccountAuthenticationFailure.SessionRetired;
        SqlAccountAuthenticationFailure failure = SqlAccountAuthenticationFailure.None;
        Exception? primary = null;
        try { failure = await VerifyHeldAsync(connection, token).ConfigureAwait(false); }
        catch (Exception error) { primary = error; }
        if (primary is not null || failure != SqlAccountAuthenticationFailure.None)
        {
            primary = await RetireHeldAsync(primary).ConfigureAwait(false);
            if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        }
        return failure;
    }

    private async ValueTask<SqlAccountAuthenticationFailure> VerifyHeldAsync(MySqlConnection actual, CancellationToken token)
    {
        if (!await ClaimsAreCurrentAsync(actual, null, identity, true, token).ConfigureAwait(false))
            return SqlAccountAuthenticationFailure.SessionRetired;
        var rows = await ReadRowsAsync(actual, Username, token).ConfigureAwait(false);
        if (!rows.Valid) return SqlAccountAuthenticationFailure.InvalidAccountState;
        if (rows.Rows!.Count != 1 || !IsUsableRecord(rows.Rows[0])) return SqlAccountAuthenticationFailure.IdentityConflict;
        var current = rows.Rows[0];
        if (current.Id != AccountId || !string.Equals(current.Username, Username, StringComparison.Ordinal) ||
            current.EntityId != CharacterUid || current.BannedId != AccountLoginService.AllowedBannedId || current.IsOnline)
            return SqlAccountAuthenticationFailure.IdentityConflict;
        byte[] actualFingerprint = Fingerprint(salt, current.Password);
        bool matches;
        try { matches = CryptographicOperations.FixedTimeEquals(passwordFingerprint, actualFingerprint); }
        finally { CryptographicOperations.ZeroMemory(actualFingerprint); }
        if (!matches) return SqlAccountAuthenticationFailure.IdentityConflict;
        return await ClaimsAreCurrentAsync(actual, null, identity, true, token).ConfigureAwait(false)
            ? SqlAccountAuthenticationFailure.None : SqlAccountAuthenticationFailure.SessionRetired;
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var failure = await RetireHeldAsync(null).ConfigureAwait(false);
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
        finally { gate.Release(); }
    }

    private async ValueTask<Exception?> RetireHeldAsync(Exception? primary)
    {
        var owned = connection ?? retiringConnection;
        connection = null;
        CryptographicOperations.ZeroMemory(salt);
        CryptographicOperations.ZeroMemory(passwordFingerprint);
        if (owned is null) return primary;
        retiringConnection = owned;
        try { await owned.DisposeAsync().ConfigureAwait(false); retiringConnection = null; }
        catch (Exception cleanup) { return primary is null ? cleanup : new AggregateException(primary, cleanup); }
        return primary;
    }
}
