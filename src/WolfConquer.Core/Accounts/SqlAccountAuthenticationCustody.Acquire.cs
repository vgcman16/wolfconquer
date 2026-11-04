using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using MySqlConnector;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Core.Accounts;

public sealed partial class SqlAccountAuthenticationCustody
{
    internal static async ValueTask<SqlAccountAuthenticationAcquireResult> AcquireAsync(
        MySqlAccountRepository repository, AccountLoginRequest request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Username)) return Refused(SqlAccountAuthenticationFailure.MissingUsername);

        var connection = repository.CreateAuthenticationCustodyConnection();
        SqlAccountAuthenticationAcquireResult? result = null;
        Exception? primary = null, cleanup = null;
        try
        {
            await connection.OpenAsync(token).ConfigureAwait(false);
            result = await AcquireOpenedAsync(connection, request, token).ConfigureAwait(false);
        }
        catch (Exception error) { primary = error; }
        if (result?.IsAcquired != true)
        {
            try { await connection.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { cleanup = error; }
        }
        if (cleanup is not null) throw new SqlResourceCleanupException(primary, cleanup, connection);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        return result!;
    }

    private static async ValueTask<SqlAccountAuthenticationAcquireResult> AcquireOpenedAsync(
        MySqlConnection connection, AccountLoginRequest request, CancellationToken token)
    {
        var initial = await ReadRowsAsync(connection, request.Username, token).ConfigureAwait(false);
        if (!initial.Valid) return Refused(SqlAccountAuthenticationFailure.InvalidAccountState);
        var candidates = initial.Rows!;
        if (candidates.Count == 0) return Refused(SqlAccountAuthenticationFailure.MissingUsername);
        if (candidates.Count != 1) return Refused(SqlAccountAuthenticationFailure.AmbiguousAccount);
        var selected = candidates[0];
        if (!IsUsableRecord(selected)) return Refused(SqlAccountAuthenticationFailure.InvalidAccountState);
        if (!string.Equals(selected.Password, request.Password, StringComparison.Ordinal)) return Refused(SqlAccountAuthenticationFailure.WrongPassword);
        if (selected.BannedId != AccountLoginService.AllowedBannedId) return Refused(SqlAccountAuthenticationFailure.Banned);
        if (selected.IsOnline) return Refused(SqlAccountAuthenticationFailure.AlreadyOnline);

        var physical = await ReadPhysicalIdentityAsync(connection, token).ConfigureAwait(false);
        string accountLock = AccountLockName(physical.Database, selected.Id);
        if (!await AcquireClaimAsync(connection, accountLock, token).ConfigureAwait(false)) return Refused(SqlAccountAuthenticationFailure.SessionConflict);
        string claimLock = NewClaimLockName();
        if (!await AcquireClaimAsync(connection, claimLock, token).ConfigureAwait(false)) return Refused(SqlAccountAuthenticationFailure.SessionConflict);
        var identity = new Identity(physical.Database, physical.ConnectionId, accountLock, claimLock);
        if (!await ClaimsAreCurrentAsync(connection, null, identity, true, token).ConfigureAwait(false))
            return Refused(SqlAccountAuthenticationFailure.SessionRetired);

        var current = await ReadRowsAsync(connection, request.Username, token).ConfigureAwait(false);
        if (!current.Valid) return Refused(SqlAccountAuthenticationFailure.InvalidAccountState);
        if (current.Rows!.Count != 1 || !IsUsableRecord(current.Rows[0]) || !SameRecord(selected, current.Rows[0]))
            return Refused(SqlAccountAuthenticationFailure.IdentityConflict);
        if (!await ClaimsAreCurrentAsync(connection, null, identity, true, token).ConfigureAwait(false))
            return Refused(SqlAccountAuthenticationFailure.SessionRetired);

        byte[] salt = RandomNumberGenerator.GetBytes(32); byte[]? fingerprint = null;
        try
        {
            fingerprint = Fingerprint(salt, current.Rows[0].Password);
            token.ThrowIfCancellationRequested();
            var owner = new SqlAccountAuthenticationCustody(connection, identity,
                selected.Id, selected.Username, checked((uint)selected.EntityId), salt, fingerprint);
            return new(owner, SqlAccountAuthenticationFailure.None);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(salt);
            if (fingerprint is not null) CryptographicOperations.ZeroMemory(fingerprint);
            throw;
        }
    }

    private static async ValueTask<(bool Valid, IReadOnlyList<AccountRecord>? Rows)> ReadRowsAsync(
        MySqlConnection connection, string username, CancellationToken token)
    {
        try { return (true, await MySqlAccountRepository.ReadAuthenticationRowsAsync(connection, username, token).ConfigureAwait(false)); }
        catch (InvalidDataException) { return (false, null); }
    }

    private static bool IsUsableRecord(AccountRecord? record) => record is not null && record.Id > 0 &&
        !string.IsNullOrWhiteSpace(record.Username) && record.Password is not null && record.EntityId is >= 0 and <= uint.MaxValue;

    private static bool SameRecord(AccountRecord left, AccountRecord right) => left.Id == right.Id &&
        string.Equals(left.Username, right.Username, StringComparison.Ordinal) &&
        string.Equals(left.Password, right.Password, StringComparison.Ordinal) &&
        left.EntityId == right.EntityId && left.BannedId == right.BannedId && left.IsOnline == right.IsOnline;

    // Hash the exact UTF-16 code units so ordinally distinct strings never
    // collapse through a lossy text encoding; no temporary password array exists.
    private static byte[] Fingerprint(byte[] salt, string password) =>
        HMACSHA256.HashData(salt, MemoryMarshal.AsBytes(password.AsSpan()));

    private static SqlAccountAuthenticationAcquireResult Refused(SqlAccountAuthenticationFailure failure) => new(null, failure);
}
