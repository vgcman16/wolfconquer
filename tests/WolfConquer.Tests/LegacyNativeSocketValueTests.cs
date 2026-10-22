using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using TimeValue = WolfConquer.Core.Networking.LegacyNativeWs2_32.TimeValue;
using ShutDownFlags = WolfConquer.Core.Networking.LegacyNativeWs2_32.ShutDownFlags;
using NativeAddressFamily = WolfConquer.Core.Networking.LegacyNativeWs2_32.AddressFamily;
using NativeSocketType = WolfConquer.Core.Networking.LegacyNativeWs2_32.SocketType;
using NativeProtocolType = WolfConquer.Core.Networking.LegacyNativeWs2_32.ProtocolType;
using SocketConstructorFlags = WolfConquer.Core.Networking.LegacyNativeWs2_32.SocketConstructorFlags;
using NativeSocketFlags = WolfConquer.Core.Networking.LegacyNativeWs2_32.SocketFlags;
using NativeSocketAddress = WolfConquer.Core.Networking.LegacyNativeWs2_32.sockaddr_in;

namespace WolfConquer.Tests;

public sealed class LegacyNativeSocketValueTests
{
    [Fact]
    public void GenuineNestedValueTypesKeepTheirNativeNamesAndVisibility()
    {
        var owner = typeof(LegacyNativeWs2_32);
        Assert.False(owner.IsPublic || owner.IsAbstract || owner.IsSealed);
        // Goal567 selected-partial inventory only; not the complete native ws2_32 owner.
        Assert.Equal(new[] { "AddressFamily", "ProtocolType", "ShutDownFlags", "SocketConstructorFlags", "SocketFlags", "SocketType", "TimeValue", "WSAData", "fd_set", "sockaddr_in" },
            owner.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(type => type.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.True(typeof(TimeValue).IsNestedAssembly);
        Assert.True(typeof(ShutDownFlags).IsNestedPublic);
        Assert.Equal(owner, typeof(TimeValue).DeclaringType);
        Assert.Equal(owner, typeof(ShutDownFlags).DeclaringType);
        // The selected partial now contains exactly the three genuine native constants.
        Assert.Equal(new[] { "INVALID_SOCKET", "SOCKET_ERROR", "WS2_32" },
            owner.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        // Goal571 selected imports plus three genuine managed helpers only; CanRead/CanWrite stay open.
        Assert.Equal(new[] { "GetUnixTime64(Int64)", "SelectFileDescriptor(List`1,IntPtr[])", "SocketListToFileDescriptorSet(List`1)", "SocketSelect(Int32,List`1,List`1,List`1,Int32)", "WSACleanup()", "WSAGetLastError()", "WSAIoctl(IntPtr,IOControlCode,IntPtr,Int32,IntPtr,Int32,Int32&,IntPtr,IntPtr)", "WSALookupServiceNext(Int32,Int32,Int32&,IntPtr)", "WSASocket(AddressFamily,SocketType,ProtocolType,IntPtr,Int32,SocketConstructorFlags)", "WSAStartup(UInt16,WSAData&)", "accept(IntPtr,sockaddr_in&,Int32&)", "bind(IntPtr,sockaddr_in&,Int32)", "closesocket(IntPtr)", "connect(IntPtr,sockaddr_in&,Int32)", "getpeername(IntPtr,sockaddr_in*,Int32*)", "htonl(UInt64)", "htons(UInt16)", "inet_addr(String)", "inet_ntoa(Int32)", "ioctlsocket(IntPtr,IOControlCode,Int32&)", "listen(IntPtr,Int32)", "ntohl(UInt64)", "ntohs(UInt16)", "recv(IntPtr,Byte*,Int32,Int32)", "recv(IntPtr,Byte[],Int32,Int32)", "recvfrom(IntPtr,Byte*,Int32,SocketFlags,Byte[],Int32&)", "recvfrom(IntPtr,Byte*,Int32,SocketFlags,sockaddr_in,Int32&)", "select(Int32,IntPtr[],IntPtr[],IntPtr[],TimeValue&)", "send(IntPtr,Byte*,Int32,Int32)", "send(IntPtr,Byte[],Int32,Int32)", "sendto(IntPtr,Byte*,Int32,SocketFlags,Byte[],Int32)", "setsockopt(IntPtr,SocketOptionLevel,SocketOptionName,Int32&,Int32)", "shutdown(IntPtr,ShutDownFlags)", "socket(AddressFamily,SocketType,ProtocolType)" },
            owner.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(method => method.Name + "(" + string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.Name)) + ")")
                .OrderBy(identity => identity, StringComparer.Ordinal));
    }

    [Fact]
    public void TimeValueKeepsBothOrderedPublicMutableInt32Fields()
    {
        var fields = typeof(TimeValue).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "Seconds", "Microseconds" }, fields.Select(field => field.Name));
        Assert.All(fields, field =>
        {
            Assert.Equal(typeof(int), field.FieldType);
            Assert.True(field.IsPublic);
            Assert.False(field.IsStatic || field.IsInitOnly || field.IsLiteral);
        });
        Assert.Empty(typeof(TimeValue).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly));
    }

    [Fact]
    public void TimeValueConstructorKeepsOneRequiredSignedLongMicrosecondsArgument()
    {
        var constructor = Assert.Single(typeof(TimeValue).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        var parameter = Assert.Single(constructor.GetParameters());
        Assert.Equal("microSeconds", parameter.Name);
        Assert.Equal(typeof(long), parameter.ParameterType);
        Assert.False(parameter.IsOptional);
        Assert.Empty(typeof(TimeValue).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly));
    }

    [Fact]
    public void DefaultAndDirectFieldWritesDoNotIntroduceNormalizationOrValidation()
    {
        TimeValue value = default;
        Assert.Equal(0, value.Seconds); Assert.Equal(0, value.Microseconds);
        value.Seconds = int.MinValue; value.Microseconds = int.MaxValue;
        Assert.Equal(int.MinValue, value.Seconds); Assert.Equal(int.MaxValue, value.Microseconds);
        var copy = value;
        copy.Seconds = 7; copy.Microseconds = -1;
        Assert.Equal(int.MinValue, value.Seconds); Assert.Equal(int.MaxValue, value.Microseconds);
        Assert.Equal(7, copy.Seconds); Assert.Equal(-1, copy.Microseconds);
    }

    [Fact]
    public void TargetClrLayoutMetadataUsesSequentialAndDefaultPackAndSize()
    {
        var type = typeof(TimeValue);
        Assert.True(type.IsValueType && type.IsLayoutSequential);
        var layout = type.StructLayoutAttribute!;
        Assert.Equal(LayoutKind.Sequential, layout.Value);
        Assert.Equal(0, layout.Pack);
        Assert.Equal(0, layout.Size);
    }

    [Fact]
    public void TargetManagedMarshalLayoutHasEightBytesWithInt32FieldsAtZeroAndFour()
    {
        Assert.Equal(8, Marshal.SizeOf<TimeValue>());
        Assert.Equal(IntPtr.Zero, Marshal.OffsetOf<TimeValue>("Seconds"));
        Assert.Equal(new IntPtr(4), Marshal.OffsetOf<TimeValue>("Microseconds"));
    }

    [Fact]
    public void PositiveMicrosecondBoundariesKeepDivisionAndRemainderWithoutRounding()
    {
        AssertValue(0L, 0, 0);
        AssertValue(1L, 0, 1);
        AssertValue(999999L, 0, 999999);
        AssertValue(1000000L, 1, 0);
        AssertValue(1000001L, 1, 1);
        AssertValue(2123456L, 2, 123456);
    }

    [Fact]
    public void NegativeMicrosecondsTruncateTowardZeroAndKeepNegativeRemainders()
    {
        AssertValue(-1L, 0, -1);
        AssertValue(-999999L, 0, -999999);
        AssertValue(-1000000L, -1, 0);
        AssertValue(-1000001L, -1, -1);
        AssertValue(-2123456L, -2, -123456);
    }

    [Fact]
    public void PositiveSecondNarrowingWrapsAfterInt32MaximumWithoutAClamp()
    {
        AssertValue(2147483647999999L, int.MaxValue, 999999);
        checked { AssertValue(2147483648000000L, int.MinValue, 0); }
    }

    [Fact]
    public void NegativeSecondNarrowingWrapsBelowInt32MinimumWithoutBorrowing()
    {
        AssertValue(-2147483648000000L, int.MinValue, 0);
        AssertValue(-2147483648000001L, int.MinValue, -1);
        checked { AssertValue(-2147483649000000L, int.MaxValue, 0); }
    }

    [Fact]
    public void Int64ExtremesKeepLiteralSignedQuotientNarrowingAndRemainder()
    {
        AssertValue(long.MaxValue, 2077252342, 775807);
        AssertValue(long.MinValue, -2077252342, -775808);
    }

    [Fact]
    public void ShutDownEnumKeepsDefaultIntOrdinalsAndIsNotAFlagsEnum()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(ShutDownFlags)));
        Assert.Equal(new[] { "SD_RECEIVE", "SD_SEND", "SD_BOTH" }, Enum.GetNames<ShutDownFlags>());
        Assert.Equal(new[] { 0, 1, 2 }, Enum.GetValues<ShutDownFlags>().Select(value => (int)value));
        Assert.False(typeof(ShutDownFlags).IsDefined(typeof(FlagsAttribute), inherit: false));
        ShutDownFlags initial = default;
        Assert.Equal(ShutDownFlags.SD_RECEIVE, initial);
        Assert.Equal(3, (int)(ShutDownFlags)3);
        Assert.Equal(-1, (int)(ShutDownFlags)(-1));
    }

    [Fact]
    public void FiveAdditionalSocketEnumsKeepTheirPublicNestedInt32Identity()
    {
        var owner = typeof(LegacyNativeWs2_32);
        foreach (var type in new[] { typeof(NativeAddressFamily), typeof(NativeSocketType), typeof(NativeProtocolType), typeof(SocketConstructorFlags), typeof(NativeSocketFlags) })
        {
            Assert.True(type.IsEnum && type.IsNestedPublic);
            Assert.Equal(owner, type.DeclaringType);
            Assert.Equal(typeof(int), Enum.GetUnderlyingType(type));
        }
    }

    [Fact]
    public void AddressFamilyKeepsEveryLiteralNameAndBothSeventeenAliases()
    {
        AssertEnumFields<NativeAddressFamily>(
            new[] { "Unknown", "InterNetworkv4", "Ipx", "AppleTalk", "NetBios", "InterNetworkv6", "Irda", "BlueTooth" },
            new[] { 0, 2, 4, 17, 17, 23, 26, 32 });
        Assert.Equal(NativeAddressFamily.AppleTalk, NativeAddressFamily.NetBios);
        Assert.False(typeof(NativeAddressFamily).IsDefined(typeof(FlagsAttribute), inherit: false));
    }

    [Fact]
    public void SocketTypeKeepsTheSixImplicitOrdinalsWithoutBclRenaming()
    {
        AssertEnumFields<NativeSocketType>(
            new[] { "Unknown", "Stream", "DGram", "Raw", "Rdm", "SeqPacket" },
            new[] { 0, 1, 2, 3, 4, 5 });
        Assert.False(typeof(NativeSocketType).IsDefined(typeof(FlagsAttribute), inherit: false));
    }

    [Fact]
    public void ProtocolTypeKeepsItsFourExplicitValuesAndUnnamedZeroDefault()
    {
        AssertEnumFields<NativeProtocolType>(
            new[] { "BlueTooth", "Tcp", "Udp", "ReliableMulticast" },
            new[] { 3, 6, 17, 113 });
        NativeProtocolType value = default;
        Assert.Equal(0, (int)value);
        Assert.False(Enum.IsDefined(value));
        Assert.False(typeof(NativeProtocolType).IsDefined(typeof(FlagsAttribute), inherit: false));
    }

    [Fact]
    public void ConstructorFlagsKeepNativeOrderFlagsAttributeAndFiveDistinctBits()
    {
        AssertEnumFields<SocketConstructorFlags>(
            new[] { "WSA_FLAG_MULTIPOINT_C_LEAF", "WSA_FLAG_MULTIPOINT_C_ROOT", "WSA_FLAG_MULTIPOINT_D_LEAF", "WSA_FLAG_MULTIPOINT_D_ROOT", "WSA_FLAG_OVERLAPPED" },
            new[] { 4, 2, 16, 8, 1 });
        Assert.True(typeof(SocketConstructorFlags).IsDefined(typeof(FlagsAttribute), inherit: false));
        var all = SocketConstructorFlags.WSA_FLAG_MULTIPOINT_C_LEAF | SocketConstructorFlags.WSA_FLAG_MULTIPOINT_C_ROOT |
            SocketConstructorFlags.WSA_FLAG_MULTIPOINT_D_LEAF | SocketConstructorFlags.WSA_FLAG_MULTIPOINT_D_ROOT | SocketConstructorFlags.WSA_FLAG_OVERLAPPED;
        Assert.Equal(31, (int)all);
        Assert.False(Enum.IsDefined(all));
    }

    [Fact]
    public void SocketFlagsKeepTheirLiteralValuesAndDeliberateLackOfFlagsAttribute()
    {
        AssertEnumFields<NativeSocketFlags>(
            new[] { "Broadcast", "ControlDataTruncated", "DontRoute", "MaxIOVectorLength", "Multicast", "None", "OutOfBand", "Partial", "Peek", "Truncated" },
            new[] { 1024, 512, 4, 16, 2048, 0, 1, 32768, 2, 256 });
        Assert.False(typeof(NativeSocketFlags).IsDefined(typeof(FlagsAttribute), inherit: false));
    }

    [Fact]
    public void SocketEnumDefaultsAndSpareSignedCastsDoNotInventValidation()
    {
        Assert.Equal(NativeAddressFamily.Unknown, default(NativeAddressFamily));
        Assert.Equal(NativeSocketType.Unknown, default(NativeSocketType));
        Assert.Equal(NativeSocketFlags.None, default(NativeSocketFlags));
        Assert.Equal(0, (int)default(SocketConstructorFlags));
        Assert.False(Enum.IsDefined(default(SocketConstructorFlags)));
        Assert.Equal(-1, (int)(NativeAddressFamily)(-1));
        Assert.Equal(7, (int)(NativeSocketType)7);
        Assert.Equal(-1, (int)(NativeProtocolType)(-1));
        Assert.Equal(-1, (int)(SocketConstructorFlags)(-1));
        Assert.Equal(-1, (int)(NativeSocketFlags)(-1));
    }

    [Fact]
    public void SocketAddressKeepsFourOrderedPublicMutableLiteralFieldTypes()
    {
        var type = typeof(NativeSocketAddress);
        Assert.True(type.IsValueType && type.IsNestedPublic);
        Assert.Equal(typeof(LegacyNativeWs2_32), type.DeclaringType);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "sin_family", "sin_port", "sin_addr", "sin_zero" }, fields.Select(field => field.Name));
        Assert.Equal(new[] { typeof(short), typeof(ushort), typeof(int), typeof(long) }, fields.Select(field => field.FieldType));
        Assert.All(fields, field =>
        {
            Assert.True(field.IsPublic);
            Assert.False(field.IsStatic || field.IsLiteral || field.IsInitOnly);
        });
    }

    [Fact]
    public void SocketAddressDefaultAndDirectWritesDoNotAddByteOrderOrNormalization()
    {
        NativeSocketAddress value = default;
        Assert.Equal((short)0, value.sin_family); Assert.Equal((ushort)0, value.sin_port);
        Assert.Equal(0, value.sin_addr); Assert.Equal(0L, value.sin_zero);
        value.sin_family = short.MinValue; value.sin_port = ushort.MaxValue;
        value.sin_addr = int.MinValue; value.sin_zero = long.MinValue;
        Assert.Equal(short.MinValue, value.sin_family); Assert.Equal(ushort.MaxValue, value.sin_port);
        Assert.Equal(int.MinValue, value.sin_addr); Assert.Equal(long.MinValue, value.sin_zero);
    }

    [Fact]
    public void SocketAddressValueCopiesKeepAllFourOriginalFieldsIndependent()
    {
        var original = new NativeSocketAddress { sin_family = -7, sin_port = 43210, sin_addr = -1, sin_zero = long.MaxValue };
        var copy = original;
        copy.sin_family = 2; copy.sin_port = 0; copy.sin_addr = 123; copy.sin_zero = -1;
        Assert.Equal((short)-7, original.sin_family); Assert.Equal((ushort)43210, original.sin_port);
        Assert.Equal(-1, original.sin_addr); Assert.Equal(long.MaxValue, original.sin_zero);
        Assert.Equal((short)2, copy.sin_family); Assert.Equal((ushort)0, copy.sin_port);
        Assert.Equal(123, copy.sin_addr); Assert.Equal(-1L, copy.sin_zero);
    }

    [Fact]
    public void SocketAddressTargetClrMetadataAddsNoExplicitPackingSizeOrMarshallingRepair()
    {
        // Target CLR metadata only: no managed size/offset claim and no original Winsock ABI proof.
        var type = typeof(NativeSocketAddress);
        Assert.True(type.IsLayoutSequential);
        var layout = type.StructLayoutAttribute!;
        Assert.Equal(LayoutKind.Sequential, layout.Value);
        Assert.Equal(0, layout.Pack);
        Assert.Equal(0, layout.Size);
        Assert.All(type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            field => Assert.False(field.IsDefined(typeof(MarshalAsAttribute), inherit: false)));
    }

    [Fact]
    public void OuterConstantsKeepTwoPublicSignedSentinelsAndPrivateUnusedDllLiteral()
    {
        var owner = typeof(LegacyNativeWs2_32);
        foreach (var name in new[] { "SOCKET_ERROR", "INVALID_SOCKET" })
        {
            var field = owner.GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)!;
            Assert.True(field.IsPublic && field.IsStatic && field.IsLiteral);
            Assert.False(field.IsInitOnly);
            Assert.Equal(typeof(int), field.FieldType);
            Assert.Equal(-1, (int)field.GetRawConstantValue()!);
        }
        Assert.Equal(-1, LegacyNativeWs2_32.SOCKET_ERROR);
        Assert.Equal(-1, LegacyNativeWs2_32.INVALID_SOCKET);
        var dll = owner.GetField("WS2_32", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)!;
        Assert.True(dll.IsPrivate && dll.IsStatic && dll.IsLiteral);
        Assert.False(dll.IsInitOnly);
        Assert.Equal(typeof(string), dll.FieldType);
        Assert.Equal("ws2_32.dll", (string)dll.GetRawConstantValue()!);
    }

    private static void AssertEnumFields<TEnum>(string[] names, int[] values) where TEnum : struct, Enum
    {
        var fields = typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(names, fields.Select(field => field.Name));
        Assert.Equal(values, fields.Select(field => (int)field.GetRawConstantValue()!));
        Assert.All(fields, field => Assert.True(field.IsLiteral && field.IsPublic && field.IsStatic));
    }

    private static void AssertValue(long microseconds, int seconds, int remainder)
    {
        var value = new TimeValue(microseconds);
        Assert.Equal(seconds, value.Seconds);
        Assert.Equal(remainder, value.Microseconds);
    }
}
