using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MySqlConnector;

namespace WolfConquer.Tests;

internal sealed partial class OwnedModernSqlAuthDatabase
{
    // Observe the real server lock boundary in this nonce-owned schema before
    // testing a two-account prefix; the second owner must remain present.
    internal async Task WaitForOwnedWorldPrefixAsync(long first, long second)
    {
        if (disposed || !created || !markerReady || first <= 0 || second <= first ||
            SchemaName != Prefix + nonce || !Guid.TryParseExact(nonce, "N", out _))
            throw new InvalidOperationException("Owned custody fixture identity is invalid.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync(deadline.Token);
        await using (var marker = connection.CreateCommand())
        {
            marker.CommandText = "SELECT Token FROM __wolf_fixture_owner";
            await using var reader = await marker.ExecuteReaderAsync(deadline.Token);
            if (!await reader.ReadAsync(deadline.Token) || reader.GetString(0) != nonce || await reader.ReadAsync(deadline.Token))
                throw new InvalidOperationException("Owned custody fixture marker changed.");
        }
        await using var database = connection.CreateCommand(); database.CommandText = "SELECT DATABASE()";
        var actual = (string?)await database.ExecuteScalarAsync(deadline.Token);
        if (actual is null || !string.Equals(actual, SchemaName, StringComparison.Ordinal))
            throw new InvalidOperationException("The owned custody database changed.");
        string prefix = "wolf:sql:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(actual.ToLowerInvariant())))[..24] + ":a:";
        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT IS_USED_LOCK(@first),IS_USED_LOCK(@second)";
        query.Parameters.AddWithValue("@first", prefix + first.ToString(CultureInfo.InvariantCulture));
        query.Parameters.AddWithValue("@second", prefix + second.ToString(CultureInfo.InvariantCulture));
        while (true)
        {
            bool free;
            await using (var reader = await query.ExecuteReaderAsync(deadline.Token))
            {
                if (!await reader.ReadAsync(deadline.Token) || reader.IsDBNull(1))
                    throw new InvalidOperationException("The second owned account lock was lost.");
                free = reader.IsDBNull(0);
                if (await reader.ReadAsync(deadline.Token)) throw new InvalidOperationException("Unexpected lock observation shape.");
            }
            if (free) return;
            await Task.Delay(TimeSpan.FromMilliseconds(25), deadline.Token);
        }
    }
}
