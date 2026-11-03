using MySqlConnector;

namespace WolfConquer.Core.Accounts;

public sealed partial class MySqlAccountRepository
{
    private MySqlConnection CreateUsernameReadConnection()
    {
        // Preserve the legacy read's configured pooling/endpoint behavior. SQL
        // TINYINT(1)/BOOL values must retain their integer state, including 2.
        var settings = new MySqlConnectionStringBuilder(_connectionString) { TreatTinyAsBoolean = false };
        return new(settings.ConnectionString);
    }
}
