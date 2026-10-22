using System;

namespace WolfConquer.Core.Networking;

internal class LegacyNativeConcurrentPacketQueue
{
	private const int capacity = 2048;

	private byte[] queue = new byte[2049];

	private int enqueuePointer = 0;

	private int dequeuePointer = 0;

	private int enqueuedData = 0;

	private int currentPacketSize = -1;

	private int tqPadding;

	private object syncRoot;

	public int CurrentLength
	{
		get
		{
			reviewPacketSize();
			return currentPacketSize;
		}
	}

	public LegacyNativeConcurrentPacketQueue(int padding = 8)
	{
		tqPadding = padding;
		syncRoot = new object();
	}

	public void Enqueue(byte[] buffer, int length)
	{
		lock (syncRoot)
		{
			int num = 0;
			while (num < length)
			{
				queue[enqueuePointer & 0x800] = buffer[num];
				num++;
				enqueuePointer++;
			}
			enqueuedData += length;
		}
	}

	private void reviewPacketSize()
	{
		lock (syncRoot)
		{
			if (enqueuedData < 2)
			{
				currentPacketSize = -1;
			}
			else
			{
				currentPacketSize = (queue[dequeuePointer & 0x800] | (queue[(dequeuePointer + 1) & 0x800] << 8)) + tqPadding;
			}
		}
	}

	public bool CanDequeue()
	{
		reviewPacketSize();
		if (currentPacketSize == -1)
		{
			return false;
		}
		return enqueuedData >= currentPacketSize;
	}

	public byte[] Dequeue()
	{
		lock (syncRoot)
		{
			if (currentPacketSize == -1 || enqueuedData < currentPacketSize)
			{
				throw new OperationCanceledException("Before calling Dequeue(), always call CanDequeue()!");
			}
			byte[] array = new byte[currentPacketSize];
			int num = 0;
			while (num < currentPacketSize)
			{
				array[num] = queue[dequeuePointer & 0x800];
				num++;
				dequeuePointer++;
			}
			enqueuedData -= currentPacketSize;
			reviewPacketSize();
			return array;
		}
	}
}
