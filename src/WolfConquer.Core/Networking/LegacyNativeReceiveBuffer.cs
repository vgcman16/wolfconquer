using System;

namespace WolfConquer.Core.Networking;

internal class LegacyNativeReceiveBuffer
{
	public const int RECV_BUFFER_SIZE = 16384;

	public const int HeadSize = 16384;

	public byte[] buffer;

	private int nLen;

	public LegacyNativeReceiveBuffer(int ReceiveBufferSize, bool changesize = false)
	{
		if (changesize)
		{
			buffer = new byte[ReceiveBufferSize];
		}
		else
		{
			buffer = new byte[16384];
		}
		Reset();
	}

	public void AddLength(int length)
	{
		nLen += length;
	}

	public ushort ReadHead()
	{
		return BitConverter.ToUInt16(buffer, 0);
	}

	public void DelLength(int length)
	{
		nLen -= length;
	}

	public int Length()
	{
		return nLen;
	}

	public int MaxLength()
	{
		return buffer.Length;
	}

	public void Reset()
	{
		nLen = 0;
	}
}
