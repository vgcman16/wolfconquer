using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace WolfConquer.Tests;

internal sealed partial class OwnedModernSqlAuthDatabase
{
    private string AuthenticationAccountClaim(long accountId)
    {
        if (accountId <= 0) throw new ArgumentOutOfRangeException(nameof(accountId));
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SchemaName.ToLowerInvariant())))[..24];
        return "wolf:auth:" + hash + ":a:" + accountId.ToString(CultureInfo.InvariantCulture);
    }

    // The live schema/marker is verified by the connection helper before the
    // canonical name is derived. Private random claim names are never exported.
    internal Task<ulong?> ObserveAuthenticationHolderAsync(long accountId) => WithAuthenticationConnectionAsync(async connection =>
        await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = "SELECT IS_USED_LOCK(@name)";
            command.Parameters.AddWithValue("@name", AuthenticationAccountClaim(accountId));
            object? value = await command.ExecuteScalarAsync();
            return value is null or DBNull ? (ulong?)null : Convert.ToUInt64(value, CultureInfo.InvariantCulture);
        }));

    internal Task AssertAuthenticationClaimsReleasedAsync(params long[] accountIds) => WithAuthenticationConnectionAsync(async connection =>
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (true)
        {
            bool claimsFree = true;
            foreach (long accountId in accountIds)
                claimsFree &= await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
                {
                    command.CommandText = "SELECT IS_USED_LOCK(@name)";
                    command.Parameters.AddWithValue("@name", AuthenticationAccountClaim(accountId));
                    return await command.ExecuteScalarAsync(deadline.Token) is null or DBNull;
                });
            // Advisory claims are connection-scoped. Observing no other actual
            // session in this schema also covers the private, unexported claims
            // of these nonpooled fixture owners, which never change database.
            bool sessionsClosed = await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
            {
                command.CommandText = "SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE DB=@schema AND ID<>CONNECTION_ID()";
                command.Parameters.AddWithValue("@schema", SchemaName);
                return Convert.ToInt64(await command.ExecuteScalarAsync(deadline.Token), CultureInfo.InvariantCulture) == 0;
            });
            if (claimsFree && sessionsClosed) return true;
            // A deadline is a failure, never evidence of successful cleanup.
            await Task.Delay(TimeSpan.FromMilliseconds(25), deadline.Token);
        }
    });

    internal Task KillOwnedAuthenticationCustodyConnectionAsync(long accountId) => WithAuthenticationConnectionAsync(async connection =>
    {
        string claim = AuthenticationAccountClaim(accountId);
        ulong holder = await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = "SELECT IS_USED_LOCK(@name)";
            command.Parameters.AddWithValue("@name", claim);
            object? value = await command.ExecuteScalarAsync();
            if (value is null or DBNull)
                throw new InvalidOperationException("No actual owned authentication account holder exists.");
            return Convert.ToUInt64(value, CultureInfo.InvariantCulture);
        });
        await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = """
                SELECT COUNT(*) FROM information_schema.PROCESSLIST
                WHERE ID=@id AND DB=@schema AND ID=IS_USED_LOCK(@name) AND ID<>CONNECTION_ID()
                """;
            command.Parameters.AddWithValue("@id", holder);
            command.Parameters.AddWithValue("@schema", SchemaName);
            command.Parameters.AddWithValue("@name", claim);
            if (Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) != 1)
                throw new InvalidOperationException("The actual authentication holder is outside the owned schema.");
            return true;
        });
        return await WithAuthenticationResourceAsync(connection.CreateCommand(), async command =>
        {
            command.CommandText = "KILL CONNECTION " + holder.ToString(CultureInfo.InvariantCulture);
            await command.ExecuteNonQueryAsync();
            return true;
        });
    });
}
