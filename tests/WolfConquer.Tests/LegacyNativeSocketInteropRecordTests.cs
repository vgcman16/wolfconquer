using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using NativeWsaData = WolfConquer.Core.Networking.LegacyNativeWs2_32.WSAData;
using NativeFdSet = WolfConquer.Core.Networking.LegacyNativeWs2_32.fd_set;

namespace WolfConquer.Tests;

public sealed class LegacyNativeSocketInteropRecordTests
{
    [Fact]
    public void BothRecordsKeepTheirNativePublicNestedValueTypeIdentity()
    {
        foreach (var type in new[] { typeof(NativeWsaData), typeof(NativeFdSet) })
        {
            Assert.True(type.IsValueType && type.IsNestedPublic);
            Assert.Equal(typeof(LegacyNativeWs2_32), type.DeclaringType);
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.Empty(type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));
            Assert.Empty(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));
        }
        Assert.Equal("WSAData", typeof(NativeWsaData).Name);
        Assert.Equal("fd_set", typeof(NativeFdSet).Name);
    }

    [Fact]
    public void WsaDataKeepsAllSevenFieldsInNativeOrderAndOriginalVisibility()
    {
        var fields = typeof(NativeWsaData).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "Version", "HighVersion", "Description", "SystemStatus", "MaxSockets", "MaxUdpDg", "lpVendorInfo" }, fields.Select(field => field.Name));
        Assert.Equal(new[] { typeof(ushort), typeof(ushort), typeof(string), typeof(string), typeof(ushort), typeof(ushort), typeof(sbyte).MakePointerType() }, fields.Select(field => field.FieldType));
        Assert.All(fields.Take(6), field => Assert.True(field.IsPublic));
        Assert.True(fields[6].IsPrivate);
        Assert.All(fields, field => Assert.False(field.IsStatic || field.IsLiteral || field.IsInitOnly));
        Assert.Empty(typeof(NativeWsaData).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly));
    }

    [Fact]
    public void WsaDataStringFieldsKeepExactlyTheTwoOriginalByValTStrLengths()
    {
        var fields = typeof(NativeWsaData).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var attributes = fields.Select(field => (Field: field, Attribute: field.GetCustomAttribute<MarshalAsAttribute>())).Where(pair => pair.Attribute is not null).ToArray();
        Assert.Equal(2, attributes.Length);
        var description = typeof(NativeWsaData).GetField("Description")!.GetCustomAttribute<MarshalAsAttribute>()!;
        var status = typeof(NativeWsaData).GetField("SystemStatus")!.GetCustomAttribute<MarshalAsAttribute>()!;
        Assert.Equal(UnmanagedType.ByValTStr, description.Value); Assert.Equal(257, description.SizeConst);
        Assert.Equal(UnmanagedType.ByValTStr, status.Value); Assert.Equal(129, status.SizeConst);
        Assert.All(fields.Where(field => field.Name is not ("Description" or "SystemStatus")),
            field => Assert.Null(field.GetCustomAttribute<MarshalAsAttribute>()));
    }

    [Fact]
    public void WsaDataDefaultKeepsZeroScalarsAndNullManagedStrings()
    {
        NativeWsaData value = default;
        Assert.Equal((ushort)0, value.Version); Assert.Equal((ushort)0, value.HighVersion);
        Assert.Equal((ushort)0, value.MaxSockets); Assert.Equal((ushort)0, value.MaxUdpDg);
        Assert.Null(value.Description); Assert.Null(value.SystemStatus);
    }

    [Fact]
    public void WsaDataDirectWritesDoNotTruncateStringsOrNormalizeUnsignedValues()
    {
        var description = new string('D', 300);
        var status = new string('S', 160);
        var value = new NativeWsaData
        {
            Version = ushort.MaxValue, HighVersion = 0x8123,
            MaxSockets = 0xFEDC, MaxUdpDg = ushort.MaxValue,
            Description = description, SystemStatus = status
        };
        Assert.Equal(ushort.MaxValue, value.Version); Assert.Equal((ushort)0x8123, value.HighVersion);
        Assert.Equal((ushort)0xFEDC, value.MaxSockets); Assert.Equal(ushort.MaxValue, value.MaxUdpDg);
        Assert.Same(description, value.Description); Assert.Same(status, value.SystemStatus);
        Assert.Equal(300, value.Description.Length); Assert.Equal(160, value.SystemStatus.Length);
    }

    [Fact]
    public void WsaDataValueCopiesPreserveStringReferencesButFieldReassignmentIsIndependent()
    {
        var original = new NativeWsaData { Version = 1, HighVersion = 2, MaxSockets = 3, MaxUdpDg = 4, Description = new string('A', 3), SystemStatus = new string('B', 4) };
        var copy = original;
        Assert.Same(original.Description, copy.Description); Assert.Same(original.SystemStatus, copy.SystemStatus);
        copy.Version = 11; copy.HighVersion = 12; copy.MaxSockets = 13; copy.MaxUdpDg = 14;
        copy.Description = "replacement"; copy.SystemStatus = string.Empty;
        Assert.Equal((ushort)1, original.Version); Assert.Equal((ushort)2, original.HighVersion);
        Assert.Equal((ushort)3, original.MaxSockets); Assert.Equal((ushort)4, original.MaxUdpDg);
        Assert.Equal("AAA", original.Description); Assert.Equal("BBBB", original.SystemStatus);
        Assert.Equal((ushort)11, copy.Version); Assert.Equal((ushort)12, copy.HighVersion);
        Assert.Equal((ushort)13, copy.MaxSockets); Assert.Equal((ushort)14, copy.MaxUdpDg);
        Assert.Equal("replacement", copy.Description); Assert.Equal(string.Empty, copy.SystemStatus);
    }

    [Fact]
    public void FdSetKeepsPublicUInt32CountAndBufferAlongsideItsLiteralInt32Capacity()
    {
        var type = typeof(NativeFdSet);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "fd_count", "fd_array" }, fields.Select(field => field.Name));
        Assert.Equal(typeof(uint), fields[0].FieldType);
        Assert.All(fields, field => { Assert.True(field.IsPublic); Assert.False(field.IsInitOnly || field.IsStatic || field.IsLiteral); });
        var constant = Assert.Single(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("FD_SETSIZE", constant.Name); Assert.Equal(typeof(int), constant.FieldType);
        Assert.True(constant.IsPublic && constant.IsStatic && constant.IsLiteral);
        Assert.False(constant.IsInitOnly);
        Assert.Equal(64, (int)constant.GetRawConstantValue()!);
        Assert.Equal(64, NativeFdSet.FD_SETSIZE);
    }

    [Fact]
    public void FdSetBufferMetadataKeepsUInt32ElementsAndExactlySixtyFourSlots()
    {
        var field = typeof(NativeFdSet).GetField("fd_array")!;
        var buffer = field.GetCustomAttribute<FixedBufferAttribute>()!;
        Assert.NotNull(buffer);
        Assert.Equal(typeof(uint), buffer.ElementType); Assert.Equal(64, buffer.Length);
        Assert.True(field.FieldType.IsValueType);
        Assert.Equal(typeof(NativeFdSet), field.FieldType.DeclaringType);
        Assert.Null(field.GetCustomAttribute<MarshalAsAttribute>());
        Assert.False(field.FieldType.IsArray || field.FieldType.IsPointer);
    }

    [Fact]
    public unsafe void FdSetDefaultZerosItsCountAndEveryFixedBufferElement()
    {
        NativeFdSet value = default;
        Assert.Equal(0u, value.fd_count);
        for (var index = 0; index < NativeFdSet.FD_SETSIZE; index++)
            Assert.Equal(0u, value.fd_array[index]);
    }

    [Fact]
    public unsafe void FdSetDirectWritesKeepUnboundedCountAndBoundaryUInt32Values()
    {
        NativeFdSet value = default;
        value.fd_count = uint.MaxValue;
        value.fd_array[0] = uint.MaxValue; value.fd_array[31] = 0x80000000u; value.fd_array[63] = 0xFEDCBA98u;
        Assert.Equal(uint.MaxValue, value.fd_count);
        Assert.Equal(uint.MaxValue, value.fd_array[0]); Assert.Equal(0x80000000u, value.fd_array[31]);
        Assert.Equal(0xFEDCBA98u, value.fd_array[63]); Assert.Equal(0u, value.fd_array[1]);
    }

    [Fact]
    public unsafe void FdSetValueCopiesRetainAllSlotsWithoutSharingBufferStorage()
    {
        NativeFdSet original = default;
        original.fd_count = 64;
        for (var index = 0; index < NativeFdSet.FD_SETSIZE; index++)
            original.fd_array[index] = (uint)(index + 100);
        var copy = original;
        copy.fd_count = 1; copy.fd_array[0] = 900; copy.fd_array[63] = 901;
        Assert.Equal(64u, original.fd_count); Assert.Equal(1u, copy.fd_count);
        for (var index = 0; index < NativeFdSet.FD_SETSIZE; index++)
        {
            Assert.Equal((uint)(index + 100), original.fd_array[index]);
            Assert.Equal(index == 0 ? 900u : index == 63 ? 901u : (uint)(index + 100), copy.fd_array[index]);
        }
    }

    [Fact]
    public void RecordTargetClrMetadataDoesNotAddExplicitPackSizeOrCharsetRepair()
    {
        // Target metadata only; no Marshal.SizeOf/OffsetOf, original Framework or native ABI assertion.
        foreach (var type in new[] { typeof(NativeWsaData), typeof(NativeFdSet) })
        {
            Assert.True(type.IsLayoutSequential);
            var layout = type.StructLayoutAttribute!;
            Assert.Equal(LayoutKind.Sequential, layout.Value);
            Assert.Equal(0, layout.Pack); Assert.Equal(0, layout.Size);
            Assert.Equal(CharSet.Ansi, layout.CharSet);
        }
    }
}
