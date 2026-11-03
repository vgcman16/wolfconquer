using System.Runtime.ExceptionServices;
using MySqlConnector;

namespace WolfConquer.Core.Accounts;

public sealed partial class MySqlAccountRepository
{
    internal static async ValueTask<IReadOnlyList<AccountRecord>> ReadAuthenticationRowsAsync(
        MySqlConnection actual, string username, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(username);
        var accounts = new List<AccountRecord>();
        MySqlCommand? command = null;
        MySqlDataReader? reader = null;
        Exception? failure = null;
        try
        {
            command = actual.CreateCommand();
            command.CommandText = """
                SELECT ID, Username, Password, EntityID, BannedID, Online
                FROM accounts
                WHERE Username = @username
                ORDER BY ID
                """;
            command.Parameters.AddWithValue("@username", username);
            reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            // Do not select a single matching password or return a valid prefix.
            // DB collation still selects rows; their stored names are not rewritten.
            while (await reader.ReadAsync(token).ConfigureAwait(false))
                accounts.Add(ReadAuthenticationRow(reader));
        }
        catch (Exception error) { failure = error; }
        failure = await DisposeAuthenticationResourceAsync(reader, failure).ConfigureAwait(false);
        failure = await DisposeAuthenticationResourceAsync(command, failure).ConfigureAwait(false);
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        return accounts.ToArray();
    }
}
