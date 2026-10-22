#nullable disable

// Genuine native network-capable owner; this prerequisite installs no caller or loading route.
using System;
using System.Text;
using MySql.Data.MySqlClient;

namespace WolfConquer.Core.Data;

internal class LegacyNativeMySqlCommand : IDisposable
{
	private LegacyNativeMySqlCommandType _type;

	protected StringBuilder _command;

	private bool firstPart = true;

	private LegacyNativeDbSafeDictionary<byte, string> insertFields;

	private LegacyNativeDbSafeDictionary<byte, string> insertValues;

	private byte lastpair;

	public LegacyNativeMySqlCommandType Type
	{
		get
		{
			return _type;
		}
		set
		{
			_type = value;
		}
	}

	public string Command
	{
		get
		{
			return _command.ToString();
		}
		set
		{
			_command = new StringBuilder(value);
		}
	}

	public LegacyNativeMySqlCommand(LegacyNativeMySqlCommandType Type)
	{
		this.Type = Type;
		switch (Type)
		{
		case LegacyNativeMySqlCommandType.SELECT:
			_command = new StringBuilder("SELECT * FROM <R>");
			break;
		case LegacyNativeMySqlCommandType.UPDATE:
			_command = new StringBuilder("UPDATE <R> SET ");
			break;
		case LegacyNativeMySqlCommandType.INSERT:
			insertFields = new LegacyNativeDbSafeDictionary<byte, string>();
			insertValues = new LegacyNativeDbSafeDictionary<byte, string>();
			lastpair = 0;
			_command = new StringBuilder("INSERT INTO <R> (<F>) VALUES (<V>)");
			break;
		case LegacyNativeMySqlCommandType.DELETE:
			_command = new StringBuilder("DELETE FROM <R> WHERE <C> = <V>");
			break;
		case LegacyNativeMySqlCommandType.COUNT:
			_command = new StringBuilder("SELECT count(<V>) FROM <R>");
			break;
		}
	}

	public int Execute2()
	{
		if (Type == LegacyNativeMySqlCommandType.INSERT)
		{
			string text = "";
			string text2 = "";
			for (byte b = 0; b < lastpair; b++)
			{
				bool flag = ((b + 1 != lastpair) ? true : false);
				text = ((!flag) ? (text + "`" + insertFields[b] + "`") : (text + "`" + insertFields[b] + "`,"));
				text2 = ((!flag) ? (text2 + "'" + insertValues[b] + "'") : (text2 + "'" + insertValues[b] + "',"));
			}
			_command = _command.Replace("<F>", text);
			_command = _command.Replace("<V>", text2);
		}
		// Publication change: never override explicit configuration with original deployment credentials.
		if (string.IsNullOrWhiteSpace(LegacyNativeDataHolder.ConnectionString))
		{
			throw new InvalidOperationException("Execute2 requires an explicitly configured database connection string.");
		}
		using MySqlConnection mySqlConnection = LegacyNativeDataHolder.MySqlConnection;
		mySqlConnection.Open();
		MySql.Data.MySqlClient.MySqlCommand mySqlCommand = new MySql.Data.MySqlClient.MySqlCommand(Command + ";", mySqlConnection);
		return mySqlCommand.ExecuteNonQuery();
	}

	private bool Comma()
	{
		if (firstPart)
		{
			firstPart = false;
			return false;
		}
		string text = _command.ToString();
		if (text[text.Length - 1] == ',' || text[text.Length - 2] == ',' || text[text.Length - 3] == ',')
		{
			return false;
		}
		return true;
	}

	public LegacyNativeMySqlCommand Select(string table)
	{
		_command = _command.Replace("<R>", "`" + table + "`");
		return this;
	}

	public LegacyNativeMySqlCommand Count(string table)
	{
		_command = _command.Replace("<R>", "`" + table + "`");
		return this;
	}

	public LegacyNativeMySqlCommand Delete(string table, string column, string value)
	{
		_command = _command.Replace("<R>", "`" + table + "`");
		_command = _command.Replace("<C>", "`" + column + "`");
		_command = _command.Replace("<V>", "'" + value.MySqlEscape() + "'");
		return this;
	}

	public LegacyNativeMySqlCommand Delete(string table, string column, long value)
	{
		_command = _command.Replace("<R>", "`" + table + "`");
		_command = _command.Replace("<C>", "`" + column + "`");
		_command = _command.Replace("<V>", value.ToString());
		return this;
	}

	public LegacyNativeMySqlCommand Delete(string table, string column, ulong value)
	{
		_command = _command.Replace("<R>", "`" + table + "`");
		_command = _command.Replace("<C>", "`" + column + "`");
		_command = _command.Replace("<V>", value.ToString());
		return this;
	}

	public LegacyNativeMySqlCommand Delete(string table, string column, bool value)
	{
		_command = _command.Replace("<R>", "`" + table + "`");
		_command = _command.Replace("<C>", "`" + column + "`");
		_command = _command.Replace("<V>", value ? "1" : "0");
		return this;
	}

	public LegacyNativeMySqlCommand Update(string table)
	{
		_command = _command.Replace("<R>", "`" + table + "`");
		return this;
	}

	public LegacyNativeMySqlCommand Set(string column, long value)
	{
		if (Type == LegacyNativeMySqlCommandType.UPDATE)
		{
			if (Comma())
			{
				_command = _command.Append(",`" + column + "` = " + value + " ");
			}
			else
			{
				_command = _command.Append("`" + column + "` = " + value + " ");
			}
		}
		return this;
	}

	public LegacyNativeMySqlCommand Set(string column, double value)
	{
		if (Type == LegacyNativeMySqlCommandType.UPDATE)
		{
			if (Comma())
			{
				_command = _command.Append(",`" + column + "` = " + value + " ");
			}
			else
			{
				_command = _command.Append("`" + column + "` = " + value + " ");
			}
		}
		return this;
	}

	public LegacyNativeMySqlCommand Set(string column, ulong value)
	{
		if (Type == LegacyNativeMySqlCommandType.UPDATE)
		{
			if (Comma())
			{
				_command = _command.Append(",`" + column + "` = " + value + " ");
			}
			else
			{
				_command = _command.Append("`" + column + "` = " + value + " ");
			}
		}
		return this;
	}

	public LegacyNativeMySqlCommand Set(string column, string value)
	{
		if (Type == LegacyNativeMySqlCommandType.UPDATE)
		{
			if (Comma())
			{
				_command = _command.Append(",`" + column + "` = '" + value + "' ");
			}
			else
			{
				_command = _command.Append("`" + column + "` = '" + value.MySqlEscape() + "' ");
			}
		}
		return this;
	}

	public LegacyNativeMySqlCommand Set(string column, bool value)
	{
		if (Type == LegacyNativeMySqlCommandType.UPDATE)
		{
			if (Comma())
			{
				_command = _command.Append(",`" + column + "` = " + (value ? "1" : "0") + " ");
			}
			else
			{
				_command = _command.Append("`" + column + "` = " + (value ? "1" : "0") + " ");
			}
		}
		return this;
	}

	public LegacyNativeMySqlCommand Set(string column, object value)
	{
		if (value is bool)
		{
			Set(column, (bool)value);
		}
		else
		{
			Set(column, value.ToString());
		}
		return this;
	}

	public LegacyNativeMySqlCommand Insert(string table)
	{
		_command = _command.Replace("<R>", "`" + table + "`");
		return this;
	}

	public LegacyNativeMySqlCommand Insert(string field, long value)
	{
		insertFields.Add(lastpair, field);
		insertValues.Add(lastpair, value.ToString());
		lastpair++;
		return this;
	}

	public LegacyNativeMySqlCommand Insert(string field, ulong value)
	{
		insertFields.Add(lastpair, field);
		insertValues.Add(lastpair, value.ToString());
		lastpair++;
		return this;
	}

	public LegacyNativeMySqlCommand Insert(string field, bool value)
	{
		insertFields.Add(lastpair, field);
		insertValues.Add(lastpair, (value ? 1 : 0).ToString());
		lastpair++;
		return this;
	}

	public LegacyNativeMySqlCommand Insert(string field, string value)
	{
		char[] array = value.ToCharArray();
		string text = Encoding.Default.GetString(Encoding.Unicode.GetBytes(array, 0, array.Length));
		insertFields.Add(lastpair, field);
		insertValues.Add(lastpair, value.MySqlEscape());
		lastpair++;
		return this;
	}

	public LegacyNativeMySqlCommand Where(string column, long value)
	{
		_command = _command.Append("WHERE `" + column + "` = " + value);
		return this;
	}

	public LegacyNativeMySqlCommand Where(string column, long value, bool greater)
	{
		if (greater)
		{
			_command = _command.Append("WHERE `" + column + "` > " + value);
		}
		else
		{
			_command = _command.Append("WHERE `" + column + "` < " + value);
		}
		return this;
	}

	public LegacyNativeMySqlCommand Where(string column, ulong value)
	{
		_command = _command.Append("WHERE `" + column + "` = " + value);
		return this;
	}

	public LegacyNativeMySqlCommand Where(string column, string value)
	{
		_command = _command.Append("WHERE `" + column + "` = '" + value.MySqlEscape() + "'");
		return this;
	}

	public LegacyNativeMySqlCommand Where(string column, bool value)
	{
		_command = _command.Append("WHERE `" + column + "` = " + (value ? "1" : "0"));
		return this;
	}

	public LegacyNativeMySqlCommand And(string column, long value)
	{
		_command = _command.Append(" AND `" + column + "` = " + value);
		return this;
	}

	public LegacyNativeMySqlCommand And(string column, ulong value)
	{
		_command = _command.Append(" AND `" + column + "` = " + value);
		return this;
	}

	public LegacyNativeMySqlCommand And(string column, string value)
	{
		_command = _command.Append(" AND `" + column + "` = '" + value.MySqlEscape() + "'");
		return this;
	}

	public LegacyNativeMySqlCommand And(string column, bool value)
	{
		_command = _command.Append(" AND `" + column + "` = " + (value ? "1" : "0"));
		return this;
	}

	public LegacyNativeMySqlCommand And(string column, long value, bool greater)
	{
		if (greater)
		{
			_command = _command.Append(" AND `" + column + "` > " + value);
		}
		else
		{
			_command = _command.Append(" AND `" + column + "` < " + value);
		}
		return this;
	}

	public LegacyNativeMySqlCommand Or(string column, long value)
	{
		_command = _command.Append(" Or `" + column + "` = " + value);
		return this;
	}

	public LegacyNativeMySqlCommand Or(string column, ulong value)
	{
		_command = _command.Append(" Or `" + column + "` = " + value);
		return this;
	}

	public LegacyNativeMySqlCommand Or(string column, string value)
	{
		_command = _command.Append(" Or `" + column + "` = '" + value.MySqlEscape() + "'");
		return this;
	}

	public LegacyNativeMySqlCommand Or(string column, bool value)
	{
		_command = _command.Append(" Or `" + column + "` = " + (value ? "1" : "0"));
		return this;
	}

	public LegacyNativeMySqlCommand Order(string column)
	{
		_command = _command.Append("ORDER BY " + column);
		return this;
	}

	public int Execute()
	{
		using MySqlConnection mySqlConnection = LegacyNativeDataHolder.MySqlConnection;
		mySqlConnection.Open();
		return Execute(mySqlConnection);
	}

	public int Execute(MySqlConnection conn)
	{
		if (Type == LegacyNativeMySqlCommandType.INSERT)
		{
			string text = "";
			string text2 = "";
			for (byte b = 0; b < lastpair; b++)
			{
				bool flag = ((b + 1 != lastpair) ? true : false);
				text = ((!flag) ? (text + "`" + insertFields[b] + "`") : (text + "`" + insertFields[b] + "`,"));
				text2 = ((!flag) ? (text2 + "'" + insertValues[b] + "'") : (text2 + "'" + insertValues[b] + "',"));
			}
			_command = _command.Replace("<F>", text);
			_command = _command.Replace("<V>", text2);
		}
		MySql.Data.MySqlClient.MySqlCommand mySqlCommand = new MySql.Data.MySqlClient.MySqlCommand(Command, conn);
		return mySqlCommand.ExecuteNonQuery();
	}

	public LegacyNativeMySqlReader CreateReader()
	{
		return new LegacyNativeMySqlReader(this);
	}

	void IDisposable.Dispose()
	{
		if (insertValues != null)
		{
			insertValues.Clear();
			insertFields.Clear();
		}
		_command = null;
	}
}
