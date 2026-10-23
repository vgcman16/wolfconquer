using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using NativeTimeValue = WolfConquer.Core.Networking.LegacyNativeWs2_32.TimeValue;
using NativeWsaData = WolfConquer.Core.Networking.LegacyNativeWs2_32.WSAData;
using NativeSocketAddress = WolfConquer.Core.Networking.LegacyNativeWs2_32.sockaddr_in;
using NativeAddressFamily = WolfConquer.Core.Networking.LegacyNativeWs2_32.AddressFamily;
using NativeSocketType = WolfConquer.Core.Networking.LegacyNativeWs2_32.SocketType;
using NativeProtocolType = WolfConquer.Core.Networking.LegacyNativeWs2_32.ProtocolType;
using NativeConstructorFlags = WolfConquer.Core.Networking.LegacyNativeWs2_32.SocketConstructorFlags;
using NativeSocketFlags = WolfConquer.Core.Networking.LegacyNativeWs2_32.SocketFlags;
using NativeShutdownFlags = WolfConquer.Core.Networking.LegacyNativeWs2_32.ShutDownFlags;

namespace WolfConquer.Tests;

// Reflection metadata only. No import is invoked, resolved to an address, or marshalled.
public sealed class LegacyNativeSocketImportTests
{
    [Fact]
    public void SelectedImportsHaveAllThirtyOneExactOverloadIdentitiesAndStaticVisibility()
    {
        var methods = Imports();
        Assert.Equal(new[] { "GetUnixTime64(Int64)", "WSACleanup()", "WSAGetLastError()", "WSAIoctl(IntPtr,IOControlCode,IntPtr,Int32,IntPtr,Int32,Int32&,IntPtr,IntPtr)", "WSALookupServiceNext(Int32,Int32,Int32&,IntPtr)", "WSASocket(AddressFamily,SocketType,ProtocolType,IntPtr,Int32,SocketConstructorFlags)", "WSAStartup(UInt16,WSAData&)", "accept(IntPtr,sockaddr_in&,Int32&)", "bind(IntPtr,sockaddr_in&,Int32)", "closesocket(IntPtr)", "connect(IntPtr,sockaddr_in&,Int32)", "getpeername(IntPtr,sockaddr_in*,Int32*)", "htonl(UInt64)", "htons(UInt16)", "inet_addr(String)", "inet_ntoa(Int32)", "ioctlsocket(IntPtr,IOControlCode,Int32&)", "listen(IntPtr,Int32)", "ntohl(UInt64)", "ntohs(UInt16)", "recv(IntPtr,Byte*,Int32,Int32)", "recv(IntPtr,Byte[],Int32,Int32)", "recvfrom(IntPtr,Byte*,Int32,SocketFlags,Byte[],Int32&)", "recvfrom(IntPtr,Byte*,Int32,SocketFlags,sockaddr_in,Int32&)", "select(Int32,IntPtr[],IntPtr[],IntPtr[],TimeValue&)", "send(IntPtr,Byte*,Int32,Int32)", "send(IntPtr,Byte[],Int32,Int32)", "sendto(IntPtr,Byte*,Int32,SocketFlags,Byte[],Int32)", "setsockopt(IntPtr,SocketOptionLevel,SocketOptionName,Int32&,Int32)", "shutdown(IntPtr,ShutDownFlags)", "socket(AddressFamily,SocketType,ProtocolType)" },
            methods.Select(Identity).OrderBy(identity => identity, StringComparer.Ordinal));
        Assert.Equal(31, methods.Length);
        Assert.Equal(29, methods.Count(method => method.IsPublic));
        Assert.Single(methods, method => method.IsAssembly);
        Assert.Single(methods, method => method.IsPrivate);
        Assert.All(methods, method =>
        {
            Assert.True(method.IsStatic);
            Assert.False(method.IsAbstract || method.IsGenericMethodDefinition);
            Assert.True((method.Attributes & MethodAttributes.PinvokeImpl) != 0);
            Assert.NotNull(method.GetCustomAttribute<DllImportAttribute>());
            Assert.Null(method.GetMethodBody());
        });
    }

    [Fact]
    public void UnixTimeImportKeepsUnsignedReturnCdeclEntryPointAndOptionalSignedLongZero()
    {
        var method = Signature("GetUnixTime64", typeof(ulong), new[] { "timer" }, new[] { "" }, typeof(long));
        Assert.True(method.IsPublic);
        var parameter = Assert.Single(method.GetParameters());
        Assert.True(parameter.IsOptional && parameter.HasDefaultValue);
        Assert.Equal(0L, Assert.IsType<long>(parameter.DefaultValue));
        var import = method.GetCustomAttribute<DllImportAttribute>()!;
        Assert.Equal("msvcrt.dll", import.Value);
        Assert.Equal("_time64", import.EntryPoint);
        Assert.Equal(CallingConvention.Cdecl, import.CallingConvention);
        // Source-declared ABI only. This scalar/pointer-sensitive native function is never called.
    }

    [Fact]
    public void EveryLibraryEntryPointCallingConventionCharsetAndLastErrorFlagIsLiteral()
    {
        var expected = new (string Identity, string Library, string EntryPoint, CallingConvention Convention, CharSet Charset, bool LastError)[]
        {
            ("GetUnixTime64(Int64)", "msvcrt.dll", "_time64", CallingConvention.Cdecl, CharSet.None, false),
            ("select(Int32,IntPtr[],IntPtr[],IntPtr[],TimeValue&)", "ws2_32.dll", "select", CallingConvention.Winapi, CharSet.None, true),
            ("ioctlsocket(IntPtr,IOControlCode,Int32&)", "Ws2_32.dll", "ioctlsocket", CallingConvention.Winapi, CharSet.None, false),
            ("WSAIoctl(IntPtr,IOControlCode,IntPtr,Int32,IntPtr,Int32,Int32&,IntPtr,IntPtr)", "Ws2_32.dll", "WSAIoctl", CallingConvention.Winapi, CharSet.None, false),
            ("WSACleanup()", "ws2_32.dll", "WSACleanup", CallingConvention.Winapi, CharSet.Auto, true),
            ("WSAStartup(UInt16,WSAData&)", "Ws2_32.dll", "WSAStartup", CallingConvention.Winapi, CharSet.None, false),
            ("WSAGetLastError()", "Ws2_32.dll", "WSAGetLastError", CallingConvention.Winapi, CharSet.None, false),
            ("socket(AddressFamily,SocketType,ProtocolType)", "Ws2_32.dll", "socket", CallingConvention.Winapi, CharSet.None, false),
            ("send(IntPtr,Byte[],Int32,Int32)", "Ws2_32.dll", "send", CallingConvention.Winapi, CharSet.None, false),
            ("recv(IntPtr,Byte[],Int32,Int32)", "Ws2_32.dll", "recv", CallingConvention.Winapi, CharSet.None, false),
            ("send(IntPtr,Byte*,Int32,Int32)", "Ws2_32.dll", "send", CallingConvention.Winapi, CharSet.None, false),
            ("recv(IntPtr,Byte*,Int32,Int32)", "Ws2_32.dll", "recv", CallingConvention.Winapi, CharSet.None, false),
            ("accept(IntPtr,sockaddr_in&,Int32&)", "ws2_32.dll", "accept", CallingConvention.Winapi, CharSet.Auto, true),
            ("listen(IntPtr,Int32)", "Ws2_32.dll", "listen", CallingConvention.Winapi, CharSet.None, false),
            ("inet_addr(String)", "Ws2_32.dll", "inet_addr", CallingConvention.Winapi, CharSet.Ansi, false),
            ("htons(UInt16)", "Ws2_32.dll", "htons", CallingConvention.Winapi, CharSet.None, false),
            ("connect(IntPtr,sockaddr_in&,Int32)", "Ws2_32.dll", "connect", CallingConvention.Winapi, CharSet.None, false),
            ("closesocket(IntPtr)", "Ws2_32.dll", "closesocket", CallingConvention.Winapi, CharSet.Unicode, true),
            ("getpeername(IntPtr,sockaddr_in*,Int32*)", "Ws2_32.dll", "getpeername", CallingConvention.Winapi, CharSet.None, false),
            ("bind(IntPtr,sockaddr_in&,Int32)", "Ws2_32.dll", "bind", CallingConvention.Winapi, CharSet.None, false),
            ("inet_ntoa(Int32)", "Ws2_32.dll", "inet_ntoa", CallingConvention.Winapi, CharSet.None, false),
            ("htonl(UInt64)", "Ws2_32.dll", "htonl", CallingConvention.Winapi, CharSet.None, false),
            ("ntohl(UInt64)", "Ws2_32.dll", "ntohl", CallingConvention.Winapi, CharSet.None, false),
            ("ntohs(UInt16)", "Ws2_32.dll", "ntohs", CallingConvention.Winapi, CharSet.None, false),
            ("setsockopt(IntPtr,SocketOptionLevel,SocketOptionName,Int32&,Int32)", "ws2_32.dll", "setsockopt", CallingConvention.Winapi, CharSet.None, true),
            ("WSASocket(AddressFamily,SocketType,ProtocolType,IntPtr,Int32,SocketConstructorFlags)", "ws2_32.dll", "WSASocket", CallingConvention.StdCall, CharSet.Unicode, true),
            ("recvfrom(IntPtr,Byte*,Int32,SocketFlags,Byte[],Int32&)", "ws2_32.dll", "recvfrom", CallingConvention.Winapi, CharSet.None, true),
            ("recvfrom(IntPtr,Byte*,Int32,SocketFlags,sockaddr_in,Int32&)", "ws2_32.dll", "recvfrom", CallingConvention.Winapi, CharSet.None, true),
            ("sendto(IntPtr,Byte*,Int32,SocketFlags,Byte[],Int32)", "ws2_32.dll", "sendto", CallingConvention.Winapi, CharSet.None, true),
            ("shutdown(IntPtr,ShutDownFlags)", "ws2_32.dll", "shutdown", CallingConvention.Winapi, CharSet.Unicode, true),
            ("WSALookupServiceNext(Int32,Int32,Int32&,IntPtr)", "ws2_32.dll", "WSALookupServiceNext", CallingConvention.Winapi, CharSet.Auto, true),
        };
        var methods = Imports();
        Assert.Equal(31, expected.Length);
        foreach (var row in expected)
        {
            var method = Assert.Single(methods, candidate => Identity(candidate) == row.Identity);
            var import = method.GetCustomAttribute<DllImportAttribute>()!;
            Assert.Equal(row.Library, import.Value);
            Assert.Equal(row.EntryPoint, import.EntryPoint);
            Assert.Equal(row.Convention, import.CallingConvention);
            Assert.Equal(row.Charset, import.CharSet);
            Assert.Equal(row.LastError, import.SetLastError);
        }
    }

    [Fact]
    public void SelectKeepsInternalVisibilityThreeIntPtrArraysAndInOutByRefTimeout()
    {
        var method = Signature("select", typeof(int), new[] { "ignoredParameter", "readfds", "writefds", "exceptfds", "timeout" }, new[] { "I", "IO", "IO", "IO", "I" }, typeof(int), typeof(IntPtr).MakeArrayType(), typeof(IntPtr).MakeArrayType(), typeof(IntPtr).MakeArrayType(), typeof(NativeTimeValue).MakeByRefType());
        Assert.True(method.IsAssembly);
        Assert.False(method.IsPublic || method.IsPrivate);
    }

    [Fact]
    public void IoctlImportsKeepBclControlCodeAndEveryRefScalar()
    {
        Signature("ioctlsocket", typeof(int), new[] { "s", "dwIoControlCode", "argp" }, new[] { "", "", "" }, typeof(IntPtr), typeof(IOControlCode), typeof(int).MakeByRefType());
        Signature("WSAIoctl", typeof(int), new[] { "s", "dwIoControlCode", "lpvInBuffer", "cbInBuffer", "lpvOutBuffer", "cbOutBuffer", "lpcbBytesReturned", "lpOverlapped", "lpCompletionRoutine" }, new[] { "", "", "", "", "", "", "", "", "" }, typeof(IntPtr), typeof(IOControlCode), typeof(IntPtr), typeof(int), typeof(IntPtr), typeof(int), typeof(int).MakeByRefType(), typeof(IntPtr), typeof(IntPtr));
    }

    [Fact]
    public void StartupCleanupErrorAndSocketCreationKeepOriginalTypes()
    {
        Signature("WSACleanup", typeof(int), Array.Empty<string>(), Array.Empty<string>());
        Signature("WSAStartup", typeof(int), new[] { "Version", "Data" }, new[] { "", "O" }, typeof(ushort), typeof(NativeWsaData).MakeByRefType());
        Signature("WSAGetLastError", typeof(SocketError), Array.Empty<string>(), Array.Empty<string>());
        Signature("socket", typeof(IntPtr), new[] { "af", "type", "protocol" }, new[] { "", "", "" }, typeof(NativeAddressFamily), typeof(NativeSocketType), typeof(NativeProtocolType));
    }

    [Fact]
    public void SendAndReceiveKeepBothArrayAndPointerOverloadsAndDirections()
    {
        Signature("send", typeof(int), new[] { "SocketHandle", "buf", "len", "flags" }, new[] { "", "", "", "" }, typeof(IntPtr), typeof(byte).MakeArrayType(), typeof(int), typeof(int));
        Signature("recv", typeof(int), new[] { "SocketHandle", "buf", "len", "flags" }, new[] { "", "", "", "" }, typeof(IntPtr), typeof(byte).MakeArrayType(), typeof(int), typeof(int));
        Signature("send", typeof(int), new[] { "s", "buf", "len", "flags" }, new[] { "I", "I", "I", "I" }, typeof(IntPtr), typeof(byte).MakePointerType(), typeof(int), typeof(int));
        Signature("recv", typeof(int), new[] { "s", "buf", "len", "flags" }, new[] { "I", "O", "I", "I" }, typeof(IntPtr), typeof(byte).MakePointerType(), typeof(int), typeof(int));
    }

    [Fact]
    public void AddressLifecycleKeepsByRefPointerAndSignedScalarShapes()
    {
        Signature("accept", typeof(IntPtr), new[] { "socketHandle", "socketAddress", "addressLength" }, new[] { "", "", "" }, typeof(IntPtr), typeof(NativeSocketAddress).MakeByRefType(), typeof(int).MakeByRefType());
        Signature("listen", typeof(int), new[] { "s", "backlog" }, new[] { "", "" }, typeof(IntPtr), typeof(int));
        Signature("connect", typeof(int), new[] { "SocketHandle", "addr", "addrsize" }, new[] { "", "", "" }, typeof(IntPtr), typeof(NativeSocketAddress).MakeByRefType(), typeof(int));
        Signature("closesocket", typeof(int), new[] { "SocketHandle" }, new[] { "" }, typeof(IntPtr));
        Signature("getpeername", typeof(int), new[] { "SocketHandle", "addr", "addrsize" }, new[] { "", "", "" }, typeof(IntPtr), typeof(NativeSocketAddress).MakePointerType(), typeof(int).MakePointerType());
        Signature("bind", typeof(int), new[] { "SocketHandle", "addr", "namelen" }, new[] { "", "", "" }, typeof(IntPtr), typeof(NativeSocketAddress).MakeByRefType(), typeof(int));
    }

    [Fact]
    public void TextAddressAndHostNetworkConversionsKeepNativeWidthQuirks()
    {
        Signature("inet_addr", typeof(uint), new[] { "cp" }, new[] { "" }, typeof(string));
        Signature("htons", typeof(ushort), new[] { "hostshort" }, new[] { "" }, typeof(ushort));
        Signature("inet_ntoa", typeof(sbyte).MakePointerType(), new[] { "_in" }, new[] { "" }, typeof(int));
        Signature("htonl", typeof(ulong), new[] { "hostlong" }, new[] { "" }, typeof(ulong));
        Signature("ntohl", typeof(ulong), new[] { "netlong" }, new[] { "" }, typeof(ulong));
        Signature("ntohs", typeof(ushort), new[] { "netshort" }, new[] { "" }, typeof(ushort));
    }

    [Fact]
    public void SocketOptionAndWsaSocketImportsKeepLocalAndBclEnumDistinctions()
    {
        Signature("setsockopt", typeof(SocketError), new[] { "socketHandle", "optionLevel", "optionName", "optionValue", "optionLength" }, new[] { "I", "I", "I", "I", "I" }, typeof(IntPtr), typeof(SocketOptionLevel), typeof(SocketOptionName), typeof(int).MakeByRefType(), typeof(int));
        Signature("WSASocket", typeof(IntPtr), new[] { "af", "socket_type", "protocol", "lpProtocolInfo", "group", "dwFlags" }, new[] { "", "", "", "", "", "" }, typeof(NativeAddressFamily), typeof(NativeSocketType), typeof(NativeProtocolType), typeof(IntPtr), typeof(int), typeof(NativeConstructorFlags));
    }

    [Fact]
    public void DatagramImportsKeepByValueStructArrayAndRefSizeDifferences()
    {
        Signature("recvfrom", typeof(int), new[] { "socketHandle", "pinnedBuffer", "len", "socketFlags", "socketAddress", "socketAddressSize" }, new[] { "I", "I", "I", "I", "O", "IO" }, typeof(IntPtr), typeof(byte).MakePointerType(), typeof(int), typeof(NativeSocketFlags), typeof(byte).MakeArrayType(), typeof(int).MakeByRefType());
        Signature("recvfrom", typeof(int), new[] { "socketHandle", "pinnedBuffer", "len", "socketFlags", "socketAddress", "socketAddressSize" }, new[] { "I", "I", "I", "I", "O", "IO" }, typeof(IntPtr), typeof(byte).MakePointerType(), typeof(int), typeof(NativeSocketFlags), typeof(NativeSocketAddress), typeof(int).MakeByRefType());
        Signature("sendto", typeof(int), new[] { "socketHandle", "pinnedBuffer", "len", "socketFlags", "socketAddress", "socketAddressSize" }, new[] { "I", "I", "I", "I", "I", "I" }, typeof(IntPtr), typeof(byte).MakePointerType(), typeof(int), typeof(NativeSocketFlags), typeof(byte).MakeArrayType(), typeof(int));
    }

    [Fact]
    public void ShutdownAndPrivateLookupKeepExactVisibilityAndMetadata()
    {
        Signature("shutdown", typeof(int), new[] { "s", "how" }, new[] { "", "" }, typeof(IntPtr), typeof(NativeShutdownFlags));
        Signature("WSALookupServiceNext", typeof(int), new[] { "hLookup", "dwControlFlags", "lpdwBufferLength", "pqsResults" }, new[] { "", "", "", "" }, typeof(int), typeof(int), typeof(int).MakeByRefType(), typeof(IntPtr));
        var lookup = Assert.Single(Imports(), method => method.Name == "WSALookupServiceNext");
        Assert.True(lookup.IsPrivate);
        Assert.False(lookup.IsPublic || lookup.IsAssembly);
    }

    private static MethodInfo[] Imports() =>
        typeof(LegacyNativeWs2_32).GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => (method.Attributes & MethodAttributes.PinvokeImpl) != 0).ToArray();

    private static string Identity(MethodInfo method) =>
        method.Name + "(" + string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.Name)) + ")";

    private static MethodInfo Signature(string name, Type returnType, string[] names, string[] directions, params Type[] parameterTypes)
    {
        var method = Assert.Single(Imports(), candidate =>
            candidate.Name == name && candidate.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameterTypes));
        Assert.Equal(returnType, method.ReturnType);
        var parameters = method.GetParameters();
        Assert.Equal(names, parameters.Select(parameter => parameter.Name));
        Assert.Equal(parameters.Length, directions.Length);
        for (var index = 0; index < parameters.Length; index++)
        {
            Assert.Equal(directions[index].Contains('I'), parameters[index].IsIn);
            Assert.Equal(directions[index].Contains('O'), parameters[index].IsOut);
            if (name != "GetUnixTime64")
            {
                Assert.False(parameters[index].IsOptional);
                Assert.False(parameters[index].HasDefaultValue);
            }
        }
        return method;
    }
}
