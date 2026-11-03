using System.Runtime.InteropServices;
using System.Security.Cryptography;
using MySqlConnector;

namespace WolfConquer.Core.Accounts;

// Reads and locks all collation-matching account rows on the actual transaction.
// No caller-supplied credential comparison or synthetic account source is used.
internal static class SqlAccountWorldHandoffCredentials
{
    internal static byte[] Fingerprint(ReadOnlySpan<byte> salt, string password) =>
        HMACSHA256.HashData(salt, MemoryMarshal.AsBytes(password.AsSpan()));

    internal static async ValueTask<SqlAccountWorldHandoffFailure> VerifyAsync(
        MySqlConnection connection, MySqlTransaction transaction, long accountId, uint characterUid,
        string username, byte[] salt, byte[] expectedFingerprint, CancellationToken token)
    {
        IReadOnlyList<AccountRecord> rows;
        try
        {
            rows = await MySqlAccountRepository.ReadHandoffAuthenticationRowsForUpdateAsync(
                connection, transaction, username, token).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return SqlAccountWorldHandoffFailure.InvalidAccountState;
        }

        if (rows.Count != 1) return SqlAccountWorldHandoffFailure.IdentityConflict;
        var row = rows[0];
        if (row is null || row.Id <= 0 || string.IsNullOrWhiteSpace(row.Username) || row.Password is null ||
            row.EntityId is < 0 or > uint.MaxValue)
            return SqlAccountWorldHandoffFailure.InvalidAccountState;
        if (row.Id != accountId || row.EntityId != characterUid ||
            !string.Equals(row.Username, username, StringComparison.Ordinal))
            return SqlAccountWorldHandoffFailure.IdentityConflict;
        byte[] current = Fingerprint(salt, row.Password);
        bool matches;
        try { matches = CryptographicOperations.FixedTimeEquals(current, expectedFingerprint); }
        finally { CryptographicOperations.ZeroMemory(current); }
        if (!matches) return SqlAccountWorldHandoffFailure.IdentityConflict;
        if (row.BannedId != AccountLoginService.AllowedBannedId) return SqlAccountWorldHandoffFailure.Banned;
        if (row.IsOnline) return SqlAccountWorldHandoffFailure.AlreadyOnline;
        return SqlAccountWorldHandoffFailure.None;
    }
}