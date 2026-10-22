using System.Runtime.InteropServices;

namespace WolfConquer.Core.Networking;

internal partial class LegacyNativeWs2_32
{
	public struct WSAData
	{
		public ushort Version;

		public ushort HighVersion;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
		public string Description;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
		public string SystemStatus;

		public ushort MaxSockets;

		public ushort MaxUdpDg;

		private unsafe sbyte* lpVendorInfo;
	}

	public struct fd_set
	{
		public const int FD_SETSIZE = 64;

		public uint fd_count;

		public unsafe fixed uint fd_array[64];
	}
}
