using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

internal sealed partial class OwnedModernSqlAuthDatabase
{
    internal async Task WaitForSqlHandoffExpiryAsync(string token)
    {
        byte[] hash = HashSqlHandoffFixtureToken(token);
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await WithAuthenticationConnectionAsync(async connection =>
            {
                while (true)
                {
                    bool expired = await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
                    {
                        command.CommandText = "SELECT CAST(UTC_TIMESTAMP(6)>=expires_utc AS SIGNED) FROM wolf_sql_auth_handoff WHERE token_hash=@hash";
                        command.Parameters.AddWithValue("@hash", hash);
                        object? value = await command.ExecuteScalarAsync(deadline.Token);
                        return SqlWorldStateRepository.ReadBinaryLockScalar(value, "Owned handoff expiry observation is invalid.");
                    });
                    if (expired) return true;
                    await Task.Delay(TimeSpan.FromMilliseconds(25), deadline.Token);
                }
            });
        }
        finally { CryptographicOperations.ZeroMemory(hash); }
    }

    private static byte[] HashSqlHandoffFixtureToken(string token)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(token);
        try { return SHA256.HashData(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal async Task WaitForSqlHandoffIssuerReleaseAsync(long accountId)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (await ObserveAuthenticationHolderAsync(accountId) is not null)
            await Task.Delay(TimeSpan.FromMilliseconds(25), deadline.Token);
    }

    internal Task AssertSqlHandoffOriginalIssuerSessionGoneAsync(ulong issuerId) =>
        WithAuthenticationConnectionAsync(async connection =>
            await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
            {
                command.CommandText = "SELECT CAST(COUNT(*) AS SIGNED) FROM information_schema.PROCESSLIST WHERE ID=@issuer";
                command.Parameters.AddWithValue("@issuer", issuerId);
                if (SqlWorldStateRepository.ReadBinaryLockScalar(await command.ExecuteScalarAsync(),
                    "Owned handoff issuer session observation is invalid."))
                    throw new InvalidOperationException("The original owned handoff issuer session remains.");
                return true;
            }));
}
