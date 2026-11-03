using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MySqlConnector;

namespace WolfConquer.Tests;

internal sealed partial class OwnedModernSqlAuthDatabase
{
    // Only the known fixture schema, its verified marker and the connection
    // currently holding its exact account lock may be addressed.
    internal async Task KillOwnedWorldCustodyConnectionAsync(long accountId)
    {
        if (disposed || !created || !markerReady || accountId <= 0 ||
            SchemaName != Prefix + nonce || !Guid.TryParseExact(nonce, "N", out _))
            throw new InvalidOperationException("Owned custody fixture identity is invalid.");
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync();
        await using (var marker = connection.CreateCommand())
        {
            marker.CommandText = "SELECT Token FROM __wolf_fixture_owner";
            await using var reader = await marker.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || reader.GetString(0) != nonce || await reader.ReadAsync())
                throw new InvalidOperationException("Owned custody fixture marker changed.");
        }
        await using var database = connection.CreateCommand(); database.CommandText = "SELECT DATABASE()";
        var actualDatabase = (string?)await database.ExecuteScalarAsync();
        if (actualDatabase is null || !string.Equals(actualDatabase, SchemaName, StringComparison.Ordinal))
            throw new InvalidOperationException("The owned custody database changed.");
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(actualDatabase.ToLowerInvariant())))[..24];
        string name = "wolf:sql:" + hash + ":a:" + accountId.ToString(CultureInfo.InvariantCulture);
        await using var find = connection.CreateCommand();
        find.CommandText = "SELECT IS_USED_LOCK(@name)"; find.Parameters.AddWithValue("@name", name);
        object? value = await find.ExecuteScalarAsync();
        if (value is null or DBNull) throw new InvalidOperationException("No actual custody lock holder exists.");
        ulong holder = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
        await using var verify = connection.CreateCommand();
        verify.CommandText = "SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE ID=@id AND DB=@schema AND ID=IS_USED_LOCK(@name)";
        verify.Parameters.AddWithValue("@id", holder); verify.Parameters.AddWithValue("@schema", SchemaName); verify.Parameters.AddWithValue("@name", name);
        if (Convert.ToInt32(await verify.ExecuteScalarAsync(), CultureInfo.InvariantCulture) != 1)
            throw new InvalidOperationException("The lock holder is not in the owned fixture schema.");
        await using var kill = connection.CreateCommand();
        kill.CommandText = "KILL CONNECTION " + holder.ToString(CultureInfo.InvariantCulture);
        await kill.ExecuteNonQueryAsync();
    }
}
