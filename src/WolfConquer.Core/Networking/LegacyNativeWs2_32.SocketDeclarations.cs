using System;

namespace WolfConquer.Core.Networking;

internal partial class LegacyNativeWs2_32
{
	public enum AddressFamily
	{
		Unknown = 0,
		InterNetworkv4 = 2,
		Ipx = 4,
		AppleTalk = 17,
		NetBios = 17,
		InterNetworkv6 = 23,
		Irda = 26,
		BlueTooth = 32
	}

	public enum SocketType
	{
		Unknown,
		Stream,
		DGram,
		Raw,
		Rdm,
		SeqPacket
	}

	public enum ProtocolType
	{
		BlueTooth = 3,
		Tcp = 6,
		Udp = 17,
		ReliableMulticast = 113
	}

	[Flags]
	public enum SocketConstructorFlags
	{
		WSA_FLAG_MULTIPOINT_C_LEAF = 4,
		WSA_FLAG_MULTIPOINT_C_ROOT = 2,
		WSA_FLAG_MULTIPOINT_D_LEAF = 0x10,
		WSA_FLAG_MULTIPOINT_D_ROOT = 8,
		WSA_FLAG_OVERLAPPED = 1
	}

	public struct sockaddr_in
	{
		public short sin_family;

		public ushort sin_port;

		public int sin_addr;

		public long sin_zero;
	}

	public enum SocketFlags
	{
		Broadcast = 1024,
		ControlDataTruncated = 512,
		DontRoute = 4,
		MaxIOVectorLength = 16,
		Multicast = 2048,
		None = 0,
		OutOfBand = 1,
		Partial = 32768,
		Peek = 2,
		Truncated = 256
	}

	public const int SOCKET_ERROR = -1;

	public const int INVALID_SOCKET = -1;

	private const string WS2_32 = "ws2_32.dll";
}
