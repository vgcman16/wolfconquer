#nullable disable
using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace WolfConquer.Core.Networking;

internal partial class LegacyNativeWs2_32
{
	public static int SocketSelect(int FD_SETSIZE, List<Socket> readsocket, List<Socket> writesockets, List<Socket> errorsockets, int stampms)
	{
		IntPtr[] array = SocketListToFileDescriptorSet(readsocket);
		IntPtr[] array2 = SocketListToFileDescriptorSet(readsocket);
		IntPtr[] array3 = SocketListToFileDescriptorSet(readsocket);
		TimeValue timeout = new TimeValue(stampms);
		int result = select(FD_SETSIZE, array, array2, array3, ref timeout);
		SelectFileDescriptor(readsocket, array);
		SelectFileDescriptor(writesockets, array2);
		SelectFileDescriptor(errorsockets, array3);
		return result;
	}
}
