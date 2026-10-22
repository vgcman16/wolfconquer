using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemRecordFileTests
{
    [Fact]
    public void UnopenedCountReadFaultLeavesCallerCountUntouched()
    {
        var reader = new LegacyNativeItemRecordFile();
        var count = -17;
        Assert.False(reader.Success);
        var error = Assert.Throws<IOException>(() => reader.ReadCount(ref count));
        Assert.Equal("File isn't open/failed to open previously", error.Message);
        Assert.Equal(-17, count);
    }

    [Fact]
    public void UnopenedRecordReadFaultLeavesEveryCallerByteUntouched()
    {
        var reader = new LegacyNativeItemRecordFile();
        var record = PatternRecord(19);
        var before = Bytes(ref record);
        Assert.Throws<IOException>(() => reader.ReadRecord(ref record));
        Assert.Equal(before, Bytes(ref record));
    }

    [Fact]
    public void InvalidPathOpenIsCaughtAndCloseUsesNativeFailureMessage()
    {
        var reader = new LegacyNativeItemRecordFile("\0", FileMode.Open);
        Assert.False(reader.Success);
        Assert.False(reader.Open("\0", FileMode.Open));
        Assert.Equal("File isn't open/failed to open previously",
            Assert.Throws<IOException>(() => reader.Close()).Message);
    }

    [Fact]
    public void WindowsFullReadWritesAllRecordBytesDirectlyIntoCallerStorage()
    {
        var expected = Enumerable.Range(0, 184).Select(index => unchecked((byte)(index * 37 + 11))).ToArray();
        using var owned = new OwnedFile(expected);
        var reader = owned.Open();
        var record = default(LegacyNativeClientItemRecord);
        Assert.Equal(184, Unsafe.SizeOf<LegacyNativeClientItemRecord>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<LegacyNativeClientItemRecord>());
        Assert.True(reader.ReadRecord(ref record));
        Assert.Equal(expected, Bytes(ref record));
    }

    [Fact]
    public void WindowsShortRecordReadOverwritesOnlyTheAvailablePrefix()
    {
        var prefix = Enumerable.Range(0, 23).Select(index => unchecked((byte)(index + 161))).ToArray();
        using var owned = new OwnedFile(prefix);
        var reader = owned.Open();
        var record = PatternRecord(7);
        var expected = Bytes(ref record);
        prefix.CopyTo(expected, 0);
        Assert.False(reader.ReadRecord(ref record));
        Assert.Equal(expected, Bytes(ref record));
    }

    [Fact]
    public void WindowsEofAfterFullReadDoesNotClearTheReusedRecord()
    {
        var record = PatternRecord(31);
        using var owned = new OwnedFile(Bytes(ref record));
        var reader = owned.Open();
        record = default;
        Assert.True(reader.ReadRecord(ref record));
        var beforeEof = Bytes(ref record);
        Assert.False(reader.ReadRecord(ref record));
        Assert.Equal(beforeEof, Bytes(ref record));
        Assert.False(reader.ReadRecord(ref record));
        Assert.Equal(beforeEof, Bytes(ref record));
    }

    [Fact]
    public void WindowsShortCountReadPreservesUnwrittenCallerIntegerBytes()
    {
        using var owned = new OwnedFile([0xA5]);
        var reader = owned.Open();
        var count = 0x11223344;
        var expected = BitConverter.GetBytes(count);
        expected[0] = 0xA5;
        Assert.False(reader.ReadCount(ref count));
        Assert.Equal(BitConverter.ToInt32(expected), count);
    }

    [Fact]
    public void WindowsNegativeCountIsReturnedWithoutNormalization()
    {
        using var owned = new OwnedFile(BitConverter.GetBytes(-1));
        var reader = owned.Open();
        var count = 99;
        Assert.True(reader.ReadCount(ref count));
        Assert.Equal(-1, count);
    }

    [Fact]
    public void WindowsCountRecordCountReadsUseOneAdvancingKernelCursor()
    {
        var record = PatternRecord(43);
        var recordBytes = Bytes(ref record);
        var payload = BitConverter.GetBytes(1).Concat(recordBytes).Concat(BitConverter.GetBytes(-3)).ToArray();
        using var owned = new OwnedFile(payload);
        var reader = owned.Open();
        var count = 0;
        record = default;
        Assert.True(reader.ReadCount(ref count));
        Assert.Equal(1, count);
        Assert.True(reader.ReadRecord(ref record));
        Assert.Equal(recordBytes, Bytes(ref record));
        Assert.True(reader.ReadCount(ref count));
        Assert.Equal(-3, count);
        Assert.False(reader.ReadCount(ref count));
        Assert.Equal(-3, count);
    }

    [Fact]
    public void WindowsFalseEofReadStillLeavesThePreviousRecordAvailableForConversion()
    {
        var counter = LegacyNativePoolItems.ItemCounter;
        var table = LegacyNativePoolItems.ItemsBase;
        try
        {
            LegacyNativePoolItems.ItemCounter = null!;
            LegacyNativePoolItems.ItemsBase = null;
            var record = new LegacyNativeClientItemRecord { UID = 0u, ITEM_ID = 600000u, Plus = 19 };
            using var owned = new OwnedFile(Bytes(ref record));
            var reader = owned.Open();
            record = default;
            _ = reader.ReadRecord(ref record); // Native caller ignores the exact-count boolean.
            var first = record.GetDataItem();
            Assert.False(reader.ReadRecord(ref record));
            var second = record.GetDataItem();
            Assert.Equal(0u, first.Uid);
            Assert.Equal(0u, second.Uid);
            Assert.Equal(600000u, second.ITEM_ID);
            Assert.Equal((byte)19, second.Plus);
            Assert.NotSame(first, second);
            Assert.Null(LegacyNativePoolItems.ItemCounter);
        }
        finally { LegacyNativePoolItems.ItemsBase = table; LegacyNativePoolItems.ItemCounter = counter; }
    }

    [Fact]
    public void WindowsRepeatedFullReadsReplaceTheSameCallerRecordInOrder()
    {
        var first = PatternRecord(5);
        var second = PatternRecord(113);
        var firstBytes = Bytes(ref first);
        var secondBytes = Bytes(ref second);
        using var owned = new OwnedFile(firstBytes.Concat(secondBytes).ToArray());
        var reader = owned.Open();
        var buffer = default(LegacyNativeClientItemRecord);
        Assert.True(reader.ReadRecord(ref buffer));
        Assert.Equal(firstBytes, Bytes(ref buffer));
        Assert.True(reader.ReadRecord(ref buffer));
        Assert.Equal(secondBytes, Bytes(ref buffer));
    }

    [Fact]
    public void WindowsOpenRetainsNativeDefaultWriterExclusionAndReadSharing()
    {
        using var owned = new OwnedFile([]);
        _ = owned.Open();
        var second = owned.Track(new LegacyNativeItemRecordFile());
        Assert.False(second.Open(owned.FilePath, FileMode.Open));
        Assert.False(second.Success);
        // Two-argument FileStream defaults to FileShare.Read, not FileShare.None.
        using var compatible = new FileStream(owned.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Assert.True(compatible.CanRead);
    }

    [Fact]
    public void WindowsCloseRetainsNativeNonNullSuccessRatherThanInventingReset()
    {
        using var owned = new OwnedFile(BitConverter.GetBytes(-3));
        var reader = owned.Open(useConstructor: true);
        var count = 0;
        Assert.True(reader.ReadCount(ref count)); // Populate this Open's cache.
        Assert.Equal(-3, count);
        reader.Close();
        Assert.True(reader.Success); // Native Success tests only pFile != null, not handle liveness.
        count = 91;
        Assert.Throws<ObjectDisposedException>(() => reader.ReadCount(ref count));
        Assert.Equal(91, count);
        Assert.True(reader.Open(owned.FilePath, FileMode.Open)); // Closed cache must not survive Open.
        Assert.True(reader.ReadCount(ref count));
        Assert.Equal(-3, count);
        reader.Close();
        Assert.False(reader.Open("\0", FileMode.Open)); // Failure also invalidates that cache.
        Assert.False(reader.Success);
        Assert.Equal("File isn't open/failed to open previously",
            Assert.Throws<IOException>(() => reader.ReadCount(ref count)).Message);
        Assert.Equal(-3, count);
    }

    [Fact]
    public void UnopenedPerfectionReadFaultLeavesEveryCallerByteUntouched()
    {
        var reader = new LegacyNativeItemRecordFile();
        var perfection = PatternPerfection(19);
        var before = Bytes(ref perfection);
        Assert.False(reader.Success);
        var error = Assert.Throws<IOException>(() => reader.ReadPerfection(ref perfection));
        Assert.Equal("File isn't open/failed to open previously", error.Message);
        Assert.Equal(before, Bytes(ref perfection));
    }

    [Fact]
    public void WindowsFullPerfectionReadWritesAllEightyCallerBytes()
    {
        var expected = Enumerable.Range(0, 80).Select(index => unchecked((byte)(index * 37 + 11))).ToArray();
        using var owned = new OwnedFile(expected);
        var reader = owned.Open();
        var perfection = default(LegacyNativeClientItemPerfectionRecord);
        Assert.Equal(80, Unsafe.SizeOf<LegacyNativeClientItemPerfectionRecord>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<LegacyNativeClientItemPerfectionRecord>());
        Assert.True(reader.ReadPerfection(ref perfection));
        Assert.Equal(expected, Bytes(ref perfection)); // Raw-only arbitrary string buffers.
    }

    [Fact]
    public void WindowsShortPerfectionReadOverwritesOnlyAvailablePrefixAndKeepsTail()
    {
        var prefix = Enumerable.Range(0, 23).Select(index => unchecked((byte)(index + 161))).ToArray();
        using var owned = new OwnedFile(prefix);
        var reader = owned.Open();
        var perfection = PatternPerfection(7);
        var expected = Bytes(ref perfection);
        prefix.CopyTo(expected, 0);
        Assert.False(reader.ReadPerfection(ref perfection));
        Assert.Equal(expected, Bytes(ref perfection)); // No getter on partial/arbitrary buffers.
    }

    [Fact]
    public void WindowsEmptyFileRepeatedPerfectionReadsKeepEveryCallerByte()
    {
        using var owned = new OwnedFile([]);
        var reader = owned.Open();
        var perfection = PatternPerfection(79);
        var expected = Bytes(ref perfection);
        Assert.False(reader.ReadPerfection(ref perfection));
        Assert.Equal(expected, Bytes(ref perfection));
        Assert.False(reader.ReadPerfection(ref perfection));
        Assert.Equal(expected, Bytes(ref perfection));
    }

    [Fact]
    public void WindowsEofAfterFullPerfectionReadDoesNotClearTheReusedBuffer()
    {
        var original = PatternPerfection(31);
        var expected = Bytes(ref original);
        using var owned = new OwnedFile(expected);
        var reader = owned.Open();
        var perfection = default(LegacyNativeClientItemPerfectionRecord);
        Assert.True(reader.ReadPerfection(ref perfection));
        Assert.Equal(expected, Bytes(ref perfection));
        Assert.False(reader.ReadPerfection(ref perfection));
        Assert.Equal(expected, Bytes(ref perfection));
        Assert.False(reader.ReadPerfection(ref perfection));
        Assert.Equal(expected, Bytes(ref perfection));
    }

    [Fact]
    public void WindowsRepeatedPerfectionReadsReplaceTheSameCallerBufferInOrder()
    {
        var first = PatternPerfection(5);
        var second = PatternPerfection(113);
        var firstBytes = Bytes(ref first);
        var secondBytes = Bytes(ref second);
        using var owned = new OwnedFile(firstBytes.Concat(secondBytes).ToArray());
        var reader = owned.Open();
        var buffer = default(LegacyNativeClientItemPerfectionRecord);
        Assert.True(reader.ReadPerfection(ref buffer));
        Assert.Equal(firstBytes, Bytes(ref buffer));
        Assert.True(reader.ReadPerfection(ref buffer));
        Assert.Equal(secondBytes, Bytes(ref buffer));
    }

    [Fact]
    public void WindowsCountPerfectionCountReadsUseTheSameAdvancingKernelCursor()
    {
        var original = PatternPerfection(43);
        var perfectionBytes = Bytes(ref original);
        using var owned = new OwnedFile(BitConverter.GetBytes(1).Concat(perfectionBytes)
            .Concat(BitConverter.GetBytes(-3)).ToArray());
        var reader = owned.Open();
        var count = 0;
        var buffer = default(LegacyNativeClientItemPerfectionRecord);
        Assert.True(reader.ReadCount(ref count));
        Assert.Equal(1, count);
        Assert.True(reader.ReadPerfection(ref buffer));
        Assert.Equal(perfectionBytes, Bytes(ref buffer));
        Assert.True(reader.ReadCount(ref count));
        Assert.Equal(-3, count);
        Assert.False(reader.ReadCount(ref count));
        Assert.Equal(-3, count);
    }

    [Fact]
    public void WindowsCountDbItemCountPerfectionCountShareOneCachedHandle()
    {
        var item = PatternRecord(17);
        var perfection = PatternPerfection(91);
        var itemBytes = Bytes(ref item);
        var perfectionBytes = Bytes(ref perfection);
        var payload = BitConverter.GetBytes(1).Concat(itemBytes).Concat(BitConverter.GetBytes(1))
            .Concat(perfectionBytes).Concat(BitConverter.GetBytes(-3)).ToArray();
        using var owned = new OwnedFile(payload);
        var reader = owned.Open();
        var count = 0;
        item = default;
        perfection = default;
        Assert.True(reader.ReadCount(ref count));
        Assert.Equal(1, count);
        Assert.True(reader.ReadRecord(ref item));
        Assert.Equal(itemBytes, Bytes(ref item));
        Assert.True(reader.ReadCount(ref count));
        Assert.Equal(1, count);
        Assert.True(reader.ReadPerfection(ref perfection));
        Assert.Equal(perfectionBytes, Bytes(ref perfection));
        Assert.True(reader.ReadCount(ref count));
        Assert.Equal(-3, count);
        Assert.False(reader.ReadCount(ref count));
        Assert.Equal(-3, count);
    }

    [Fact]
    public void WindowsFreshPerfectionAtEofIsDistinctFromReusedRecordAndCountStorage()
    {
        var source = PatternPerfection(57);
        var sourceBytes = Bytes(ref source);
        using var owned = new OwnedFile(BitConverter.GetBytes(2).Concat(sourceBytes).ToArray());
        var reader = owned.Open();
        var count = 0;
        Assert.True(reader.ReadCount(ref count));
        Assert.Equal(2, count);
        var earlier = default(LegacyNativeClientItemPerfectionRecord);
        _ = reader.ReadPerfection(ref earlier); // Native ignores the exact-count boolean.
        Assert.Equal(sourceBytes, Bytes(ref earlier));
        var fresh = default(LegacyNativeClientItemPerfectionRecord); // Caller chooses fresh storage, not the reader.
        Assert.False(reader.ReadPerfection(ref fresh));
        Assert.Equal(new byte[80], Bytes(ref fresh));
        Assert.Equal(sourceBytes, Bytes(ref earlier));
        Assert.False(reader.ReadCount(ref count)); // Same reused integer keeps its earlier bytes.
        Assert.Equal(2, count);
        // This is straight-line storage control, not a synthetic loader loop or route.
    }

    [Fact]
    public void WindowsClosedPerfectionReadKeepsFaultOrderAndReopenResetsHandleCache()
    {
        var source = PatternPerfection(73);
        var expected = Bytes(ref source);
        using var owned = new OwnedFile(expected);
        var reader = owned.Open();
        var perfection = default(LegacyNativeClientItemPerfectionRecord);
        Assert.True(reader.ReadPerfection(ref perfection));
        reader.Close();
        Assert.True(reader.Success);
        perfection = PatternPerfection(29);
        var before = Bytes(ref perfection);
        Assert.Throws<ObjectDisposedException>(() => reader.ReadPerfection(ref perfection));
        Assert.Equal(before, Bytes(ref perfection));
        Assert.True(reader.Open(owned.FilePath, FileMode.Open));
        Assert.True(reader.ReadPerfection(ref perfection));
        Assert.Equal(expected, Bytes(ref perfection));
        reader.Close();
        Assert.False(reader.Open("\0", FileMode.Open));
        Assert.False(reader.Success);
        Assert.Throws<IOException>(() => reader.ReadPerfection(ref perfection));
        Assert.Equal(expected, Bytes(ref perfection));
    }

    [Fact]
    public void WindowsKnownTerminatedPerfectionReadKeepsBothStringsAndHeaders()
    {
        var source = new LegacyNativeClientItemPerfectionRecord
        {
            ItemUID = 99u, Level = 18u, Progres = 123u, OwnerUID = 456u,
            OwnerName = "owner", SpecialText = "signature"
        }; // Fresh zero storage; both short strings remain terminated inside their 32-byte fields.
        using var owned = new OwnedFile(Bytes(ref source));
        var reader = owned.Open();
        var perfection = default(LegacyNativeClientItemPerfectionRecord);
        Assert.True(reader.ReadPerfection(ref perfection));
        Assert.Equal(99u, perfection.ItemUID);
        Assert.Equal(18u, perfection.Level);
        Assert.Equal(123u, perfection.Progres);
        Assert.Equal(456u, perfection.OwnerUID);
        Assert.Equal("owner", perfection.OwnerName);
        Assert.Equal("signature", perfection.SpecialText);
    }

    private static LegacyNativeClientItemPerfectionRecord PatternPerfection(int seed)
    {
        var bytes = Enumerable.Range(0, 80).Select(index => unchecked((byte)(index * 17 + seed))).ToArray();
        return MemoryMarshal.Read<LegacyNativeClientItemPerfectionRecord>(bytes);
    }

    private static byte[] Bytes(ref LegacyNativeClientItemPerfectionRecord value) =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1)).ToArray();

    private static LegacyNativeClientItemRecord PatternRecord(int seed)
    {
        var bytes = Enumerable.Range(0, 184).Select(index => unchecked((byte)(index * 17 + seed))).ToArray();
        return MemoryMarshal.Read<LegacyNativeClientItemRecord>(bytes);
    }

    private static byte[] Bytes(ref LegacyNativeClientItemRecord value) =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1)).ToArray();

    // Physical controls only. Every native control fails on a non-Windows host:
    // an early return must not look like a passing Kernel32/short-read test.
    private sealed class OwnedFile : IDisposable
    {
        private readonly string workspace;
        private readonly string directory;
        private readonly List<LegacyNativeItemRecordFile> readers = [];
        private bool ownsDirectory;
        private bool ownsFile;
        internal string FilePath { get; }

        internal OwnedFile(byte[] bytes)
        {
            Assert.True(OperatingSystem.IsWindows(), "Windows is required for physical Kernel32 reader controls.");
            workspace = FindWorkspace();
            var parent = Path.Combine(workspace, ".artifacts", "native-item-record-reader-tests");
            directory = Path.Combine(parent, "owned-" + Guid.NewGuid().ToString("N"));
            FilePath = Path.Combine(directory, "items.bin");
            Guard();
            Assert.False(Directory.Exists(directory));
            Directory.CreateDirectory(directory);
            ownsDirectory = true;
            try
            {
                Guard();
                using var stream = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                ownsFile = true;
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            catch
            {
                RemoveOwnedLeaves();
                throw;
            }
        }

        internal LegacyNativeItemRecordFile Track(LegacyNativeItemRecordFile reader)
        {
            readers.Add(reader);
            return reader;
        }

        internal LegacyNativeItemRecordFile Open(bool useConstructor = false)
        {
            var reader = Track(useConstructor
                ? new LegacyNativeItemRecordFile(FilePath, FileMode.Open)
                : new LegacyNativeItemRecordFile());
            if (!useConstructor) Assert.True(reader.Open(FilePath, FileMode.Open));
            Assert.True(reader.Success);
            return reader;
        }

        public void Dispose()
        {
            foreach (var reader in readers)
                if (reader.Success) reader.Close();
            RemoveOwnedLeaves();
        }

        private void RemoveOwnedLeaves()
        {
            Guard();
            if (ownsFile && File.Exists(FilePath))
            {
                Assert.Equal(0, (int)(File.GetAttributes(FilePath) & FileAttributes.ReparsePoint));
                File.Delete(FilePath);
                ownsFile = false;
            }
            if (ownsDirectory && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: false); // Unknown extra entries make cleanup fail.
                ownsDirectory = false;
            }
        }

        private void Guard()
        {
            var expectedParent = Path.GetFullPath(Path.Combine(workspace, ".artifacts", "native-item-record-reader-tests"));
            Assert.Equal(expectedParent, Path.GetDirectoryName(Path.GetFullPath(directory)));
            Assert.Equal(directory, Path.GetDirectoryName(Path.GetFullPath(FilePath)));
            Assert.Equal("items.bin", Path.GetFileName(FilePath));
            Assert.Matches("^owned-[0-9a-f]{32}$", Path.GetFileName(directory));
            var prefix = workspace.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.True(Path.GetFullPath(directory).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            for (var cursor = new DirectoryInfo(directory); cursor is not null; cursor = cursor.Parent)
            {
                if (cursor.Exists)
                    Assert.Equal(0, (int)(cursor.Attributes & FileAttributes.ReparsePoint));
                if (string.Equals(cursor.FullName, workspace, StringComparison.OrdinalIgnoreCase)) break;
            }
        }

        private static string FindWorkspace()
        {
            for (var cursor = new DirectoryInfo(AppContext.BaseDirectory); cursor is not null; cursor = cursor.Parent)
                if (File.Exists(Path.Combine(cursor.FullName, "src", "WolfConquer.Core", "WolfConquer.Core.csproj"))
                    && File.Exists(Path.Combine(cursor.FullName, "tests", "WolfConquer.Tests", "WolfConquer.Tests.csproj")))
                    return Path.GetFullPath(cursor.FullName);
            throw new InvalidOperationException("Owned physical controls require this workspace's test build output.");
        }
    }
}
