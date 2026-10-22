using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeClientProficiencyBinaryIoTests
{
    [Fact]
    public void UnopenedAndFailedOpenOperationsThrowWithoutTouchingCallerStorage()
    {
        var file = new LegacyNativeItemRecordFile();
        var row = Pattern(31);
        var before = LegacyNativeClientProficiencyPersistenceTests.Bytes(ref row);
        Assert.Equal("File isn't open/failed to open previously", Assert.Throws<IOException>(() => file.ReadProf(ref row)).Message);
        Assert.Throws<IOException>(() => file.WriteProf(ref row));
        Assert.False(file.Open("\0", FileMode.Open));
        Assert.Throws<IOException>(() => file.ReadProf(ref row));
        Assert.Throws<IOException>(() => file.WriteProf(ref row));
        Assert.Equal(before, LegacyNativeClientProficiencyPersistenceTests.Bytes(ref row));
    }

    [Fact]
    public void FullShortAndRepeatedEofReadsKeepTheReusedRecordAndPhysicalPadding()
    {
        using var files = new LegacyNativeClientProficiencyPersistenceTests.OwnedProfs();
        var full = LegacyNativeClientProficiencyPersistenceTests.Row(0x11223344, 0x55667788, 0x99, 0xAABBCCDD, uint.MaxValue, 0xEE);
        var prefix = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        files.Seed(full.Concat(prefix).ToArray());
        var file = files.Open(FileMode.Open);
        var row = Pattern(61);
        Assert.True(file.ReadProf(ref row));
        Assert.Equal(full, LegacyNativeClientProficiencyPersistenceTests.Bytes(ref row));
        var expected = full.ToArray(); prefix.CopyTo(expected, 0);
        Assert.False(file.ReadProf(ref row));
        Assert.Equal(expected, LegacyNativeClientProficiencyPersistenceTests.Bytes(ref row));
        Assert.False(file.ReadProf(ref row)); Assert.False(file.ReadProf(ref row));
        Assert.Equal(expected, LegacyNativeClientProficiencyPersistenceTests.Bytes(ref row));
    }

    [Fact]
    public void TypedWritesAndReadsShareTheCountCursorAndReopenResetsTheHandleLifetime()
    {
        using var files = new LegacyNativeClientProficiencyPersistenceTests.OwnedProfs();
        var file = files.Open(FileMode.Create);
        var row = Pattern(87);
        var expected = LegacyNativeClientProficiencyPersistenceTests.Bytes(ref row);
        var count = -3; var trailer = 19;
        Assert.True(file.WriteCount(ref count)); Assert.True(file.WriteProf(ref row)); Assert.True(file.WriteCount(ref trailer));
        Assert.Equal(expected, LegacyNativeClientProficiencyPersistenceTests.Bytes(ref row));
        file.Close();
        Assert.Equal(BitConverter.GetBytes(-3).Concat(expected).Concat(BitConverter.GetBytes(19)).ToArray(), File.ReadAllBytes(files.FilePath));
        Assert.True(file.Open(files.FilePath, FileMode.Open));
        count = 0; row = default;
        Assert.True(file.ReadCount(ref count)); Assert.Equal(-3, count);
        Assert.True(file.ReadProf(ref row)); Assert.Equal(expected, LegacyNativeClientProficiencyPersistenceTests.Bytes(ref row));
        trailer = 28;
        Assert.True(file.WriteCount(ref trailer));
        file.Close();
        Assert.Equal(BitConverter.GetBytes(-3).Concat(expected).Concat(BitConverter.GetBytes(28)).ToArray(), File.ReadAllBytes(files.FilePath));
    }

    [Fact]
    public void ClosedOwnerStillReportsSuccessButTypedOperationsFaultUntilReopen()
    {
        using var files = new LegacyNativeClientProficiencyPersistenceTests.OwnedProfs();
        var prefix = new byte[] { 7, 8, 9, 10, 11 };
        files.Seed(prefix);
        var file = files.Open(FileMode.Open);
        var row = Pattern(42);
        var expected = LegacyNativeClientProficiencyPersistenceTests.Bytes(ref row); prefix.CopyTo(expected, 0);
        Assert.False(file.ReadProf(ref row));
        Assert.Equal(expected, LegacyNativeClientProficiencyPersistenceTests.Bytes(ref row));
        file.Close();
        Assert.True(file.Success);
        Assert.Throws<ObjectDisposedException>(() => file.ReadProf(ref row));
        Assert.Throws<ObjectDisposedException>(() => file.WriteProf(ref row));
        Assert.Equal(expected, LegacyNativeClientProficiencyPersistenceTests.Bytes(ref row));
        Assert.True(file.Open(files.FilePath, FileMode.Open));
        row = default;
        Assert.False(file.ReadProf(ref row));
        Assert.Equal(prefix.Concat(new byte[15]).ToArray(), LegacyNativeClientProficiencyPersistenceTests.Bytes(ref row));
    }

    private static LegacyNativeClientProficiency.DBProf Pattern(int seed) =>
        MemoryMarshal.Read<LegacyNativeClientProficiency.DBProf>(Enumerable.Range(0, 20)
            .Select(index => unchecked((byte)(seed + index * 17))).ToArray());
}
