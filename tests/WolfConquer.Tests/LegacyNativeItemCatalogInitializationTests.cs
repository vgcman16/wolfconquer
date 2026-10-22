using System.Globalization;
using System.Text;
using WolfConquer.Core.World;
using Boxes = WolfConquer.Core.World.LegacyNativeRefinaryBoxes;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;
using ItemType = WolfConquer.Core.World.LegacyNativeItemTypeRuneMetadata;
using Rifinery = WolfConquer.Core.World.LegacyNativeRifinery;

namespace WolfConquer.Tests;

// Real files exercise only the native contiguous catalog phase, not host startup.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemCatalogInitializationTests
{
    private const uint RuneId = 4_010_042;

    [Fact]
    public void SuccessfulPhasePublishesThreeOwnersAndMaterializesRuneFromLoadedCatalog()
    {
        using var owned = new OwnedCatalog();
        Initialize();

        var items = NewItems(owned);
        Assert.Equal(2, items.Count);
        Assert.Equal("Rune", items[RuneId].Name);
        Assert.Equal("Sub", items[112001].Name);
        Assert.Equal(RuneId, items[RuneId].RuneSpellID);
        Assert.NotSame(owned.PreviousRifinery, LegacyNativePoolItems.RifineryItems);
        var refinery = Assert.Single(LegacyNativePoolItems.RifineryItems!).Value;
        Assert.Equal(101u, refinery.ItemID);
        Assert.Equal(1u, refinery.Level);
        Assert.Equal(4u, refinery.ForItemPosition);
        var box = Assert.Single(LegacyNativePoolItems.DBRerinaryBoxes!).Value;
        Assert.NotSame(owned.PreviousBoxes, LegacyNativePoolItems.DBRerinaryBoxes);
        Assert.Equal(9u, box.Identifier);
        Assert.Equal(4u, box.Position);
        Assert.Equal(Rifinery.RefineryType.MDefence, box.Type);
        Assert.True(box.Untradable);
        Assert.Same(owned.Counter, LegacyNativePoolItems.ItemCounter);

        var record = new LegacyNativeClientItemRecord { UID = 77, ITEM_ID = RuneId };
        var item = record.GetDataItem();
        Assert.Equal(77u, item.Uid);
        Assert.Equal(RuneId, item.ITEM_ID);
        Assert.Equal(4_010_001u, item.SpellID);
        Assert.Equal(Flags.ItemEffect.RuneEffect, item.Effect);
    }

    [Fact]
    public void MissingRifineryPublishesEmptyItemsButRetainsBothPreviousConstructorOwners()
    {
        using var owned = new OwnedCatalog();
        owned.Remove("Rifinery.txt");
        Assert.Throws<FileNotFoundException>(Initialize);

        Assert.Empty(NewItems(owned));
        Assert.Same(owned.PreviousRifinery, LegacyNativePoolItems.RifineryItems);
        Assert.Same(owned.PreviousBoxes, LegacyNativePoolItems.DBRerinaryBoxes);
        Assert.Equal("Previous", owned.PreviousItems[7].Name);
    }

    [Fact]
    public void LateRifineryFaultRetainsEarlierStaticAdmissionWithoutPublishingConstructor()
    {
        using var owned = new OwnedCatalog();
        owned.Write("Rifinery.txt", RefineryRow(102) + "\n" + RefineryRow(103, "bad") + "\n");
        Assert.Throws<FormatException>(Initialize);

        Assert.Empty(NewItems(owned));
        Assert.Same(owned.PreviousRifinery, LegacyNativePoolItems.RifineryItems);
        Assert.Same(owned.PreviousBoxes, LegacyNativePoolItems.DBRerinaryBoxes);
        Assert.Equal(102u, ItemType.Refinary[1][102].ItemID);
        Assert.False(owned.PreviousRifinery.ContainsKey(102));
        Assert.False(ItemType.Refinary[1].ContainsKey(103));
    }

    [Fact]
    public void DuplicateBoxFaultRetainsNewItemsAndRifineryButPreviousBoxes()
    {
        using var owned = new OwnedCatalog();
        owned.Write("databaserefineryboxes.txt", "Count=2\n9,4,1,0\n9,5,2,1\n");
        Assert.Throws<ArgumentException>(Initialize);

        Assert.Empty(NewItems(owned));
        Assert.NotSame(owned.PreviousRifinery, LegacyNativePoolItems.RifineryItems);
        Assert.Equal(101u, Assert.Single(LegacyNativePoolItems.RifineryItems!).Value.ItemID);
        Assert.Same(owned.PreviousBoxes, LegacyNativePoolItems.DBRerinaryBoxes);
        Assert.True(owned.PreviousBoxes[9].Untradable);
    }

    [Theory]
    [InlineData("ItemAdd.ini")]
    [InlineData("itemtype.txt")]
    public void MissingLoadingInputLeavesAllThreePublishedOwnersAndEmptyItemTable(string missing)
    {
        using var owned = new OwnedCatalog();
        owned.Remove(missing);
        Assert.Throws<FileNotFoundException>(Initialize);
        AssertNewConstructorOwners(owned);
        Assert.Empty(NewItems(owned));
    }

    [Fact]
    public void InvalidItemAddSlotFailsBeforeMainAdmissionButAfterAllOwnerPublications()
    {
        using var owned = new OwnedCatalog();
        owned.Write("ItemAdd.ini", "111000 13 10 0 0 0 0 0 0 0\n");
        Assert.Throws<IndexOutOfRangeException>(Initialize);
        AssertNewConstructorOwners(owned);
        Assert.Empty(NewItems(owned));
    }

    [Fact]
    public void MissingSubFileKeepsAlreadyAdmittedMainRowAndItsRealPlusTable()
    {
        using var owned = new OwnedCatalog();
        owned.Write("ItemAdd.ini", "111000 1 10 20 30 40 50 60 70 80\n");
        owned.Write("itemtype.txt", ItemRow(111003, "Main") + "\n");
        owned.Remove("ItemtypeSub.txt");
        Assert.Throws<FileNotFoundException>(Initialize);

        AssertNewConstructorOwners(owned);
        var main = Assert.Single(NewItems(owned)).Value;
        Assert.Equal(111003u, main.ID);
        Assert.Equal("Main", main.Name);
        Assert.True(main.AllowUpgradePlus);
        Assert.Equal(13, main.Plus!.Length);
        Assert.Equal((ushort)10, main.Plus[1].ItemHP);
        Assert.Equal(20u, main.Plus[1].MinAttack);
        Assert.Equal(30u, main.Plus[1].MaxAttack);
    }

    [Fact]
    public void InitializationDoesNotResetAnyExistingStaticCatalogMapOrBucket()
    {
        using var owned = new OwnedCatalog();
        var marker = new LegacyNativeItemRuneMetadata { ID = 8, Name = "Retained" };
        var purification = ItemType.PurificationItems;
        var bucket = new Dictionary<uint, LegacyNativeItemRuneMetadata> { [8] = marker };
        purification.Add(7, bucket);
        var accessories = ItemType.Accessorys;
        var garments = ItemType.Garments;
        var mounts = ItemType.SteedMounts;
        accessories.Add(8, marker);
        garments.Add(8, marker);
        mounts.Add(8, marker);
        var refinary = ItemType.Refinary;
        var oldRefinaryBucket = refinary[1];
        var oldRefinaryItem = oldRefinaryBucket[101];

        Initialize();

        Assert.Same(purification, ItemType.PurificationItems);
        Assert.Same(bucket, ItemType.PurificationItems[7]);
        Assert.Same(marker, bucket[8]);
        Assert.Same(accessories, ItemType.Accessorys);
        Assert.Same(marker, ItemType.Accessorys[8]);
        Assert.Same(garments, ItemType.Garments);
        Assert.Same(marker, ItemType.Garments[8]);
        Assert.Same(mounts, ItemType.SteedMounts);
        Assert.Same(marker, ItemType.SteedMounts[8]);
        Assert.Same(refinary, ItemType.Refinary);
        Assert.Same(oldRefinaryBucket, ItemType.Refinary[1]);
        Assert.Same(oldRefinaryItem, ItemType.Refinary[1][101]);
        Assert.NotSame(oldRefinaryItem, LegacyNativePoolItems.RifineryItems![101]);
    }

    [Fact]
    public void RepeatedInitializationReplacesOwnersButStaticMapsKeepFirstRowsAndOldAliases()
    {
        using var owned = new OwnedCatalog();
        uint[] ids = [111003, 350001, 181305, 200000];
        owned.Write("itemtype.txt", string.Join('\n', ids.Select(id => ItemRow(id, "First", 1))) + "\n");
        Initialize();
        var firstItems = LegacyNativePoolItems.ItemsBase!;
        var firstRifinery = LegacyNativePoolItems.RifineryItems!;
        var firstBoxes = LegacyNativePoolItems.DBRerinaryBoxes!;
        var firstPurification = ItemType.PurificationItems[1];
        var refineryCache = ItemType.Refinary[1][101];
        owned.Write("itemtype.txt", string.Join('\n', ids.Select(id => ItemRow(id, "Second", 1))) + "\n");
        owned.Write("Rifinery.txt", RefineryRow(101, "17") + "\n");
        owned.Write("databaserefineryboxes.txt", "Count=1\n10,5,2,0\n");
        Initialize();

        var secondItems = LegacyNativePoolItems.ItemsBase!;
        Assert.NotSame(firstItems, secondItems);
        Assert.NotSame(firstRifinery, LegacyNativePoolItems.RifineryItems);
        Assert.NotSame(firstBoxes, LegacyNativePoolItems.DBRerinaryBoxes);
        Assert.Same(firstPurification, ItemType.PurificationItems[1]);
        foreach (var id in ids)
        {
            Assert.Equal("First", firstItems[id].Name);
            Assert.Equal("Second", secondItems[id].Name);
            Assert.NotSame(firstItems[id], secondItems[id]);
            Assert.Same(firstItems[id], firstPurification[id]);
        }
        Assert.Same(firstItems[350001], ItemType.Accessorys[350001]);
        Assert.Same(firstItems[181305], ItemType.Garments[181305]);
        Assert.Same(firstItems[200000], ItemType.SteedMounts[200000]);
        Assert.Same(refineryCache, ItemType.Refinary[1][101]);
        Assert.Equal(7u, firstRifinery[101].Procent);
        Assert.Equal(17u, LegacyNativePoolItems.RifineryItems![101].Procent);
        Assert.True(firstBoxes.ContainsKey(9));
        Assert.False(LegacyNativePoolItems.DBRerinaryBoxes!.ContainsKey(9));
        Assert.True(LegacyNativePoolItems.DBRerinaryBoxes.ContainsKey(10));
    }

    private static void Initialize() => LegacyNativeItemCatalogInitialization.InitializeAtNativeCatalogPhase();

    private static ItemType NewItems(OwnedCatalog owned)
    {
        var items = Assert.IsType<ItemType>(LegacyNativePoolItems.ItemsBase);
        Assert.NotSame(owned.PreviousItems, items);
        return items;
    }

    private static void AssertNewConstructorOwners(OwnedCatalog owned)
    {
        NewItems(owned);
        Assert.NotNull(LegacyNativePoolItems.RifineryItems);
        Assert.NotNull(LegacyNativePoolItems.DBRerinaryBoxes);
        Assert.NotSame(owned.PreviousRifinery, LegacyNativePoolItems.RifineryItems);
        Assert.NotSame(owned.PreviousBoxes, LegacyNativePoolItems.DBRerinaryBoxes);
        Assert.Single(LegacyNativePoolItems.RifineryItems);
        Assert.Single(LegacyNativePoolItems.DBRerinaryBoxes);
    }

    private static string ItemRow(uint id, string name, ushort purification = 0)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = id.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[25] = id.ToString(CultureInfo.InvariantCulture);
        fields[52] = "Item";
        fields[55] = purification.ToString(CultureInfo.InvariantCulture);
        fields[58] = "67";
        return string.Join("@@", fields);
    }

    private static string RefineryRow(uint id, string percent = "7")
    {
        var fields = Enumerable.Repeat("0", 54).ToArray();
        fields[0] = id.ToString(CultureInfo.InvariantCulture);
        fields[1] = "Normal";
        fields[2] = percent;
        fields[3] = "1";
        fields[4] = "2";
        fields[5] = "9";
        fields[53] = "Bow";
        return string.Join(' ', fields);
    }

    private sealed class OwnedCatalog : IDisposable
    {
        private readonly string oldLocation = LegacyNativeServerConfig.DbLocation;
        private readonly CultureInfo oldCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo oldUiCulture = CultureInfo.CurrentUICulture;
        private readonly ItemType? oldItems = LegacyNativePoolItems.ItemsBase;
        private readonly Rifinery? oldRifinery = LegacyNativePoolItems.RifineryItems;
        private readonly Boxes? oldBoxes = LegacyNativePoolItems.DBRerinaryBoxes;
        private readonly Dictionary<ushort, Dictionary<uint, LegacyNativeItemRuneMetadata>> oldPurification = ItemType.PurificationItems;
        private readonly Dictionary<uint, LegacyNativeItemRuneMetadata> oldAccessories = ItemType.Accessorys;
        private readonly Dictionary<uint, Dictionary<uint, Rifinery.Item>> oldRefinary = ItemType.Refinary;
        private readonly Dictionary<uint, LegacyNativeItemRuneMetadata> oldGarments = ItemType.Garments;
        private readonly Dictionary<uint, LegacyNativeItemRuneMetadata> oldMounts = ItemType.SteedMounts;
        private readonly Dictionary<string, byte[]> files = new(StringComparer.OrdinalIgnoreCase);
        private readonly string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(),
            "WolfConquer.NativeCatalog." + Guid.NewGuid().ToString("N")));
        internal readonly LegacyNativeCounter Counter = LegacyNativePoolItems.ItemCounter;
        internal ItemType PreviousItems { get; private set; } = null!;
        internal Rifinery PreviousRifinery { get; private set; } = null!;
        internal Boxes PreviousBoxes { get; private set; } = null!;

        internal OwnedCatalog()
        {
            Ordinary(root);
            if (Directory.Exists(root) || File.Exists(root)) throw new IOException("Fresh test directory required.");
            Directory.CreateDirectory(root);
            try
            {
                Write("Rifinery.txt", RefineryRow(101) + "\n");
                Write("databaserefineryboxes.txt", "Count=1\n9,4,1,1\n");
                Write("ItemAdd.ini", "");
                Write("itemtype.txt", ItemRow(RuneId, "Rune") + "\n");
                Write("ItemtypeSub.txt", ItemRow(112001, "Sub") + "\n");
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                LegacyNativeServerConfig.DbLocation = root + Path.DirectorySeparatorChar;
                ItemType.PurificationItems = new();
                ItemType.Accessorys = new();
                ItemType.Refinary = new();
                ItemType.Garments = new();
                ItemType.SteedMounts = new();
                PreviousItems = new ItemType { [7] = new() { ID = 7, Name = "Previous" } };
                PreviousRifinery = new Rifinery();
                PreviousBoxes = new Boxes();
                LegacyNativePoolItems.ItemsBase = PreviousItems;
                LegacyNativePoolItems.RifineryItems = PreviousRifinery;
                LegacyNativePoolItems.DBRerinaryBoxes = PreviousBoxes;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal void Write(string name, string text)
        {
            var path = Leaf(name);
            var mode = FileMode.CreateNew;
            if (files.TryGetValue(path, out var previous))
            {
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(previous))
                    throw new IOException("Changed test file preserved.");
                mode = FileMode.Truncate;
            }
            var bytes = new UTF8Encoding(false, true).GetBytes(text);
            using (var stream = new FileStream(path, mode, FileAccess.Write, FileShare.None))
                stream.Write(bytes);
            files[path] = bytes;
        }

        internal void Remove(string name)
        {
            var path = Leaf(name);
            if (!files.TryGetValue(path, out var bytes) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
                throw new IOException("Only unchanged owned test files may be removed.");
            File.Delete(path);
            files.Remove(path);
        }

        private string Leaf(string name)
        {
            if (name is not ("Rifinery.txt" or "databaserefineryboxes.txt" or "ItemAdd.ini" or "itemtype.txt" or "ItemtypeSub.txt"))
                throw new IOException("Unexpected test leaf.");
            var path = Path.GetFullPath(Path.Combine(root, name));
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(path), root))
                throw new IOException("Uncontained test leaf.");
            Ordinary(path);
            return path;
        }

        private static void Ordinary(string path)
        {
            for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Reparse path preserved.");
            }
        }

        public void Dispose()
        {
            // Every test's using scope restores global substitutions in its finally.
            LegacyNativePoolItems.ItemsBase = oldItems;
            LegacyNativePoolItems.RifineryItems = oldRifinery;
            LegacyNativePoolItems.DBRerinaryBoxes = oldBoxes;
            LegacyNativePoolItems.ItemCounter = Counter;
            ItemType.PurificationItems = oldPurification;
            ItemType.Accessorys = oldAccessories;
            ItemType.Refinary = oldRefinary;
            ItemType.Garments = oldGarments;
            ItemType.SteedMounts = oldMounts;
            LegacyNativeServerConfig.DbLocation = oldLocation;
            CultureInfo.CurrentCulture = oldCulture;
            CultureInfo.CurrentUICulture = oldUiCulture;
            Ordinary(root);
            var entries = Directory.GetFileSystemEntries(root);
            if (entries.Length != files.Count || entries.Any(path => !files.ContainsKey(path)))
                throw new IOException("Unexpected test entries preserved.");
            foreach (var pair in files)
            {
                Leaf(Path.GetFileName(pair.Key));
                if (!File.ReadAllBytes(pair.Key).AsSpan().SequenceEqual(pair.Value))
                    throw new IOException("Changed test data preserved.");
            }
            foreach (var path in files.Keys) File.Delete(path);
            Directory.Delete(root, recursive: false);
        }
    }
}
