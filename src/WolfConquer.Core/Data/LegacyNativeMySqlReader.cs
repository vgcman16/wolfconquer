#nullable disable

// Genuine native network-capable owner; this prerequisite installs no caller or loading route.
using System;
using System.Data;
using MySql.Data.MySqlClient;

namespace WolfConquer.Core.Data;

internal class LegacyNativeMySqlReader : IDisposable
{
	private DataSet _dataset;

	private DataRow _datarow;

	private int _row;

	private const string Table = "table";

	public int NumberOfRows
	{
		get
		{
			if (_dataset == null)
			{
				return 0;
			}
			if (_dataset.Tables.Count == 0)
			{
				return 0;
			}
			return _dataset.Tables["table"].Rows.Count;
		}
	}

	public LegacyNativeMySqlReader(LegacyNativeMySqlCommand command)
	{
		if (command.Type != LegacyNativeMySqlCommandType.SELECT)
		{
			return;
		}
		_dataset = new DataSet();
		_row = 0;
		using MySqlConnection mySqlConnection = LegacyNativeDataHolder.MySqlConnection;
		mySqlConnection.Open();
		using (MySqlDataAdapter mySqlDataAdapter = new MySqlDataAdapter(command.Command, mySqlConnection))
		{
			mySqlDataAdapter.SelectCommand.CommandTimeout = 0;
			mySqlDataAdapter.Fill(_dataset, "table");
		}
		((IDisposable)command).Dispose();
	}

	void IDisposable.Dispose()
	{
		if (_dataset != null)
		{
			_dataset.Dispose();
		}
	}

	public bool Read()
	{
		if (_dataset == null)
		{
			return false;
		}
		if (_dataset.Tables.Count == 0)
		{
			return false;
		}
		if (_dataset.Tables["table"].Rows.Count > _row)
		{
			_datarow = _dataset.Tables["table"].Rows[_row];
			_row++;
			return true;
		}
		_row++;
		return false;
	}

	public void Dispose()
	{
	}

	public void Close()
	{
	}

	public sbyte ReadSByte(string columnName)
	{
		if (_datarow.IsNull(columnName))
		{
			return 0;
		}
		sbyte result = 0;
		sbyte.TryParse(_datarow[columnName].ToString(), out result);
		return result;
	}

	public byte ReadByte(string columnName)
	{
		if (_datarow.IsNull(columnName))
		{
			return 0;
		}
		byte result = 0;
		byte.TryParse(_datarow[columnName].ToString(), out result);
		return result;
	}

	public short ReadInt16(string columnName)
	{
		if (_datarow.IsNull(columnName))
		{
			return 0;
		}
		short result = 0;
		short.TryParse(_datarow[columnName].ToString(), out result);
		return result;
	}

	public ushort ReadUInt16(string columnName)
	{
		if (_datarow.IsNull(columnName))
		{
			return 0;
		}
		ushort result = 0;
		ushort.TryParse(_datarow[columnName].ToString(), out result);
		return result;
	}

	public int ReadInt32(string columnName)
	{
		if (_datarow.IsNull(columnName))
		{
			return 0;
		}
		int result = 0;
		int.TryParse(_datarow[columnName].ToString(), out result);
		return result;
	}

	public uint ReadUInt32(string columnName)
	{
		if (_datarow.IsNull(columnName))
		{
			return 0u;
		}
		uint result = 0u;
		uint.TryParse(_datarow[columnName].ToString(), out result);
		return result;
	}

	public long ReadInt64(string columnName)
	{
		if (_datarow.IsNull(columnName))
		{
			return 0L;
		}
		long result = 0L;
		long.TryParse(_datarow[columnName].ToString(), out result);
		return result;
	}

	public ulong ReadUInt64(string columnName)
	{
		if (_datarow.IsNull(columnName))
		{
			return 0uL;
		}
		ulong result = 0uL;
		ulong.TryParse(_datarow[columnName].ToString(), out result);
		return result;
	}

	public double ReadDouble(string columnName)
	{
		if (_datarow.IsNull(columnName))
		{
			return 0.0;
		}
		double result = 0.0;
		double.TryParse(_datarow[columnName].ToString(), out result);
		return result;
	}

	public ulong ReadUInt128(string columnName)
	{
		if (_datarow.IsNull(columnName))
		{
			return 0uL;
		}
		ulong result = 0uL;
		ulong.TryParse(_datarow[columnName].ToString(), out result);
		return result;
	}

	public string ReadString(string columnName)
	{
		if (_datarow.IsNull(columnName))
		{
			return "";
		}
		return _datarow[columnName].ToString();
	}

	public uint method_6(string columnName)
	{
		uint result = 0u;
		uint.TryParse(_datarow[columnName].ToString(), out result);
		return result;
	}

	public long method_7(string columnName)
	{
		long result = 0L;
		long.TryParse(_datarow[columnName].ToString(), out result);
		return result;
	}

	public bool ReadBoolean(string columnName)
	{
		if (_datarow.IsNull(columnName))
		{
			return false;
		}
		bool result = false;
		string text = _datarow[columnName].ToString();
		if (text == "")
		{
			return false;
		}
		if (text[0] == '1')
		{
			return true;
		}
		if (text[0] == '0')
		{
			return false;
		}
		bool.TryParse(text, out result);
		return result;
	}

	public byte[] ReadBlob(string columnName)
	{
		if (_datarow.IsNull(columnName))
		{
			return new byte[0];
		}
		return (byte[])_datarow[columnName];
	}

	public uint[] ReadUIntArray(string columnName, string[] separator)
	{
		if (_datarow.IsNull(columnName))
		{
			return new uint[0];
		}
		string[] array = _datarow[columnName].ToString().Split(separator, StringSplitOptions.RemoveEmptyEntries);
		uint[] array2 = new uint[array.Length];
		for (int i = 0; i < array.Length; i++)
		{
			uint result = 0u;
			uint.TryParse(array[i], out result);
			array2[i] = result;
		}
		return array2;
	}
}
