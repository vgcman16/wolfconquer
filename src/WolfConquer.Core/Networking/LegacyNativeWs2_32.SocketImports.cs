using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace WolfConquer.Core.Networking;

internal partial class LegacyNativeWs2_32
{
	[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "_time64")]
	public static extern ulong GetUnixTime64(long timer = 0L);

	[DllImport("ws2_32.dll", SetLastError = true)]
	internal static extern int select([In] int ignoredParameter, [In][Out] IntPtr[] readfds, [In][Out] IntPtr[] writefds, [In][Out] IntPtr[] exceptfds, [In] ref TimeValue timeout);

	[DllImport("Ws2_32.dll")]
	public static extern int ioctlsocket(IntPtr s, IOControlCode dwIoControlCode, ref int argp);

	[DllImport("Ws2_32.dll")]
	public static extern int WSAIoctl(IntPtr s, IOControlCode dwIoControlCode, IntPtr lpvInBuffer, int cbInBuffer, IntPtr lpvOutBuffer, int cbOutBuffer, ref int lpcbBytesReturned, IntPtr lpOverlapped, IntPtr lpCompletionRoutine);

	[DllImport("ws2_32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	public static extern int WSACleanup();

	[DllImport("Ws2_32.dll")]
	public static extern int WSAStartup(ushort Version, out WSAData Data);

	[DllImport("Ws2_32.dll")]
	public static extern SocketError WSAGetLastError();

	[DllImport("Ws2_32.dll")]
	public static extern IntPtr socket(AddressFamily af, SocketType type, ProtocolType protocol);

	[DllImport("Ws2_32.dll")]
	public static extern int send(IntPtr SocketHandle, byte[] buf, int len, int flags);

	[DllImport("Ws2_32.dll")]
	public static extern int recv(IntPtr SocketHandle, byte[] buf, int len, int flags);

	[DllImport("Ws2_32.dll")]
	public unsafe static extern int send([In] IntPtr s, [In] byte* buf, [In] int len, [In] int flags);

	[DllImport("Ws2_32.dll")]
	public unsafe static extern int recv([In] IntPtr s, [Out] byte* buf, [In] int len, [In] int flags);

	[DllImport("ws2_32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	public static extern IntPtr accept(IntPtr socketHandle, ref sockaddr_in socketAddress, ref int addressLength);

	[DllImport("Ws2_32.dll")]
	public static extern int listen(IntPtr s, int backlog);

	[DllImport("Ws2_32.dll", CharSet = CharSet.Ansi)]
	public static extern uint inet_addr(string cp);

	[DllImport("Ws2_32.dll")]
	public static extern ushort htons(ushort hostshort);

	[DllImport("Ws2_32.dll")]
	public static extern int connect(IntPtr SocketHandle, ref sockaddr_in addr, int addrsize);

	[DllImport("Ws2_32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern int closesocket(IntPtr SocketHandle);

	[DllImport("Ws2_32.dll")]
	public unsafe static extern int getpeername(IntPtr SocketHandle, sockaddr_in* addr, int* addrsize);

	[DllImport("Ws2_32.dll")]
	public static extern int bind(IntPtr SocketHandle, ref sockaddr_in addr, int namelen);

	[DllImport("Ws2_32.dll")]
	public unsafe static extern sbyte* inet_ntoa(int _in);

	[DllImport("Ws2_32.dll")]
	public static extern ulong htonl(ulong hostlong);

	[DllImport("Ws2_32.dll")]
	public static extern ulong ntohl(ulong netlong);

	[DllImport("Ws2_32.dll")]
	public static extern ushort ntohs(ushort netshort);

	[DllImport("ws2_32.dll", SetLastError = true)]
	public static extern SocketError setsockopt([In] IntPtr socketHandle, [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, [In] ref int optionValue, [In] int optionLength);

	[DllImport("ws2_32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr WSASocket(AddressFamily af, SocketType socket_type, ProtocolType protocol, IntPtr lpProtocolInfo, int group, SocketConstructorFlags dwFlags);

	[DllImport("ws2_32.dll", SetLastError = true)]
	public unsafe static extern int recvfrom([In] IntPtr socketHandle, [In] byte* pinnedBuffer, [In] int len, [In] SocketFlags socketFlags, [Out] byte[] socketAddress, [In][Out] ref int socketAddressSize);

	[DllImport("ws2_32.dll", SetLastError = true)]
	public unsafe static extern int recvfrom([In] IntPtr socketHandle, [In] byte* pinnedBuffer, [In] int len, [In] SocketFlags socketFlags, [Out] sockaddr_in socketAddress, [In][Out] ref int socketAddressSize);

	[DllImport("ws2_32.dll", SetLastError = true)]
	public unsafe static extern int sendto([In] IntPtr socketHandle, [In] byte* pinnedBuffer, [In] int len, [In] SocketFlags socketFlags, [In] byte[] socketAddress, [In] int socketAddressSize);

	[DllImport("ws2_32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern int shutdown(IntPtr s, ShutDownFlags how);

	[DllImport("ws2_32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern int WSALookupServiceNext(int hLookup, int dwControlFlags, ref int lpdwBufferLength, IntPtr pqsResults);
}
