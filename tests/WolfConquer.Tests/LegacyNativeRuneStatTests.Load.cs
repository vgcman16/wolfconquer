using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using RuneTable = WolfConquer.Core.Data.LegacyNativeRuneTable;
using Stat = WolfConquer.Core.Data.LegacyNativeRuneTable.RuneAttribute;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeRuneStatTests
{
    [Fact]
    public void LoadParsesAllThreeNativeFilesWithLiteralSlotsWidthsAndDelimiterRules()
    {
        using var scope = new Scope(); using var files = new Files();
        files.Write("rune_storage_attr.txt", "50@@500@@1@@4294967295@@14@@99@@255@@77@@ ignored\r\n3 0 0 4 2 8 3 9");
        files.Write("rune_levexp.txt", "90@@403@@255@@4294967295@@ ignored\r\n1 402 27 850");
        files.Write("xuanbao_rand_attr.txt", "7000000@@255@@4294967295@@1@@ ignored\r\n7000001 6 100 221");
        RuneTable.Load();
        Assert.Equal(new uint[] { 50, 3 }, RuneTable.Attributes.Keys);
        var attribute = RuneTable.Attributes[50];
        Assert.Equal(500u, attribute.Score); Assert.Equal(3, attribute.AttributeValue!.Length);
        Assert.Equal(new[] { Stat.HPAdd, Stat.Counteraction, (Stat)255 }, attribute.AttributeValue.Select(value => value.Type));
        Assert.Equal(new uint[] { uint.MaxValue, 99, 77 }, attribute.AttributeValue.Select(value => value.Value));
        var exp = RuneTable.RuneLevelEXPList[90];
        Assert.Equal(403u, exp.ItemType); Assert.Equal((byte)255, exp.Level); Assert.Equal(uint.MaxValue, exp.RequiredProgress);
        Assert.Equal(new uint[] { 90, 1 }, RuneTable.RuneLevelEXPList.Keys);
        Assert.Equal(2, RuneTable.RandomAttributes.Count);
        var first = RuneTable.RandomAttributes[0];
        Assert.Equal(7000000u, first.ItemID); Assert.Equal((LegacyNativeChiAttribute)255, first.Attribute);
        Assert.Equal(uint.MaxValue, first.Min); Assert.Equal(0u, first.Max); Assert.True(first.dwParam);
        Assert.Equal(220u, RuneTable.RandomAttributes[1].Max); Assert.True(RuneTable.RandomAttributes[1].dwParam);
        Assert.Equal(files.Location, LegacyNativeServerConfig.DbLocation);
    }

    [Theory]
    [InlineData(0u, 0u, false)] [InlineData(1u, 0u, true)]
    [InlineData(10u, 10u, false)] [InlineData(11u, 10u, true)]
    [InlineData(21u, 20u, true)] [InlineData(99u, 99u, false)]
    [InlineData(4294967295u, 4294967295u, false)]
    public void RandomLoadUsesOnlyExactLastDecimalDigitAndNoMinMaxNormalization(uint maximum, uint expected, bool parameter)
    {
        using var scope = new Scope(); using var files = new Files();
        files.Write("xuanbao_rand_attr.txt", "7 6 4294967295 " + maximum);
        RuneTable.LoadRandoms();
        var row = Assert.Single(RuneTable.RandomAttributes);
        Assert.Equal(expected, row.Max); Assert.Equal(parameter, row.dwParam); Assert.Equal(uint.MaxValue, row.Min);
    }

    [Fact]
    public void MissingFilesRetainAllPhysicalOwnersIncludingNullAndDoNotInsertPathSeparator()
    {
        using var scope = new Scope(); using var files = new Files();
        var exp = RuneTable.RuneLevelEXPList; var attributes = RuneTable.Attributes; var randoms = RuneTable.RandomAttributes;
        exp[1] = Exp(403, 9, 1); attributes[1] = Attribute(0, (Stat.HPAdd, 1)); randoms.Add(new());
        RuneTable.Load();
        Assert.Same(exp, RuneTable.RuneLevelEXPList); Assert.Same(attributes, RuneTable.Attributes); Assert.Same(randoms, RuneTable.RandomAttributes);
        Assert.Single(exp); Assert.Single(attributes); Assert.Single(randoms);
        files.Write("rune_levexp.txt", "2 403 9 99");
        LegacyNativeServerConfig.DbLocation = files.Root;
        RuneTable.LoadRuneEXP(); Assert.Single(exp);
        RuneTable.Attributes = null!; RuneTable.RuneLevelEXPList = null!; RuneTable.RandomAttributes = null!;
        RuneTable.Load();
        Assert.Null(RuneTable.Attributes); Assert.Null(RuneTable.RuneLevelEXPList); Assert.Null(RuneTable.RandomAttributes);
    }

    [Fact]
    public void ExperienceLoadDuplicateAndLaterMalformedRowsRetainEarlierPublishedReferences()
    {
        using var scope = new Scope(); using var files = new Files();
        var previous = Exp(402, 1, 9); RuneTable.RuneLevelEXPList[99] = previous;
        files.Write("rune_levexp.txt", "50 403 9 37\r\n3 402 27 11\r\n50 403 1 100\r\n4 402 1 77");
        Assert.Throws<ArgumentException>(() => RuneTable.LoadRuneEXP());
        Assert.Same(previous, RuneTable.RuneLevelEXPList[99]);
        Assert.Equal(new uint[] { 99, 50, 3 }, RuneTable.RuneLevelEXPList.Keys);
        Assert.Equal(37u, RuneTable.RuneLevelEXPList[50].RequiredProgress);
        files.Write("rune_levexp.txt", "10 403 1 4\r\n11 403 bad 5\r\n12 403 1 7");
        Assert.Throws<FormatException>(() => RuneTable.LoadRuneEXP());
        Assert.True(RuneTable.RuneLevelEXPList.ContainsKey(10)); Assert.False(RuneTable.RuneLevelEXPList.ContainsKey(11));
        using var exclusive = File.Open(Path.Combine(files.Root, "rune_levexp.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void AttributeLoadParsesEntireCurrentRowBeforeDuplicatePublicationAndKeepsEarlierRows()
    {
        using var scope = new Scope(); using var files = new Files();
        var retained = Attribute(0, (Stat.HPAdd, 99)); RuneTable.Attributes[50] = retained;
        files.Write("rune_storage_attr.txt", "3 0 1 7 0 0 0 0\r\n50 0 1 8 0 0 0 bad");
        Assert.Throws<FormatException>(() => RuneTable.LoadRuneAttributes()); //Not duplicate-key yet.
        Assert.Same(retained, RuneTable.Attributes[50]); Assert.Equal(7u, RuneTable.Attributes[3].AttributeValue![0].Value);
        files.Write("rune_storage_attr.txt", "50 0 1 8 0 0 0 0");
        Assert.Throws<ArgumentException>(() => RuneTable.LoadRuneAttributes());
        Assert.Same(retained, RuneTable.Attributes[50]);
    }

    [Fact]
    public void RandomLoadAppendsDuplicateIdsAndRetainsPrefixOnLaterParseFailure()
    {
        using var scope = new Scope(); using var files = new Files();
        var retained = new RuneTable.RandomAttribute { ItemID = 7, Max = 9 };
        RuneTable.RandomAttributes.Add(retained);
        files.Write("xuanbao_rand_attr.txt", "7 6 10 11\r\n7 6 20 31\r\n8 256 30 50\r\n9 6 1 2");
        Assert.Throws<OverflowException>(() => RuneTable.LoadRandoms());
        Assert.Equal(3, RuneTable.RandomAttributes.Count); Assert.Same(retained, RuneTable.RandomAttributes[0]);
        Assert.Equal(new uint[] { 7, 7, 7 }, RuneTable.RandomAttributes.Select(row => row.ItemID));
        Assert.Equal(new uint[] { 9, 10, 30 }, RuneTable.RandomAttributes.Select(row => row.Max));
        Assert.All(RuneTable.RandomAttributes.Skip(1), row => Assert.True(row.dwParam));
    }

    [Theory]
    [InlineData("rune_levexp.txt", "\r\n", typeof(IndexOutOfRangeException))]
    [InlineData("rune_levexp.txt", "7 403 256 5", typeof(OverflowException))]
    [InlineData("rune_levexp.txt", "7 403 1 -1", typeof(OverflowException))]
    [InlineData("rune_storage_attr.txt", "7 0 256 1 0 0 0 0", typeof(OverflowException))]
    [InlineData("rune_storage_attr.txt", "7 0 1 1 0 0 0", typeof(IndexOutOfRangeException))]
    [InlineData("xuanbao_rand_attr.txt", "7 6 1", typeof(IndexOutOfRangeException))]
    public void LoadersDoNotSkipBlankOrShortRowsOrRelaxUnsignedByteWidths(string name, string text, Type exception)
    {
        using var scope = new Scope(); using var files = new Files(); files.Write(name, text);
        Action loader = name switch
        {
            "rune_levexp.txt" => RuneTable.LoadRuneEXP,
            "rune_storage_attr.txt" => RuneTable.LoadRuneAttributes,
            _ => RuneTable.LoadRandoms,
        };
        Assert.Throws(exception, loader);
        Assert.Empty(RuneTable.RuneLevelEXPList); Assert.Empty(RuneTable.Attributes); Assert.Empty(RuneTable.RandomAttributes);
    }

    [Fact]
    public void LoadOrderIsAttributesThenExperienceThenRandomAndDoesNotRollbackEarlierPublication()
    {
        using var scope = new Scope(); using var files = new Files();
        files.Write("rune_storage_attr.txt", "7 500 1 4 0 0 0 0");
        files.Write("rune_levexp.txt", "7 403 bad 5");
        files.Write("xuanbao_rand_attr.txt", "7 6 1 2");
        Assert.Throws<FormatException>(() => RuneTable.Load());
        Assert.Single(RuneTable.Attributes); Assert.Empty(RuneTable.RuneLevelEXPList); Assert.Empty(RuneTable.RandomAttributes);
        files.Write("rune_levexp.txt", "7 403 9 5");
        Assert.Throws<ArgumentException>(() => RuneTable.Load()); //Attributes duplicate comes before fixed experience.
        Assert.Empty(RuneTable.RuneLevelEXPList); Assert.Empty(RuneTable.RandomAttributes);
    }

    [Fact]
    public void NativeRowDefaultsAndEnumValuesAreNotManagedCatalogProjection()
    {
        using var scope = new Scope();
        var row = new RuneTable.Attribute();
        Assert.Equal(0u, row.Score); Assert.Null(row.AttributeValue);
        var exp = new RuneTable.RuneLevelEXP(); Assert.Equal(0u, exp.ItemType); Assert.Equal((byte)0, exp.Level); Assert.Equal(0u, exp.RequiredProgress);
        var random = new RuneTable.RandomAttribute(); Assert.Equal(LegacyNativeChiAttribute.None, random.Attribute); Assert.False(random.dwParam);
        Assert.Equal(new[] { 0, 1, 2, 3, 10, 11, 12, 13, 14 }, Enum.GetValues<Stat>().Select(value => (int)value));
        Assert.NotSame(RuneTable.Attributes, RuneTable.RuneLevelEXPList);
    }
}
