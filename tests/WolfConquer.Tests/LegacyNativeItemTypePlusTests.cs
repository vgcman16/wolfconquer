using System.Globalization;
using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNativeItemTypePlusTests
{
    [Fact]
    public void NativeFieldAndAliasStorageWidthsAreExact()
    {
        var expected = new Dictionary<string, Type>
        {
            [nameof(LegacyNativeItemTypePlus.ID)] = typeof(uint),
            [nameof(LegacyNativeItemTypePlus.Plus)] = typeof(byte),
            [nameof(LegacyNativeItemTypePlus.ItemHP)] = typeof(ushort),
            [nameof(LegacyNativeItemTypePlus.MinAttack)] = typeof(uint),
            [nameof(LegacyNativeItemTypePlus.MaxAttack)] = typeof(uint),
            [nameof(LegacyNativeItemTypePlus.PhysicalDefence)] = typeof(ushort),
            [nameof(LegacyNativeItemTypePlus.MagicAttack)] = typeof(ushort),
            [nameof(LegacyNativeItemTypePlus.MagicDefence)] = typeof(ushort),
            [nameof(LegacyNativeItemTypePlus.Agility)] = typeof(ushort),
            [nameof(LegacyNativeItemTypePlus.Dodge)] = typeof(byte)
        };
        var type = typeof(LegacyNativeItemTypePlus);
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
        Assert.Equal(10, fields.Length);
        foreach (var field in fields) Assert.Equal(expected[field.Name], field.FieldType);
        Assert.Equal(typeof(ushort), type.GetProperty(nameof(LegacyNativeItemTypePlus.Vigor))!.PropertyType);
        Assert.Equal(typeof(ushort), type.GetProperty(nameof(LegacyNativeItemTypePlus.SpeedPlus))!.PropertyType);
        Assert.Null(type.GetProperty(nameof(LegacyNativeItemTypePlus.Vigor))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(LegacyNativeItemTypePlus.SpeedPlus))!.SetMethod);
    }

    [Fact]
    public void FreshConcreteRowsHaveTenZeroFieldsAndNoSharedIdentity()
    {
        var first = new LegacyNativeItemTypePlus();
        var second = new LegacyNativeItemTypePlus();
        Assert.NotSame(first, second);
        Assert.Equal(new uint[10], Read(first));
        Assert.Equal((ushort)0, first.Vigor);
        Assert.Equal((ushort)0, first.SpeedPlus);
        first.ID = 19;
        Assert.Equal(0U, second.ID);
    }

    [Fact]
    public void MixedNativeSeparatorsRemoveEmptyEntriesAndKeepEveryField()
    {
        var row = Seed();
        row.Parse("@@1 @ @2@@3 4@5@@6 7@8@@9 10@@");
        Assert.Equal(ExpectedPrefix(10), Read(row));
    }

    [Fact]
    public void TabIsNotAnAdditionalFieldSeparator()
    {
        var row = Seed();
        Assert.Throws<FormatException>(() => row.Parse("1\t2@@3@@4@@5@@6@@7@@8@@9@@10"));
        Assert.Equal(ExpectedPrefix(0), Read(row));
    }

    [Fact]
    public void NullLineFaultsBeforeAnyFieldAssignment()
    {
        var row = Seed();
        Assert.Throws<NullReferenceException>(() => row.Parse(null!));
        Assert.Equal(ExpectedPrefix(0), Read(row));
    }

    [Fact]
    public void EmptyAndDelimiterOnlyLinesFaultBeforeAnyFieldAssignment()
    {
        foreach (var text in new[] { string.Empty, "@@ @@ @ @" })
        {
            var row = Seed();
            Assert.Throws<IndexOutOfRangeException>(() => row.Parse(text));
            Assert.Equal(ExpectedPrefix(0), Read(row));
        }
    }

    [Fact]
    public void EachMissingCellKeepsEveryEarlierAssignmentWithoutResettingTheSuffix()
    {
        for (var count = 0; count < 10; count++)
        {
            var row = Seed();
            var line = string.Join("@@", Tokens().Take(count));
            Assert.Throws<IndexOutOfRangeException>(() => row.Parse(line));
            Assert.Equal(ExpectedPrefix(count), Read(row));
        }
    }

    [Fact]
    public void EachNativeWidthOverflowKeepsOnlyItsEarlierAssignments()
    {
        var overflow = new[]
        {
            "4294967296", "256", "65536", "4294967296", "4294967296",
            "65536", "65536", "65536", "65536", "256"
        };
        for (var index = 0; index < overflow.Length; index++)
        {
            var row = Seed();
            var tokens = Tokens();
            tokens[index] = overflow[index];
            Assert.Throws<OverflowException>(() => row.Parse(string.Join("@@", tokens)));
            Assert.Equal(ExpectedPrefix(index), Read(row));
        }
    }

    [Fact]
    public void EachMalformedCellKeepsOnlyItsEarlierAssignments()
    {
        for (var index = 0; index < 10; index++)
        {
            var row = Seed();
            var tokens = Tokens();
            tokens[index] = "not-a-number!";
            Assert.Throws<FormatException>(() => row.Parse(string.Join("@@", tokens)));
            Assert.Equal(ExpectedPrefix(index), Read(row));
        }
    }

    [Fact]
    public void AllUnsignedMaximumValuesRetainTheirFullNativeBits()
    {
        var row = new LegacyNativeItemTypePlus();
        row.Parse("4294967295@@255@@65535@@4294967295@@4294967295@@65535@@65535@@65535@@65535@@255");
        Assert.Equal(new uint[]
        {
            uint.MaxValue, byte.MaxValue, ushort.MaxValue, uint.MaxValue, uint.MaxValue,
            ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, byte.MaxValue
        }, Read(row));
        Assert.Equal(ushort.MaxValue, row.Vigor);
        Assert.Equal((ushort)byte.MaxValue, row.SpeedPlus);
    }

    [Fact]
    public void SurplusMalformedCellsAreNeverParsed()
    {
        var row = Seed();
        row.Parse(string.Join("@@", Tokens()) + "@@not-a-number!@@4294967296");
        Assert.Equal(ExpectedPrefix(10), Read(row));
    }

    [Fact]
    public void RepeatedParseMutatesTheSameReferenceVisibleThroughItsArrayAlias()
    {
        var row = Seed();
        var alias = row;
        var plusArray = new[] { row };
        row.Parse(string.Join("@@", Tokens()));
        Assert.Same(alias, plusArray[0]);
        Assert.Equal(ExpectedPrefix(10), Read(alias));
        Assert.Throws<IndexOutOfRangeException>(() => plusArray[0].Parse("21@@22"));
        var expected = ExpectedPrefix(10);
        expected[0] = 21;
        expected[1] = 22;
        Assert.Equal(expected, Read(alias));
    }

    [Fact]
    public void NumericParsingUsesTheCurrentCultureRatherThanInvariantCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
        culture.NumberFormat.PositiveSign = "p";
        try
        {
            CultureInfo.CurrentCulture = culture;
            var row = Seed();
            row.Parse(string.Join("@@", Tokens().Select(value => "p" + value)));
            Assert.Equal(ExpectedPrefix(10), Read(row));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void NumericTokensKeepTheNativeLeadingAndTrailingTabWhitespaceRules()
    {
        var row = Seed();
        row.Parse(string.Join("@@", Tokens().Select(value => "\t" + value + "\t")));
        Assert.Equal(ExpectedPrefix(10), Read(row));
    }

    [Fact]
    public void NegativeIdOverflowsInsteadOfClampingOrFallingBack()
    {
        var row = Seed();
        Assert.Throws<OverflowException>(() => row.Parse("-1@@2@@3@@4@@5@@6@@7@@8@@9@@10"));
        Assert.Equal(ExpectedPrefix(0), Read(row));
    }

    [Fact]
    public void VigorAndSpeedPlusAlwaysReadTheLiveBackingFields()
    {
        var row = new LegacyNativeItemTypePlus { Agility = 65535, Dodge = 255 };
        Assert.Equal((ushort)65535, row.Vigor);
        Assert.Equal((ushort)255, row.SpeedPlus);
        row.Agility = 7;
        row.Dodge = 3;
        Assert.Equal((ushort)7, row.Vigor);
        Assert.Equal((ushort)3, row.SpeedPlus);
    }

    private static LegacyNativeItemTypePlus Seed() => new()
    {
        ID = 99, Plus = 99, ItemHP = 99, MinAttack = 99, MaxAttack = 99,
        PhysicalDefence = 99, MagicAttack = 99, MagicDefence = 99, Agility = 99, Dodge = 99
    };

    private static string[] Tokens() => Enumerable.Range(1, 10)
        .Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray();

    private static uint[] ExpectedPrefix(int count) => Enumerable.Range(0, 10)
        .Select(index => index < count ? (uint)(index + 1) : 99U).ToArray();

    private static uint[] Read(LegacyNativeItemTypePlus row) => new uint[]
    {
        row.ID, row.Plus, row.ItemHP, row.MinAttack, row.MaxAttack,
        row.PhysicalDefence, row.MagicAttack, row.MagicDefence, row.Agility, row.Dodge
    };
}
