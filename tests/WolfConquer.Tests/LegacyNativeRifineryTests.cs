using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.World;
using ItemTable = WolfConquer.Core.World.LegacyNativeItemTypeRuneMetadata;
using Rifinery = WolfConquer.Core.World.LegacyNativeRifinery;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeRifineryTests
{
    [Fact]
    public void DeclaredItemFieldsAndIntEnumKeepNativeWidthsOrderAndDefaults()
    {
        var fields = typeof(Rifinery.Item).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "Name", "ItemID", "Level", "ForItemPosition", "Procent", "Procent2", "Type", "Type2" },
            fields.Select(field => field.Name));
        Assert.Equal(typeof(string), fields[0].FieldType);
        foreach (var field in fields.Skip(1).Take(5)) Assert.Equal(typeof(uint), field.FieldType);
        Assert.Equal(typeof(Rifinery.RefineryType), fields[6].FieldType);
        Assert.Equal(typeof(Rifinery.RefineryType), fields[7].FieldType);
        Assert.All(fields, field => Assert.False(field.IsInitOnly));
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(Rifinery.RefineryType)));
        Assert.Equal(new[] { "None", "MDefence", "CriticalStrike", "SkillCriticalStrike", "Immunity", "Break",
            "Counteraction", "Detoxication", "Block", "Penetration", "Intensification", "FinalMDamage", "FinalMAttack" },
            Enum.GetNames<Rifinery.RefineryType>());
        Assert.Equal(Enumerable.Range(0, 13), Enum.GetValues<Rifinery.RefineryType>().Select(value => (int)value));
        var item = new Rifinery.Item();
        Assert.Equal("", item.Name);
        Assert.Equal(0U, item.ItemID);
        Assert.Equal(0U, item.Level);
        Assert.Equal(0U, item.ForItemPosition);
        Assert.Equal(0U, item.Procent);
        Assert.Equal(0U, item.Procent2);
        Assert.Equal(Rifinery.RefineryType.None, item.Type);
        Assert.Equal(Rifinery.RefineryType.None, item.Type2);
        Assert.False(typeof(Rifinery).IsSealed);
        Assert.False(typeof(Rifinery.Item).IsSealed);
    }

    [Fact]
    public void RefinaryIsTheRealReplaceableNestedItemDictionaryAtItsNativePosition()
    {
        var fields = typeof(ItemTable).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "PurificationItems", "Accessorys", "Refinary", "Garments", "SteedMounts", "unabletradeitem" },
            fields.Select(field => field.Name));
        Assert.Equal(typeof(Dictionary<uint, Dictionary<uint, Rifinery.Item>>), fields[2].FieldType);
        Assert.False(fields[2].IsInitOnly);
        Assert.Equal(typeof(List<uint>), fields[5].FieldType);
        Assert.False(fields[5].IsInitOnly);
        var before = ItemTable.Refinary;
        try
        {
            var item = new Rifinery.Item { ItemID = 7 };
            var bucket = new Dictionary<uint, Rifinery.Item> { [7] = item };
            var owner = new Dictionary<uint, Dictionary<uint, Rifinery.Item>> { [1] = bucket };
            ItemTable.Refinary = owner;
            Assert.Same(owner, ItemTable.Refinary);
            Assert.Same(bucket, ItemTable.Refinary[1]);
            Assert.Same(item, ItemTable.Refinary[1][7]);
            ItemTable.Refinary = null!;
            Assert.Null(ItemTable.Refinary);
            Assert.Same(item, owner[1][7]);
        }
        finally { ItemTable.Refinary = before; }
    }

    [Fact]
    public void CalculateLevelUsesCaseSensitiveIndependentContainsRulesWithLaterRulesWinning()
    {
        var names = new[] { "Normal", "Refined", "Unique", "Elite", "Super", "Sacred" };
        for (var i = 0; i < names.Length; i++) Assert.Equal((uint)i + 1, Rifinery.CalculateLevel("prefix" + names[i] + "suffix"));
        Assert.Equal(0U, Rifinery.CalculateLevel("normal refined unique elite super sacred"));
        Assert.Equal(0U, Rifinery.CalculateLevel(""));
        Assert.Equal(6U, Rifinery.CalculateLevel("SacredNormalRefinedUniqueEliteSuper"));
        Assert.Equal(6U, Rifinery.CalculateLevel("NormalRefinedUniqueEliteSuperSacred"));
        Assert.Throws<NullReferenceException>(() => Rifinery.CalculateLevel(null!));
    }

    [Fact]
    public void EmptyPhysicalFileCreatesAFreshLocalTableWithoutClearingTheSharedCache()
    {
        using var fixture = new OwnedRifineryFile("");
        var cache = ItemTable.Refinary;
        var row = new Rifinery.Item { ItemID = 9 };
        var bucket = new Dictionary<uint, Rifinery.Item> { [9] = row };
        cache.Add(6, bucket);
        var first = new Rifinery();
        var second = new Rifinery();
        Assert.Empty(first);
        Assert.Empty(second);
        Assert.NotSame(first, second);
        Assert.Same(cache, ItemTable.Refinary);
        Assert.Same(bucket, ItemTable.Refinary[6]);
        Assert.Same(row, ItemTable.Refinary[6][9]);
        // No deterministic assertion about GC.Collect timing or collection counts.
    }

    [Fact]
    public void AllNumericFieldsAndUndeclaredByteEnumValuesKeepTheirNativeWidths()
    {
        using var fixture = new OwnedRifineryFile(Row(uint.MaxValue.ToString(CultureInfo.InvariantCulture),
            "Super", "[Ring]tail", uint.MaxValue.ToString(CultureInfo.InvariantCulture), "255", "12",
            uint.MaxValue.ToString(CultureInfo.InvariantCulture)));
        var table = new Rifinery();
        var item = table[uint.MaxValue];
        Assert.Equal(uint.MaxValue, item.ItemID);
        Assert.Equal(5U, item.Level);
        Assert.Equal("Ring", item.Name);
        Assert.Equal(6U, item.ForItemPosition);
        Assert.Equal(uint.MaxValue, item.Procent);
        Assert.Equal(uint.MaxValue, item.Procent2);
        Assert.Equal((Rifinery.RefineryType)255, item.Type);
        Assert.Equal(Rifinery.RefineryType.FinalMAttack, item.Type2);
        Assert.Same(item, ItemTable.Refinary[5][uint.MaxValue]);
    }

    [Fact]
    public void LocalAndLevelCacheEntriesShareTheSameLiveMutableItemReference()
    {
        using var fixture = new OwnedRifineryFile(Row("101", "Normal", "Bow"));
        var table = new Rifinery();
        var item = table[101];
        Assert.Same(item, ItemTable.Refinary[1][101]);
        ItemTable.Refinary[1][101].Name = "live";
        ItemTable.Refinary[1][101].ItemID = 999;
        table[101].Procent = uint.MaxValue;
        Assert.Equal("live", item.Name);
        Assert.Equal(999U, item.ItemID);
        Assert.Equal(uint.MaxValue, ItemTable.Refinary[1][101].Procent);
        Assert.True(table.ContainsKey(101));
        Assert.False(table.ContainsKey(999));
        Assert.True(ItemTable.Refinary[1].ContainsKey(101));
        Assert.False(ItemTable.Refinary[1].ContainsKey(999));
    }

    [Fact]
    public void SameLevelDuplicateRetainsTheFirstLocalAndFirstCachedObject()
    {
        using var fixture = new OwnedRifineryFile(Row("101", "Normal", "Bow") + "\n" +
            Row("101", "Normal", "Armor", "77"));
        var table = new Rifinery();
        Assert.Single(table);
        Assert.Single(ItemTable.Refinary[1]);
        Assert.Equal("Bow", table[101].Name);
        Assert.Equal(4U, table[101].ForItemPosition);
        Assert.Equal(7U, table[101].Procent);
        Assert.Same(table[101], ItemTable.Refinary[1][101]);
    }

    [Fact]
    public void DifferentLevelDuplicateKeepsFirstLocalButAdmitsTheNewCacheObject()
    {
        using var fixture = new OwnedRifineryFile(Row("101", "Normal", "Bow") + "\n" +
            Row("101", "Refined", "Armor", "77"));
        var table = new Rifinery();
        Assert.Single(table);
        Assert.Equal(1U, table[101].Level);
        Assert.Same(table[101], ItemTable.Refinary[1][101]);
        var other = ItemTable.Refinary[2][101];
        Assert.NotSame(table[101], other);
        Assert.Equal("Armor", other.Name);
        Assert.Equal(3U, other.ForItemPosition);
        Assert.Equal(77U, other.Procent);
        Assert.Equal(2U, other.Level);
    }

    [Fact]
    public void APreexistingCacheRowIsNotOverwrittenByTheNewLocalRow()
    {
        using var fixture = new OwnedRifineryFile(Row("101", "Normal", "Bow"));
        var old = new Rifinery.Item { Name = "preexisting", ItemID = 999 };
        var bucket = new Dictionary<uint, Rifinery.Item> { [101] = old };
        ItemTable.Refinary[1] = bucket;
        var table = new Rifinery();
        Assert.Same(bucket, ItemTable.Refinary[1]);
        Assert.Same(old, ItemTable.Refinary[1][101]);
        Assert.Equal(999U, old.ItemID);
        Assert.NotSame(old, table[101]);
        Assert.Equal("Bow", table[101].Name);
    }

    [Fact]
    public void SuccessiveConstructorsKeepIndependentLocalRowsAndTheFirstSharedEntry()
    {
        using var fixture = new OwnedRifineryFile(Row("101", "Normal", "Bow"));
        var first = new Rifinery();
        var second = new Rifinery();
        Assert.NotSame(first, second);
        Assert.NotSame(first[101], second[101]);
        Assert.Same(first[101], ItemTable.Refinary[1][101]);
        Assert.Equal("Bow", second[101].Name);
    }

    [Fact]
    public void AnExistingLevelBucketAndItsRowsRemainTheSameExactReferences()
    {
        using var fixture = new OwnedRifineryFile(Row("101", "Normal", "Bow"));
        var old = new Rifinery.Item { ItemID = 99 };
        var bucket = new Dictionary<uint, Rifinery.Item> { [99] = old };
        var cache = ItemTable.Refinary;
        cache[1] = bucket;
        var table = new Rifinery();
        Assert.Same(cache, ItemTable.Refinary);
        Assert.Same(bucket, cache[1]);
        Assert.Same(old, bucket[99]);
        Assert.Same(table[101], bucket[101]);
    }

    [Fact]
    public void UnknownLevelsAndEmptyOrUnrecognizedNamesRemainAdmitted()
    {
        using var fixture = new OwnedRifineryFile(Row("101", "unknown", "0") + "\n" +
            Row("102", "unknown", "unrecognized") + "\n" + Row("3004136", "unknown", "]suffix"));
        var table = new Rifinery();
        Assert.Equal(3, table.Count);
        Assert.Equal("0", table[101].Name);
        Assert.Equal("unrecognized", table[102].Name);
        Assert.Equal("", table[3004136].Name);
        Assert.All(table.Values, item =>
        {
            Assert.Equal(0U, item.Level);
            Assert.Equal(0U, item.ForItemPosition);
            Assert.Same(item, ItemTable.Refinary[0][item.ItemID]);
        });
    }

    [Fact]
    public void ParenthesisRangeIncludesBothBoundsAndPrecedesTheSquareBracketRule()
    {
        using var fixture = new OwnedRifineryFile(Row("3006164", "Normal", "[Boots]tail") + "\n" +
            Row("3006165", "Normal", "(Shield)tail") + "\n" +
            Row("3006170", "Normal", "(Hossu)tail") + "\n" +
            Row("3006171", "Normal", "[Armor]tail") + "\n" +
            Row("3006166", "Normal", "[(Bow)]tail"));
        var table = new Rifinery();
        Assert.Equal("Boots", table[3006164].Name);
        Assert.Equal(8U, table[3006164].ForItemPosition);
        Assert.Equal("Shield", table[3006165].Name);
        Assert.Equal(5U, table[3006165].ForItemPosition);
        Assert.Equal("Hossu", table[3006170].Name);
        Assert.Equal(5U, table[3006170].ForItemPosition);
        Assert.Equal("Armor", table[3006171].Name);
        Assert.Equal(3U, table[3006171].ForItemPosition);
        Assert.Equal("[Bow", table[3006166].Name);
        Assert.Equal(0U, table[3006166].ForItemPosition);
    }

    [Fact]
    public void SquareRuleBeginsAtItsExactIdAndUsesTheFirstClosingBracket()
    {
        using var fixture = new OwnedRifineryFile(Row("3004135", "Normal", "[Ring]tail") + "\n" +
            Row("3004136", "Normal", "[[Ring]]tail") + "\n" +
            Row("3005000", "Normal", "[[Necklace]ignored"));
        var table = new Rifinery();
        Assert.Equal("[Ring]tail", table[3004135].Name);
        Assert.Equal(0U, table[3004135].ForItemPosition);
        Assert.Equal("Ring", table[3004136].Name);
        Assert.Equal(6U, table[3004136].ForItemPosition);
        Assert.Equal("Necklace", table[3005000].Name);
        Assert.Equal(2U, table[3005000].ForItemPosition);
    }

    [Fact]
    public void EveryPositionSpellingAndNullUseTheLiteralNativeRules()
    {
        using var fixture = new OwnedRifineryFile("");
        var table = new Rifinery();
        foreach (var name in new[] { "Bow", "2-Handed", "1-Handed", "Backsword", "2-handed", "1-handed" })
            Assert.Equal(4U, table.ForItemPosition(name));
        foreach (var name in new[] { "Shield", "Hossu" }) Assert.Equal(5U, table.ForItemPosition(name));
        foreach (var name in new[] { "Ring", "Bracelet" }) Assert.Equal(6U, table.ForItemPosition(name));
        Assert.Equal(3U, table.ForItemPosition("Armor"));
        Assert.Equal(8U, table.ForItemPosition("Boots"));
        Assert.Equal(1U, table.ForItemPosition("Headgear"));
        foreach (var name in new[] { "Necklace", "Bag" }) Assert.Equal(2U, table.ForItemPosition(name));
        foreach (var name in new[] { "bow", "armor", "Shielded", "", "unknown" })
            Assert.Equal(0U, table.ForItemPosition(name));
        Assert.Equal(0U, table.ForItemPosition(null!));
    }

    [Fact]
    public void RawDbLocationConcatenationDoesNotInsertADirectorySeparator()
    {
        using var fixture = new OwnedRifineryFile(Row("101", "Normal", "Bow"), rawPrefix: true);
        Assert.EndsWith("prefix", LegacyNativeServerConfig.DbLocation, StringComparison.Ordinal);
        Assert.EndsWith("prefixRifinery.txt", fixture.FilePath, StringComparison.Ordinal);
        var table = new Rifinery();
        Assert.Equal("Bow", table[101].Name);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "prefix")));
    }

    [Fact]
    public void ProviderlessParsingUsesCurrentCultureForAllFiveNumericTokens()
    {
        using var fixture = new OwnedRifineryFile(Row("p101", "Normal", "Bow", "p42", "p255", "p12", "p43"));
        var before = CultureInfo.CurrentCulture;
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        try
        {
            CultureInfo.CurrentCulture = culture;
            var item = new Rifinery()[101];
            Assert.Equal(101U, item.ItemID);
            Assert.Equal(42U, item.Procent);
            Assert.Equal((Rifinery.RefineryType)255, item.Type);
            Assert.Equal(Rifinery.RefineryType.FinalMAttack, item.Type2);
            Assert.Equal(43U, item.Procent2);
        }
        finally { CultureInfo.CurrentCulture = before; }
    }

    [Fact]
    public void MissingFileEscapesWithoutMutatingTheActualSharedCache()
    {
        using var fixture = new OwnedRifineryFile(null);
        var cache = ItemTable.Refinary;
        Assert.Throws<FileNotFoundException>(() => new Rifinery());
        Assert.Same(cache, ItemTable.Refinary);
        Assert.Empty(cache);
        Assert.False(File.Exists(fixture.FilePath));
    }

    [Fact]
    public void MissingNameColumnFaultKeepsEarlierRowsAndDoesNotCreateTheLaterBucket()
    {
        using var fixture = new OwnedRifineryFile(Row("101", "Normal", "Bow") + "\n102 Refined 7 1 2 9");
        Assert.Throws<IndexOutOfRangeException>(() => new Rifinery());
        Assert.Equal("Bow", ItemTable.Refinary[1][101].Name);
        Assert.False(ItemTable.Refinary.ContainsKey(2));
        Assert.Single(ItemTable.Refinary[1]);
    }

    [Fact]
    public void LaterNumericFaultKeepsEarlierAdmissionAndPreventsTheCallerAssignment()
    {
        using var fixture = new OwnedRifineryFile(Row("101", "Normal", "Bow") + "\n" +
            Row("102", "Refined", "Armor", "invalid"));
        Rifinery? completed = null;
        Assert.Throws<FormatException>(() => completed = new Rifinery());
        Assert.Null(completed);
        Assert.Equal("Bow", ItemTable.Refinary[1][101].Name);
        Assert.False(ItemTable.Refinary.ContainsKey(2));
        // C# caller assignment witness, not a fabricated Pool.RifineryItems host.
    }

    [Fact]
    public void DuplicateLocalIdStillParsesAndMayFaultBeforeAnyDuplicateGuard()
    {
        using var fixture = new OwnedRifineryFile(Row("101", "Normal", "Bow") + "\n" +
            Row("101", "Normal", "Armor", "invalid"));
        Assert.Throws<FormatException>(() => new Rifinery());
        Assert.Single(ItemTable.Refinary[1]);
        Assert.Equal("Bow", ItemTable.Refinary[1][101].Name);
    }

    [Fact]
    public void BlankCommentAndRepeatedSpaceRowsAreNeitherTrimmedNorSkipped()
    {
        var valid = Row("101", "Normal", "Bow");
        var badRows = new[] { "", "//comment", " " + valid,
            valid.Replace("101 Normal", "101  Normal", StringComparison.Ordinal) };
        foreach (var bad in badRows)
        {
            using var fixture = new OwnedRifineryFile(bad + "\n");
            Assert.Throws<FormatException>(() => new Rifinery());
            Assert.Empty(ItemTable.Refinary);
        }
    }

    [Fact]
    public void UnsignedAndEnumOverflowFaultBeforeNameLookupOrAnyAdmission()
    {
        foreach (var row in new[] { "4294967296 Normal 7 1 2 9", "-1 Normal 7 1 2 9",
                     "101 Normal 4294967296 1 2 9", "101 Normal 7 256 2 9",
                     "101 Normal 7 -1 2 9", "101 Normal 7 1 256 9", "101 Normal 7 1 2 4294967296" })
        {
            using var fixture = new OwnedRifineryFile(row);
            Assert.Throws<OverflowException>(() => new Rifinery());
            Assert.Empty(ItemTable.Refinary);
        }
    }

    [Fact]
    public void NullGlobalCacheAndNullBucketStatesFaultWithoutRepairingPriorEntries()
    {
        using (var fixture = new OwnedRifineryFile(Row("101", "Normal", "Bow")))
        {
            ItemTable.Refinary = null!;
            Assert.Throws<NullReferenceException>(() => new Rifinery());
            Assert.Null(ItemTable.Refinary);
        }
        using (var fixture = new OwnedRifineryFile(Row("101", "Normal", "Bow") + "\n" +
                   Row("102", "Refined", "Armor")))
        {
            var cache = ItemTable.Refinary;
            cache[2] = null!;
            Assert.Throws<NullReferenceException>(() => new Rifinery());
            Assert.Same(cache, ItemTable.Refinary);
            Assert.Null(cache[2]);
            Assert.Equal("Bow", cache[1][101].Name);
        }
        // Explicit mutable-field MODEL injection; not claimed native cold-start reachability.
    }

    [Fact]
    public void APreexistingNullCacheRowRemainsNullWhileTheLocalTableHasAConcreteItem()
    {
        using var fixture = new OwnedRifineryFile(Row("101", "Normal", "Bow"));
        var bucket = new Dictionary<uint, Rifinery.Item> { [101] = null! };
        ItemTable.Refinary[1] = bucket;
        var table = new Rifinery();
        Assert.Same(bucket, ItemTable.Refinary[1]);
        Assert.Null(bucket[101]);
        Assert.NotNull(table[101]);
        Assert.Equal("Bow", table[101].Name);
    }

    [Fact]
    public void ConstructionDoesNotPublishItemsBaseOrInvokeItsLoadingMethod()
    {
        using var fixture = new OwnedRifineryFile(Row("101", "Normal", "Bow"));
        var before = LegacyNativePoolItems.ItemsBase;
        var sentinel = new ItemTable();
        try
        {
            LegacyNativePoolItems.ItemsBase = sentinel;
            var table = new Rifinery();
            Assert.Same(sentinel, LegacyNativePoolItems.ItemsBase);
            Assert.Empty(sentinel);
            Assert.Equal("Bow", table[101].Name);
            Assert.False(File.Exists(Path.Combine(fixture.Root, "ItemAdd.ini")));
        }
        finally { LegacyNativePoolItems.ItemsBase = before; }
    }

    private static string Row(string id, string level, string name,
        string procent = "7", string type = "1", string type2 = "2", string procent2 = "9")
    {
        var tokens = Enumerable.Repeat("0", 54).ToArray();
        tokens[0] = id;
        tokens[1] = level;
        tokens[2] = procent;
        tokens[3] = type;
        tokens[4] = type2;
        tokens[5] = procent2;
        tokens[53] = name;
        return string.Join(' ', tokens);
    }

    private sealed class OwnedRifineryFile : IDisposable
    {
        private readonly string _beforeLocation;
        private readonly Dictionary<uint, Dictionary<uint, Rifinery.Item>> _beforeCache;
        private readonly byte[]? _seedBytes;
        public string Root { get; }
        public string FilePath { get; }

        public OwnedRifineryFile(string? text, bool rawPrefix = false)
        {
            _beforeLocation = LegacyNativeServerConfig.DbLocation;
            _beforeCache = ItemTable.Refinary;
            DirectoryInfo? workspace = new(AppContext.BaseDirectory);
            while (workspace is not null && !File.Exists(Path.Combine(workspace.FullName, "WolfConquer.sln")))
                workspace = workspace.Parent;
            if (workspace is null) throw new InvalidOperationException("A real workspace is required.");
            var parent = Path.GetFullPath(Path.Combine(workspace.FullName, ".artifacts"));
            NoReparse(parent);
            Root = Path.Combine(parent, "goal540-rifinery-owned-" + Guid.NewGuid().ToString("N"));
            if (Directory.Exists(Root) || File.Exists(Root)) throw new IOException("Owned root exists.");
            Directory.CreateDirectory(Root);
            NoReparse(Root);
            FilePath = Path.Combine(Root, rawPrefix ? "prefixRifinery.txt" : "Rifinery.txt");
            ValidateFile();
            if (File.Exists(FilePath) || Directory.Exists(FilePath)) throw new IOException("Owned file exists.");
            if (text is not null)
            {
                _seedBytes = new UTF8Encoding(false, true).GetBytes(text);
                using (var stream = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    stream.Write(_seedBytes);
                    stream.Flush(true);
                }
                Assert.Equal(_seedBytes, File.ReadAllBytes(FilePath));
            }
            // Mutate only after owned file preparation; production has no reset/factory.
            ItemTable.Refinary = new Dictionary<uint, Dictionary<uint, Rifinery.Item>>();
            LegacyNativeServerConfig.DbLocation = rawPrefix
                ? Path.Combine(Root, "prefix") : Root + Path.DirectorySeparatorChar;
        }

        private void ValidateFile()
        {
            var full = Path.GetFullPath(FilePath);
            if (!string.Equals(full, FilePath, StringComparison.OrdinalIgnoreCase) ||
                !full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Outside this owned root.");
            NoReparse(full);
        }

        private static void NoReparse(string path)
        {
            string? full = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(full))
            {
                try
                {
                    if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Reparse path refused.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                full = Path.GetDirectoryName(full);
            }
        }

        public void Dispose()
        {
            ItemTable.Refinary = _beforeCache;
            LegacyNativeServerConfig.DbLocation = _beforeLocation;
            NoReparse(Root);
            ValidateFile();
            foreach (var entry in Directory.EnumerateFileSystemEntries(Root))
                if (!string.Equals(entry, FilePath, StringComparison.OrdinalIgnoreCase) || Directory.Exists(entry))
                    throw new IOException("Unexpected owned contents preserved.");
            if (_seedBytes is null)
            {
                if (File.Exists(FilePath)) throw new IOException("Unexpected missing-control file preserved.");
            }
            else
            {
                var actual = File.ReadAllBytes(FilePath);
                if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(actual), SHA256.HashData(_seedBytes)))
                    throw new IOException("Changed owned bytes preserved.");
                File.Delete(FilePath);
            }
            Directory.Delete(Root, recursive: false);
        }
    }
}
