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
			ThrowIfDisposed();
			if (_dataset == null)
			{
				return 0;
			}
			if (!_dataset.Tables.Contains(Table))
			{
				return 0;
			}
			return _dataset.Tables["table"].Rows.Count;
		}
	}

	public LegacyNativeMySqlReader(LegacyNativeMySqlCommand command)
        : this(Load(command))
    {
    }

    // Ownership transfers to this buffered reader; the caller must not reuse/dispose the DataSet.
    internal LegacyNativeMySqlReader(DataSet dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        _dataset = dataset;
    }

    internal static MySqlCommand PrepareSelect(LegacyNativeMySqlCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Type != LegacyNativeMySqlCommandType.SELECT)
            throw new ArgumentException("A SELECT command is required.", nameof(command));
        MySqlCommand select = command.CreateProviderCommand();
        select.CommandTimeout = 0;
        return select;
    }

    private static DataSet Load(LegacyNativeMySqlCommand command)
    {
        using MySqlCommand select = PrepareSelect(command);
        LegacyNativeMySqlCommand.RequireConfiguredConnection("Reader");
        using MySqlConnection connection = LegacyNativeDataHolder.MySqlConnection;
        select.Connection = connection;
        using MySqlDataAdapter adapter = new(select);
        var dataset = new DataSet();
        try
        {
            connection.Open();
            adapter.Fill(dataset, Table);
            return dataset;
        }
        catch
        {
            dataset.Dispose();
            throw;
        }
    }

    private bool _disposed;
    private DataRow CurrentRow
    {
        get
        {
            ThrowIfDisposed();
            return _datarow ?? throw new InvalidOperationException("Read must select a current row.");
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public bool Read()
    {
        ThrowIfDisposed();
        _datarow = null;
        if (!_dataset.Tables.Contains(Table)) return false;
        DataTable table = _dataset.Tables[Table];
        if (_row < table.Rows.Count)
        {
            _datarow = table.Rows[_row++];
            return true;
        }
        _datarow = null;
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        DataSet dataset = _dataset;
        _dataset = null;
        _datarow = null;
        _row = 0;
        _disposed = true;
        dataset?.Dispose();
    }

    public void Close() => Dispose();

    public sbyte ReadSByte(string columnName)
	{
		if (CurrentRow.IsNull(columnName))
		{
			return 0;
		}
		sbyte result = 0;
		sbyte.TryParse(CurrentRow[columnName].ToString(), out result);
		return result;
	}

	public byte ReadByte(string columnName)
	{
		if (CurrentRow.IsNull(columnName))
		{
			return 0;
		}
		byte result = 0;
		byte.TryParse(CurrentRow[columnName].ToString(), out result);
		return result;
	}

	public short ReadInt16(string columnName)
	{
		if (CurrentRow.IsNull(columnName))
		{
			return 0;
		}
		short result = 0;
		short.TryParse(CurrentRow[columnName].ToString(), out result);
		return result;
	}

	public ushort ReadUInt16(string columnName)
	{
		if (CurrentRow.IsNull(columnName))
		{
			return 0;
		}
		ushort result = 0;
		ushort.TryParse(CurrentRow[columnName].ToString(), out result);
		return result;
	}

	public int ReadInt32(string columnName)
	{
		if (CurrentRow.IsNull(columnName))
		{
			return 0;
		}
		int result = 0;
		int.TryParse(CurrentRow[columnName].ToString(), out result);
		return result;
	}

	public uint ReadUInt32(string columnName)
	{
		if (CurrentRow.IsNull(columnName))
		{
			return 0u;
		}
		uint result = 0u;
		uint.TryParse(CurrentRow[columnName].ToString(), out result);
		return result;
	}

	public long ReadInt64(string columnName)
	{
		if (CurrentRow.IsNull(columnName))
		{
			return 0L;
		}
		long result = 0L;
		long.TryParse(CurrentRow[columnName].ToString(), out result);
		return result;
	}

	public ulong ReadUInt64(string columnName)
	{
		if (CurrentRow.IsNull(columnName))
		{
			return 0uL;
		}
		ulong result = 0uL;
		ulong.TryParse(CurrentRow[columnName].ToString(), out result);
		return result;
	}

	public double ReadDouble(string columnName)
	{
		if (CurrentRow.IsNull(columnName))
		{
			return 0.0;
		}
		double result = 0.0;
		double.TryParse(CurrentRow[columnName].ToString(), out result);
		return result;
	}

	public ulong ReadUInt128(string columnName)
	{
		if (CurrentRow.IsNull(columnName))
		{
			return 0uL;
		}
		ulong result = 0uL;
		ulong.TryParse(CurrentRow[columnName].ToString(), out result);
		return result;
	}

	public string ReadString(string columnName)
	{
		if (CurrentRow.IsNull(columnName))
		{
			return "";
		}
		return CurrentRow[columnName].ToString();
	}

	public uint method_6(string columnName)
	{
		uint result = 0u;
		uint.TryParse(CurrentRow[columnName].ToString(), out result);
		return result;
	}

	public long method_7(string columnName)
	{
		long result = 0L;
		long.TryParse(CurrentRow[columnName].ToString(), out result);
		return result;
	}

	public bool ReadBoolean(string columnName)
	{
		if (CurrentRow.IsNull(columnName))
		{
			return false;
		}
		bool result = false;
		string text = CurrentRow[columnName].ToString();
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
		if (CurrentRow.IsNull(columnName))
		{
			return new byte[0];
		}
		return (byte[])CurrentRow[columnName];
	}

	public uint[] ReadUIntArray(string columnName, string[] separator)
	{
		if (CurrentRow.IsNull(columnName))
		{
			return new uint[0];
		}
		string[] array = CurrentRow[columnName].ToString().Split(separator, StringSplitOptions.RemoveEmptyEntries);
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
