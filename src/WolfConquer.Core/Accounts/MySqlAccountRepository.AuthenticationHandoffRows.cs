using System.Runtime.ExceptionServices;
using MySqlConnector;

namespace WolfConquer.Core.Accounts;

public sealed partial class MySqlAccountRepository
{
    internal static async ValueTask<IReadOnlyList<AccountRecord>> ReadHandoffAuthenticationRowsForUpdateAsync(
        MySqlConnection actual, MySqlTransaction transaction, string username, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(transaction);
        if (!ReferenceEquals(transaction.Connection, actual))
            throw new ArgumentException("Transaction must belong to the supplied connection.", nameof(transaction));
        ArgumentNullException.ThrowIfNull(username);
        // Preflight gives missing/view tables a deterministic refusal; the
        // post-read check runs while the actual accounts metadata lock is held.
        await VerifyHandoffAccountsEngineAsync(actual, transaction, token).ConfigureAwait(false);
        var accounts = new List<AccountRecord>();
        MySqlCommand? command = null;
        MySqlDataReader? reader = null;
        Exception? failure = null;
        try
        {
            command = actual.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                SELECT ID, Username, Password, EntityID, BannedID, Online
                FROM accounts
                WHERE Username = @username
                ORDER BY ID
                FOR UPDATE
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
        await VerifyHandoffAccountsEngineAsync(actual, transaction, token).ConfigureAwait(false);
        return accounts.ToArray();
    }
    private static async ValueTask VerifyHandoffAccountsEngineAsync(MySqlConnection actual,
        MySqlTransaction transaction, CancellationToken token)
    {
        MySqlCommand? command = null;
        MySqlDataReader? reader = null;
        Exception? failure = null;
        try
        {
            command = actual.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT TABLE_TYPE, ENGINE FROM information_schema.TABLES " +
                "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'accounts'";
            reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (!await reader.ReadAsync(token).ConfigureAwait(false) || reader.IsDBNull(0) || reader.IsDBNull(1) ||
                !string.Equals(reader.GetString(0), "BASE TABLE", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(reader.GetString(1), "InnoDB", StringComparison.OrdinalIgnoreCase) ||
                await reader.ReadAsync(token).ConfigureAwait(false))
                throw new InvalidOperationException("sqlHandoffAccountsEngineUnsupported");
        }
        catch (Exception error) { failure = error; }
        failure = await DisposeAuthenticationResourceAsync(reader, failure).ConfigureAwait(false);
        failure = await DisposeAuthenticationResourceAsync(command, failure).ConfigureAwait(false);
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
