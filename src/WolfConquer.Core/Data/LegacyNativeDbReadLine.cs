using System;
using System.Linq;

namespace WolfConquer.Core.Data;

internal class LegacyNativeDbReadLine : IDisposable
{
	public bool Valid = false;

	private string[]? data;

	private int Count = 0;

	public int GetCount()
	{
		if (data == null)
		{
			return 0;
		}
		return data!.Length;
	}

	public LegacyNativeDbReadLine(string? line, char Split)
	{
		if (line != null && line.Contains(Split))
		{
			data = line.Split(Split);
			Valid = true;
		}
	}

	public int Read(int def_val)
	{
		if (!Valid)
		{
			Count++;
			return def_val;
		}
		if (data!.Length > Count)
		{
			int result = 0;
			if (!int.TryParse(data![Count], out result))
			{
				Count++;
				return def_val;
			}
			Count++;
			return result;
		}
		Count++;
		return def_val;
	}

	public uint Read(uint def_val)
	{
		if (!Valid)
		{
			Count++;
			return def_val;
		}
		if (data!.Length > Count)
		{
			uint result = 0u;
			if (!uint.TryParse(data![Count], out result))
			{
				Count++;
				return def_val;
			}
			Count++;
			return result;
		}
		Count++;
		return def_val;
	}

	public byte Read(byte def_val)
	{
		if (!Valid)
		{
			Count++;
			return def_val;
		}
		if (data!.Length > Count)
		{
			byte result = 0;
			if (!byte.TryParse(data![Count], out result))
			{
				Count++;
				return def_val;
			}
			Count++;
			return result;
		}
		Count++;
		return def_val;
	}

	public sbyte Read(sbyte def_val)
	{
		if (!Valid)
		{
			Count++;
			return def_val;
		}
		if (data!.Length > Count)
		{
			sbyte result = 0;
			if (!sbyte.TryParse(data![Count], out result))
			{
				Count++;
				return def_val;
			}
			Count++;
			return result;
		}
		Count++;
		return def_val;
	}

	public ushort Read(ushort def_val)
	{
		if (!Valid)
		{
			Count++;
			return def_val;
		}
		if (data!.Length > Count)
		{
			ushort result = 0;
			if (!ushort.TryParse(data![Count], out result))
			{
				Count++;
				return def_val;
			}
			Count++;
			return result;
		}
		Count++;
		return def_val;
	}

	public ulong Read(ulong def_val)
	{
		if (!Valid)
		{
			Count++;
			return def_val;
		}
		if (data!.Length > Count)
		{
			ulong result = 0uL;
			if (!ulong.TryParse(data![Count], out result))
			{
				Count++;
				return def_val;
			}
			Count++;
			return result;
		}
		Count++;
		return def_val;
	}

	public long Read(long def_val)
	{
		if (!Valid)
		{
			Count++;
			return def_val;
		}
		if (data!.Length > Count)
		{
			long result = 0L;
			if (!long.TryParse(data![Count], out result))
			{
				Count++;
				return def_val;
			}
			Count++;
			return result;
		}
		Count++;
		return def_val;
	}

	public string? Read(string? def_val)
	{
		if (!Valid)
		{
			Count++;
			return def_val;
		}
		if (data!.Length > Count)
		{
			string result = data![Count];
			Count++;
			return result;
		}
		Count++;
		return def_val;
	}

	public void Dispose()
	{
		data = null;
	}
}
