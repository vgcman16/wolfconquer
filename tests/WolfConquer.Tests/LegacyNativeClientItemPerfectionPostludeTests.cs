using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeClientItemPerfectionPostludeTests
{
    [Fact]
    public void NullReaderFaultPrecedesAnyCountOrOwnerAccess()
    {
        int count = 7;
        Assert.Throws<NullReferenceException>(() =>
            LegacyNativeClientItemPerfectionPostlude.ReadAndApply(null!, null!, null!, ref count));
        Assert.Equal(7, count);
    }

    [Fact]
    public void UnopenedCountReadFaultPrecedesNullLookupOwners()
    {
        var reader = new LegacyNativeItemRecordFile();
        int count = 7;
        Assert.Throws<IOException>(() =>
            LegacyNativeClientItemPerfectionPostlude.ReadAndApply(reader, null!, null!, ref count));
        Assert.Equal(7, count);
        Assert.False(reader.Success);
    }

    [Fact]
    public void WindowsZeroCountClosesWithoutDereferencingNullClientOrDepositedMap()
    {
        using var file = new OwnedFile(Image(0));
        var reader = file.Open();
        int count = 9;
        LegacyNativeClientItemPerfectionPostlude.ReadAndApply(reader, null!, null!, ref count);
        Assert.Equal(0, count);
        AssertClosedButNativeSuccess(reader);
    }

    [Fact]
    public void WindowsNegativeCountStaysNegativeAndClosesWithoutLookup()
    {
        using var file = new OwnedFile(Image(-1));
        var reader = file.Open();
        int count = 9;
        LegacyNativeClientItemPerfectionPostlude.ReadAndApply(reader, null!, null!, ref count);
        Assert.Equal(-1, count);
        AssertClosedButNativeSuccess(reader);
    }

    [Fact]
    public void WindowsDepositedItemWinsBeforeEquipmentInventoryAndWarehouse()
    {
        var client = Client();
        var depositedItem = Item(7);
        var equipped = Item(7);
        var inventory = Item(7);
        var bankItem = Item(7);
        client.Equipment!.ClientItems.TryAdd(7, equipped);
        client.Inventory!.AddDBItem(inventory);
        client.Warehouse!.ClientItems.TryAdd(44, Bank(7, bankItem));
        using var file = new OwnedFile(Image(1, Record(7)));
        int count = 0;
        LegacyNativeClientItemPerfectionPostlude.ReadAndApply(
            file.Open(), client, new() { [7] = depositedItem }, ref count);
        AssertApplied(depositedItem);
        AssertUntouched(equipped);
        AssertUntouched(inventory);
        AssertUntouched(bankItem);
    }

    [Fact]
    public void WindowsDepositedHitDoesNotEvenDereferenceNullClient()
    {
        var item = Item(7);
        using var file = new OwnedFile(Image(1, Record(7)));
        int count = 0;
        LegacyNativeClientItemPerfectionPostlude.ReadAndApply(
            file.Open(), null!, new() { [7] = item }, ref count);
        AssertApplied(item);
    }

    [Fact]
    public void WindowsEquipmentHitPrecedesNullInventoryAndWarehouse()
    {
        var client = new LegacyNativeGameClientRuntime();
        client.Equipment = new LegacyNativeEquipItemStorage(client);
        var item = Item(7);
        client.Equipment.ClientItems.TryAdd(7, item);
        using var file = new OwnedFile(Image(1, Record(7)));
        int count = 0;
        LegacyNativeClientItemPerfectionPostlude.ReadAndApply(file.Open(), client, new(), ref count);
        AssertApplied(item);
        Assert.Null(client.Inventory);
        Assert.Null(client.Warehouse);
    }

    [Fact]
    public void WindowsInventoryHitPrecedesNullWarehouse()
    {
        var client = Client();
        client.Warehouse = null;
        var item = Item(7);
        client.Inventory!.AddDBItem(item);
        using var file = new OwnedFile(Image(1, Record(7)));
        int count = 0;
        LegacyNativeClientItemPerfectionPostlude.ReadAndApply(file.Open(), client, new(), ref count);
        AssertApplied(item);
        Assert.Null(client.Warehouse);
    }

    [Fact]
    public void WindowsFirstMatchingNativeBankEnumerationWinsWithoutSortingBanks()
    {
        var client = Client();
        var a = Item(7);
        var b = Item(7);
        client.Warehouse!.ClientItems.TryAdd(44, Bank(7, a));
        client.Warehouse.ClientItems.TryAdd(9, Bank(7, b));
        var first = client.Warehouse.ClientItems.Values.First(bank => bank.ContainsKey(7))[7];
        var other = ReferenceEquals(first, a) ? b : a;
        using var file = new OwnedFile(Image(1, Record(7)));
        int count = 0;
        LegacyNativeClientItemPerfectionPostlude.ReadAndApply(file.Open(), client, new(), ref count);
        AssertApplied(first);
        AssertUntouched(other);
    }

    [Fact]
    public void WindowsUnmatchedPerfectionDoesNotInventRuneOrOtherFallbackSearches()
    {
        var client = Client();
        var rune = Item(7);
        rune.Position = 212;
        Assert.True(client.Rune!.Add(rune));
        using var file = new OwnedFile(Image(1, Record(7)));
        int count = 0;
        LegacyNativeClientItemPerfectionPostlude.ReadAndApply(file.Open(), client, new(), ref count);
        AssertUntouched(rune);
        Assert.Empty(client.Inventory!.ClientItems);
        Assert.Empty(client.Equipment!.ClientItems);
        Assert.Empty(client.Warehouse!.ClientItems);
    }

    [Fact]
    public void WindowsLookupUsesNativeDictionaryKeyNotTheItemsCurrentUid()
    {
        var client = Client();
        var item = Item(99);
        client.Equipment!.ClientItems.TryAdd(7, item);
        using var file = new OwnedFile(Image(1, Record(7)));
        int count = 0;
        LegacyNativeClientItemPerfectionPostlude.ReadAndApply(file.Open(), client, new(), ref count);
        Assert.Same(item, client.Equipment.ClientItems[7]);
        Assert.Equal(99u, item.Uid);
        AssertApplied(item);
    }

    [Fact]
    public void WindowsLaterPerfectionRecordOverwritesFiveStoresOnTheSameItem()
    {
        var item = Item(7);
        using var file = new OwnedFile(Image(2, Record(7), Record(7, 23, 24, 25, "second", "last")));
        int count = 0;
        var map = new Dictionary<uint, LegacyNativeGameItemRuntime> { [7] = item };
        LegacyNativeClientItemPerfectionPostlude.ReadAndApply(file.Open(), null!, map, ref count);
        Assert.Same(item, map[7]);
        Assert.Equal(23u, item.PerfectionLevel);
        Assert.Equal(24u, item.PerfectionProgress);
        Assert.Equal(25u, item.OwnerUID);
        Assert.Equal("second", item.OwnerName);
        Assert.Equal("last", item.Signature);
        Assert.Equal(410009u, item.ITEM_ID);
        Assert.Equal((ushort)17, item.Inscribed);
    }

    [Fact]
    public void WindowsShortPerfectionReadUsesItsAvailablePrefixAndFreshZeroTail()
    {
        var item = Item(7);
        using var file = new OwnedFile(Image(1, Record(7, 23)[..8]));
        int count = 0;
        LegacyNativeClientItemPerfectionPostlude.ReadAndApply(
            file.Open(), null!, new() { [7] = item }, ref count);
        Assert.Equal(23u, item.PerfectionLevel);
        Assert.Equal(0u, item.PerfectionProgress);
        Assert.Equal(0u, item.OwnerUID);
        Assert.Equal("", item.OwnerName);
        Assert.Equal("", item.Signature);
    }

    [Fact]
    public void WindowsEachIterationStartsFreshPerfectionRatherThanReusingPreviousBytes()
    {
        var first = Item(7);
        var atEof = Item(99);
        using var file = new OwnedFile(Image(2, Record(7)));
        int count = 0;
        LegacyNativeClientItemPerfectionPostlude.ReadAndApply(
            file.Open(), null!, new() { [7] = first, [0] = atEof }, ref count);
        AssertApplied(first);
        Assert.Equal(0u, atEof.PerfectionLevel);
        Assert.Equal(0u, atEof.PerfectionProgress);
        Assert.Equal(0u, atEof.OwnerUID);
        Assert.Equal("", atEof.OwnerName);
        Assert.Equal("", atEof.Signature);
        Assert.Equal(99u, atEof.Uid);
    }

    [Fact]
    public void WindowsFalseEofCountReadRetainsTheCallersEarlierCount()
    {
        var item = Item(99);
        using var file = new OwnedFile([]);
        var reader = file.Open();
        int count = 2; // Same integer retained by the preceding native outer phase.
        LegacyNativeClientItemPerfectionPostlude.ReadAndApply(
            reader, null!, new() { [0] = item }, ref count);
        Assert.Equal(2, count);
        Assert.Equal(0u, item.PerfectionLevel);
        Assert.Equal("", item.OwnerName);
        AssertClosedButNativeSuccess(reader);
    }

    [Fact]
    public void WindowsShortCountReadPreservesCallerHighBytesWithoutNormalization()
    {
        using var file = new OwnedFile([0xFF]);
        var reader = file.Open();
        int count = unchecked((int)0xFFFFFF00);
        LegacyNativeClientItemPerfectionPostlude.ReadAndApply(reader, null!, null!, ref count);
        Assert.Equal(-1, count); // One written low byte; three old high bytes retained.
        AssertClosedButNativeSuccess(reader);
    }

    [Fact]
    public void WindowsLookupFaultHappensAfterReadAndSkipsTheNativeFinalClose()
    {
        var marker = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(marker, 123456789);
        using var file = new OwnedFile(Image(1, Record(7), marker));
        var reader = file.Open();
        int count = 0;
        Assert.Throws<NullReferenceException>(() =>
            LegacyNativeClientItemPerfectionPostlude.ReadAndApply(reader, null!, null!, ref count));
        Assert.Equal(1, count);
        int next = 0;
        Assert.True(reader.ReadCount(ref next)); // No finally closed the actual handle.
        Assert.Equal(123456789, next); // Full80-byte read preceded lookup fault.
    }

    [Fact]
    public void WindowsNullDepositedValueFaultDoesNotFallThroughToEquipmentOrClose()
    {
        var client = Client();
        var equipped = Item(7);
        client.Equipment!.ClientItems.TryAdd(7, equipped);
        var marker = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(marker, 71);
        using var file = new OwnedFile(Image(1, Record(7), marker));
        var reader = file.Open();
        int count = 0;
        Assert.Throws<NullReferenceException>(() =>
            LegacyNativeClientItemPerfectionPostlude.ReadAndApply(
                reader, client, new() { [7] = null! }, ref count));
        AssertUntouched(equipped);
        int next = 0;
        Assert.True(reader.ReadCount(ref next));
        Assert.Equal(71, next);
    }

    [Fact]
    public void WindowsRealCountRecordCountPerfectionPhasesShareReaderCountAndItemIdentity()
    {
        var raw = new LegacyNativeClientItemRecord
        {
            UID = 7, ITEM_ID = 410009, Bless = 8, Plus = 13, Enchant = 2,
        };
        var rawBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref raw, 1)).ToArray();
        using var file = new OwnedFile(Image(1, rawBytes, Image(1, Record(7))));
        var reader = file.Open();
        int count = 0;
        Assert.True(reader.ReadCount(ref count));
        Assert.Equal(1, count);
        var reused = default(LegacyNativeClientItemRecord);
        Assert.True(reader.ReadRecord(ref reused));
        LegacyNativeOuterItemRecordSanitation.SanitizeBeforeConversion(ref reused);
        var item = reused.GetDataItem();
        var client = Client();
        client.Inventory!.AddDBItem(item);
        LegacyNativeClientItemPerfectionPostlude.ReadAndApply(reader, client, new(), ref count);
        Assert.Equal(1, count);
        Assert.Same(item, client.Inventory.ClientItems[7]);
        Assert.Equal((byte)7, item.Bless);
        Assert.Equal((byte)12, item.Plus);
        Assert.Equal((byte)2, item.Enchant);
        AssertApplied(item);
        AssertClosedButNativeSuccess(reader);
        // Explicit two-phase control, NOT a test of omitted Demon/child/routing branches.
    }

    private static LegacyNativeGameClientRuntime Client()
    {
        var client = new LegacyNativeGameClientRuntime();
        LegacyNativeItemContainerMemberPhases.AssignLoadClientItemOwnersPrefix(client.Player);
        return client;
    }

    private static ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> Bank(
        uint key, LegacyNativeGameItemRuntime item) => new(new[]
        {
            new KeyValuePair<uint, LegacyNativeGameItemRuntime>(key, item),
        });

    private static LegacyNativeGameItemRuntime Item(uint uid) => new()
    {
        Uid = uid, ITEM_ID = 410009, PerfectionLevel = 101,
        PerfectionProgress = 102, OwnerUID = 103, OwnerName = "old-owner",
        Signature = "old-text", Inscribed = 17,
    };

    private static void AssertApplied(LegacyNativeGameItemRuntime item)
    {
        Assert.Equal(9u, item.PerfectionLevel);
        Assert.Equal(10u, item.PerfectionProgress);
        Assert.Equal(11u, item.OwnerUID);
        Assert.Equal("owner", item.OwnerName);
        Assert.Equal("tag", item.Signature);
    }

    private static void AssertUntouched(LegacyNativeGameItemRuntime item)
    {
        Assert.Equal(101u, item.PerfectionLevel);
        Assert.Equal(102u, item.PerfectionProgress);
        Assert.Equal(103u, item.OwnerUID);
        Assert.Equal("old-owner", item.OwnerName);
        Assert.Equal("old-text", item.Signature);
    }

    private static void AssertClosedButNativeSuccess(LegacyNativeItemRecordFile reader)
    {
        Assert.True(reader.Success); // Native Close retains the nonnull FileStream reference.
        int probe = 0;
        Assert.Throws<ObjectDisposedException>(() => reader.ReadCount(ref probe));
    }

    private static byte[] Image(int count, params byte[][] records)
    {
        var bytes = new byte[4 + records.Sum(record => record.Length)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, count);
        int offset = 4;
        foreach (var record in records)
        {
            record.CopyTo(bytes, offset);
            offset += record.Length;
        }
        return bytes;
    }

    private static byte[] Record(
        uint uid, uint level = 9, uint progress = 10, uint owner = 11,
        string name = "owner", string text = "tag")
    {
        // Known ASCII and NUL termination INSIDE each32-byte native buffer only.
        Assert.InRange(name.Length, 0, 31);
        Assert.InRange(text.Length, 0, 31);
        Assert.All(name, value => Assert.InRange((int)value, 0, 127));
        Assert.All(text, value => Assert.InRange((int)value, 0, 127));
        var bytes = new byte[80];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0), uid);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), level);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), progress);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), owner);
        Encoding.ASCII.GetBytes(name).CopyTo(bytes, 16);
        Encoding.ASCII.GetBytes(text).CopyTo(bytes, 48);
        return bytes;
    }

    private sealed class OwnedFile : IDisposable
    {
        private readonly string workspace;
        private readonly string directory;
        private readonly string filePath;
        private readonly List<LegacyNativeItemRecordFile> readers = [];
        private bool ownsDirectory;
        private bool ownsFile;

        internal OwnedFile(byte[] bytes)
        {
            Assert.True(OperatingSystem.IsWindows(), "Physical postlude controls require Windows.");
            workspace = FindWorkspace();
            directory = Path.Combine(workspace, ".artifacts",
                "native-item-perfection-postlude-tests", "owned-" + Guid.NewGuid().ToString("N"));
            filePath = Path.Combine(directory, "items.bin");
            Guard();
            Assert.False(Directory.Exists(directory));
            Directory.CreateDirectory(directory);
            ownsDirectory = true;
            try
            {
                Guard();
                using var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
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

        internal LegacyNativeItemRecordFile Open()
        {
            var reader = new LegacyNativeItemRecordFile();
            readers.Add(reader);
            Assert.True(reader.Open(filePath, FileMode.Open));
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
            if (ownsFile && File.Exists(filePath))
            {
                Assert.Equal(0, (int)(File.GetAttributes(filePath) & FileAttributes.ReparsePoint));
                File.Delete(filePath);
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
            var expectedParent = Path.GetFullPath(Path.Combine(workspace, ".artifacts",
                "native-item-perfection-postlude-tests"));
            Assert.Equal(expectedParent, Path.GetDirectoryName(Path.GetFullPath(directory)));
            Assert.Equal(directory, Path.GetDirectoryName(Path.GetFullPath(filePath)));
            Assert.Equal("items.bin", Path.GetFileName(filePath));
            Assert.Matches("^owned-[0-9a-f]{32}$", Path.GetFileName(directory));
            var prefix = workspace.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.StartsWith(prefix, Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase);
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
                if (File.Exists(Path.Combine(cursor.FullName, "WolfConquer.sln")) &&
                    File.Exists(Path.Combine(cursor.FullName, "src", "WolfConquer.Core", "WolfConquer.Core.csproj")))
                    return cursor.FullName;
            throw new InvalidOperationException("Owned postlude fixture requires the actual repository.");
        }
    }
}
