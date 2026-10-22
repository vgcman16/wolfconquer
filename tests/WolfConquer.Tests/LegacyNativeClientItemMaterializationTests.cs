using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeClientItemMaterializationTests
{
    [Fact]
    public void ClosedReaderFaultsBeforeDemonOrSanitation()
    {
        var record = Record(7, 750000);
        record.Plus = 255;
        Assert.Throws<IOException>(() => LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(
            new LegacyNativeItemRecordFile(), null!, ref record, null!));
        Assert.Equal(7U, record.UID);
        Assert.Equal((byte)255, record.Plus);
    }

    [Fact]
    public void NullReaderFaultsBeforeAccessingClientOrDictionary()
    {
        var record = Record(7, 750000);
        Assert.Throws<NullReferenceException>(() => LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(
            null!, null!, ref record, null!));
        Assert.Equal(7U, record.UID);
    }

    [Fact]
    public void WindowsOuterOnlyUsesSanitationAndDoesNotTouchUnusedOwners()
    {
        var outer = Record(7, 203009);
        outer.Plus = 255; outer.Bless = 9; outer.Enchant = 200;
        using var file = new OwnedFile(Bytes(outer));
        var record = default(LegacyNativeClientItemRecord);
        var item = LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(file.Reader, null!, ref record, null!)!;
        Assert.Equal(7U, item.Uid);
        Assert.Equal(203009U, item.ITEM_ID);
        Assert.Equal((byte)12, item.Plus);
        Assert.Equal((byte)0, item.Bless);
        Assert.Equal((byte)0, item.Enchant);
        Assert.Empty(item.Deposite);
        Assert.Equal(item.Plus, record.Plus);
    }

    [Fact]
    public void WindowsOuterDemonWritesUidNotItemId()
    {
        using var file = new OwnedFile(Bytes(Record(7, 750000)));
        var client = Client(); var record = default(LegacyNativeClientItemRecord);
        var item = LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(file.Reader, client, ref record, null!)!;
        Assert.Equal(7U, client.DemonExterminator!.ItemUID);
        Assert.Equal(7U, item.Uid);
        Assert.Null(client.Inventory);
        Assert.Null(client.Equipment);
    }

    [Fact]
    public void WindowsMissingDemonFaultsAfterReadWithoutClosingReader()
    {
        using var file = new OwnedFile(Bytes(Record(7, 750000)).Concat(BitConverter.GetBytes(93)).ToArray());
        var record = default(LegacyNativeClientItemRecord);
        Assert.Throws<NullReferenceException>(() => LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(
            file.Reader, new LegacyNativeGameClientRuntime(), ref record, null!));
        Assert.Equal(7U, record.UID);
        int marker = 0;
        Assert.True(file.Reader.ReadCount(ref marker));
        Assert.Equal(93, marker);
    }

    [Fact]
    public void Windows205901SkipDoesNotSanitizeConvertOrConsumeChildren()
    {
        var outer = Record(7, 205901, deposits: 1);
        outer.Plus = 255; outer.Bless = 9;
        using var file = new OwnedFile(Bytes(outer).Concat(Bytes(Record(8))).ToArray());
        var record = default(LegacyNativeClientItemRecord);
        Assert.Null(LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(file.Reader, null!, ref record, null!));
        Assert.Equal((byte)255, record.Plus);
        Assert.Equal((byte)9, record.Bless);
        Assert.True(file.Reader.ReadRecord(ref record));
        Assert.Equal(8U, record.UID);
    }

    [Fact]
    public void WindowsChildDemonUsesItemIdAndFinalChildRecordEscapesForRouting()
    {
        var outer = Record(50, deposits: 1); outer.WH_ID = 7;
        var child = Record(77, 750000); child.WH_ID = 100; child.Position = 42; child.Inscribed = 9;
        using var file = new OwnedFile(Bytes(outer).Concat(Bytes(child)).ToArray());
        var client = Client(); var record = default(LegacyNativeClientItemRecord);
        var deposited = new Dictionary<uint, LegacyNativeGameItemRuntime>();
        var item = LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(file.Reader, client, ref record, deposited)!;
        Assert.Equal(50U, item.Uid);
        Assert.Equal(7U, item.WH_ID);
        Assert.Equal(77U, record.UID);
        Assert.Equal(100U, record.WH_ID);
        Assert.Equal((ushort)42, record.Position);
        Assert.Equal(750000U, client.DemonExterminator!.ItemUID);
        Assert.Same(item.Deposite[77], deposited[77]);
        Assert.Equal((ushort)0, deposited[77].Inscribed);
    }

    [Fact]
    public void WindowsChildrenAreNeitherSanitizedNorFilteredAndCountIsCapturedOnce()
    {
        var first = Record(7, 203009); first.Plus = 255; first.Bless = 9; first.Enchant = 200;
        var second = Record(8, 205901);
        using var file = new OwnedFile(Bytes(Record(50, deposits: 2)).Concat(Bytes(first)).Concat(Bytes(second))
            .Concat(BitConverter.GetBytes(93)).ToArray());
        var record = default(LegacyNativeClientItemRecord);
        var item = LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(file.Reader, Client(), ref record, new())!;
        Assert.Equal(2, item.Deposite.Count); // First child's DepositeCount=0 must not shorten the outer count.
        Assert.Equal((byte)255, item.Deposite[7].Plus);
        Assert.Equal((byte)9, item.Deposite[7].Bless);
        Assert.Equal((byte)200, item.Deposite[7].Enchant);
        Assert.Equal(205901U, item.Deposite[8].ITEM_ID);
        int marker = 0;
        Assert.True(file.Reader.ReadCount(ref marker));
        Assert.Equal(93, marker);
    }

    [Fact]
    public void WindowsDuplicateChildUidRetainsFirstChildAndExistingGlobalEntry()
    {
        var first = Record(7); first.Durability = 11;
        var second = Record(7); second.Durability = 22;
        using var file = new OwnedFile(Bytes(Record(50, deposits: 2)).Concat(Bytes(first)).Concat(Bytes(second)).ToArray());
        var existing = new LegacyNativeGameItemRuntime { Uid = 7, Durability = 33 };
        var deposited = new Dictionary<uint, LegacyNativeGameItemRuntime> { [7] = existing };
        var record = default(LegacyNativeClientItemRecord);
        var item = LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(file.Reader, Client(), ref record, deposited)!;
        Assert.Single(item.Deposite);
        Assert.Equal((ushort)11, item.Deposite[7].Durability);
        Assert.Same(existing, deposited[7]);
        Assert.NotSame(existing, item.Deposite[7]);
        Assert.Equal((ushort)22, record.Durability);
    }

    [Fact]
    public void WindowsSameDepositedMapKeepsFirstReferenceAcrossTwoOuterCalls()
    {
        using var file = new OwnedFile(Bytes(Record(50, deposits: 1)).Concat(Bytes(Record(7)))
            .Concat(Bytes(Record(60, deposits: 1))).Concat(Bytes(Record(7))).ToArray());
        var deposited = new Dictionary<uint, LegacyNativeGameItemRuntime>();
        var client = Client(); var record = default(LegacyNativeClientItemRecord);
        var first = LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(file.Reader, client, ref record, deposited)!;
        var second = LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(file.Reader, client, ref record, deposited)!;
        Assert.Same(first.Deposite[7], deposited[7]);
        Assert.NotSame(first.Deposite[7], second.Deposite[7]);
        Assert.Equal(60U, second.Uid);
    }

    [Fact]
    public void WindowsNonzeroGuildPreservesChildInscription()
    {
        var child = Record(7); child.Inscribed = 9;
        using var file = new OwnedFile(Bytes(Record(50, deposits: 1)).Concat(Bytes(child)).ToArray());
        var client = Client(); client.Player.GuildID = 1;
        var record = default(LegacyNativeClientItemRecord);
        var item = LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(file.Reader, client, ref record, new())!;
        Assert.Equal((ushort)9, item.Deposite[7].Inscribed);
    }

    [Fact]
    public void WindowsChildConversionFaultOccursAfterDemonWriteBeforeGlobalInsertion()
    {
        var child = Record(7, 750000); child.EffectAddedOn = long.MaxValue;
        using var file = new OwnedFile(Bytes(Record(50, deposits: 1)).Concat(Bytes(child)).ToArray());
        var client = Client(); var record = default(LegacyNativeClientItemRecord);
        var deposited = new Dictionary<uint, LegacyNativeGameItemRuntime>();
        Assert.Throws<ArgumentException>(() => LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(
            file.Reader, client, ref record, deposited));
        Assert.Equal(750000U, client.DemonExterminator!.ItemUID);
        Assert.Empty(deposited);
        Assert.Equal(7U, record.UID);
        Assert.True(file.Reader.Success);
    }

    [Fact]
    public void WindowsOuterConversionFaultLeavesChildUnread()
    {
        var outer = Record(50, deposits: 1); outer.EffectAddedOn = long.MaxValue;
        using var file = new OwnedFile(Bytes(outer).Concat(Bytes(Record(7))).ToArray());
        var record = default(LegacyNativeClientItemRecord);
        Assert.Throws<ArgumentException>(() => LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(
            file.Reader, null!, ref record, null!));
        Assert.True(file.Reader.ReadRecord(ref record));
        Assert.Equal(7U, record.UID);
    }

    [Fact]
    public void WindowsShortChildReadRetainsSanitizedOuterTailAndIgnoresFalseRead()
    {
        var outer = Record(50, 203009, deposits: 1); outer.Plus = 255; outer.Bless = 9; outer.WH_ID = 7;
        using var file = new OwnedFile(Bytes(outer).Concat(BitConverter.GetBytes(99U)).ToArray());
        var record = default(LegacyNativeClientItemRecord);
        var deposited = new Dictionary<uint, LegacyNativeGameItemRuntime>();
        var item = LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(file.Reader, Client(), ref record, deposited)!;
        Assert.Equal(50U, item.Uid);
        Assert.Equal(99U, record.UID);
        Assert.Equal(203009U, record.ITEM_ID);
        Assert.Equal(7U, record.WH_ID);
        Assert.Equal((byte)12, deposited[99].Plus);
        Assert.Equal((byte)0, deposited[99].Bless);
        Assert.Same(item.Deposite[99], deposited[99]);
    }

    [Fact]
    public void WindowsEofRetainsLastChildForTheNextOuterCall()
    {
        using var file = new OwnedFile(Bytes(Record(50, deposits: 1)).Concat(Bytes(Record(7))).ToArray());
        var client = Client(); var record = default(LegacyNativeClientItemRecord);
        var deposited = new Dictionary<uint, LegacyNativeGameItemRuntime>();
        var first = LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(file.Reader, client, ref record, deposited)!;
        var next = LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(file.Reader, null!, ref record, null!)!;
        Assert.Equal(7U, next.Uid);
        Assert.Empty(next.Deposite);
        Assert.NotSame(first.Deposite[7], next);
        Assert.Same(first.Deposite[7], deposited[7]);
    }

    [Fact]
    public void WindowsPhaseLeavesFollowingCountAndCloseToItsCaller()
    {
        using var file = new OwnedFile(Bytes(Record(7)).Concat(BitConverter.GetBytes(93)).ToArray());
        var record = default(LegacyNativeClientItemRecord);
        _ = LegacyNativeClientItemMaterialization.ReadOuterAndDeposits(file.Reader, null!, ref record, null!);
        int count = 0;
        Assert.True(file.Reader.ReadCount(ref count));
        Assert.Equal(93, count);
        Assert.True(file.Reader.Success);
    }

    private static LegacyNativeClientItemRecord Record(uint uid, uint itemId = 410009, uint deposits = 0) =>
        new() { UID = uid, ITEM_ID = itemId, DepositeCount = deposits };

    private static LegacyNativeGameClientRuntime Client()
    {
        var client = new LegacyNativeGameClientRuntime();
        LegacyNativeItemContainerMemberPhases.AssignDemonExterminatorAtNativeConstructorSlot(client);
        return client;
    }

    private static byte[] Bytes(LegacyNativeClientItemRecord record) =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref record, 1)).ToArray();

    private sealed class OwnedFile : IDisposable
    {
        private readonly string workspace;
        private readonly string directory;
        private readonly string file;
        private bool ownsDirectory;
        private bool ownsFile;
        internal LegacyNativeItemRecordFile Reader { get; } = new();

        internal OwnedFile(byte[] bytes)
        {
            Assert.True(OperatingSystem.IsWindows(), "Windows is required for physical Kernel32 controls.");
            workspace = FindWorkspace();
            directory = Path.Combine(workspace, ".artifacts", "native-item-materialization-tests", "owned-" + Guid.NewGuid().ToString("N"));
            file = Path.Combine(directory, "items.bin");
            Guard(); Assert.False(Directory.Exists(directory));
            Directory.CreateDirectory(directory); ownsDirectory = true;
            try
            {
                Guard();
                using (var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    ownsFile = true; stream.Write(bytes); stream.Flush(flushToDisk: true);
                }
                Assert.True(Reader.Open(file, FileMode.Open));
            }
            catch { RemoveOwnedLeaves(); throw; }
        }

        public void Dispose()
        {
            if (Reader.Success) Reader.Close();
            RemoveOwnedLeaves();
        }

        private void RemoveOwnedLeaves()
        {
            Guard();
            if (ownsFile && File.Exists(file))
            {
                Assert.Equal(0, (int)(File.GetAttributes(file) & FileAttributes.ReparsePoint));
                File.Delete(file); ownsFile = false;
            }
            if (ownsDirectory && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: false); ownsDirectory = false;
            }
        }

        private void Guard()
        {
            var parent = Path.GetFullPath(Path.Combine(workspace, ".artifacts", "native-item-materialization-tests"));
            Assert.Equal(parent, Path.GetDirectoryName(Path.GetFullPath(directory)));
            Assert.Equal(directory, Path.GetDirectoryName(Path.GetFullPath(file)));
            Assert.Equal("items.bin", Path.GetFileName(file));
            Assert.Matches("^owned-[0-9a-f]{32}$", Path.GetFileName(directory));
            for (var cursor = new DirectoryInfo(directory); cursor is not null; cursor = cursor.Parent)
            {
                if (cursor.Exists) Assert.Equal(0, (int)(cursor.Attributes & FileAttributes.ReparsePoint));
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
