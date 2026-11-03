using MySqlConnector;

namespace WolfConquer.Core.Accounts;

public sealed partial class SqlAccountAuthenticationCustody
{
    internal async ValueTask<bool> HandoffSettledAsync(byte[] hash, DateTime expectedIssuedUtc, DateTime expectedExpiresUtc, CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (connection is null) return true;
            var actual = connection;
            if (!await ClaimsAreCurrentAsync(actual, null, identity, true, token).ConfigureAwait(false)) return true;
            return await UseSqlAsync<MySqlCommand, bool>(actual.CreateCommand(), async command =>
            {
                command.CommandText = "SELECT redeemed_utc, expires_utc, UTC_TIMESTAMP(6), redeemed_connection_id, issued_utc FROM wolf_sql_auth_handoff WHERE token_hash=@hash";
                command.Parameters.AddWithValue("@hash", hash);
                return await UseSqlAsync<MySqlDataReader, bool>(await command.ExecuteReaderAsync(token).ConfigureAwait(false), async reader =>
                {
                    if (!await reader.ReadAsync(token).ConfigureAwait(false)) return true;
                    if (reader.IsDBNull(1) || reader.IsDBNull(2) || reader.IsDBNull(4))
                        throw new InvalidDataException("sqlHandoffInvalidRow");
                    DateTime storedExpiry = DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc);
                    DateTime storedIssued = DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc);
                    TimeSpan lifetime = storedExpiry - storedIssued;
                    if (storedExpiry != expectedExpiresUtc || storedIssued != expectedIssuedUtc ||
                        lifetime < TimeSpan.FromMilliseconds(1) || lifetime > TimeSpan.FromMinutes(5) ||
                        lifetime.Ticks % TimeSpan.TicksPerMillisecond != 0)
                        throw new InvalidDataException("sqlHandoffInvalidRow");
                    bool redeemed = !reader.IsDBNull(0);
                    if (redeemed != !reader.IsDBNull(3)) throw new InvalidDataException("sqlHandoffInvalidRow");
                    if (redeemed)
                    {
                        DateTime redeemedAt = reader.GetDateTime(0);
                        ulong redeemedId = reader.GetUInt64(3);
                        if (redeemedAt < storedIssued || redeemedAt >= storedExpiry || redeemedId == 0 || redeemedId > long.MaxValue)
                            throw new InvalidDataException("sqlHandoffInvalidRow");
                    }
                    bool settled = redeemed || reader.GetDateTime(2) >= storedExpiry;
                    if (await reader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidDataException("sqlHandoffDuplicateTicket");
                    return settled;
                }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
}