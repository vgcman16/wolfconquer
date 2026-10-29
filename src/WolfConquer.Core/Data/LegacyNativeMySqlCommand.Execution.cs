using MySql.Data.MySqlClient;
using System.Globalization;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeMySqlCommand
{
    // Pure preparation: no connection is created or opened here. Caller owns the result.
    public MySqlCommand CreateProviderCommand(MySqlConnection? connection = null)
    {
        ThrowIfDisposed();
        string text = RenderCommand();
        if (string.IsNullOrWhiteSpace(text) || (!_rawSql &&
            (text.Contains("<R>", StringComparison.Ordinal) || text.Contains("<C>", StringComparison.Ordinal) ||
             text.Contains("<F>", StringComparison.Ordinal) || text.Contains("<V>", StringComparison.Ordinal) ||
             (_type == LegacyNativeMySqlCommandType.UPDATE && !_hasSet))))
            throw new InvalidOperationException("The SQL command is incomplete.");
        var command = new MySqlCommand(text, connection);
        try
        {
            for (int index = 0; index < _values.Count; index++)
            {
                BoundValue bound = _values[index];
                command.Parameters.Add("@p" + index.ToString(CultureInfo.InvariantCulture), bound.Type).Value = bound.Value;
            }
            return command;
        }
        catch
        {
            command.Dispose();
            throw;
        }
    }

    public int Execute2()
    {
        RequireConfiguredConnection(nameof(Execute2));
        using MySqlCommand command = CreateProviderCommand();
        using MySqlConnection connection = LegacyNativeDataHolder.MySqlConnection;
        command.Connection = connection;
        connection.Open();
        return command.ExecuteNonQuery();
    }

    public int Execute()
    {
        using MySqlCommand command = CreateProviderCommand();
        RequireConfiguredConnection(nameof(Execute));
        using MySqlConnection connection = LegacyNativeDataHolder.MySqlConnection;
        command.Connection = connection;
        connection.Open();
        return command.ExecuteNonQuery();
    }

    internal static void RequireConfiguredConnection(string operation)
    {
        if (string.IsNullOrWhiteSpace(LegacyNativeDataHolder.ConnectionString))
            throw new InvalidOperationException(operation + " requires an explicitly configured database connection string.");
    }
    public int Execute(MySqlConnection conn)
    {
        ArgumentNullException.ThrowIfNull(conn);
        using MySqlCommand command = CreateProviderCommand(conn);
        return command.ExecuteNonQuery();
    }
}
