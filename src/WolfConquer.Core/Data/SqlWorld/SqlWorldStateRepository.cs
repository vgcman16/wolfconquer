using MySqlConnector;

namespace WolfConquer.Core.Data.SqlWorld;

/// <summary>
/// Owns only SQL persistence. Callers must independently establish gameplay authority.
/// </summary>
public sealed partial class SqlWorldStateRepository
{
    private readonly string connectionString;

    public SqlWorldStateRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("MySQL connection string cannot be empty.", nameof(connectionString));
        }

        this.connectionString = connectionString;
    }

    private MySqlConnection CreateConnection()
    {
        var settings = new MySqlConnectionStringBuilder(connectionString) { Pooling = false };
        if (settings.Server.Contains(','))
            throw new InvalidOperationException("sqlWorldMultipleServerDomainsUnsupported");
        return new(settings.ConnectionString);
    }
}
