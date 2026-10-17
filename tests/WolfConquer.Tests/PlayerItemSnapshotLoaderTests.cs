using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class PlayerItemSnapshotLoaderTests : IDisposable
{
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolf-player-items-" + Guid.NewGuid());

    [Fact]
    public void PlayerItemSnapshotLoaderClassifiesStartupVisibleItems()
    {
        Directory.CreateDirectory(_root);
        var bytes = BuildPlayerItems(
        [
            DbItem(uid: 1001, itemId: 3324226, position: 0, stackSize: 1, color: 6, locked: 2, unlockTimer: 20991231),
            DbItem(uid: 1002, itemId: 500109, position: 4, plus: 12, color: 5, purificationItemId: 800001, purificationLevel: 3, refineryEffectId: 724440, refineryEffectLevel: 2, refineryEffectPercent: 10, refineryEffectPercent2: 20),
            DbItem(uid: 1003, itemId: 300001, position: 101, runeExperience: 1234),
            DbItem(uid: 1004, itemId: 1088001, position: 0, warehouseId: 6),
            DbItem(uid: 1005, itemId: 4100001, position: 217),
            DbItem(uid: 1006, itemId: 120269, position: 7, warehouseId: 100),
            DbItem(uid: 1007, itemId: 200614, position: 0, warehouseId: 100, maximumDurability: 250, plus: 5),
        ],
        [
            Perfection(itemUid: 1002, level: 9, progress: 123, ownerUid: 1_293_600, ownerName: "HeroOne", signature: "ready")
        ]);

        var snapshot = PlayerItemSnapshotLoader.Parse(bytes, "1293600.bin");

        Assert.Single(snapshot.InventoryItems);
        Assert.Single(snapshot.MythsoulBagItems);
        Assert.Empty(snapshot.RelicFuseItems);
        Assert.Equal([1002U, 1006U], snapshot.EquipmentItems.Select(item => item.Uid).ToArray());
        Assert.Single(snapshot.RuneItems);
        Assert.Equal([1004U], snapshot.WarehouseItems.Select(item => item.Uid).ToArray());
        Assert.Equal(6U, snapshot.WarehouseItems[0].WarehouseId);
        var wardrobeItem = Assert.Single(snapshot.WardrobeItems);
        Assert.Equal(1007U, wardrobeItem.Uid);
        Assert.Equal(200614U, wardrobeItem.ItemId);
        Assert.Equal((ushort)0, wardrobeItem.Position);
        Assert.Equal(100U, wardrobeItem.WarehouseId);
        Assert.Equal((ushort)250, wardrobeItem.MaximumDurability);
        Assert.Equal((byte)5, wardrobeItem.Plus);
        Assert.Equal(1001U, snapshot.InventoryItems[0].Uid);
        Assert.Equal((byte)2, snapshot.InventoryItems[0].Locked);
        Assert.Equal(20991231, snapshot.InventoryItems[0].UnlockTimer);
        Assert.Equal(1003U, snapshot.RuneItems[0].Uid);
        Assert.Equal((uint)9, snapshot.EquipmentItems[0].PerfectionLevel);
        Assert.Equal((uint)123, snapshot.EquipmentItems[0].PerfectionProgress);
        Assert.Equal((uint)1_293_600, snapshot.EquipmentItems[0].OwnerUid);
        Assert.Equal("HeroOne", snapshot.EquipmentItems[0].OwnerName);
        Assert.Equal("ready", snapshot.EquipmentItems[0].Signature);
        Assert.Equal(800001U, snapshot.EquipmentItems[0].PurificationItemId);
        Assert.Equal(3U, snapshot.EquipmentItems[0].PurificationLevel);
        Assert.Equal(724440U, snapshot.EquipmentItems[0].RefineryEffectId);
        Assert.Equal(2U, snapshot.EquipmentItems[0].RefineryEffectLevel);
        Assert.Equal(10U, snapshot.EquipmentItems[0].RefineryEffectPercent);
        Assert.Equal(20U, snapshot.EquipmentItems[0].RefineryEffectPercent2);
    }

    [Fact]
    public void PlayerItemSnapshotLoaderKeepsRuneBagItemsInLegacyFileOrder()
    {
        var bytes = BuildPlayerItems(
        [
            DbItem(uid: 3003, itemId: 4030309, position: 103),
            DbItem(uid: 3001, itemId: 4030109, position: 101),
            DbItem(uid: 4100, itemId: 4040001, position: 211),
            DbItem(uid: 4099, itemId: 4050001, position: 211),
            DbItem(uid: 4200, itemId: 4010801, position: 211),
            DbItem(uid: 4101, itemId: 4060001, position: 211),
        ],
        []);

        var snapshot = PlayerItemSnapshotLoader.Parse(bytes, "1293600.bin");

        Assert.Equal(
            [3001U, 3003U, 4100U, 4099U, 4200U, 4101U],
            snapshot.RuneItems.Select(item => item.Uid).ToArray());
    }

    [Fact]
    public void PlayerItemSnapshotLoaderKeepsWardrobeItemsInLegacyFileOrder()
    {
        var bytes = BuildPlayerItems(
        [
            DbItem(uid: 5001, itemId: 200743, position: 0, warehouseId: 100),
            DbItem(uid: 5002, itemId: 200765, position: 0, warehouseId: 100),
            DbItem(uid: 5003, itemId: 200614, position: 0, warehouseId: 100),
            DbItem(uid: 5004, itemId: 205003, position: 0, warehouseId: 100),
            DbItem(uid: 5005, itemId: 200629, position: 0, warehouseId: 100),
        ],
        []);

        var snapshot = PlayerItemSnapshotLoader.Parse(bytes, "1293600.bin");

        Assert.Equal(
            [5001U, 5002U, 5003U, 5004U, 5005U],
            snapshot.WardrobeItems.Select(item => item.Uid).ToArray());
    }

    [Fact]
    public void PlayerItemSnapshotLoaderKeepsFirstVisibleDuplicateUidLikeLegacyTryAdd()
    {
        var bytes = BuildPlayerItems(
        [
            DbItem(uid: 1001, itemId: 195485, position: 9, warehouseId: 100, maximumDurability: 100),
            DbItem(uid: 1001, itemId: 195486, position: 10, warehouseId: 100, maximumDurability: 200),
        ],
        [
            Perfection(itemUid: 1001, level: 7, progress: 456, ownerUid: 1_293_600, ownerName: "HeroOne", signature: "first")
        ]);

        var snapshot = PlayerItemSnapshotLoader.Parse(bytes, "1293600.bin");

        var item = Assert.Single(snapshot.EquipmentItems);
        Assert.Equal(195485U, item.ItemId);
        Assert.Equal((ushort)9, item.Position);
        Assert.Equal((ushort)100, item.MaximumDurability);
        Assert.Equal(7U, item.PerfectionLevel);
        Assert.Equal(456U, item.PerfectionProgress);
    }

    [Fact]
    public void PlayerItemSnapshotLoaderPreservesNestedDepositedItemsUnderInventorySash()
    {
        var bytes = BuildPlayerItems(
        [
            DbItem(
                uid: 2001,
                itemId: 1_100_009,
                position: 0,
                depositedItems:
                [
                    DbItem(uid: 3001, itemId: 1088001, position: 0, stackSize: 3),
                ]),
        ],
        []);

        var snapshot = PlayerItemSnapshotLoader.Parse(bytes, "1293600.bin");

        var sash = Assert.Single(snapshot.InventoryItems);
        Assert.Equal(2001U, sash.Uid);
        Assert.Equal(1_100_009U, sash.ItemId);
        var deposited = Assert.Single(sash.DepositedItems);
        Assert.Equal(3001U, deposited.Uid);
        Assert.Equal(1088001U, deposited.ItemId);
        Assert.Equal((ushort)3, deposited.StackSize);
        Assert.DoesNotContain(snapshot.InventoryItems, item => item.Uid == 3001);
        Assert.Empty(snapshot.WarehouseItems);
    }

    [Fact]
    public void CharacterSnapshotLoaderDerivesLegacyPlayerItemsSiblingRoot()
    {
        var usersRoot = Path.Combine(_root, "Users");
        var itemsRoot = Path.Combine(_root, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, "1293600.bin"),
            BuildPlayerItems([DbItem(uid: 1001, itemId: 3324226, position: 0)], []));

        var loader = new CharacterSnapshotLoader(usersRoot);
        var snapshot = loader.LoadPlayerItemsByCharacterUid(1_293_600);

        Assert.Equal(itemsRoot, loader.PlayerItemsRoot);
        Assert.Single(snapshot.InventoryItems);
    }

    [Fact]
    public void PlayerItemSnapshotLoaderSaveByCharacterUidCreatesRoundTrippableLegacyFile()
    {
        var loader = new PlayerItemSnapshotLoader(_root);
        var snapshot = PlayerItemSnapshotLoader.Parse(
            BuildPlayerItems(
            [
                DbItem(
                    uid: 2001,
                    itemId: 725016,
                    position: 0,
                    stackSize: 2,
                    depositedItems:
                    [
                        DbItem(uid: 3001, itemId: 1088001, position: 0, stackSize: 3),
                    ]),
                DbItem(uid: 2002, itemId: 500109, position: 4, plus: 12, purificationItemId: 800001),
            ],
            [
                Perfection(itemUid: 2002, level: 7, progress: 123, ownerUid: 1_293_600, ownerName: "HeroOne", signature: "ready")
            ]));

        loader.SaveByCharacterUid(CharacterUid, snapshot);

        var reloaded = loader.LoadByCharacterUid(CharacterUid);
        var inventory = Assert.Single(reloaded.InventoryItems);
        Assert.Equal(2001U, inventory.Uid);
        Assert.Equal(725016U, inventory.ItemId);
        Assert.Equal((ushort)2, inventory.StackSize);
        Assert.Equal(3001U, Assert.Single(inventory.DepositedItems).Uid);
        var equipment = Assert.Single(reloaded.EquipmentItems);
        Assert.Equal(2002U, equipment.Uid);
        Assert.Equal((byte)12, equipment.Plus);
        Assert.Equal(800001U, equipment.PurificationItemId);
        Assert.Equal(7U, equipment.PerfectionLevel);
        Assert.Equal("HeroOne", equipment.OwnerName);
        Assert.Equal("ready", equipment.Signature);
    }

    [Fact]
    public async Task PlayerItemSnapshotLoaderLoadAndSaveSerializeWithEquivalentPathLock()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, CharacterUid + ".bin");
        var equivalentPath = Path.Combine(_root, ".", CharacterUid + ".bin");
        var original = BuildPlayerItems([DbItem(uid: 1001, itemId: 3324226, position: 0)], []);
        File.WriteAllBytes(path, original);
        var loader = new PlayerItemSnapshotLoader(_root);
        var replacement = PlayerItemSnapshotLoader.Parse(
            BuildPlayerItems([DbItem(uid: 1002, itemId: 1088001, position: 0, stackSize: 3)], []));

        await AssertWaitsForFileLock(
            equivalentPath,
            () => Assert.Single(loader.LoadByCharacterUid(CharacterUid).InventoryItems));
        await AssertWaitsForFileLock(
            equivalentPath,
            () => loader.SaveByCharacterUid(CharacterUid, replacement));

        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(replacement), File.ReadAllBytes(path));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public async Task PlayerItemSnapshotLoaderSaveRetriesTransientDestinationContention()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, CharacterUid + ".bin");
        var original = BuildPlayerItems([DbItem(uid: 1001, itemId: 3324226, position: 0)], []);
        File.WriteAllBytes(path, original);
        var loader = new PlayerItemSnapshotLoader(_root);
        var replacement = PlayerItemSnapshotLoader.Parse(
            BuildPlayerItems([DbItem(uid: 1002, itemId: 1088001, position: 0, stackSize: 5)], []));
        using var persistenceStarted = new ManualResetEventSlim();

        Task persistence;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            persistence = StartLongRunning(() =>
            {
                persistenceStarted.Set();
                loader.SaveByCharacterUid(CharacterUid, replacement);
                return true;
            });
            Assert.True(persistenceStarted.Wait(TimeSpan.FromSeconds(5)));
            await Task.Delay(50);
            Assert.False(persistence.IsCompleted);
        }

        await persistence.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(replacement), File.ReadAllBytes(path));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void PlayerItemSnapshotLoaderSavePersistentContentionPreservesOriginalAndThrows()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, CharacterUid + ".bin");
        var original = BuildPlayerItems([DbItem(uid: 1001, itemId: 3324226, position: 0)], []);
        File.WriteAllBytes(path, original);
        var loader = new PlayerItemSnapshotLoader(_root);
        var replacement = PlayerItemSnapshotLoader.Parse(
            BuildPlayerItems([DbItem(uid: 1002, itemId: 1088001, position: 0, stackSize: 7)], []));

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Throws<IOException>(() => loader.SaveByCharacterUid(CharacterUid, replacement));
            Assert.Equal(original, File.ReadAllBytes(path));
            AssertNoTransactionArtifacts();
        }

        Assert.Equal(original, File.ReadAllBytes(path));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void PlayerItemSnapshotLoaderSaveCreatesMissingFileAtomicallyWithExactLegacyBytes()
    {
        var loader = new PlayerItemSnapshotLoader(_root);
        var snapshot = PlayerItemSnapshotLoader.Parse(
            BuildPlayerItems(
            [
                DbItem(uid: 1001, itemId: 725016, position: 0, stackSize: 2),
                DbItem(uid: 1002, itemId: 500109, position: 4, plus: 12),
            ],
            [
                Perfection(
                    itemUid: 1002,
                    level: 7,
                    progress: 123,
                    ownerUid: (uint)CharacterUid,
                    ownerName: "HeroOne",
                    signature: "ready"),
            ]));
        var expected = PlayerItemSnapshotLoader.ToLegacyBytes(snapshot);

        loader.SaveByCharacterUid(CharacterUid, snapshot);

        Assert.True(Directory.Exists(_root));
        Assert.Equal(expected, File.ReadAllBytes(Path.Combine(_root, CharacterUid + ".bin")));
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void PlayerItemSnapshotLoaderRejectsTruncatedFiles()
    {
        var ex = Assert.Throws<FormatException>(
            () => PlayerItemSnapshotLoader.Parse([1, 0, 0, 0, 2], "bad.bin"));

        Assert.Contains("truncated", ex.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task AssertWaitsForFileLock(string path, Action action)
    {
        using var lockEntered = new ManualResetEventSlim();
        using var releaseLock = new ManualResetEventSlim();
        using var operationStarted = new ManualResetEventSlim();

        var lockHolder = StartLongRunning(() => SnapshotFileLockCoordinator.ExecuteLocked(
            path,
            () =>
            {
                lockEntered.Set();
                if (!releaseLock.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("Timed out waiting to release the player item file lock.");
                }

                return true;
            }));
        Assert.True(lockEntered.Wait(TimeSpan.FromSeconds(5)));

        var operation = StartLongRunning(() =>
        {
            operationStarted.Set();
            action();
            return true;
        });
        Assert.True(operationStarted.Wait(TimeSpan.FromSeconds(5)));

        try
        {
            await Task.Delay(250);
            Assert.False(operation.IsCompleted);
        }
        finally
        {
            releaseLock.Set();
        }

        Assert.True(await lockHolder.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(await operation.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private void AssertNoTransactionArtifacts()
    {
        Assert.Empty(Directory.EnumerateFiles(
            _root,
            "*.player-item-file-*",
            SearchOption.TopDirectoryOnly));
    }

    private static Task<TResult> StartLongRunning<TResult>(Func<TResult> action)
    {
        return Task.Factory.StartNew(
            action,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public static byte[] BuildPlayerItems(
        IReadOnlyList<TestDbItem> items,
        IReadOnlyList<TestPerfection> perfections)
    {
        var depositedItemCount = items.Sum(item => item.DepositedItems.Count);
        var bytes = new byte[
            sizeof(int) +
            ((items.Count + depositedItemCount) * PlayerItemSnapshotLoader.LegacyDbItemSize) +
            sizeof(int) +
            (perfections.Count * PlayerItemSnapshotLoader.LegacyPerfectionSize)];
        var offset = 0;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)), items.Count);
        offset += sizeof(int);

        foreach (var item in items)
        {
            WriteDbItem(bytes.AsSpan(offset, PlayerItemSnapshotLoader.LegacyDbItemSize), item);
            offset += PlayerItemSnapshotLoader.LegacyDbItemSize;

            foreach (var depositedItem in item.DepositedItems)
            {
                WriteDbItem(bytes.AsSpan(offset, PlayerItemSnapshotLoader.LegacyDbItemSize), depositedItem);
                offset += PlayerItemSnapshotLoader.LegacyDbItemSize;
            }
        }

        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)), perfections.Count);
        offset += sizeof(int);

        foreach (var perfection in perfections)
        {
            WritePerfection(bytes.AsSpan(offset, PlayerItemSnapshotLoader.LegacyPerfectionSize), perfection);
            offset += PlayerItemSnapshotLoader.LegacyPerfectionSize;
        }

        return bytes;
    }

    public static TestDbItem DbItem(
        uint uid,
        uint itemId,
        ushort position,
        ushort durability = 1,
        ushort maximumDurability = 1,
        byte plus = 0,
        ushort stackSize = 0,
        uint warehouseId = 0,
        uint color = 0,
        uint runeExperience = 0,
        byte locked = 0,
        byte bound = 0,
        int unlockTimer = 0,
        uint purificationItemId = 0,
        uint purificationLevel = 0,
        uint purificationDuration = 0,
        long purificationAddedOn = 0,
        uint refineryEffectId = 0,
        uint refineryEffectLevel = 0,
        uint refineryEffectPercent = 0,
        uint refineryEffectPercent2 = 0,
        uint refineryEffectDuration = 0,
        long refineryEffectAddedOn = 0,
        uint attribute1 = 0,
        uint attribute2 = 0,
        uint attribute3 = 0,
        uint attribute4 = 0,
        uint attribute5 = 0,
        IReadOnlyList<TestDbItem>? depositedItems = null)
    {
        return new TestDbItem(
            uid,
            itemId,
            durability,
            maximumDurability,
            position,
            plus,
            stackSize,
            warehouseId,
            color,
            runeExperience,
            locked,
            bound,
            unlockTimer,
            purificationItemId,
            purificationLevel,
            purificationDuration,
            purificationAddedOn,
            refineryEffectId,
            refineryEffectLevel,
            refineryEffectPercent,
            refineryEffectPercent2,
            refineryEffectDuration,
            refineryEffectAddedOn,
            attribute1,
            attribute2,
            attribute3,
            attribute4,
            attribute5)
        {
            DepositedItems = depositedItems ?? [],
        };
    }

    public static TestPerfection Perfection(
        uint itemUid,
        uint level,
        uint progress,
        uint ownerUid,
        string ownerName,
        string signature)
    {
        return new TestPerfection(itemUid, level, progress, ownerUid, ownerName, signature);
    }

    private static void WriteDbItem(Span<byte> destination, TestDbItem item)
    {
        WriteUInt32(destination, 0, item.Uid);
        WriteUInt32(destination, 4, item.ItemId);
        WriteUInt16(destination, 8, item.Durability);
        WriteUInt16(destination, 10, item.MaximumDurability);
        WriteUInt16(destination, 12, item.Position);
        destination[28] = item.Plus;
        destination[30] = item.Bound;
        destination[33] = item.Locked;
        WriteUInt16(destination, 52, item.StackSize);
        WriteUInt32(destination, 56, item.WarehouseId);
        WriteUInt32(destination, 60, item.Color);
        WriteInt32(destination, 64, item.UnlockTimer);
        WriteUInt32(destination, 84, item.PurificationItemId);
        WriteUInt32(destination, 88, item.PurificationLevel);
        WriteUInt32(destination, 92, item.PurificationDuration);
        WriteInt64(destination, 96, item.PurificationAddedOn);
        WriteUInt32(destination, 104, item.RefineryEffectId);
        WriteUInt32(destination, 108, item.RefineryEffectLevel);
        WriteUInt32(destination, 112, item.RefineryEffectPercent);
        WriteUInt32(destination, 116, item.RefineryEffectPercent2);
        WriteUInt32(destination, 120, item.RefineryEffectDuration);
        WriteInt64(destination, 128, item.RefineryEffectAddedOn);
        WriteUInt32(destination, 136, (uint)item.DepositedItems.Count);
        WriteUInt32(destination, 144, item.RuneExperience);
        WriteUInt32(destination, 148, item.Attribute1);
        WriteUInt32(destination, 152, item.Attribute2);
        WriteUInt32(destination, 156, item.Attribute3);
        WriteUInt32(destination, 160, item.Attribute4);
        WriteUInt32(destination, 164, item.Attribute5);
    }

    private static void WritePerfection(Span<byte> destination, TestPerfection perfection)
    {
        WriteUInt32(destination, 0, perfection.ItemUid);
        WriteUInt32(destination, 4, perfection.Level);
        WriteUInt32(destination, 8, perfection.Progress);
        WriteUInt32(destination, 12, perfection.OwnerUid);
        WriteFixedUtf8(destination.Slice(16, 32), perfection.OwnerName);
        WriteFixedUtf8(destination.Slice(48, 32), perfection.Signature);
    }

    private static void WriteUInt16(Span<byte> destination, int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, sizeof(ushort)), value);
    }

    private static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
    }

    private static void WriteInt32(Span<byte> destination, int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset, sizeof(int)), value);
    }

    private static void WriteInt64(Span<byte> destination, int offset, long value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(destination.Slice(offset, sizeof(long)), value);
    }

    private static void WriteFixedUtf8(Span<byte> destination, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }

    public sealed record TestDbItem(
        uint Uid,
        uint ItemId,
        ushort Durability,
        ushort MaximumDurability,
        ushort Position,
        byte Plus,
        ushort StackSize,
        uint WarehouseId,
        uint Color,
        uint RuneExperience,
        byte Locked,
        byte Bound,
        int UnlockTimer,
        uint PurificationItemId,
        uint PurificationLevel,
        uint PurificationDuration,
        long PurificationAddedOn,
        uint RefineryEffectId,
        uint RefineryEffectLevel,
        uint RefineryEffectPercent,
        uint RefineryEffectPercent2,
        uint RefineryEffectDuration,
        long RefineryEffectAddedOn,
        uint Attribute1,
        uint Attribute2,
        uint Attribute3,
        uint Attribute4,
        uint Attribute5)
    {
        public IReadOnlyList<TestDbItem> DepositedItems { get; init; } = [];
    }

    public sealed record TestPerfection(
        uint ItemUid,
        uint Level,
        uint Progress,
        uint OwnerUid,
        string OwnerName,
        string Signature);
}
