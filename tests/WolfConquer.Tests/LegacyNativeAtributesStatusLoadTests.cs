using System.Globalization;
using WolfConquer.Core.World;
using Table = WolfConquer.Core.World.LegacyNativeAtributesStatus;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeAtributesStatusLoadTests
{
    [Fact]
    public void AllTenLiteralPrefixesMapToNativeFamiliesAndPreserveFileAndColumnOrder()
    {
        using var f = new LoadFixture();
        (string Prefix, uint Family)[] families =
        [
            ("Monk", 6005), ("Archer", 4005), ("Ninja", 5005), ("Taoist", 10000),
            ("Trojan", 1005), ("Warrior", 2005), ("Pirate", 7005), ("LongLee", 8005),
            ("WindWalker", 16005), ("Thunderstriker", 9005)
        ];
        f.WriteRows(families.Select((family, i) => $"{family.Prefix}[{i}]={i + 1},{i + 101},{i + 201},{i + 301}").ToArray());

        f.Table.Load();

        Assert.Equal(families.Select(family => family.Family), f.Table.Keys);
        for (var i = 0; i < families.Length; i++)
        {
            var inner = f.Table[families[i].Family];
            Assert.Equal((ushort)i, Assert.Single(inner).Key);
            AssertRow(inner[(ushort)i], i + 1, i + 101, i + 201, i + 301);
        }
    }

    [Fact]
    public void InstantDefaultsAreZeroMutableFieldsAndLoadedLevelKeysUseTheFullByteRange()
    {
        using var f = new LoadFixture();
        AssertRow(new Table.Instant(), 0, 0, 0, 0);
        f.WriteRows("Monk[0]=0,65535,1,65534", "Monk[255]=65535,0,65534,1");

        f.Table.Load();

        Assert.Equal(new ushort[] { 0, 255 }, f.Table[6005].Keys);
        AssertRow(f.Table[6005][0], 0, 65535, 1, 65534);
        var row = f.Table[6005][255];
        AssertRow(row, 65535, 0, 65534, 1);
        row.Spirit = 17;
        Assert.Same(row, f.Table[6005][255]);
        Assert.Equal((ushort)17, f.Table[6005][255].Spirit);
    }

    [Fact]
    public void LevelAndEveryUshortUseTheCurrentCulturesIntegerParsingWithoutInvariantNormalization()
    {
        using var f = new LoadFixture();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = culture;
        f.WriteRows("Monk[p7]=p1,p2,p3,p4", "Archer[ 8 ]= 5 , 6 , 7 , 8 ");

        f.Table.Load();

        AssertRow(f.Table[6005][7], 1, 2, 3, 4);
        AssertRow(f.Table[4005][8], 5, 6, 7, 8);
        Assert.Same(culture, CultureInfo.CurrentCulture);
    }

    [Fact]
    public void UnknownLowercaseLeadingSpaceAndCommentLinesAreIgnoredWithoutAttemptingTheirMalformedValues()
    {
        using var f = new LoadFixture();
        f.WriteRows("", "Unknown[broken]=bad", "monk[broken]=bad", "MONK[broken]=bad",
            " Monk[broken]=bad", "\tNinja[broken]=bad", ";Archer[broken]=bad", "#Trojan[broken]=bad",
            "ThunderStriker[broken]=bad", "Windwalker[broken]=bad", "Monk[3]=1,2,3,4");

        f.Table.Load();

        Assert.Equal(6005u, Assert.Single(f.Table).Key);
        Assert.Equal((ushort)3, Assert.Single(f.Table[6005]).Key);
        AssertRow(f.Table[6005][3], 1, 2, 3, 4);
    }

    [Fact]
    public void LiteralOffsetsDiscardAnyPrefixAndPayloadMarkerAndExtraSplitTokensAreIgnored()
    {
        using var f = new LoadFixture();
        f.WriteRows("MonkX7]Q1,2,3,4", "Monk[8]=5,6,7,8,not-a-number",
            "Monk[9]=9,10,11,12]ignored", "Monk[10]=1,000,3,4");

        f.Table.Load();

        Assert.Equal(new ushort[] { 7, 8, 9, 10 }, f.Table[6005].Keys);
        AssertRow(f.Table[6005][7], 1, 2, 3, 4);
        AssertRow(f.Table[6005][8], 5, 6, 7, 8);
        AssertRow(f.Table[6005][9], 9, 10, 11, 12);
        AssertRow(f.Table[6005][10], 1, 0, 3, 4); // Comma is always a field separator, not a thousands marker.
    }

    [Theory]
    [InlineData("Monk[256]=bad,2,3,4", typeof(OverflowException))]
    [InlineData("Monk[-1]=1,2,3,4", typeof(OverflowException))]
    [InlineData("Monk[]=1,2,3,4", typeof(FormatException))]
    [InlineData("Monk[2]=65536,bad,3,4", typeof(OverflowException))]
    [InlineData("Monk[2]=1,65536,bad,4", typeof(OverflowException))]
    [InlineData("Monk[2]=1,2,65536,bad", typeof(OverflowException))]
    [InlineData("Monk[2]=1,2,3,65536", typeof(OverflowException))]
    [InlineData("Monk[2]=1,2,3,-1", typeof(OverflowException))]
    [InlineData("Monk[2]=1,2,3,bad", typeof(FormatException))]
    [InlineData("Monk[2]=1.5,2,3,4", typeof(FormatException))]
    [InlineData("Monk[2]=1,,3,4", typeof(FormatException))]
    [InlineData("Monk[2]=1,2,3", typeof(IndexOutOfRangeException))]
    [InlineData("Monk[2", typeof(IndexOutOfRangeException))]
    [InlineData("Monk[2]", typeof(ArgumentOutOfRangeException))]
    [InlineData("Monk", typeof(ArgumentOutOfRangeException))]
    [InlineData("MonkExtra[2]=1,2,3,4", typeof(FormatException))]
    public void MalformedLateRowRetainsEarlierPublicationAndPriorAliasesWithoutPublishingTheBadRow(string bad, Type errorType)
    {
        using var f = new LoadFixture();
        var priorRow = new Table.Instant { Strenght = 77 };
        var priorInner = new Dictionary<ushort, Table.Instant> { [0] = priorRow };
        f.Table[6005] = priorInner;
        f.WriteRows("Archer[1]=11,12,13,14", bad, "Taoist[1]=21,22,23,24");

        var error = Record.Exception(f.Table.Load);

        Assert.NotNull(error);
        Assert.Equal(errorType, error.GetType());
        Assert.Equal(2, f.Table.Count);
        Assert.Same(priorInner, f.Table[6005]);
        Assert.Same(priorRow, Assert.Single(priorInner).Value);
        AssertRow(priorRow, 77, 0, 0, 0);
        AssertRow(f.Table[4005][1], 11, 12, 13, 14);
        Assert.False(f.Table.ContainsKey(10000));
    }

    [Fact]
    public void DuplicateNumericLevelAliasesThrowAfterEarlierRowsWithoutReplacingTheFirstRow()
    {
        using var f = new LoadFixture();
        f.WriteRows("Archer[1]=11,12,13,14", "Monk[7]=1,2,3,4", "Monk[007]=9,8,7,6", "Taoist[1]=5,6,7,8");

        Assert.Throws<ArgumentException>(f.Table.Load);

        Assert.Equal(new uint[] { 4005, 6005 }, f.Table.Keys);
        AssertRow(f.Table[6005][7], 1, 2, 3, 4);
        Assert.Single(f.Table[6005]);
        AssertRow(f.Table[4005][1], 11, 12, 13, 14);
        Assert.False(f.Table.ContainsKey(10000));
    }

    [Fact]
    public void RepeatedLoadDoesNotClearAndStopsAtTheFirstDuplicateAfterNewEarlierRows()
    {
        using var f = new LoadFixture();
        f.WriteRows("Monk[7]=1,2,3,4");
        f.Table.Load();
        var inner = f.Table[6005];
        var row = inner[7];
        f.WriteRows("Archer[1]=11,12,13,14", "Monk[7]=9,8,7,6", "Taoist[1]=5,6,7,8");

        Assert.Throws<ArgumentException>(f.Table.Load);

        Assert.Same(inner, f.Table[6005]);
        Assert.Same(row, f.Table[6005][7]);
        AssertRow(row, 1, 2, 3, 4);
        AssertRow(f.Table[4005][1], 11, 12, 13, 14);
        Assert.False(f.Table.ContainsKey(10000));
    }

    [Theory]
    [InlineData(false, typeof(NullReferenceException))]
    [InlineData(true, typeof(FormatException))]
    public void NullExistingInnerDictionaryIsNotRepairedAndRowParsingPrecedesItsPublicationFault(bool badSpirit, Type errorType)
    {
        using var f = new LoadFixture();
        f.Table[6005] = null!;
        f.WriteRows("Archer[1]=11,12,13,14", badSpirit ? "Monk[7]=1,2,3,bad" : "Monk[7]=1,2,3,4",
            "Taoist[1]=5,6,7,8");

        var error = Record.Exception(f.Table.Load);

        Assert.NotNull(error);
        Assert.Equal(errorType, error.GetType());
        Assert.Null(f.Table[6005]);
        AssertRow(f.Table[4005][1], 11, 12, 13, 14);
        Assert.False(f.Table.ContainsKey(10000));
    }

    [Fact]
    public void LoadingUsesTheCurrentInnerDictionaryAndKeepsUnrelatedEntriesAndRetainedOldAliases()
    {
        using var f = new LoadFixture();
        f.WriteRows("Monk[7]=1,2,3,4");
        f.Table.Load();
        var old = f.Table[6005];
        var oldRow = old[7];
        var retained = new Table.Instant { Spirit = 88 };
        var current = new Dictionary<ushort, Table.Instant> { [8] = retained };
        var unrelated = new Dictionary<ushort, Table.Instant> { [ushort.MaxValue] = oldRow };
        f.Table[6005] = current;
        f.Table[99999] = unrelated;
        f.WriteRows("Monk[9]=9,10,11,12");

        f.Table.Load();

        Assert.Same(current, f.Table[6005]);
        Assert.Same(retained, current[8]);
        AssertRow(current[9], 9, 10, 11, 12);
        Assert.Same(unrelated, f.Table[99999]);
        Assert.Same(oldRow, Assert.Single(old).Value);
        current[9].Strenght = 123;
        Assert.Equal((ushort)123, f.Table[6005][9].Strenght);
        AssertRow(oldRow, 1, 2, 3, 4);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingFileAndMissingSeparatorUseTheRawConcatenatedPathWithoutCreatingOrClearing(bool missingSeparator)
    {
        using var f = new LoadFixture();
        var retained = new Dictionary<ushort, Table.Instant> { [1] = new() };
        f.Table[6005] = retained;
        if (missingSeparator)
        {
            f.WriteRows("Monk[2]=1,2,3,4");
            LegacyNativeServerConfig.DbLocation = f.DataDirectory;
        }
        var expectedPath = LegacyNativeServerConfig.DbLocation + "Stats.ini";

        var error = Assert.Throws<FileNotFoundException>(f.Table.Load);

        Assert.Equal(expectedPath, error.FileName);
        Assert.False(File.Exists(expectedPath));
        Assert.Same(retained, Assert.Single(f.Table).Value);
        Assert.Single(retained);
        Assert.Equal(missingSeparator, File.Exists(f.StatsPath));
    }

    [Fact]
    public void MissingDirectoryFaultsBeforeReadingAnyRowsWithoutPublishingOrCreatingIt()
    {
        using var f = new LoadFixture();
        var missing = Path.Combine(f.Root, "missing");
        LegacyNativeServerConfig.DbLocation = missing + Path.DirectorySeparatorChar;

        Assert.Throws<DirectoryNotFoundException>(f.Table.Load);

        Assert.Empty(f.Table);
        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public void EmptyFileIsANoOpRatherThanAnImplicitTableReset()
    {
        using var f = new LoadFixture();
        var retained = new Dictionary<ushort, Table.Instant> { [ushort.MaxValue] = new() { Spirit = 91 } };
        f.Table[99999] = retained;
        f.WriteRows();

        f.Table.Load();

        Assert.Same(retained, Assert.Single(f.Table).Value);
        Assert.Equal((ushort)91, retained[ushort.MaxValue].Spirit);
    }

    private static void AssertRow(Table.Instant row, int strength, int vitality, int agility, int spirit)
    {
        Assert.Equal((ushort)strength, row.Strenght);
        Assert.Equal((ushort)vitality, row.Vitality);
        Assert.Equal((ushort)agility, row.Agility);
        Assert.Equal((ushort)spirit, row.Spirit);
    }
}
