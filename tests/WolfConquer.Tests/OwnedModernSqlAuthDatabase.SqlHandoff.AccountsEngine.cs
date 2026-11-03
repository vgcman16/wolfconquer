using MySqlConnector;

namespace WolfConquer.Tests;

internal sealed partial class OwnedModernSqlAuthDatabase
{
    // Only this already-created nonce-owned schema is addressed. This helper
    // opens a fresh nonpooled guarded connection and begins no transaction.
    internal Task UseOwnedSqlHandoffMyIsamAccountsAsync(CancellationToken token) =>
        WithAuthenticationConnectionAsync(async actual =>
        {
            await RequireOwnedSqlHandoffAccountsEngineAsync(actual, "InnoDB", token);
            await WithAuthenticationResourceAsync(actual.CreateCommand(), async command =>
            {
                command.CommandText = "ALTER TABLE accounts ENGINE=MyISAM";
                await command.ExecuteNonQueryAsync(token);
                return true;
            });
            await RequireOwnedSqlHandoffAccountsEngineAsync(actual, "MyISAM", token);
            return true;
        });

    internal Task AssertOwnedSqlHandoffAccountsIsMyIsamAsync(CancellationToken token) =>
        WithAuthenticationConnectionAsync(async actual =>
        {
            await RequireOwnedSqlHandoffAccountsEngineAsync(actual, "MyISAM", token);
            return true;
        });

    internal Task<string> SnapshotOwnedSqlHandoffAccountsDefinitionAsync(CancellationToken token) =>
        WithAuthenticationConnectionAsync(async actual =>
            await WithAuthenticationResourceAsync(actual.CreateCommand(), async command =>
            {
                command.CommandText = "SHOW CREATE TABLE accounts";
                return await WithAuthenticationResourceAsync(await command.ExecuteReaderAsync(token), async rows =>
                {
                    if (!await rows.ReadAsync(token) || !string.Equals(rows.GetString(0), "accounts", StringComparison.Ordinal))
                        throw new InvalidOperationException("The owned accounts definition is missing.");
                    string definition = rows.GetString(1);
                    if (await rows.ReadAsync(token))
                        throw new InvalidOperationException("The owned accounts definition is ambiguous.");
                    return definition;
                });
            }));

    private static Task RequireOwnedSqlHandoffAccountsEngineAsync(
        MySqlConnection actual, string expected, CancellationToken token) =>
        WithAuthenticationResourceAsync(actual.CreateCommand(), async command =>
        {
            command.CommandText = """
                SELECT table_type,engine FROM information_schema.tables
                WHERE BINARY table_schema=BINARY DATABASE() AND BINARY table_name=BINARY 'accounts'
                """;
            return await WithAuthenticationResourceAsync(await command.ExecuteReaderAsync(token), async rows =>
            {
                if (!await rows.ReadAsync(token) || rows.IsDBNull(0) || rows.IsDBNull(1) ||
                    !string.Equals(rows.GetString(0), "BASE TABLE", StringComparison.Ordinal) ||
                    !string.Equals(rows.GetString(1), expected, StringComparison.Ordinal) ||
                    await rows.ReadAsync(token))
                    throw new InvalidOperationException("The actual owned accounts engine does not match the fixture phase.");
                return true;
            });
        });
}
