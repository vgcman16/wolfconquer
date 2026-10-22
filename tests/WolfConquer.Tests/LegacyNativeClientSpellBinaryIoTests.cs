using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeClientSpellBinaryIoTests
{
    [Fact]
    public void UnopenedAndFailedOpenTypedOperationsKeepCallerBytesAndNativeFaults()
    {
        var file = new LegacyNativeItemRecordFile();
        var row = Pattern(11);
        var before = LegacyNativeClientSpellPersistenceTests.Bytes(ref row);
        Assert.Equal("File isn't open/failed to open previously", Assert.Throws<IOException>(() => file.ReadSpell(ref row)).Message);
        Assert.Throws<IOException>(() => file.WriteSpell(ref row));
        Assert.False(file.Open("\0", FileMode.Open));
        Assert.Throws<IOException>(() => file.ReadSpell(ref row));
        Assert.Throws<IOException>(() => file.WriteSpell(ref row));
        Assert.Equal(before, LegacyNativeClientSpellPersistenceTests.Bytes(ref row));
    }

    [Fact]
    public void FullThenShortAndRepeatedEofReadsReuseTheSameCallerRecordWithoutClearing()
    {
        using var files = new LegacyNativeClientSpellPersistenceTests.OwnedSpells();
        var full = LegacyNativeClientSpellPersistenceTests.Row(71, 4, -77, 9, 2, 3, 0xEE);
        files.Seed(full.Concat(new byte[] { 0x34, 0x12 }).ToArray());
        var file = files.Open(FileMode.Open);
        var row = default(LegacyNativeClientSpells.DBSpell);
        Assert.True(file.ReadSpell(ref row));
        Assert.Equal(full, LegacyNativeClientSpellPersistenceTests.Bytes(ref row));
        Assert.False(file.ReadSpell(ref row));
        var partial = full.ToArray(); partial[0] = 0x34; partial[1] = 0x12;
        Assert.Equal(partial, LegacyNativeClientSpellPersistenceTests.Bytes(ref row));
        Assert.False(file.ReadSpell(ref row)); Assert.False(file.ReadSpell(ref row));
        Assert.Equal(partial, LegacyNativeClientSpellPersistenceTests.Bytes(ref row));
    }

    [Fact]
    public void ShortFirstReadOverwritesOnlyItsPhysicalPrefixIncludingNoPaddingRepair()
    {
        using var files = new LegacyNativeClientSpellPersistenceTests.OwnedSpells();
        var prefix = new byte[] { 1, 2, 3, 4, 5, 6, 7 };
        files.Seed(prefix);
        var file = files.Open(FileMode.Open);
        var row = Pattern(83);
        var expected = LegacyNativeClientSpellPersistenceTests.Bytes(ref row);
        prefix.CopyTo(expected, 0);
        Assert.False(file.ReadSpell(ref row));
        Assert.Equal(expected, LegacyNativeClientSpellPersistenceTests.Bytes(ref row));
    }

    [Fact]
    public void TypedSpellWritesShareTheAdvancingCountCursorAndPreserveAllTwelveBytes()
    {
        using var files = new LegacyNativeClientSpellPersistenceTests.OwnedSpells();
        var file = files.Open(FileMode.Create);
        var row = Pattern(39);
        var expected = LegacyNativeClientSpellPersistenceTests.Bytes(ref row);
        var first = 7; var last = -9;
        Assert.True(file.WriteCount(ref first)); Assert.True(file.WriteSpell(ref row)); Assert.True(file.WriteCount(ref last));
        Assert.Equal(expected, LegacyNativeClientSpellPersistenceTests.Bytes(ref row));
        file.Close();
        Assert.Equal(BitConverter.GetBytes(7).Concat(expected).Concat(BitConverter.GetBytes(-9)).ToArray(), File.ReadAllBytes(files.FilePath));
        Assert.True(file.Open(files.FilePath, FileMode.Open));
        first = 0; row = default;
        Assert.True(file.ReadCount(ref first)); Assert.Equal(7, first);
        Assert.True(file.ReadSpell(ref row)); Assert.Equal(expected, LegacyNativeClientSpellPersistenceTests.Bytes(ref row));
        last = 15;
        Assert.True(file.WriteCount(ref last));
        file.Close();
        Assert.Equal(BitConverter.GetBytes(7).Concat(expected).Concat(BitConverter.GetBytes(15)).ToArray(), File.ReadAllBytes(files.FilePath));
    }

    [Fact]
    public void ClosedSpellOperationsRetainNativeSuccessAndDisposedHandleFaultUntilReopen()
    {
        using var files = new LegacyNativeClientSpellPersistenceTests.OwnedSpells();
        var image = LegacyNativeClientSpellPersistenceTests.Row(22, 3, 4, 5, 6, 7, 8);
        files.Seed(image);
        var file = files.Open(FileMode.Open);
        var row = default(LegacyNativeClientSpells.DBSpell);
        Assert.True(file.ReadSpell(ref row));
        var before = LegacyNativeClientSpellPersistenceTests.Bytes(ref row);
        file.Close();
        Assert.True(file.Success);
        Assert.Throws<ObjectDisposedException>(() => file.ReadSpell(ref row));
        Assert.Throws<ObjectDisposedException>(() => file.WriteSpell(ref row));
        Assert.Equal(before, LegacyNativeClientSpellPersistenceTests.Bytes(ref row));
        Assert.True(file.Open(files.FilePath, FileMode.Open));
        Assert.True(file.ReadSpell(ref row));
        Assert.Equal(image, LegacyNativeClientSpellPersistenceTests.Bytes(ref row));
    }

    private static LegacyNativeClientSpells.DBSpell Pattern(int seed) => MemoryMarshal.Read<LegacyNativeClientSpells.DBSpell>(
        Enumerable.Range(0, 12).Select(index => unchecked((byte)(seed + index * 17))).ToArray());
}
