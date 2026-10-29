using System.Net;
using MySqlConnector;

namespace WolfConquer.Core.Accounts;

public sealed record LegacyGuardShieldAccountConfig(
    string ServerIp,
    string DbLocation,
    string MySqlHost,
    string MySqlUsername,
    string MySqlPassword,
    string MySqlDatabase)
{
    public bool HasMySqlSettings =>
        !string.IsNullOrWhiteSpace(MySqlHost) &&
        !string.IsNullOrWhiteSpace(MySqlUsername) &&
        !string.IsNullOrWhiteSpace(MySqlDatabase);

    public string ToMySqlConnectionString()
    {
        if (!HasMySqlSettings || MySqlPassword is null)
        {
            throw new InvalidOperationException("GuardShield account config does not contain complete MySQL settings.");
        }

        var host = MySqlHost.Trim();
        var local = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
        return new MySqlConnectionStringBuilder
        {
            Server = host,
            Port = 3306,
            Database = MySqlDatabase,
            UserID = MySqlUsername,
            Password = MySqlPassword,
            SslMode = local ? MySqlSslMode.None : MySqlSslMode.VerifyFull,
            AllowPublicKeyRetrieval = local,
        }.ConnectionString;
    }

    public override string ToString() =>
        $"LegacyGuardShieldAccountConfig {{ HasMySqlSettings = {HasMySqlSettings} }}";
}
