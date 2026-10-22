using MySql.Data.MySqlClient;

namespace WolfConquer.Core.Data;

// Native DataHolder storage, CreateConnection and original-provider getter only.
// Host startup/database I/O remain unwired. The public build restores MySql.Data 6.3.6
// from NuGet; package bytes are not asserted equivalent to the private reference DLL.
public static class LegacyNativeDataHolder
{
	public static string? ConnectionString;

	public static MySqlConnection MySqlConnection
	{
		get
		{
			MySqlConnection mySqlConnection = new MySqlConnection();
			mySqlConnection.ConnectionString = ConnectionString;
			return mySqlConnection;
		}
	}

	public static bool CreateConnection(string? host, string? username, string? password, string? database)
	{
		ConnectionString = "Server=" + host + ";Port=3306;Database=" + database + ";Uid=" + username + ";Password=" + password + ";Persist Security Info=True;Pooling=true; Min Pool Size = 32;  Max Pool Size = 300;";
		return true;
	}
}
