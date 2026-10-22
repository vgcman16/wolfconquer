using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNativeItemRecordFileWriteTests
{
    [Fact]
    public void UnopenedTypedWritesFaultWithoutChangingCallerBuffers()
    {
        var file = new LegacyNativeItemRecordFile();
        int count = -17;
        var record = PatternRecord(19);
        var perfection = PatternPerfection(97);
        var recordBefore = Bytes(ref record);
        var perfectionBefore = Bytes(ref perfection);
        Assert.False(file.Success);
        Assert.Equal("File isn't open/failed to open previously",
            Assert.Throws<IOException>(() => file.WriteCount(ref count)).Message);
        Assert.Throws<IOException>(() => file.WriteRecord(ref record));
        Assert.Throws<IOException>(() => file.WritePerfection(ref perfection));
        Assert.Equal(-17, count);
        Assert.Equal(recordBefore, Bytes(ref record));
        Assert.Equal(perfectionBefore, Bytes(ref perfection));
    }

    [Fact]
    public void FailedOpenRetainsNativeTypedWriteFailureAndCallerBytes()
    {
        var file = new LegacyNativeItemRecordFile("\0", FileMode.Create);
        int count = int.MinValue;
        var record = PatternRecord(29);
        var perfection = PatternPerfection(71);
        var recordBefore = Bytes(ref record);
        var perfectionBefore = Bytes(ref perfection);
        Assert.False(file.Success);
        Assert.False(file.Open("\0", FileMode.Create));
        Assert.Equal("File isn't open/failed to open previously",
            Assert.Throws<IOException>(() => file.WriteCount(ref count)).Message);
        Assert.Throws<IOException>(() => file.WriteRecord(ref record));
        Assert.Throws<IOException>(() => file.WritePerfection(ref perfection));
        Assert.Equal(int.MinValue, count);
        Assert.Equal(recordBefore, Bytes(ref record));
        Assert.Equal(perfectionBefore, Bytes(ref perfection));
    }

    [Fact]
    public void WindowsClosedTypedWritesKeepNonNullSuccessAndDisposedFaultOrder()
    {
        using var owned = new OwnedFile([]);
        var file = owned.Open();
        int count = 11;
        var record = PatternRecord(43);
        var perfection = PatternPerfection(67);
        var recordBefore = Bytes(ref record);
        var perfectionBefore = Bytes(ref perfection);
        Assert.True(file.WriteCount(ref count)); // Populate the shared per-Open handle cache.
        file.Close();
        Assert.True(file.Success);
        Assert.Throws<ObjectDisposedException>(() => file.WriteCount(ref count));
        Assert.Throws<ObjectDisposedException>(() => file.WriteRecord(ref record));
        Assert.Throws<ObjectDisposedException>(() => file.WritePerfection(ref perfection));
        Assert.Equal(11, count);
        Assert.Equal(recordBefore, Bytes(ref record));
        Assert.Equal(perfectionBefore, Bytes(ref perfection));
        Assert.Equal(BitConverter.GetBytes(11), File.ReadAllBytes(owned.FilePath));
    }

    [Fact]
    public void WindowsMixedTypedWritesPersistEveryByteAndPreserveCallerStorage()
    {
        using var owned = new OwnedFile([]);
        var file = owned.Open();
        int leading = unchecked((int)0x87654321);
        int middle = -3;
        int trailing = 0;
        var record = PatternRecord(5);
        var perfection = PatternPerfection(113);
        var recordBytes = Pattern(184, 5);
        var perfectionBytes = Pattern(80, 113);
        Assert.Equal(184, Unsafe.SizeOf<LegacyNativeClientItemRecord>());
        Assert.Equal(80, Unsafe.SizeOf<LegacyNativeClientItemPerfectionRecord>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<LegacyNativeClientItemRecord>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<LegacyNativeClientItemPerfectionRecord>());
        Assert.Equal(recordBytes, Bytes(ref record)); // Includes native alignment padding.
        Assert.Equal(perfectionBytes, Bytes(ref perfection)); // Arbitrary raw fixed strings, never decoded.
        Assert.True(file.WriteCount(ref leading));
        Assert.True(file.WriteRecord(ref record));
        Assert.True(file.WriteCount(ref middle));
        Assert.True(file.WritePerfection(ref perfection));
        Assert.True(file.WriteCount(ref trailing));
        Assert.Equal(unchecked((int)0x87654321), leading);
        Assert.Equal(-3, middle);
        Assert.Equal(0, trailing);
        Assert.Equal(recordBytes, Bytes(ref record));
        Assert.Equal(perfectionBytes, Bytes(ref perfection));
        file.Close();
        var expected = BitConverter.GetBytes(leading).Concat(recordBytes)
            .Concat(BitConverter.GetBytes(middle)).Concat(perfectionBytes)
            .Concat(BitConverter.GetBytes(trailing)).ToArray();
        Assert.Equal(276, expected.Length);
        Assert.Equal(expected, File.ReadAllBytes(owned.FilePath));
    }

    [Fact]
    public void WindowsReadThenWriteUsesTheExistingKernelCursorInsteadOfRewinding()
    {
        var oldRecord = Pattern(184, 13);
        var expectedPerfection = Pattern(80, 37);
        using var owned = new OwnedFile(BitConverter.GetBytes(11).Concat(oldRecord)
            .Concat(expectedPerfection).Concat(BitConverter.GetBytes(99)).ToArray());
        var file = owned.Open();
        int count = 0;
        Assert.True(file.ReadCount(ref count));
        Assert.Equal(11, count);
        var replacement = PatternRecord(83);
        var replacementBytes = Bytes(ref replacement);
        Assert.True(file.WriteRecord(ref replacement)); // Must overwrite bytes 4..187, not start at zero.
        var perfection = default(LegacyNativeClientItemPerfectionRecord);
        Assert.True(file.ReadPerfection(ref perfection));
        Assert.Equal(expectedPerfection, Bytes(ref perfection));
        count = -7;
        Assert.True(file.WriteCount(ref count));
        Assert.Equal(replacementBytes, Bytes(ref replacement));
        file.Close();
        Assert.Equal(BitConverter.GetBytes(11).Concat(replacementBytes)
            .Concat(expectedPerfection).Concat(BitConverter.GetBytes(-7)).ToArray(),
            File.ReadAllBytes(owned.FilePath));
    }

    [Fact]
    public void WindowsWriteThenReadUsesTheSameKernelCursorAcrossAllTypedShapes()
    {
        var expectedRecord = Pattern(184, 29);
        var oldPerfection = Pattern(80, 61);
        using var owned = new OwnedFile(BitConverter.GetBytes(11).Concat(expectedRecord)
            .Concat(oldPerfection).Concat(BitConverter.GetBytes(99)).ToArray());
        var file = owned.Open();
        int count = 77;
        Assert.True(file.WriteCount(ref count));
        var record = default(LegacyNativeClientItemRecord);
        Assert.True(file.ReadRecord(ref record)); // Must read after the just-written count.
        Assert.Equal(expectedRecord, Bytes(ref record));
        var replacement = PatternPerfection(103);
        var replacementBytes = Bytes(ref replacement);
        Assert.True(file.WritePerfection(ref replacement));
        Assert.True(file.ReadCount(ref count));
        Assert.Equal(99, count);
        Assert.False(file.ReadCount(ref count));
        Assert.Equal(99, count);
        Assert.Equal(replacementBytes, Bytes(ref replacement));
        file.Close();
        Assert.Equal(BitConverter.GetBytes(77).Concat(expectedRecord)
            .Concat(replacementBytes).Concat(BitConverter.GetBytes(99)).ToArray(),
            File.ReadAllBytes(owned.FilePath));
    }

    [Fact]
    public void WindowsCloseOpenCreateTruncatesAndStartsANewSharedHandleLifetime()
    {
        using var owned = new OwnedFile(Pattern(500, 11));
        var file = owned.Open(FileMode.Create);
        int count = -3;
        Assert.True(file.WriteCount(ref count));
        file.Close();
        Assert.Equal(BitConverter.GetBytes(-3), File.ReadAllBytes(owned.FilePath));
        Assert.True(file.Open(owned.FilePath, FileMode.Open));
        count = 0;
        Assert.True(file.ReadCount(ref count));
        Assert.Equal(-3, count);
        file.Close();
        Assert.True(file.Open(owned.FilePath, FileMode.Create));
        var perfection = PatternPerfection(79);
        var expected = Bytes(ref perfection);
        Assert.True(file.WritePerfection(ref perfection));
        file.Close();
        Assert.Equal(expected, File.ReadAllBytes(owned.FilePath));
        Assert.True(file.Open(owned.FilePath, FileMode.Open));
        perfection = default;
        Assert.True(file.ReadPerfection(ref perfection));
        Assert.Equal(expected, Bytes(ref perfection));
    }

    private static byte[] Pattern(int size, int seed) =>
        Enumerable.Range(0, size).Select(index => unchecked((byte)(index * 17 + seed))).ToArray();

    private static LegacyNativeClientItemRecord PatternRecord(int seed) =>
        MemoryMarshal.Read<LegacyNativeClientItemRecord>(Pattern(184, seed));

    private static LegacyNativeClientItemPerfectionRecord PatternPerfection(int seed) =>
        MemoryMarshal.Read<LegacyNativeClientItemPerfectionRecord>(Pattern(80, seed));

    private static byte[] Bytes<T>(ref T value) where T : struct =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1)).ToArray();

    // Own only this test's GUID directory and its one physical file. No original saves.
    private sealed class OwnedFile : IDisposable
    {
        private readonly string workspace;
        private readonly string directory;
        private readonly List<LegacyNativeItemRecordFile> files = [];
        private bool ownsDirectory;
        private bool ownsFile;
        internal string FilePath { get; }

        internal OwnedFile(byte[] bytes)
        {
            Assert.True(OperatingSystem.IsWindows(), "Windows is required for physical Kernel32 writer controls.");
            workspace = FindWorkspace();
            directory = Path.Combine(workspace, ".artifacts", "native-item-record-writer-tests", "owned-" + Guid.NewGuid().ToString("N"));
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

        internal LegacyNativeItemRecordFile Open(FileMode mode = FileMode.Open)
        {
            var file = new LegacyNativeItemRecordFile();
            files.Add(file);
            Assert.True(file.Open(FilePath, mode));
            return file;
        }

        public void Dispose()
        {
            foreach (var file in files)
                if (file.Success) file.Close();
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
                Directory.Delete(directory, recursive: false);
                ownsDirectory = false;
            }
        }

        private void Guard()
        {
            Assert.Equal(Path.GetFullPath(Path.Combine(workspace, ".artifacts", "native-item-record-writer-tests")),
                Path.GetDirectoryName(Path.GetFullPath(directory)));
            Assert.Equal(directory, Path.GetDirectoryName(Path.GetFullPath(FilePath)));
            Assert.Equal("items.bin", Path.GetFileName(FilePath));
            Assert.Matches("^owned-[0-9a-f]{32}$", Path.GetFileName(directory));
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
