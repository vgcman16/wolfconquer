using System.Globalization;
using Jade = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;
using CollectionTable = WolfConquer.Core.Data.LegacyNativeDictCollectionTable;
using Lottery = WolfConquer.Core.Data.LegacyNativeDictLotteryTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeGearMetadataLoadTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void UnloadedGateRetainsTheExactCacheAndDoesNotReadOrLogAMalformedExistingFile(int kind)
    {
        using var f = new LoadFixture(false);
        var prior = LoadFixture.Current(kind);
        var row = LoadFixture.Seed(kind, 999);
        f.Write(kind, "not-a-number");

        f.Load(kind);

        Assert.Same(prior, LoadFixture.Current(kind));
        Assert.Same(row, prior[999u]);
        Assert.Equal(1, prior.Count);
        Assert.Equal("", f.Log);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void MissingFileAndMissingSeparatorLogTheRawPathAndKeepTheCacheWithoutReplacement(int kind, bool missingSeparator)
    {
        using var f = new LoadFixture();
        var prior = LoadFixture.Current(kind);
        var row = LoadFixture.Seed(kind, 999);
        if (missingSeparator) f.Write(kind, LoadFixture.Row(kind, 1));
        var prefix = missingSeparator ? f.DataDirectory : f.Prefix;

        f.Load(kind, prefix);

        Assert.Same(prior, LoadFixture.Current(kind));
        Assert.Same(row, prior[999u]);
        Assert.Equal("!File.Exists: " + prefix + LoadFixture.FileName(kind) + Environment.NewLine, f.Log);
        Assert.False(File.Exists(prefix + LoadFixture.FileName(kind)));
        Assert.Equal(missingSeparator, File.Exists(f.PathFor(kind)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EveryExistingFileLoadReplacesTheDictionaryWithoutClearingRetainedOldGraphs(int kind)
    {
        using var f = new LoadFixture();
        var original = LoadFixture.Current(kind);
        var originalRow = LoadFixture.Seed(kind, 999);
        f.Write(kind, LoadFixture.Row(kind, 1), LoadFixture.Row(kind, 2));

        f.Load(kind);

        var first = LoadFixture.Current(kind);
        var retainedRow = first[1u];
        Assert.NotSame(original, first);
        Assert.Equal(2, first.Count);
        Assert.False(first.Contains(999u));
        f.Write(kind, LoadFixture.Row(kind, 3));
        f.Load(kind);

        var current = LoadFixture.Current(kind);
        Assert.NotSame(first, current);
        Assert.Equal(1, current.Count);
        Assert.True(current.Contains(3u));
        Assert.Same(retainedRow, first[1u]);
        Assert.Equal(2, first.Count);
        Assert.Same(originalRow, original[999u]);
        Assert.Equal(1, original.Count);
        Assert.Equal("", f.Log);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void AnExistingEmptyFileStillPublishesAFreshEmptyDictionary(int kind)
    {
        using var f = new LoadFixture();
        var prior = LoadFixture.Current(kind);
        var retained = LoadFixture.Seed(kind, 999);
        f.Write(kind);

        f.Load(kind);

        Assert.NotSame(prior, LoadFixture.Current(kind));
        Assert.Equal(0, LoadFixture.Current(kind).Count);
        Assert.Same(retained, prior[999u]);
        Assert.Equal("", f.Log);
    }

    [Fact]
    public void DaoqiLoaderMapsAllFifteenColumnsAndKeepsTheLiveMutableRowAlias()
    {
        using var f = new LoadFixture();
        uint[] expected = [uint.MaxValue, 70000, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14];
        f.Write(0, string.Join("@@", expected));

        f.Load(0);

        var row = Jade.Info[uint.MaxValue];
        Assert.Equal(expected, LoadFixture.Values(0, row));
        Assert.Equal((Jade.Type)70000u, row.Type); // Native casts retain unknown enum values.
        row.TypeCombatGear = 91;
        Assert.Same(row, Jade.Info[uint.MaxValue]);
        Assert.Equal(91u, Jade.Info[uint.MaxValue].TypeCombatGear);
        Assert.Equal("", f.Log);
    }

    [Fact]
    public void CollectionLoaderMapsAllSevenColumnsIncludingNativeMixSpellingAndRawAttrEnum()
    {
        using var f = new LoadFixture();
        uint[] expected = [41, uint.MaxValue, 3, 4, 999, 65536, uint.MaxValue];
        f.Write(1, string.Join("@@", expected));

        f.Load(1);

        var row = CollectionTable.Info[41];
        Assert.Equal(expected, LoadFixture.Values(1, row));
        Assert.Equal((Jade.AttrType)999u, row.attr_type1);
        row.Mix = 97;
        Assert.Same(row, CollectionTable.Info[41]);
        Assert.Equal(97u, CollectionTable.Info[41].Mix);
        Assert.Equal("", f.Log);
    }

    [Fact]
    public void LotteryLoaderMapsAllSixColumnsWithoutRateOrTypeNormalization()
    {
        using var f = new LoadFixture();
        uint[] expected = [42, uint.MaxValue, 2, 3, 4, uint.MaxValue];
        f.Write(2, string.Join("@@", expected));

        f.Load(2);

        var row = Lottery.Info[42];
        Assert.Equal(expected, LoadFixture.Values(2, row));
        row.rate = 101;
        Assert.Same(row, Lottery.Info[42]);
        Assert.Equal(101u, Lottery.Info[42].rate);
        Assert.Equal("", f.Log);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EveryColumnUsesCurrentCultureConvertToUInt32(int kind)
    {
        using var f = new LoadFixture();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = culture;
        f.Write(kind, string.Join("@@", Enumerable.Range(1, LoadFixture.Columns(kind)).Select(i => "p" + i)));

        f.Load(kind);

        var row = LoadFixture.Current(kind)[1u]!;
        Assert.Equal(Enumerable.Range(1, LoadFixture.Columns(kind)).Select(i => (uint)i), LoadFixture.Values(kind, row));
        Assert.Same(culture, CultureInfo.CurrentCulture);
        Assert.Equal("", f.Log);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ExactDoubleAtAndLiteralSpaceSeparatorsRemoveEmptyEntriesAndIgnoreExtraColumns(int kind)
    {
        using var f = new LoadFixture();
        var fields = Enumerable.Range(1, LoadFixture.Columns(kind)).Select(i => i.ToString(CultureInfo.InvariantCulture));
        f.Write(kind, "@@  " + string.Join("@@@@  ", fields) + "@@not-a-number");

        f.Load(kind);

        Assert.Equal(Enumerable.Range(1, LoadFixture.Columns(kind)).Select(i => (uint)i),
            LoadFixture.Values(kind, LoadFixture.Current(kind)[1u]!));
        Assert.Equal(1, LoadFixture.Current(kind).Count);
        Assert.Equal("", f.Log);
    }

    [Theory]
    [InlineData(0, "blank", typeof(IndexOutOfRangeException))]
    [InlineData(1, "blank", typeof(IndexOutOfRangeException))]
    [InlineData(2, "blank", typeof(IndexOutOfRangeException))]
    [InlineData(0, "duplicate", typeof(ArgumentException))]
    [InlineData(1, "duplicate", typeof(ArgumentException))]
    [InlineData(2, "duplicate", typeof(ArgumentException))]
    [InlineData(0, "overflow", typeof(OverflowException))]
    [InlineData(1, "overflow", typeof(OverflowException))]
    [InlineData(2, "overflow", typeof(OverflowException))]
    [InlineData(0, "format", typeof(FormatException))]
    [InlineData(1, "format", typeof(FormatException))]
    [InlineData(2, "format", typeof(FormatException))]
    public void LateMalformedOrDuplicateRowsAreLoggedAndSwallowedAfterPriorPublication(int kind, string fault, Type exceptionType)
    {
        using var f = new LoadFixture();
        var prior = LoadFixture.Current(kind);
        var priorRow = LoadFixture.Seed(kind, 999);
        var fields = LoadFixture.Row(kind, 2).Split("@@");
        var bad = fault switch
        {
            "blank" => "",
            "duplicate" => "01@@" + string.Join("@@", fields.Skip(1)),
            "overflow" => string.Join("@@", fields.Take(fields.Length - 1).Append("4294967296")),
            "format" => string.Join("@@", fields.Take(fields.Length - 1).Append("bad")),
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };
        f.Write(kind, LoadFixture.Row(kind, 1), bad, LoadFixture.Row(kind, 3));

        Assert.Null(Record.Exception(() => f.Load(kind)));

        var current = LoadFixture.Current(kind);
        Assert.NotSame(prior, current);
        Assert.Equal(1, current.Count);
        Assert.Equal(Enumerable.Range(1, LoadFixture.Columns(kind)).Select(i => (uint)i), LoadFixture.Values(kind, current[1u]!));
        Assert.False(current.Contains(2u));
        Assert.False(current.Contains(3u));
        Assert.Same(priorRow, prior[999u]);
        Assert.Contains(exceptionType.FullName!, f.Log);
        Assert.DoesNotContain("!File.Exists:", f.Log);
    }

    [Theory]
    [InlineData(0, "\t")]
    [InlineData(1, "\t")]
    [InlineData(2, "\t")]
    [InlineData(0, "@")]
    [InlineData(1, "@")]
    [InlineData(2, "@")]
    public void EmbeddedTabAndSingleAtAreNotRecognizedAsDelimiters(int kind, string separator)
    {
        using var f = new LoadFixture();
        var fields = Enumerable.Range(1, LoadFixture.Columns(kind)).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray();
        f.Write(kind, fields[0] + separator + fields[1] + "@@" + string.Join("@@", fields.Skip(2)));

        Assert.Null(Record.Exception(() => f.Load(kind)));

        Assert.Equal(0, LoadFixture.Current(kind).Count);
        Assert.Contains(typeof(FormatException).FullName!, f.Log);
    }
}
