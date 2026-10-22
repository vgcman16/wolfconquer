using System.Text;

namespace WolfConquer.Core.Data;

internal class LegacyNativeDbWriteLine
{
	private char Spliter { get; set; }

	private StringBuilder builder { get; set; }

	public LegacyNativeDbWriteLine(char split)
	{
		builder = new StringBuilder();
		Spliter = split;
	}

	public LegacyNativeDbWriteLine Add(int[] data)
	{
		for (int i = 0; i < data.Length; i++)
		{
			Add(data[i]);
		}
		return this;
	}

	public LegacyNativeDbWriteLine Add(byte data)
	{
		builder.Append(data.ToString() + Spliter);
		return this;
	}

	public LegacyNativeDbWriteLine Add(sbyte data)
	{
		builder.Append(data.ToString() + Spliter);
		return this;
	}

	public LegacyNativeDbWriteLine Add(short data)
	{
		builder.Append(data.ToString() + Spliter);
		return this;
	}

	public LegacyNativeDbWriteLine Add(ushort data)
	{
		builder.Append(data.ToString() + Spliter);
		return this;
	}

	public LegacyNativeDbWriteLine Add(uint data)
	{
		builder.Append(data.ToString() + Spliter);
		return this;
	}

	public LegacyNativeDbWriteLine Add(int data)
	{
		builder.Append(data.ToString() + Spliter);
		return this;
	}

	public LegacyNativeDbWriteLine Add(long data)
	{
		builder.Append(data.ToString() + Spliter);
		return this;
	}

	public LegacyNativeDbWriteLine Add(ulong data)
	{
		builder.Append(data.ToString() + Spliter);
		return this;
	}

	public LegacyNativeDbWriteLine Add(string data)
	{
		builder.Append(data.ToString() + Spliter);
		return this;
	}

	public string Close()
	{
		return builder.ToString();
	}
}
