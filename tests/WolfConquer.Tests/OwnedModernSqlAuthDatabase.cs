using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using MySqlConnector;
using WolfConquer.Core.Accounts;

namespace WolfConquer.Tests;

// Test-only schema. Nothing here is a production schema migration or allocator.
internal sealed partial class OwnedModernSqlAuthDatabase : IAsyncDisposable
{
    internal const string FirstUsername = "sql-owner-a", SecondUsername = "sql-owner-b";
    internal const string SyntheticPassword = "fixture_sql_pw";
    private const string Prefix = "wolf_sql_auth_";
    private readonly string admin, scoped, nonce;
    private bool created, markerReady, disposed;
    internal string SchemaName { get; }

    private OwnedModernSqlAuthDatabase(string configured)
    {
        var builder = new MySqlConnectionStringBuilder(configured);
        string server = builder.Server.Trim();
        if (!server.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
            !(IPAddress.TryParse(server, out var address) && IPAddress.IsLoopback(address)))
            throw new InvalidOperationException("The owned SQL fixture requires a literal loopback endpoint.");
        if (builder.Port is < 1024 or > 65535 || builder.Port == 3306)
            throw new InvalidOperationException("The owned SQL fixture requires an explicit separate nondefault test port.");
        nonce = Guid.NewGuid().ToString("N"); SchemaName = Prefix + nonce;
        builder.Server = server; builder.Database = string.Empty; builder.Pooling = false;
        builder.ConnectionTimeout = 10; builder.DefaultCommandTimeout = 10;
        admin = builder.ConnectionString;
        builder.Database = SchemaName; scoped = builder.ConnectionString;
    }

    internal static async Task<OwnedModernSqlAuthDatabase> CreateAsync(uint firstId, uint secondId)
    {
        if (Environment.GetEnvironmentVariable(ModernSqlAuthFixtureFactAttribute.EnabledVariable) != "1")
            throw new InvalidOperationException("The owned SQL fixture is not explicitly enabled.");
        var configured = Environment.GetEnvironmentVariable(ModernSqlAuthFixtureFactAttribute.ConnectionVariable);
        if (string.IsNullOrWhiteSpace(configured)) throw new InvalidOperationException("Explicit private SQL fixture configuration is missing.");
        var fixture = new OwnedModernSqlAuthDatabase(configured);
        try
        {
            await using (var connection = new MySqlConnection(fixture.admin))
            {
                await connection.OpenAsync();
                await using var create = connection.CreateCommand();
                create.CommandText = "CREATE DATABASE `" + fixture.SchemaName + "` CHARACTER SET utf8mb4";
                await create.ExecuteNonQueryAsync(); // No IF NOT EXISTS: ownership requires this real CREATE receipt.
                fixture.created = true;
            }
            await fixture.InitializeAsync(firstId, secondId);
            return fixture;
        }
        catch (Exception primary)
        {
            try { await fixture.DisposeAsync(); }
            catch (Exception cleanup) { throw new AggregateException(primary, cleanup); }
            throw;
        }
    }

    private async Task InitializeAsync(uint firstId, uint secondId)
    {
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync();
        await ExecuteAsync(connection, "CREATE TABLE __wolf_fixture_owner (Token CHAR(32) NOT NULL PRIMARY KEY)");
        await using (var marker = connection.CreateCommand())
        {
            marker.CommandText = "INSERT INTO __wolf_fixture_owner (Token) VALUES (@token)";
            marker.Parameters.AddWithValue("@token", nonce); await marker.ExecuteNonQueryAsync(); markerReady = true;
        }
        // Only the modern repository's observed SELECT columns; fixture-only constraints.
        await ExecuteAsync(connection, """
            CREATE TABLE accounts (
                ID BIGINT NOT NULL PRIMARY KEY, Username VARCHAR(64) NOT NULL,
                Password VARCHAR(128) NOT NULL, EntityID BIGINT NULL,
                BannedID BIGINT NULL, Online INT NULL, Email VARCHAR(254) NULL)
            """);
        await using var transaction = await connection.BeginTransactionAsync();
        foreach (var row in new[] { (firstId, FirstUsername), (secondId, SecondUsername) })
        {
            await using var seed = connection.CreateCommand(); seed.Transaction = transaction;
            seed.CommandText = "INSERT INTO accounts (ID,Username,Password,EntityID,BannedID,Online,Email) VALUES (@id,@name,@password,@entity,2,0,@email)";
            seed.Parameters.AddWithValue("@id", (long)row.Item1); seed.Parameters.AddWithValue("@name", row.Item2);
            seed.Parameters.AddWithValue("@password", SyntheticPassword); seed.Parameters.AddWithValue("@entity", (long)row.Item1 + 1000);
            seed.Parameters.AddWithValue("@email", row.Item2 + "@fixture.invalid"); await seed.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    internal MySqlAccountRepository CreateRepository() => new(scoped);

    internal async Task AddDuplicateUsernameAsync(uint id)
    {
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO accounts (ID,Username,Password,EntityID,BannedID,Online,Email) VALUES (@id,@name,@password,@entity,2,0,@email)";
        command.Parameters.AddWithValue("@id", (long)id);
        command.Parameters.AddWithValue("@name", FirstUsername);
        command.Parameters.AddWithValue("@password", "other_fixture_pw");
        command.Parameters.AddWithValue("@entity", (long)id + 1000);
        command.Parameters.AddWithValue("@email", "duplicate@fixture.invalid");
        await command.ExecuteNonQueryAsync();
    }

    internal async Task<string[]> SnapshotAsync()
    {
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ID,Username,Password,EntityID,BannedID,Online,Email FROM accounts ORDER BY ID";
        await using var reader = await command.ExecuteReaderAsync(); var rows = new List<string>();
        while (await reader.ReadAsync())
        {
            string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reader.GetString(2))));
            rows.Add(string.Join('|', reader.GetInt64(0).ToString(CultureInfo.InvariantCulture), reader.GetString(1), digest,
                reader.GetInt64(3).ToString(CultureInfo.InvariantCulture), reader.GetInt64(4).ToString(CultureInfo.InvariantCulture),
                reader.GetInt32(5).ToString(CultureInfo.InvariantCulture), reader.GetString(6)));
        }
        return rows.ToArray();
    }

    internal async Task RemoveAccountsForProviderFailureAsync()
    {
        await using var connection = new MySqlConnection(scoped); await connection.OpenAsync();
        await ExecuteAsync(connection, "DROP TABLE accounts"); // This exact fresh test schema only.
    }

    private static async Task ExecuteAsync(MySqlConnection connection, string text)
    { await using var command = connection.CreateCommand(); command.CommandText = text; await command.ExecuteNonQueryAsync(); }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return; disposed = true;
        if (!created) return;
        if (SchemaName != Prefix + nonce || !Guid.TryParseExact(nonce, "N", out _))
            throw new InvalidOperationException("Owned SQL cleanup identity is invalid.");
        if (markerReady)
        {
            await using var verify = new MySqlConnection(scoped); await verify.OpenAsync();
            await using var command = verify.CreateCommand(); command.CommandText = "SELECT Token FROM __wolf_fixture_owner";
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || reader.GetString(0) != nonce || await reader.ReadAsync())
                throw new InvalidOperationException("Owned SQL cleanup marker changed; schema retained.");
        }
        await using var connection = new MySqlConnection(admin); await connection.OpenAsync();
        await ExecuteAsync(connection, "DROP DATABASE `" + SchemaName + "`"); created = false;
    }
}
