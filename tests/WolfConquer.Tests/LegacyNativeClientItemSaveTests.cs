using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Real native save composition on owned disposable files, not host/autosave wiring.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeClientItemSaveTests
{
    [Fact]
    public void SuccessfulSaveAtomicallyReplacesConfiguredPlayerPathAndCloses()
    {
        using var owned = new OwnedSave();
        owned.Seed(Enumerable.Repeat((byte)0xA5, 512).ToArray());
        var client = ClientWithNativeItemOwners();

        LegacyNativeClientItemSave.SaveClientItems(client);

        Assert.Equal(Image(0, [], []), File.ReadAllBytes(owned.FilePath));
        // Real staging/verification handles are all closed after the atomic result.
        using var exclusive = new FileStream(owned.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(8, exclusive.Length);
        Assert.Equal(1293600u, client.Player.UID);
    }

    [Fact]
    public void SaveUsesEveryRootInNativeOrderAndLiveBagValuesInsteadOfStaleCaches()
    {
        using var owned = new OwnedSave();
        var client = ClientWithNativeItemOwners();
        var inventory = Item(11, 1001000);
        var equipment = Item(12, 410003);
        var warehouse = Item(13, 1001000);
        var garment = Item(14, 181305, position: 9);
        var mount = Item(15, 200000, position: 17);
        var rune = Item(16, 1001000, position: 211);
        var relic = Item(17, 1001000);
        var gear = Item(18, 1001000);
        client.Inventory!.AddDBItem(inventory);
        client.Equipment!.ClientItems.TryAdd(equipment.Uid, equipment);
        Assert.True(client.Warehouse!.AddItem(warehouse, 44));
        Assert.True(client.MyWardrobe!.AddItem(garment));
        Assert.True(client.MyWardrobe.AddItem(mount));
        Assert.True(client.Rune!.Add(rune));
        var staleRelic = Item(117, 1001000);
        var staleGear = Item(118, 1001000);
        client.RelicFuse!.Add(99, staleRelic);
        client.GearMythsoulBag!.Add(99, staleGear);
        var relicCache = client.RelicFuse.GetValues();
        var gearCache = client.GearMythsoulBag.GetValues();
        client.RelicFuse[99] = relic;
        client.GearMythsoulBag[99] = gear;
        Assert.Equal(6, client.GetItemsCount()); // That native counter omits both bags.

        LegacyNativeClientItemSave.SaveClientItems(client);

        Assert.Equal(Image(8,
        [
            Db(11, 1001000), Db(12, 410003), Db(13, 1001000, warehouse: 44),
            Db(14, 181305, position: 9, warehouse: 100),
            Db(15, 200000, position: 17, warehouse: 100),
            Db(16, 1001000, position: 211), Db(17, 1001000), Db(18, 1001000)
        ], []), File.ReadAllBytes(owned.FilePath));
        Assert.Same(relicCache, client.RelicFuse.GetValues());
        Assert.Same(gearCache, client.GearMythsoulBag.GetValues());
        Assert.Same(staleRelic, Assert.Single(relicCache));
        Assert.Same(staleGear, Assert.Single(gearCache));
        Assert.Same(relic, client.RelicFuse[99]);
        Assert.Same(gear, client.GearMythsoulBag[99]);
    }

    [Fact]
    public void HeaderCountsOnlyRootsAndFlatDepositBytesKeepNativeOrder()
    {
        using var owned = new OwnedSave();
        var client = ClientWithNativeItemOwners();
        var parent = Item(21, 1001000);
        var child = Item(22, 410003, level: 7, owner: 0, name: "child", signature: "one-level");
        parent.Deposite.TryAdd(child.Uid, child);
        client.Inventory!.AddDBItem(parent);

        LegacyNativeClientItemSave.SaveClientItems(client);

        Assert.Equal(Image(1,
            [Db(21, 1001000, deposits: 1), Db(22, 410003)],
            [Perfection(22, 7, 0, 0, "child", "one-level")]), File.ReadAllBytes(owned.FilePath));
        Assert.Same(child, parent.Deposite[22]);
        Assert.Empty(child.Deposite);
    }

    [Fact]
    public void MainAndDepositedPerfectionPredicatesKeepTheirDifferentOwnerAndRootRules()
    {
        using var owned = new OwnedSave();
        var client = ClientWithNativeItemOwners();
        var inventory = Item(31, 410003, level: 1, owner: 0, name: "ownerless-main");
        var equipment = Item(32, 410004, progress: 1, owner: 99, name: "main", signature: "equip");
        var warehouse = Item(33, 1001000, level: 3, owner: 88, name: "not-equipment");
        var wardrobe = Item(34, 181305, level: 4, owner: 77, name: "wardrobe");
        var ownerlessChild = Item(41, 410005, level: 5, owner: 0, name: "child", signature: "owner-zero");
        var zeroChild = Item(42, 410006, owner: 66, name: "no-progress");
        var ordinaryChild = Item(43, 1001000, progress: 1, owner: 0, name: "not-equipment");
        inventory.Deposite.TryAdd(41, ownerlessChild);
        equipment.Deposite.TryAdd(42, zeroChild);
        warehouse.Deposite.TryAdd(43, ordinaryChild);
        client.Inventory!.AddDBItem(inventory);
        client.Equipment!.ClientItems.TryAdd(32, equipment);
        Assert.True(client.Warehouse!.AddItem(warehouse, 44));
        Assert.True(client.MyWardrobe!.AddItem(wardrobe));

        LegacyNativeClientItemSave.SaveClientItems(client);

        Assert.Equal(Image(4,
        [
            Db(31, 410003, deposits: 1), Db(41, 410005),
            Db(32, 410004, deposits: 1), Db(42, 410006),
            Db(33, 1001000, warehouse: 44, deposits: 1), Db(43, 1001000),
            Db(34, 181305, warehouse: 100)
        ],
        [
            Perfection(32, 0, 1, 99, "main", "equip"),
            Perfection(41, 5, 0, 0, "child", "owner-zero")
        ]), File.ReadAllBytes(owned.FilePath));
    }

    [Fact]
    public void DistinctMainItemsSharingUidRejectBeforeReplacingExistingBytes()
    {
        using var owned = new OwnedSave();
        byte[] sentinel = [1, 9, 3, 7]; owned.Seed(sentinel);
        var client = ClientWithNativeItemOwners();
        var first = Item(51, 410003, level: 5, owner: 1, name: "first");
        var second = Item(51, 410004, level: 6, owner: 2, name: "second");
        client.Inventory!.AddDBItem(first); client.Equipment!.ClientItems.TryAdd(51, second);
        var result = LegacyNativeClientItemSave.TrySaveClientItems(client);
        Assert.Equal(LegacyNativeClientItemSave.Outcome.Rejected, result.Status);
        Assert.Equal(sentinel, File.ReadAllBytes(owned.FilePath));
        Assert.Same(first, client.Inventory.ClientItems[51]); Assert.Same(second, client.Equipment.ClientItems[51]);
        using var exclusive = new FileStream(owned.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void HydratedExtrasSendThenSaveActualBytesAndFreshNativeReaderReloadsThem()
    {
        using var owned = new OwnedSave();
        var client = ClientWithNativeItemOwners();
        var added = new DateTime(2000, 1, 2, 3, 4, 5);
        var utc = DateTime.SpecifyKind(added, DateTimeKind.Utc);
        var raw = new LegacyNativeClientItemRecord
        {
            UID = 61, ITEM_ID = 1001000, Activate = 2, TimeLeftInMinutes = 77,
            PurificationItemID = 800001, PurificationLevel = 7,
            PurificationAddedOn = added.Ticks,
            EffectID = 724440, EffectLevel = 9, EffectPercent = 11,
            EffectPercent2 = 13, EffectAddedOn = utc.ToBinary()
        };
        var item = raw.GetDataItem();
        var expectedFrame = Frame(
            2, 61, 8, 800001, 7, 0, 0, 0, 0,
            61, 3, 301, 9, 11, 13, 0);
        Assert.Equal(expectedFrame, item.CreateItemExtraFrame());
        client.Inventory!.AddDBItem(item);
        var expectedRecord = Db(61, 1001000);
        Put32(expectedRecord, 44, 2);
        Put32(expectedRecord, 84, 800001);
        Put32(expectedRecord, 88, 7);
        Put64(expectedRecord, 96, added.Ticks);
        Put32(expectedRecord, 104, 724440);
        Put32(expectedRecord, 108, 9);
        Put32(expectedRecord, 112, 11);
        Put32(expectedRecord, 116, 13);
        Put64(expectedRecord, 128, utc.Ticks); // Native writer stores Ticks, not ToBinary.

        LegacyNativeClientItemSave.SaveClientItems(client);

        Assert.Equal(Image(1, [expectedRecord], []), File.ReadAllBytes(owned.FilePath));
        Assert.Same(item, client.Inventory.ClientItems[61]);
        Assert.Equal(77u, raw.TimeLeftInMinutes);
        var reader = owned.Open(FileMode.Open);
        int count = -1;
        Assert.True(reader.ReadCount(ref count));
        Assert.Equal(1, count);
        var saved = default(LegacyNativeClientItemRecord);
        Assert.True(reader.ReadRecord(ref saved));
        Assert.Equal(0u, saved.TimeLeftInMinutes);
        Assert.Equal(utc.Ticks, saved.EffectAddedOn);
        Assert.True(reader.ReadCount(ref count));
        Assert.Equal(0, count);
        var loaded = saved.GetDataItem();
        Assert.NotSame(item, loaded);
        Assert.Equal(61u, loaded.Purification.ItemUID);
        Assert.Equal(61u, loaded.Refinary.ItemUID);
        Assert.Equal(DateTimeKind.Unspecified, loaded.Refinary.AddedOn.Kind);
        Assert.Equal(expectedFrame, loaded.CreateItemExtraFrame());
        reader.Close();
    }

    [Fact]
    public void MissingDirectoryOpenFailureReturnsBeforeAllocatingAnyItemOwners()
    {
        using var owned = new OwnedSave(createPlayersItems: false);
        var client = new LegacyNativeGameClientRuntime();
        client.Player.UID = 1293600;

        var result = LegacyNativeClientItemSave.TrySaveClientItems(client);
        Assert.Equal(LegacyNativeClientItemSave.Outcome.Rejected, result.Status);

        Assert.False(Directory.Exists(owned.PlayersItemsDirectory));
        Assert.False(File.Exists(owned.FilePath));
        AssertNoItemOwners(client);
    }

    [Fact]
    public void SharingDeniedOpenFailureLeavesExistingBytesAndNullOwnersUntouched()
    {
        using var owned = new OwnedSave();
        byte[] sentinel = [0x11, 0x22, 0x33, 0x44, 0x55];
        owned.Seed(sentinel);
        var client = new LegacyNativeGameClientRuntime();
        client.Player.UID = 1293600;
        using (var locked = new FileStream(owned.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var result = LegacyNativeClientItemSave.TrySaveClientItems(client);
            Assert.Equal(LegacyNativeClientItemSave.Outcome.Rejected, result.Status);
            Assert.Equal(sentinel.Length, locked.Length);
            AssertNoItemOwners(client);
        }
        Assert.Equal(sentinel, File.ReadAllBytes(owned.FilePath));
    }

    [Fact]
    public void MissingLaterOwnerRejectsWithoutTruncatingExistingDestinationOrLeakingAHandle()
    {
        using var owned = new OwnedSave(); byte[] sentinel = [7, 1, 7, 1]; owned.Seed(sentinel);
        var client = new LegacyNativeGameClientRuntime(); client.Player.UID = 1293600;
        client.Inventory = new LegacyNativeInventoryRuntime(client); client.Inventory.AddDBItem(Item(71, 1001000));
        var result = LegacyNativeClientItemSave.TrySaveClientItems(client);
        Assert.Equal(LegacyNativeClientItemSave.Outcome.Rejected, result.Status);
        Assert.Equal(sentinel, File.ReadAllBytes(owned.FilePath)); Assert.Null(client.Equipment);
        using var exclusive = new FileStream(owned.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void MalformedLaterRowRejectsWithoutPersistingEarlierPreparedPrefix()
    {
        using var owned = new OwnedSave(); byte[] sentinel = [8, 1, 8, 2]; owned.Seed(sentinel);
        var client = ClientWithNativeItemOwners(); var first = Item(81, 1001000); var broken = Item(82, 410003);
        broken.RelicAttributes = []; client.Inventory!.AddDBItem(first); client.Equipment!.ClientItems.TryAdd(82, broken);
        var result = LegacyNativeClientItemSave.TrySaveClientItems(client);
        Assert.Equal(LegacyNativeClientItemSave.Outcome.Rejected, result.Status);
        Assert.Equal(sentinel, File.ReadAllBytes(owned.FilePath));
        Assert.Same(first, client.Inventory.ClientItems[81]); Assert.Same(broken, client.Equipment.ClientItems[82]);
        Assert.Empty(broken.RelicAttributes);
        using var exclusive = new FileStream(owned.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    private static LegacyNativeGameClientRuntime ClientWithNativeItemOwners()
    {
        var client = new LegacyNativeGameClientRuntime();
        client.Player.UID = 1293600;
        LegacyNativeItemContainerMemberPhases.InitializeGearMythsoulBagAtNativeFieldSlot(client);
        LegacyNativeItemContainerMemberPhases.InitializeRelicFuseAtNativeFieldSlot(client);
        LegacyNativeItemContainerMemberPhases.AssignWardrobeAtNativeConstructorSlot(client);
        LegacyNativeItemContainerMemberPhases.AssignLoadClientItemOwnersPrefix(client.Player);
        return client;
    }

    private static LegacyNativeGameItemRuntime Item(
        uint uid, uint id, ushort position = 0, uint level = 0, uint progress = 0,
        uint owner = 0, string name = "", string signature = "") => new()
    {
        Uid = uid, ITEM_ID = id, Position = position, PerfectionLevel = level,
        PerfectionProgress = progress, OwnerUID = owner, OwnerName = name, Signature = signature
    };

    private static void AssertNoItemOwners(LegacyNativeGameClientRuntime client)
    {
        Assert.Null(client.Inventory);
        Assert.Null(client.Equipment);
        Assert.Null(client.Warehouse);
        Assert.Null(client.MyWardrobe);
        Assert.Null(client.Rune);
        Assert.Null(client.RelicFuse);
        Assert.Null(client.GearMythsoulBag);
    }

    // Explicit 184-byte native DB layout; no production record conversion/serializer oracle.
    private static byte[] Db(uint uid, uint id, ushort position = 0, uint warehouse = 0, uint deposits = 0)
    {
        var bytes = new byte[184];
        Put32(bytes, 0, uid);
        Put32(bytes, 4, id);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), position);
        Put32(bytes, 56, warehouse);
        Put32(bytes, 136, deposits);
        return bytes;
    }

    private static byte[] Perfection(uint uid, uint level, uint progress, uint owner, string name, string signature = "")
    {
        Assert.True(name.Length <= 32 && signature.Length <= 32);
        var bytes = new byte[80];
        Put32(bytes, 0, uid);
        Put32(bytes, 4, level);
        Put32(bytes, 8, progress);
        Put32(bytes, 12, owner);
        Encoding.ASCII.GetBytes(name).CopyTo(bytes, 16);
        Encoding.ASCII.GetBytes(signature).CopyTo(bytes, 48);
        return bytes;
    }

    private static byte[] Image(int roots, IReadOnlyList<byte[]> records, IReadOnlyList<byte[]> perfections) =>
        Int32(roots).Concat(records.SelectMany(bytes => bytes)).Concat(Int32(perfections.Count))
            .Concat(perfections.SelectMany(bytes => bytes)).ToArray();

    private static byte[] Int32(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] Frame(params uint[] words)
    {
        var bytes = new byte[4 + words.Length * 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)bytes.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2134);
        for (int index = 0; index < words.Length; index++) Put32(bytes, 4 + index * 4, words[index]);
        return bytes;
    }

    private static void Put32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);

    private static void Put64(byte[] bytes, int offset, long value) =>
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(offset), value);

    private static byte[] ReadWhileWriterOpen(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private sealed class OwnedSave : IDisposable
    {
        private readonly string workspace;
        private readonly string directory;
        private readonly string priorDbLocation;
        private readonly List<LegacyNativeItemRecordFile> files = [];
        private readonly LegacyNativeSafeRandom priorRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime priorReset = LegacyNativePoolRandom.ResetRandom;
        internal string PlayersItemsDirectory { get; }
        internal string FilePath { get; }

        internal OwnedSave(bool createPlayersItems = true)
        {
            Assert.True(OperatingSystem.IsWindows(), "Native item save tests require Windows Kernel32 IO.");
            workspace = FindWorkspace();
            directory = Path.Combine(workspace, ".artifacts", "native-client-item-save-tests", "owned-" + Guid.NewGuid().ToString("N"));
            PlayersItemsDirectory = Path.Combine(directory, "PlayersItems");
            FilePath = Path.Combine(PlayersItemsDirectory, "1293600.bin");
            Guard();
            Assert.False(Directory.Exists(directory));
            Directory.CreateDirectory(directory);
            if (createPlayersItems) Directory.CreateDirectory(PlayersItemsDirectory);
            priorDbLocation = LegacyNativeServerConfig.DbLocation;
            LegacyNativeServerConfig.DbLocation = directory;
        }

        internal void Seed(byte[] bytes)
        {
            Guard();
            using var stream = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
        }

        internal LegacyNativeItemRecordFile Open(FileMode mode)
        {
            Guard();
            var file = new LegacyNativeItemRecordFile();
            files.Add(file);
            Assert.True(file.Open(FilePath, mode));
            return file;
        }

        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation = priorDbLocation;
            LegacyNativePoolRandom.GetRandom = priorRandom;
            LegacyNativePoolRandom.ResetRandom = priorReset;
            foreach (var file in files)
                if (file.Success) file.Close();
            Guard();
            if (File.Exists(FilePath))
            {
                Assert.Equal(0, (int)(File.GetAttributes(FilePath) & FileAttributes.ReparsePoint));
                File.Delete(FilePath); // Only this fixture's exact generated leaf.
            }
            if (Directory.Exists(PlayersItemsDirectory)) Directory.Delete(PlayersItemsDirectory, recursive: false);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: false);
        }

        private void Guard()
        {
            var parent = Path.GetFullPath(Path.Combine(workspace, ".artifacts", "native-client-item-save-tests"));
            Assert.Equal(parent, Path.GetDirectoryName(Path.GetFullPath(directory)));
            Assert.Matches("^owned-[0-9a-f]{32}$", Path.GetFileName(directory));
            Assert.Equal(directory, Path.GetDirectoryName(Path.GetFullPath(PlayersItemsDirectory)));
            Assert.Equal(PlayersItemsDirectory, Path.GetDirectoryName(Path.GetFullPath(FilePath)));
            Assert.Equal("1293600.bin", Path.GetFileName(FilePath));
            var prefix = workspace.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.True(Path.GetFullPath(directory).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            for (var cursor = new DirectoryInfo(PlayersItemsDirectory); cursor is not null; cursor = cursor.Parent)
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
            throw new InvalidOperationException("Owned native save controls require the workspace test build output.");
        }
    }
}
