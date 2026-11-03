using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using MySqlConnector;

namespace WolfConquer.Tests;

internal sealed partial class OwnedModernSqlAuthDatabase
{
    // Every connection used by these controls proves the live selected schema
    // and the nonce marker before a command may observe or mutate fixture data.
    internal async Task<MySqlConnection> OpenOwnedAuthenticationDomainAsync(bool? ignoreCommandTransaction = null)
    {
        string configured = scoped;
        if (ignoreCommandTransaction is { } ignore)
        {
            var settings = new MySqlConnectionStringBuilder(scoped) { IgnoreCommandTransaction = ignore };
            configured = settings.ConnectionString;
        }
        var connection = new MySqlConnection(configured);
        try
        {
            await connection.OpenAsync();
            await VerifyOwnedAuthenticationDomainAsync(connection);
            return connection;
        }
        catch (Exception primary)
        {
            try { await connection.DisposeAsync(); }
            catch (Exception cleanup) { throw new AggregateException(primary, cleanup); }
            throw;
        }
    }

    private async Task VerifyOwnedAuthenticationDomainAsync(MySqlConnection connection)
    {
        if (disposed || !created || !markerReady || SchemaName != Prefix + nonce ||
            !Guid.TryParseExact(nonce, "N", out _))
            throw new InvalidOperationException("Owned authentication fixture identity is invalid.");
        await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = "SELECT DATABASE(),Token FROM __wolf_fixture_owner";
            return await WithAuthenticationResourceAsync(await command.ExecuteReaderAsync(), async reader =>
            {
                if (!await reader.ReadAsync() || reader.IsDBNull(0) || reader.IsDBNull(1) ||
                    !string.Equals(reader.GetString(0), SchemaName, StringComparison.Ordinal) ||
                    !string.Equals(reader.GetString(1), nonce, StringComparison.Ordinal) || await reader.ReadAsync())
                    throw new InvalidOperationException("Owned authentication fixture live domain or marker changed.");
                return true;
            });
        });
    }

    private async Task<T> WithAuthenticationConnectionAsync<T>(Func<MySqlConnection, Task<T>> body) =>
        await WithAuthenticationResourceAsync(await OpenOwnedAuthenticationDomainAsync(), body);

    private static async Task<T> WithAuthenticationResourceAsync<TResource, T>(
        TResource resource, Func<TResource, Task<T>> body) where TResource : IAsyncDisposable
    {
        var errors = new List<Exception>();
        T result = default!;
        try { result = await body(resource); }
        catch (Exception error) { errors.Add(error); }
        try { await resource.DisposeAsync(); }
        catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
        return result;
    }

    internal Task<string[]> SnapshotAuthenticationRowsAsync() => WithAuthenticationConnectionAsync(async connection =>
        await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = "SELECT ID,Username,Password,EntityID,BannedID,Online,Email FROM accounts ORDER BY ID";
            return await WithAuthenticationResourceAsync(await command.ExecuteReaderAsync(), async reader =>
            {
                var rows = new List<string>();
                while (await reader.ReadAsync())
                {
                    var fields = new string[reader.FieldCount];
                    for (int ordinal = 0; ordinal < fields.Length; ordinal++)
                        fields[ordinal] = reader.IsDBNull(ordinal) ? "<NULL>" : ordinal == 2
                            ? AuthenticationSecretDigest(reader.GetString(ordinal))
                            : Convert.ToBase64String(Encoding.UTF8.GetBytes(
                                Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)!));
                    rows.Add(string.Join('|', fields));
                }
                return rows.ToArray();
            });
        }));

    private static string AuthenticationSecretDigest(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        try { return Convert.ToHexString(SHA256.HashData(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
