#nullable disable
using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace WolfConquer.Core.Networking;

internal partial class LegacyNativeWs2_32
{
	private static IntPtr[] SocketListToFileDescriptorSet(List<Socket> socketList)
	{
		if (socketList == null || socketList.Count == 0)
		{
			return null;
		}
		IntPtr[] array = new IntPtr[socketList.Count + 1];
		array[0] = (IntPtr)socketList.Count;
		for (int i = 0; i < socketList.Count; i++)
		{
			if (socketList[i] != null)
			{
				array[i + 1] = socketList[i].Handle;
			}
		}
		return array;
	}

	private static void SelectFileDescriptor(List<Socket> socketList, IntPtr[] fileDescriptorSet)
	{
		if (socketList == null || socketList.Count == 0)
		{
			return;
		}
		if ((int)fileDescriptorSet[0] == 0)
		{
			socketList.Clear();
			return;
		}
		for (int i = 0; i < socketList.Count; i++)
		{
			Socket socket = socketList[i];
			int j;
			for (j = 0; j < (int)fileDescriptorSet[0] && !(fileDescriptorSet[j + 1] == socket.Handle); j++)
			{
			}
			if (j == (int)fileDescriptorSet[0])
			{
				socketList.RemoveAt(i--);
			}
		}
	}
}
