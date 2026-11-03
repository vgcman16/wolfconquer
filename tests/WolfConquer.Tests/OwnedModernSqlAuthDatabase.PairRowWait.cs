using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MySqlConnector;

namespace WolfConquer.Tests;

internal sealed partial class OwnedModernSqlAuthDatabase
{
    // MariaDB INNODB_LOCK_WAITS/INNODB_TRX require PROCESS privilege. Both
    // connection IDs are obtained from the actual named account locks, and
    // both process rows plus the requested lock must belong to this schema.
    internal async Task WaitForOwnedPairRowWaitAsync(long blockingAccount, long waitingAccount, CancellationToken token)
    {
        if (disposed || !created || !markerReady || blockingAccount <= 0 || waitingAccount <= blockingAccount ||
            SchemaName != Prefix + nonce || !Guid.TryParseExact(nonce, "N", out _))
            throw new InvalidOperationException("Owned pair row-wait fixture identity is invalid.");
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync(token);
        await using (var marker = connection.CreateCommand())
        {
            marker.CommandText = "SELECT Token FROM __wolf_fixture_owner";
            await using var reader = await marker.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token) || reader.GetString(0) != nonce || await reader.ReadAsync(token))
                throw new InvalidOperationException("Owned pair row-wait marker changed.");
        }
        await using var database = connection.CreateCommand(); database.CommandText = "SELECT DATABASE()";
        var actual = (string?)await database.ExecuteScalarAsync(token);
        if (actual is null || !string.Equals(actual, SchemaName, StringComparison.Ordinal))
            throw new InvalidOperationException("Owned pair row-wait database changed.");
        string prefix = "wolf:sql:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(actual.ToLowerInvariant())))[..24] + ":a:";
        await using var query = connection.CreateCommand();
        query.CommandText = """
            SELECT COUNT(*) FROM information_schema.INNODB_LOCK_WAITS w
            JOIN information_schema.INNODB_TRX waiting ON waiting.TRX_ID=w.REQUESTING_TRX_ID
            JOIN information_schema.INNODB_TRX blocking ON blocking.TRX_ID=w.BLOCKING_TRX_ID
            JOIN information_schema.INNODB_LOCKS requested ON requested.LOCK_ID=w.REQUESTED_LOCK_ID
            JOIN information_schema.PROCESSLIST wp ON wp.ID=waiting.TRX_MYSQL_THREAD_ID
            JOIN information_schema.PROCESSLIST bp ON bp.ID=blocking.TRX_MYSQL_THREAD_ID
            WHERE waiting.TRX_MYSQL_THREAD_ID=IS_USED_LOCK(@waitingName)
              AND blocking.TRX_MYSQL_THREAD_ID=IS_USED_LOCK(@blockingName)
              AND wp.DB=@schema AND bp.DB=@schema
              AND requested.LOCK_TABLE=@table AND requested.LOCK_INDEX='PRIMARY'
              AND requested.LOCK_DATA=@row
            """;
        query.Parameters.AddWithValue("@waitingName", prefix + waitingAccount.ToString(CultureInfo.InvariantCulture));
        query.Parameters.AddWithValue("@blockingName", prefix + blockingAccount.ToString(CultureInfo.InvariantCulture));
        query.Parameters.AddWithValue("@schema", SchemaName);
        query.Parameters.AddWithValue("@table", "`" + SchemaName + "`.`wolf_sql_world_state`");
        query.Parameters.AddWithValue("@row", waitingAccount.ToString(CultureInfo.InvariantCulture));
        int attempts = 0;
        long lastCount = -1;
        try
        {
            while (true)
            {
                lastCount = Convert.ToInt64(await query.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
                attempts++;
                if (lastCount == 1) return;
                if (lastCount != 0) throw new InvalidOperationException("Unexpected duplicate owned pair row-wait observations.");
                // MariaDB12.2.2 trx0i_s.cc requires >100ms idle since the last
                // metadata-cache read. Faster polling can indefinitely retain
                // the pre-wait snapshot; the exact success predicate stays.
                await Task.Delay(TimeSpan.FromMilliseconds(200), token);
            }
        }
        catch (OperationCanceledException error) when (token.IsCancellationRequested)
        {
            throw new TimeoutException("Owned pair row-wait predicate was not observed; attempts=" +
                attempts.ToString(CultureInfo.InvariantCulture) + ",lastMatches=" +
                lastCount.ToString(CultureInfo.InvariantCulture), error);
        }
    }
}
