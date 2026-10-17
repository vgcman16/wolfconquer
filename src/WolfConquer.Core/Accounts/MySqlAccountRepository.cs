using System.Globalization;
using MySqlConnector;

namespace WolfConquer.Core.Accounts;

public sealed class MySqlAccountRepository :
    IAccountRepository,
    IAccountEmailVerifier,
    IAccountPasswordDiagnosticRepository
{
    private readonly string _connectionString;

    public MySqlAccountRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("MySQL connection string cannot be empty.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async ValueTask<bool> MatchesEmailAsync(
        long accountId,
        string email,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT 1
            FROM accounts
            WHERE accounts.ID = @accountId
              AND accounts.Email = @email
            LIMIT 1
            """;
        command.Parameters.AddWithValue("@accountId", accountId);
        command.Parameters.AddWithValue("@email", email);

        var match = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return match is not null;
    }

    public async ValueTask<IReadOnlyList<AccountRecord>> FindByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(username);

        var accounts = new List<AccountRecord>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ID, Username, Password, EntityID, BannedID, Online
            FROM accounts
            WHERE Username = @username
            ORDER BY ID
            """;
        command.Parameters.AddWithValue("@username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            accounts.Add(new AccountRecord(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                reader.IsDBNull(4) ? AccountLoginService.AllowedBannedId : reader.GetInt64(4),
                !reader.IsDBNull(5) && reader.GetInt32(5) != 0));
        }

        return accounts;
    }

    public async ValueTask<IReadOnlyList<AccountPasswordOwnerDiagnostic>> FindByPasswordAsync(
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(password);

        var accounts = new List<AccountPasswordOwnerDiagnostic>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ID, Username, CHAR_LENGTH(Password)
            FROM accounts
            WHERE Password = @password
            ORDER BY ID
            LIMIT 5
            """;
        command.Parameters.AddWithValue("@password", password);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            accounts.Add(new AccountPasswordOwnerDiagnostic(
                reader.GetInt64(0),
                reader.GetString(1),
                Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture)));
        }

        return accounts;
    }
}
