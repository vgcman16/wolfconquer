using System.Runtime.ExceptionServices;
using System.Text;
using MySqlConnector;

namespace WolfConquer.Core.Accounts;

// Private SQL metadata, never a native owner or a redeemable authority by itself.
internal sealed class SqlAccountWorldHandoffRow
{
    internal required long AccountId { get; init; }
    internal required uint CharacterUid { get; init; }
    internal required string Username { get; init; }
    internal required string Host { get; init; }
    internal required int Port { get; init; }
    internal required long IssuerConnectionId { get; init; }
    internal required string Claim { get; init; }
    internal required byte[] Salt { get; init; }
    internal required byte[] Fingerprint { get; init; }
    internal required DateTime IssuedUtc { get; init; }
    internal required DateTime ExpiresUtc { get; init; }
    internal required bool Redeemed { get; init; }

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static async ValueTask<SqlAccountWorldHandoffRow?> ReadForUpdateAsync(
        MySqlConnection connection, MySqlTransaction transaction, byte[] tokenHash, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (!ReferenceEquals(transaction.Connection, connection))
            throw new InvalidOperationException("sqlHandoffTargetTransactionMismatch");
        MySqlCommand? command = null;
        MySqlDataReader? reader = null;
        Exception? primary = null;
        SqlAccountWorldHandoffRow? result = null;
        try
        {
            command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT account_id,character_uid,username,endpoint_host,endpoint_port," +
                "issuer_connection_id,handoff_claim,credential_salt,credential_fingerprint,issued_utc,expires_utc," +
                "redeemed_utc,redeemed_connection_id FROM wolf_sql_auth_handoff WHERE token_hash=@hash FOR UPDATE";
            command.Parameters.AddWithValue("@hash", tokenHash);
            reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                result = Parse(reader);
                if (await reader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidDataException("sqlHandoffDuplicateTicket");
            }
        }
        catch (Exception error) { primary = error; }
        primary = await DisposeAsync(reader, primary).ConfigureAwait(false);
        primary = await DisposeAsync(command, primary).ConfigureAwait(false);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        return result;
    }

    private static SqlAccountWorldHandoffRow Parse(MySqlDataReader r)
    {
        try
        {
            for (int i = 0; i < 11; i++) if (r.IsDBNull(i)) throw new InvalidDataException("sqlHandoffInvalidRow");
            long account = checked((long)r.GetUInt64(0));
            uint uid = r.GetUInt32(1);
            string username = Text(r, 2, 1024), host = Text(r, 3, 1024);
            uint port = r.GetUInt32(4);
            long issuer = checked((long)r.GetUInt64(5));
            string claim = Text(r, 6, 64);
            byte[] salt = r.GetFieldValue<byte[]>(7), fingerprint = r.GetFieldValue<byte[]>(8);
            DateTime issued = DateTime.SpecifyKind(r.GetDateTime(9), DateTimeKind.Utc);
            DateTime expires = DateTime.SpecifyKind(r.GetDateTime(10), DateTimeKind.Utc);
            bool redeemed = !r.IsDBNull(11);
            if (redeemed != !r.IsDBNull(12)) throw new InvalidDataException("sqlHandoffInvalidRow");
            if (redeemed && ((r.GetDateTime(11) < issued || r.GetDateTime(11) >= expires) || r.GetUInt64(12) == 0 || r.GetUInt64(12) > long.MaxValue))
                throw new InvalidDataException("sqlHandoffInvalidRow");
            if (account <= 0 || uid == 0 || issuer <= 0 || port is 0 or > 65535 ||
                string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(host) ||
                salt.Length != 32 || fingerprint.Length != 32 || expires - issued < TimeSpan.FromMilliseconds(1) ||
                expires - issued > TimeSpan.FromMinutes(5) || (expires - issued).Ticks % TimeSpan.TicksPerMillisecond != 0 ||
                !IsClaim(claim)) throw new InvalidDataException("sqlHandoffInvalidRow");
            return new() { AccountId = account, CharacterUid = uid, Username = username, Host = host,
                Port = (int)port, IssuerConnectionId = issuer, Claim = claim, Salt = salt,
                Fingerprint = fingerprint, IssuedUtc = issued, ExpiresUtc = expires, Redeemed = redeemed };
        }
        catch (Exception error) when (error is OverflowException or FormatException or InvalidCastException or DecoderFallbackException or ArgumentOutOfRangeException)
        { throw new InvalidDataException("sqlHandoffInvalidRow", error); }
    }

    private static string Text(MySqlDataReader r, int ordinal, int maximum)
    {
        byte[] bytes = r.GetFieldValue<byte[]>(ordinal);
        if (bytes.Length is 0 || bytes.Length > maximum) throw new InvalidDataException("sqlHandoffInvalidRow");
        return StrictUtf8.GetString(bytes);
    }

    private static bool IsClaim(string claim)
    {
        const string prefix = "wolf:auth:h:";
        if (!claim.StartsWith(prefix, StringComparison.Ordinal) || claim.Length != prefix.Length + 48) return false;
        foreach (char c in claim.AsSpan(prefix.Length))
            if (!(c is >= '0' and <= '9' or >= 'A' and <= 'F')) return false;
        return true;
    }

    private static async ValueTask<Exception?> DisposeAsync(IAsyncDisposable? resource, Exception? primary)
    {
        if (resource is null) return primary;
        try { await resource.DisposeAsync().ConfigureAwait(false); }
        catch (Exception cleanup) { return primary is null ? cleanup : new AggregateException(primary, cleanup); }
        return primary;
    }
}