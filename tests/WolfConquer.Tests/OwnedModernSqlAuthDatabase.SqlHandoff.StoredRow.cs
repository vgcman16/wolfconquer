using System.Security.Cryptography;

namespace WolfConquer.Tests;

internal sealed partial class OwnedModernSqlAuthDatabase
{
    internal async Task BreakSqlHandoffRedemptionPairAsync(string token)
    {
        byte[] hash = HashSqlHandoffFixtureToken(token);
        try
        {
            await WithAuthenticationConnectionAsync(async connection =>
                await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
                {
                    // Actual server time plus a missing paired owner is an
                    // intentionally malformed owned row, never fake elapsed time.
                    command.CommandText = "UPDATE wolf_sql_auth_handoff SET redeemed_utc=UTC_TIMESTAMP(6),redeemed_connection_id=NULL WHERE token_hash=@hash";
                    command.Parameters.AddWithValue("@hash", hash);
                    if (await command.ExecuteNonQueryAsync() != 1)
                        throw new InvalidOperationException("Owned handoff pair mutation did not address one row.");
                    return true;
                }));
        }
        finally { CryptographicOperations.ZeroMemory(hash); }
    }
}
