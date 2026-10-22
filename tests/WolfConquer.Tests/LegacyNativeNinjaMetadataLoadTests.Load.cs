using System.Globalization;
using Metadata = WolfConquer.Core.World.LegacyNativeNinjaFile;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaMetadataLoadTests
{
    [Fact]
    public void ColdLoadReadsAllPhysicalFieldsSkipsNativeColumnsAndDuplicatesColumnEleven()
    {
        using var files = new OwnedFiles();
        Metadata.gouyu_type = null!;
        Metadata.gouyu_immortals = null!;
        files.Seed(false, Row(false) + "@@ignored-extra-column");
        files.Seed(true, Row(true, 21) + "@@ignored-extra-column");
        Metadata.Load();
        Assert.Single(Metadata.gouyu_type);
        Assert.Single(Metadata.gouyu_immortals);
        AssertType(17);
        AssertImmortal(21);
        Assert.Empty(files.Output.ToString());
    }

    [Fact]
    public void EmptyFirstFileStillReplacesBothOwnersAndLoadsSecondFile()
    {
        using var files = new OwnedFiles();
        files.Seed(false, "");
        files.Seed(true, Row(true, 21));
        Metadata.Load();
        Assert.NotSame(files.OldTypes, Metadata.gouyu_type);
        Assert.Empty(Metadata.gouyu_type);
        Assert.NotSame(files.OldImmortals, Metadata.gouyu_immortals);
        AssertImmortal(21);
        Assert.Equal(123u, files.OldTypes[9001].Score);
        Assert.Equal(456u, files.OldImmortals[9002].MaxHP);
        Assert.Empty(files.Output.ToString());
    }

    [Fact]
    public void RepeatedLoadPublishesNewDictionariesAndRowsWithoutMutatingRetainedAliases()
    {
        using var files = new OwnedFiles();
        files.Seed(false, Row(false));
        files.Seed(true, Row(true, 21));
        Metadata.Load();
        var types = Metadata.gouyu_type;
        var immortals = Metadata.gouyu_immortals;
        var first = types[17];
        var second = immortals[21];
        first.Score = 700;
        second.MaxHP = 800;
        Metadata.Load();
        Assert.NotSame(types, Metadata.gouyu_type);
        Assert.NotSame(immortals, Metadata.gouyu_immortals);
        Assert.NotSame(first, Metadata.gouyu_type[17]);
        Assert.NotSame(second, Metadata.gouyu_immortals[21]);
        Assert.Equal(700u, first.Score);
        Assert.Equal(800u, second.MaxHP);
        AssertType(17);
        AssertImmortal(21);
        Assert.Empty(files.Output.ToString());
    }

    [Fact]
    public void LiteralDbLocationPrefixSelectsPrefixedFilesWithoutInsertingSeparator()
    {
        using var files = new OwnedFiles();
        files.Seed(false, Row(false));
        files.Seed(true, Row(true, 21));
        files.Seed(false, Row(false, 31), prefix: true);
        files.Seed(true, Row(true, 41), prefix: true);
        files.UseLocation(prefix: true);
        Metadata.Load();
        Assert.Equal(31u, Assert.Single(Metadata.gouyu_type).Key);
        Assert.Equal(41u, Assert.Single(Metadata.gouyu_immortals).Key);
        AssertType(31);
        AssertImmortal(41);
        Assert.Empty(files.Output.ToString());
    }

    [Fact]
    public void MissingFirstFileReplacesOnlyFirstOwnerAndReturnsBeforeSecondFile()
    {
        using var files = new OwnedFiles();
        files.Seed(true, Row(true, 21));
        Metadata.Load();
        Assert.NotSame(files.OldTypes, Metadata.gouyu_type);
        Assert.Empty(Metadata.gouyu_type);
        Assert.Same(files.OldImmortals, Metadata.gouyu_immortals);
        Assert.Equal(9002u, Assert.Single(Metadata.gouyu_immortals).Key);
        Assert.Equal("!File.Exists: " + files.PathName(false) + Environment.NewLine, files.Output.ToString());
    }

    [Fact]
    public void MissingSecondFileRetainsCompleteFirstTableAndPublishesEmptySecondOwner()
    {
        using var files = new OwnedFiles();
        files.Seed(false, Row(false));
        Metadata.Load();
        AssertType(17);
        Assert.NotSame(files.OldTypes, Metadata.gouyu_type);
        Assert.NotSame(files.OldImmortals, Metadata.gouyu_immortals);
        Assert.Empty(Metadata.gouyu_immortals);
        Assert.Equal("!File.Exists: " + files.PathName(true) + Environment.NewLine, files.Output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConcreteDuplicateAddThrowsAndKeepsOnlyTheCompletedPrefix(bool secondTable)
    {
        using var files = new OwnedFiles();
        files.Seed(false, secondTable ? Row(false) : Row(false) + "\n" + Row(false) + "\n" + Row(false, 19));
        files.Seed(true, secondTable ? Row(true, 21) + "\n" + Row(true, 21) + "\n" + Row(true, 23) : Row(true, 21));
        Assert.Throws<ArgumentException>(() => Metadata.Load());
        Assert.Single(Metadata.gouyu_type);
        AssertType(17);
        if (secondTable)
        {
            Assert.NotSame(files.OldImmortals, Metadata.gouyu_immortals);
            Assert.Single(Metadata.gouyu_immortals);
            AssertImmortal(21);
        }
        else Assert.Same(files.OldImmortals, Metadata.gouyu_immortals);
        Assert.Equal(123u, files.OldTypes[9001].Score);
        Assert.Empty(files.Output.ToString());
    }

    [Theory]
    [InlineData(false, "format")]
    [InlineData(true, "format")]
    [InlineData(false, "short")]
    [InlineData(true, "short")]
    [InlineData(false, "overflow")]
    [InlineData(true, "overflow")]
    public void LaterMalformedRecordThrowsBeforePublicationAndPreservesExactOwnerPrefix(bool secondTable, string fault)
    {
        using var files = new OwnedFiles();
        string[] tokens = secondTable ? ImmortalTokens(23) : TypeTokens(19);
        int last = tokens.Length - 1;
        Type exception;
        if (fault == "short")
        {
            tokens = tokens[..last];
            exception = typeof(IndexOutOfRangeException);
        }
        else
        {
            tokens[last] = fault == "format" ? "not-a-number" : "4294967296";
            exception = fault == "format" ? typeof(FormatException) : typeof(OverflowException);
        }
        string broken = string.Join("@@", tokens);
        files.Seed(false, secondTable ? Row(false) : Row(false) + "\n" + broken + "\n" + Row(false, 29));
        files.Seed(true, secondTable ? Row(true, 21) + "\n" + broken + "\n" + Row(true, 29) : Row(true, 21));
        Assert.Throws(exception, () => Metadata.Load());
        Assert.NotSame(files.OldTypes, Metadata.gouyu_type);
        Assert.Single(Metadata.gouyu_type);
        AssertType(17);
        if (secondTable)
        {
            Assert.NotSame(files.OldImmortals, Metadata.gouyu_immortals);
            Assert.Single(Metadata.gouyu_immortals);
            AssertImmortal(21);
        }
        else Assert.Same(files.OldImmortals, Metadata.gouyu_immortals);
        Assert.Empty(files.Output.ToString());
    }

    [Fact]
    public void SignedPowerAcceptsBothInt32ExtremesWithoutNarrowingOtherUInt32Fields()
    {
        using var files = new OwnedFiles();
        string[] first = TypeTokens(uint.MaxValue), second = TypeTokens(19);
        first[8] = "-2147483648";
        first[9] = first[11] = "4294967295";
        second[8] = "2147483647";
        files.Seed(false, string.Join("@@", first) + "\n" + string.Join("@@", second));
        files.Seed(true, "");
        Metadata.Load();
        Assert.Equal(2, Metadata.gouyu_type.Count);
        Assert.Equal(int.MinValue, Metadata.gouyu_type[uint.MaxValue].Power);
        Assert.Equal(int.MaxValue, Metadata.gouyu_type[19].Power);
        Assert.Equal(uint.MaxValue, Metadata.gouyu_type[uint.MaxValue].Damage);
        Assert.Equal(uint.MaxValue, Metadata.gouyu_type[uint.MaxValue].Score);
        Assert.Equal(uint.MaxValue, Metadata.gouyu_type[uint.MaxValue].UK5);
        Assert.Empty(Metadata.gouyu_immortals);
        Assert.Empty(files.Output.ToString());
    }

    [Theory]
    [InlineData("2147483648")]
    [InlineData("-2147483649")]
    public void SignedPowerOverflowStopsBeforePublishingRowOrReplacingSecondOwner(string power)
    {
        using var files = new OwnedFiles();
        string[] tokens = TypeTokens(19);
        tokens[8] = power;
        files.Seed(false, Row(false) + "\n" + string.Join("@@", tokens));
        files.Seed(true, Row(true, 21));
        Assert.Throws<OverflowException>(() => Metadata.Load());
        Assert.Single(Metadata.gouyu_type);
        AssertType(17);
        Assert.Same(files.OldImmortals, Metadata.gouyu_immortals);
        Assert.Empty(files.Output.ToString());
    }

    [Fact]
    public void CurrentCultureSignsAndMixedLiteralSpaceAtAtDelimitersAreUsedByBothTables()
    {
        using var files = new OwnedFiles();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        culture.NumberFormat.NegativeSign = "m";
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
        string[] first = TypeTokens().Select((value, index) => index == 10 ? value : index == 8 ? "m9" : "p" + value).ToArray();
        string[] second = ImmortalTokens().Select((value, index) => index is >= 9 and <= 11 ? value : "p" + value).ToArray();
        files.Seed(false, "@@ " + string.Join(" @@@@ ", first) + " @@");
        files.Seed(true, "@@ " + string.Join(" @@@@ ", second) + " @@");
        Metadata.Load();
        AssertType(17);
        AssertImmortal(21);
        Assert.Empty(files.Output.ToString());
    }

    [Fact]
    public void TabsAreNotTreatedAsRecordDelimiters()
    {
        using var files = new OwnedFiles();
        files.Seed(false, string.Join("\t", TypeTokens()));
        files.Seed(true, Row(true, 21));
        Assert.Throws<FormatException>(() => Metadata.Load());
        Assert.Empty(Metadata.gouyu_type);
        Assert.Same(files.OldImmortals, Metadata.gouyu_immortals);
        Assert.Empty(files.Output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualExclusiveFileLockThrowsAfterTheSelectedOwnerHasAlreadyBeenReplaced(bool secondTable)
    {
        using var files = new OwnedFiles();
        files.Seed(false, Row(false));
        files.Seed(true, Row(true, 21));
        using (var held = new FileStream(files.PathName(secondTable), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Throws<IOException>(() => Metadata.Load());
        Assert.NotSame(files.OldTypes, Metadata.gouyu_type);
        if (secondTable)
        {
            AssertType(17);
            Assert.NotSame(files.OldImmortals, Metadata.gouyu_immortals);
            Assert.Empty(Metadata.gouyu_immortals);
        }
        else
        {
            Assert.Empty(Metadata.gouyu_type);
            Assert.Same(files.OldImmortals, Metadata.gouyu_immortals);
        }
        Assert.Empty(files.Output.ToString());
    }
}
