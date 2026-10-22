using System.Globalization;
using System.Reflection;
using WolfConquer.Core.World;
using Cache = WolfConquer.Core.World.LegacyNativeBeastsAtrribute;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeBeastsAttributeTests
{
    [Fact]
    public void GenuineCacheAndNestedRowKeepNativeFieldNamesWidthsDefaultsAndReplacement()
    {
        using var scope = new FileScope();
        var row = new Cache.Attribute();
        var fields = typeof(Cache.Attribute).GetFields(BindingFlags.Public | BindingFlags.Instance);
        Assert.Equal(new[]
        {
            "Level", "RequiredPoints", "HPAdd", "PAttack", "MAttack", "PDefense", "MDefense",
            "FinalPDmgDealt", "FinalMDmgDealt", "FinalPDmgReceived", "FinalMDmgReceived", "YellowRuneSlots",
        }, fields.Select(field => field.Name));
        Assert.All(fields, field =>
        {
            Assert.Equal(field.Name is "Level" or "YellowRuneSlots" ? typeof(byte) : typeof(uint), field.FieldType);
            Assert.Equal(0ul, Convert.ToUInt64(field.GetValue(row)));
        });
        var cache = typeof(Cache).GetField("Attributes", BindingFlags.Public | BindingFlags.Static)!;
        Assert.Equal(typeof(Dictionary<byte, Cache.Attribute>), cache.FieldType);
        Assert.False(cache.IsInitOnly);
        var replacement = new Dictionary<byte, Cache.Attribute> { [1] = row };
        Cache.Attributes = replacement;
        Assert.Same(replacement, Cache.Attributes);
        Assert.Same(row, Cache.Attributes[1]);
    }

    [Fact]
    public void MissingPhysicalFileLeavesCurrentCacheAndExistingRowUntouched()
    {
        using var scope = new FileScope();
        var row = new Cache.Attribute { Level = 9, YellowRuneSlots = 3 };
        Cache.Attributes.Add(9, row);
        var before = Cache.Attributes;
        Cache.Load();
        Assert.Same(before, Cache.Attributes);
        Assert.Same(row, Assert.Single(Cache.Attributes).Value);
        Assert.Equal((byte)3, row.YellowRuneSlots);
    }

    [Fact]
    public void LiteralLoadPopulatesEveryNativeNamedFieldAndTokenSixteenSlots()
    {
        using var scope = new FileScope();
        scope.Write(RowText(level: "2", required: "100", slots: "4"));
        Cache.Load();
        var row = Cache.Attributes[2];
        Assert.Equal((byte)2, row.Level);
        Assert.Equal(100u, row.RequiredPoints);
        Assert.Equal(20u, row.HPAdd);
        Assert.Equal(30u, row.PAttack);
        Assert.Equal(40u, row.MAttack);
        Assert.Equal(50u, row.PDefense);
        Assert.Equal(60u, row.MDefense);
        Assert.Equal(70u, row.FinalPDmgDealt);
        Assert.Equal(80u, row.FinalMDmgDealt);
        Assert.Equal(90u, row.FinalPDmgReceived);
        Assert.Equal(100u, row.FinalMDmgReceived);
        Assert.Equal((byte)4, row.YellowRuneSlots);
        Assert.Single(Cache.Attributes);
    }

    [Fact]
    public void DuplicateLevelThrowsAfterEarlierAdmissionsWithoutResetOrOverwrite()
    {
        using var scope = new FileScope();
        var prior = new Cache.Attribute { Level = 9, RequiredPoints = 777 };
        Cache.Attributes.Add(9, prior);
        scope.Write(RowText("1", "10", "1"), RowText("2", "20", "2"), RowText("1", "999", "9"));
        var dictionary = Cache.Attributes;
        Assert.Throws<ArgumentException>(() => Cache.Load());
        Assert.Same(dictionary, Cache.Attributes);
        Assert.Same(prior, Cache.Attributes[9]);
        Assert.Equal(10u, Cache.Attributes[1].RequiredPoints);
        Assert.Equal((byte)1, Cache.Attributes[1].YellowRuneSlots);
        Assert.Equal(20u, Cache.Attributes[2].RequiredPoints);
        Assert.Equal(3, Cache.Attributes.Count);
    }

    [Fact]
    public void OverflowAtFinalSlotTokenDoesNotPublishTheFaultedLocalRow()
    {
        using var scope = new FileScope();
        scope.Write(RowText("1", "10", "1"), RowText("2", "20", "256"));
        Assert.Throws<OverflowException>(() => Cache.Load());
        Assert.Equal((byte)1, Assert.Single(Cache.Attributes).Key);
        Assert.False(Cache.Attributes.ContainsKey(2));
    }

    [Fact]
    public void ShortRecordRetainsNativeIndexFaultAndPreviouslyPublishedRows()
    {
        using var scope = new FileScope();
        scope.Write(RowText("1", "10", "1"), "2@@100");
        Assert.Throws<IndexOutOfRangeException>(() => Cache.Load());
        Assert.Equal((byte)1, Assert.Single(Cache.Attributes).Key);
    }

    [Fact]
    public void RemoveEmptyEntriesShiftsLaterTokensInsteadOfPreservingBlankFields()
    {
        using var scope = new FileScope();
        var fields = RowText("1", "10", "4").Split("@@").ToList();
        fields[11] = "";
        fields.Add("7");
        scope.Write(string.Join("@@", fields));
        Cache.Load();
        Assert.Equal((byte)7, Cache.Attributes[1].YellowRuneSlots);
    }

    [Fact]
    public void NativeUnsignedParsersUseTheCallingCurrentCulture()
    {
        using var scope = new FileScope();
        var previous = CultureInfo.CurrentCulture;
        var custom = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        custom.NumberFormat.PositiveSign = "p";
        try
        {
            CultureInfo.CurrentCulture = custom;
            var fields = RowText("1", "10", "4").Split("@@");
            foreach (var index in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 16 })
                fields[index] = "p" + fields[index];
            scope.Write(string.Join("@@", fields));
            Cache.Load();
            Assert.Equal(10u, Cache.Attributes[1].RequiredPoints);
            Assert.Equal((byte)4, Cache.Attributes[1].YellowRuneSlots);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ReplacingRealConfigPathBetweenLoadsUsesThatLiveFieldAndSameCache()
    {
        using var scope = new FileScope();
        scope.Write(RowText("1", "10", "1"));
        var dictionary = Cache.Attributes;
        Cache.Load();
        var first = Cache.Attributes[1];
        var secondPath = Path.Combine(scope.Root, "second");
        Directory.CreateDirectory(secondPath);
        File.WriteAllLines(Path.Combine(secondPath, "beasts_attr.txt"), [RowText("2", "20", "2")]);
        LegacyNativeServerConfig.DbLocation = secondPath + Path.DirectorySeparatorChar;
        Cache.Load();
        Assert.Same(dictionary, Cache.Attributes);
        Assert.Same(first, Cache.Attributes[1]);
        Assert.Equal((byte)2, Cache.Attributes[2].YellowRuneSlots);
        Assert.Equal(2, Cache.Attributes.Count);
    }

    [Fact]
    public void NullCacheIsNotReadForMissingFileAndDoesNotMaskEarlierParsingFault()
    {
        using var scope = new FileScope();
        Cache.Attributes = null!;
        Cache.Load();
        Assert.Null(Cache.Attributes);
        scope.Write(RowText("invalid", "10", "1"));
        Assert.Throws<FormatException>(() => Cache.Load());
        scope.Write(RowText("1", "10", "1"));
        Assert.Throws<NullReferenceException>(() => Cache.Load());
        Assert.Null(Cache.Attributes);
    }

    [Fact]
    public void LoaderDoesNotInventBlankOrCommentSkipping()
    {
        using var scope = new FileScope();
        scope.Write("");
        Assert.Throws<IndexOutOfRangeException>(() => Cache.Load());
        scope.Write("// native loader does not skip comments");
        Assert.Throws<FormatException>(() => Cache.Load());
        Assert.Empty(Cache.Attributes);
    }

    private static string RowText(string level, string required, string slots)
        => string.Join("@@", new[]
        {
            level, required, "20", "30", "40", "50", "60", "70", "80", "90", "100",
            "unused11", "unused12", "unused13", "unused14", "unused15", slots,
        });

    private sealed class FileScope : IDisposable
    {
        private readonly Dictionary<byte, Cache.Attribute> _previous = Cache.Attributes;
        private readonly string _previousLocation = LegacyNativeServerConfig.DbLocation;

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-native-beasts-cache-" + Guid.NewGuid().ToString("N"));

        public FileScope()
        {
            var replacement = new Dictionary<byte, Cache.Attribute>();
            Directory.CreateDirectory(Root);
            Cache.Attributes = replacement; // MODEL cache assignment, not a production reset.
            LegacyNativeServerConfig.DbLocation = Root + Path.DirectorySeparatorChar;
        }

        public void Write(params string[] rows) => File.WriteAllLines(Path.Combine(Root, "beasts_attr.txt"), rows);

        public void Dispose()
        {
            try
            {
                Cache.Attributes = _previous;
                LegacyNativeServerConfig.DbLocation = _previousLocation;
            }
            finally
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
        }
    }
}
