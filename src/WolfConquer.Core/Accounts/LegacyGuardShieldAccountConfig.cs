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
        if (!HasMySqlSettings)
        {
            throw new InvalidOperationException("GuardShield account config does not contain complete MySQL settings.");
        }

        return "Server=" + MySqlHost +
            ";Port=3306;Database=" + MySqlDatabase +
            ";User ID=" + MySqlUsername +
            ";Password=" + MySqlPassword +
            ";SslMode=None;AllowPublicKeyRetrieval=True;";
    }
}
