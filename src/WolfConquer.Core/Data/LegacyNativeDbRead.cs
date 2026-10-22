using System;
using System.IO;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal class LegacyNativeDbRead : IDisposable
{
	private string? location = "";

	private StreamReader? SR;

	public int Count = 0;

	private string?[]? items;

	private int curent_count = -1;

	public uint aCount;

	public LegacyNativeDbRead(string? file, bool setlocation = false)
	{
		if (setlocation)
		{
			location = file;
		}
		else
		{
			location = LegacyNativeServerConfig.DbLocation + file;
		}
	}

	public void Dispose()
	{
		location = string.Empty;
		SR = null;
		items = null;
		Count = 0;
	}

	public string?[]? OutBase()
	{
		return items;
	}

	public bool UseRead()
	{
		aCount++;
		return items!.Length >= aCount;
	}

	public bool Reader(bool useinvalid = true)
	{
		string?[]? array = null;
		bool flag = true;
		if (flag == File.Exists(location))
		{
			try
			{
				using (SR = File.OpenText(location!))
				{
					try
					{
						Count = int.Parse(SR.ReadLine()!.Split('=')[1]);
						array = new string?[Count];
						for (int i = 0; i < Count; i++)
						{
							array[i] = SR.ReadLine();
						}
						if (Count == 0)
						{
							return false;
						}
					}
					catch
					{
						LegacyNativeConsole.WriteLine("[Error] An error occurred while reading " + location, ConsoleColor.DarkRed);
						SR!.Close();
						return false;
					}
					SR!.Close();
				}
			}
			catch (Exception ex)
			{
				flag = false;
				LegacyNativeConsole.WriteLine(ex.ToString());
			}
		}
		else if (useinvalid)
		{
			flag = false;
			array = null;
			LegacyNativeConsole.WriteLine("Invalid Reader " + location + " location", ConsoleColor.DarkRed);
		}
		items = new string?[Count];
		for (int j = 0; j < Count; j++)
		{
			items[j] = array![j];
		}
		return flag;
	}

	public uint ReadUInt32(uint add_def)
	{
		curent_count++;
		if (curent_count < Count)
		{
			if (items![curent_count] == null)
			{
				return add_def;
			}
			return uint.Parse(items![curent_count]!);
		}
		return add_def;
	}

	public ushort ReadUInt16(ushort add_def)
	{
		curent_count++;
		if (curent_count < Count)
		{
			if (items![curent_count] == null)
			{
				return add_def;
			}
			return ushort.Parse(items![curent_count]!);
		}
		return add_def;
	}

	public ulong ReadUInt64(ulong add_def)
	{
		curent_count++;
		if (curent_count < Count)
		{
			if (items![curent_count] == null)
			{
				return add_def;
			}
			return ulong.Parse(items![curent_count]!);
		}
		return add_def;
	}

	public byte ReadByte(byte add_def)
	{
		curent_count++;
		if (curent_count < Count)
		{
			if (items![curent_count] == null)
			{
				return add_def;
			}
			return byte.Parse(items![curent_count]!);
		}
		return add_def;
	}

	public string ReadString(string add_def)
	{
		curent_count++;
		if (curent_count < Count)
		{
			if (items![curent_count] == null)
			{
				return add_def;
			}
			return items![curent_count]!;
		}
		return add_def;
	}
}
