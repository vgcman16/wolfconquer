using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeClientItemPerfectionRecordTests
{
    [Fact]
    public void SixNativeFieldsAreSequentialEightyBytesWithFixedSignedByteBuffers()
    {
        var type = typeof(LegacyNativeClientItemPerfectionRecord);
        Assert.True(type.IsLayoutSequential);
        Assert.Equal(80, Unsafe.SizeOf<LegacyNativeClientItemPerfectionRecord>());
        Assert.Equal(80, Marshal.SizeOf<LegacyNativeClientItemPerfectionRecord>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<LegacyNativeClientItemPerfectionRecord>());
        var names = new[] { "ItemUID", "Level", "Progres", "OwnerUID", "_OwnerName", "_SpecialText" };
        Assert.Equal(names, type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(field => field.MetadataToken).Select(field => field.Name));
        var offsets = new[] { 0, 4, 8, 12, 16, 48 };
        for (var index = 0; index < names.Length; index++)
            Assert.Equal(offsets[index], Marshal.OffsetOf(type, names[index]).ToInt32());
        foreach (var name in names.Skip(4))
        {
            var fixedBuffer = type.GetField(name)!.GetCustomAttribute<FixedBufferAttribute>();
            Assert.NotNull(fixedBuffer);
            Assert.Equal(typeof(sbyte), fixedBuffer!.ElementType);
            Assert.Equal(32, fixedBuffer.Length);
        }
    }

    [Fact]
    public void FreshDefaultStorageIsZeroAndBothTerminatedStringsAreEmpty()
    {
        var record = default(LegacyNativeClientItemPerfectionRecord);
        Assert.Equal(new byte[80], Bytes(ref record));
        Assert.Equal("", record.OwnerName);
        Assert.Equal("", record.SpecialText);
    }

    [Fact]
    public void AllFourHeaderValuesKeepUnsignedBitsWithoutNormalization()
    {
        var record = new LegacyNativeClientItemPerfectionRecord
        {
            ItemUID = uint.MaxValue, Level = 0x80000000u, Progres = 0xFEDCBA98u, OwnerUID = 0x76543210u
        };
        var bytes = Bytes(ref record);
        Assert.Equal(uint.MaxValue, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0)));
        Assert.Equal(0x80000000u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal(0xFEDCBA98u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)));
        Assert.Equal(0x76543210u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12)));
    }

    [Fact]
    public void CompleteRawStoragePreservesEveryByteWithoutStringConversion()
    {
        var expected = Enumerable.Range(0, 80).Select(index => unchecked((byte)(index * 11 + 7))).ToArray();
        var record = MemoryMarshal.Read<LegacyNativeClientItemPerfectionRecord>(expected);
        Assert.Equal(expected, Bytes(ref record)); // Deliberately do NOT call a getter on this arbitrary image.
    }

    [Fact]
    public void OwnerGetterStopsAtFirstNulAndDoesNotTrimSpaces()
    {
        var record = default(LegacyNativeClientItemPerfectionRecord);
        record.OwnerName = " A B \0ignored";
        Assert.Equal(" A B ", record.OwnerName);
    }

    [Fact]
    public void SpecialGetterStopsAtFirstNulAndDoesNotTrimSpaces()
    {
        var record = default(LegacyNativeClientItemPerfectionRecord);
        record.SpecialText = " note \0ignored";
        Assert.Equal(" note ", record.SpecialText);
    }

    [Fact]
    public void OwnerSetterStoresLowCharacterBytesRatherThanUtf8()
    {
        var record = default(LegacyNativeClientItemPerfectionRecord);
        record.OwnerName = "\u0141\u20AC";
        var bytes = Bytes(ref record);
        Assert.Equal((byte)0x41, bytes[16]);
        Assert.Equal((byte)0xAC, bytes[17]);
        Assert.Equal((byte)0, bytes[18]); // Bounded replacement clears the remainder of this field.
    }

    [Fact]
    public void SpecialSetterStoresLowCharacterBytesRatherThanUtf8()
    {
        var record = default(LegacyNativeClientItemPerfectionRecord);
        record.SpecialText = "\u0141\u20AC";
        var bytes = Bytes(ref record);
        Assert.Equal((byte)0x41, bytes[48]);
        Assert.Equal((byte)0xAC, bytes[49]);
        Assert.Equal((byte)0, bytes[50]);
    }

    [Fact]
    public void ShortOwnerOverwriteClearsItsUnusedFixedFieldTail()
    {
        var record = default(LegacyNativeClientItemPerfectionRecord);
        record.OwnerName = "abcdef";
        record.OwnerName = "Q";
        Assert.Equal("Q", record.OwnerName);
    }

    [Fact]
    public void ShortSpecialOverwriteClearsItsUnusedFixedFieldTail()
    {
        var record = default(LegacyNativeClientItemPerfectionRecord);
        record.SpecialText = "abcdef";
        record.SpecialText = "Q";
        Assert.Equal("Q", record.SpecialText);
    }

    [Fact]
    public void EmptySettersClearTheirOwnFieldsWithoutChangingHeader()
    {
        var record = default(LegacyNativeClientItemPerfectionRecord);
        record.OwnerName = "owner";
        record.SpecialText = "signature";
        var before = Bytes(ref record);
        record.OwnerName = "";
        record.SpecialText = "";
        var after = Bytes(ref record);
        Assert.Equal(before.AsSpan(0, 16).ToArray(), after.AsSpan(0, 16).ToArray());
        Assert.All(after.AsSpan(16).ToArray(), value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void NullOwnerSetterThrowsBeforeAnyByteWrite()
    {
        var record = default(LegacyNativeClientItemPerfectionRecord);
        record.OwnerName = "owner";
        var before = Bytes(ref record);
        Assert.Throws<ArgumentNullException>(() => record.OwnerName = null!);
        Assert.Equal(before, Bytes(ref record));
    }

    [Fact]
    public void NullSpecialSetterThrowsBeforeAnyByteWrite()
    {
        var record = default(LegacyNativeClientItemPerfectionRecord);
        record.SpecialText = "signature";
        var before = Bytes(ref record);
        Assert.Throws<ArgumentNullException>(() => record.SpecialText = null!);
        Assert.Equal(before, Bytes(ref record));
    }

    [Fact]
    public void EmbeddedNulDoesNotStopSetterFromWritingItsRemainingCharacters()
    {
        var record = default(LegacyNativeClientItemPerfectionRecord);
        record.OwnerName = "AB\0CD";
        Assert.Equal("AB", record.OwnerName);
        Assert.Equal(new byte[] { 65, 66, 0, 67, 68, 0 }, Bytes(ref record).AsSpan(16, 6).ToArray());
    }

    [Fact]
    public void ExactlyThirtyTwoCharactersStayWithinStorageWithoutAddingTerminator()
    {
        var record = default(LegacyNativeClientItemPerfectionRecord);
        record.OwnerName = new string('A', 32);
        record.SpecialText = new string('B', 32);
        var bytes = Bytes(ref record);
        Assert.Equal(Enumerable.Repeat((byte)65, 32), bytes.AsSpan(16, 32).ToArray());
        Assert.Equal(Enumerable.Repeat((byte)66, 32), bytes.AsSpan(48, 32).ToArray());
        Assert.Equal(new string('A', 32), record.OwnerName);
        Assert.Equal(new string('B', 32), record.SpecialText); // Each getter is bounded to its own field.
    }

    [Fact]
    public void NativeFactoryMapsFourHeadersAndBothStringsFromTheActualItem()
    {
        var item = new LegacyNativeGameItemRuntime
        {
            Uid = 99u, PerfectionLevel = 18u, PerfectionProgress = 123u, OwnerUID = 456u,
            OwnerName = "owner", Signature = "signature"
        };
        var record = LegacyNativeClientItemPerfectionRecord.GetPerfectionInfo(item);
        Assert.Equal(99u, record.ItemUID);
        Assert.Equal(18u, record.Level);
        Assert.Equal(123u, record.Progres);
        Assert.Equal(456u, record.OwnerUID);
        Assert.Equal("owner", record.OwnerName);
        Assert.Equal("signature", record.SpecialText);
    }

    [Fact]
    public void NativeFactoryStartsFreshZeroStorageRatherThanReusingOldStringTails()
    {
        var item = new LegacyNativeGameItemRuntime { OwnerName = "longer", Signature = "longer" };
        _ = LegacyNativeClientItemPerfectionRecord.GetPerfectionInfo(item);
        item.OwnerName = "Q";
        item.Signature = "";
        var record = LegacyNativeClientItemPerfectionRecord.GetPerfectionInfo(item);
        var bytes = Bytes(ref record);
        Assert.Equal((byte)81, bytes[16]);
        Assert.All(bytes.AsSpan(17, 31).ToArray(), value => Assert.Equal((byte)0, value));
        Assert.All(bytes.AsSpan(48, 32).ToArray(), value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void NativeFactoryNullItemIsNotSilentlyReplacedByAnEmptyRecord()
    {
        var error = Assert.Throws<ArgumentNullException>(() =>
            LegacyNativeClientItemPerfectionRecord.GetPerfectionInfo(null!));
        Assert.Equal("dataItem", error.ParamName);
    }

    [Fact]
    public void PostludeApplyMutatesOnlyItsFiveNativeMembersOnTheSameItem()
    {
        var item = new LegacyNativeGameItemRuntime
        {
            Uid = 11u, PerfectionLevel = 2u, PerfectionProgress = 3u, OwnerUID = 4u,
            OwnerName = "old", Signature = "old", PerfectionRank = 7, ItemPoints = 8u,
            EndDate = DateTime.FromBinary(12345L)
        };
        var same = item;
        var record = new LegacyNativeClientItemPerfectionRecord
        {
            ItemUID = 999u, Level = 18u, Progres = 123u, OwnerUID = 456u,
            OwnerName = "owner", SpecialText = "signature"
        };
        record.ApplyTo(item); // Native routing/UID lookup is the caller's missing responsibility.
        Assert.Same(same, item);
        Assert.Equal(18u, item.PerfectionLevel);
        Assert.Equal(456u, item.OwnerUID);
        Assert.Equal("owner", item.OwnerName);
        Assert.Equal(123u, item.PerfectionProgress);
        Assert.Equal("signature", item.Signature);
        Assert.Equal(11u, item.Uid); // Apply is not an identity setter or validation wrapper.
        Assert.Equal((ushort)7, item.PerfectionRank);
        Assert.Equal(8u, item.ItemPoints);
        Assert.Equal(DateTime.FromBinary(12345L), item.EndDate);
    }

    [Fact]
    public void PostludeApplyNullItemRejectsExplicitlyBeforeItsFirstStore()
    {
        var record = default(LegacyNativeClientItemPerfectionRecord);
        var error = Assert.Throws<ArgumentNullException>(() => record.ApplyTo(null!));
        Assert.Equal("item", error.ParamName);
    }

    [Fact]
    public void WindowsTerminatedHighBitGetterMatchesTheClrSByteStringReference()
    {
        Assert.True(OperatingSystem.IsWindows(), "Windows ANSI-code-page string reference control requires Windows.");
        var raw = new byte[80];
        raw[16] = 0xE9;
        var record = MemoryMarshal.Read<LegacyNativeClientItemPerfectionRecord>(raw);
        byte[] terminated = [0xE9, 0];
        var pin = GCHandle.Alloc(terminated, GCHandleType.Pinned);
        try
        {
            string? expected = null;
            string? actual = null;
            var referenceError = Record.Exception(() => expected = Marshal.PtrToStringAnsi(pin.AddrOfPinnedObject()));
            var actualError = Record.Exception(() => actual = record.OwnerName);
            if (referenceError is null)
            {
                Assert.Null(actualError);
                Assert.Equal(expected, actual);
            }
            else
            {
                Assert.NotNull(actualError);
                Assert.Equal(referenceError.GetType(), actualError!.GetType());
            }
        }
        finally { pin.Free(); }
    }

    private static byte[] Bytes(ref LegacyNativeClientItemPerfectionRecord value) =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1)).ToArray();
}
