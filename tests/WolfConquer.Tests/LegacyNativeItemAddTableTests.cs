using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNativeItemAddTableTests
{
    [Fact]
    public void EmptyInputKeepsTheSameCallerAllocatedEmptyDictionary()
    {
        var dictionary = new Dictionary<uint, LegacyNativeItemTypePlus[]>();
        var alias = dictionary;
        LegacyNativeItemAddTable.PopulateRows(dictionary, Array.Empty<string>());
        Assert.Same(alias, dictionary);
        Assert.Empty(dictionary);
    }

    [Fact]
    public void SuppliedDictionaryAndThirteenSlotArraysRemainConcreteMutableStorage()
    {
        var table = new Dictionary<uint, LegacyNativeItemTypePlus[]>();
        LegacyNativeItemAddTable.PopulateRows(table, new[] { Line(42, 2) });
        Assert.IsType<LegacyNativeItemTypePlus[]>(table[42]);
        Assert.Equal(13, table[42].Length);
        var originalArray = table[42];
        var replacement = new LegacyNativeItemTypePlus { ID = 42, Plus = 2, ItemHP = 77 };
        table[42][2] = replacement;
        Assert.Same(originalArray, table[42]);
        Assert.Same(replacement, table[42][2]);
        Assert.True(table.Remove(42));
        Assert.Empty(table);
    }

    [Fact]
    public void SlotZeroAndTwelveUseExactByteIndices()
    {
        var table = CreateTable(new[] { Line(0, 0, 11), Line(uint.MaxValue, 12, 22) });
        Assert.Equal(2, table.Count);
        Assert.Equal(0U, table[0][0].ID);
        Assert.Equal((byte)0, table[0][0].Plus);
        Assert.Equal((ushort)11, table[0][0].ItemHP);
        Assert.Equal(uint.MaxValue, table[uint.MaxValue][12].ID);
        Assert.Equal((byte)12, table[uint.MaxValue][12].Plus);
        Assert.Equal((ushort)22, table[uint.MaxValue][12].ItemHP);
    }

    [Fact]
    public void DifferentIdsOwnDistinctSparseArrays()
    {
        var table = CreateTable(new[] { Line(41, 2), Line(42, 2) });
        Assert.NotSame(table[41], table[42]);
        foreach (var pair in table)
        {
            Assert.Equal(13, pair.Value.Length);
            for (var slot = 0; slot < 13; slot++)
            {
                if (slot == 2) Assert.Equal(pair.Key, pair.Value[slot].ID);
                else Assert.Null(pair.Value[slot]);
            }
        }
    }

    [Fact]
    public void RepeatedIdDifferentSlotsShareOneArrayWithoutMergingRows()
    {
        var table = CreateTable(new[] { Line(42, 2, 11), Line(42, 12, 22) });
        Assert.Single(table);
        Assert.Equal(13, table[42].Length);
        Assert.Equal((ushort)11, table[42][2].ItemHP);
        Assert.Equal((ushort)22, table[42][12].ItemHP);
        Assert.NotSame(table[42][2], table[42][12]);
        Assert.Null(table[42][0]);
    }

    [Fact]
    public void DuplicateIdAndPlusLastParsedRowWins()
    {
        var table = CreateTable(new[] { Line(42, 2, 11), Line(42, 2, 22) });
        Assert.Single(table);
        Assert.Equal((ushort)22, table[42][2].ItemHP);
        Assert.Equal(42U, table[42][2].ID);
        Assert.Equal((byte)2, table[42][2].Plus);
    }

    [Fact]
    public void InputOrderIsPreservedForDuplicateReplacement()
    {
        var lines = new[] { Line(42, 2, 11), Line(42, 2, 22), Line(42, 2, 33) };
        var forward = CreateTable(lines);
        var reverse = CreateTable(lines.Reverse().ToArray());
        Assert.Equal((ushort)33, forward[42][2].ItemHP);
        Assert.Equal((ushort)11, reverse[42][2].ItemHP);
        Assert.Equal(Line(42, 2, 11), lines[0]);
        Assert.Equal(Line(42, 2, 33), lines[2]);
    }

    [Fact]
    public void EveryStoredRowIsFreshAndItsMutableAliasesRemainLive()
    {
        var table = CreateTable(new[] { Line(42, 1), Line(42, 2), Line(43, 2) });
        var alias = table[42][2];
        Assert.NotSame(alias, table[42][1]);
        Assert.NotSame(alias, table[43][2]);
        alias.ItemHP = 77;
        alias.Agility = 88;
        Assert.Same(alias, table[42][2]);
        Assert.Equal((ushort)77, table[42][2].ItemHP);
        Assert.Equal((ushort)88, table[42][2].Vigor);
        Assert.Equal((ushort)3, table[42][1].ItemHP);
        Assert.Equal((ushort)3, table[43][2].ItemHP);
    }

    [Fact]
    public void TrimmedLinesAndNativeMixedSeparatorsKeepAllTenValues()
    {
        var table = CreateTable(new[] { " \t@@42 @ @2@@3 4@5@@6 7@8@@9 10@@\r\n " });
        var row = table[42][2];
        Assert.Equal(new uint[] { 42, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, Read(row));
    }

    [Fact]
    public void BlankDelimiterAndCommentLinesAreNotSkipped()
    {
        foreach (var text in new[] { string.Empty, " \t\r\n ", "@@ @ @" })
            Assert.Throws<IndexOutOfRangeException>(() => CreateTable(new[] { text }));
        Assert.Throws<FormatException>(() => CreateTable(new[] { " // comment" }));
    }

    [Fact]
    public void NullArrayAndNullLineKeepNativeNullReferenceFaults()
    {
        Assert.Throws<NullReferenceException>(() => CreateTable(null!));
        Assert.Throws<NullReferenceException>(() => CreateTable(new string[] { null! }));
    }

    [Fact]
    public void LaterNullLineFaultKeepsEarlierCallerVisibleCommitsAndInputReferences()
    {
        var first = Line(42, 2);
        var lines = new string[] { first, null! };
        var table = new Dictionary<uint, LegacyNativeItemTypePlus[]>();
        var alias = table;
        Assert.Throws<NullReferenceException>(() => LegacyNativeItemAddTable.PopulateRows(table, lines));
        Assert.Same(alias, table);
        Assert.Single(table);
        Assert.Equal(new uint[] { 42, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, Read(table[42][2]));
        Assert.Same(first, lines[0]);
        Assert.Null(lines[1]);
    }

    [Fact]
    public void PlusThirteenAndByteMaximumFaultRatherThanResizeOrClamp()
    {
        foreach (var plus in new byte[] { 13, byte.MaxValue })
        {
            Assert.Throws<IndexOutOfRangeException>(() => CreateTable(new[] { Line(42, plus) }));
            Assert.Throws<IndexOutOfRangeException>(() => CreateTable(new[] { Line(42, 2), Line(42, plus) }));
        }
    }

    [Fact]
    public void EntireParsePrecedesNewAndExistingIdArrayIndexFaults()
    {
        var malformedLastCell = "42@@13@@3@@4@@5@@6@@7@@8@@9@@not-a-byte!";
        var overflowThirdCell = "42@@13@@65536@@4@@5@@6@@7@@8@@9@@10";
        foreach (var text in new[] { malformedLastCell, overflowThirdCell })
        {
            var expected = text == malformedLastCell ? typeof(FormatException) : typeof(OverflowException);
            Assert.Throws(expected, () => CreateTable(new[] { text }));
            Assert.Throws(expected, () => CreateTable(new[] { Line(42, 2), text }));
        }
    }

    [Fact]
    public void UnsignedParseOverflowMalformedAndMissingCellsKeepNativeExceptionKinds()
    {
        Assert.Throws<OverflowException>(() => CreateTable(new[] { "4294967296@@2@@3@@4@@5@@6@@7@@8@@9@@10" }));
        Assert.Throws<OverflowException>(() => CreateTable(new[] { "42@@256@@3@@4@@5@@6@@7@@8@@9@@10" }));
        Assert.Throws<FormatException>(() => CreateTable(new[] { "bad@@2@@3@@4@@5@@6@@7@@8@@9@@10" }));
        Assert.Throws<IndexOutOfRangeException>(() => CreateTable(new[] { "42@@2" }));
    }

    [Fact]
    public void SurplusMalformedCellsAfterTheTenNativeFieldsAreIgnored()
    {
        var table = CreateTable(new[] { Line(42, 2) + "@@not-a-number!@@4294967296" });
        Assert.Equal(new uint[] { 42, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, Read(table[42][2]));
    }

    [Fact]
    public void CurrentCultureNumericParsingIsNotReplacedByInvariantDefaults()
    {
        var previous = CultureInfo.CurrentCulture;
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
        culture.NumberFormat.PositiveSign = "p";
        try
        {
            CultureInfo.CurrentCulture = culture;
            var text = string.Join("@@", new[] { "p42", "p2", "p3", "p4", "p5", "p6", "p7", "p8", "p9", "p10" });
            var table = CreateTable(new[] { text });
            Assert.Equal(new uint[] { 42, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, Read(table[42][2]));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void DistinctCallerDictionariesReceiveDistinctArraysAndRows()
    {
        var lines = new[] { Line(42, 2) };
        var first = new Dictionary<uint, LegacyNativeItemTypePlus[]>();
        var second = new Dictionary<uint, LegacyNativeItemTypePlus[]>();
        LegacyNativeItemAddTable.PopulateRows(first, lines);
        LegacyNativeItemAddTable.PopulateRows(second, lines);
        Assert.NotSame(first[42], second[42]);
        Assert.NotSame(first[42][2], second[42][2]);
        first[42][2].ItemHP = 77;
        Assert.Equal((ushort)3, second[42][2].ItemHP);
        Assert.Equal(Line(42, 2), lines[0]);
    }

    [Fact]
    public void PreexistingUnrelatedEntriesAndArraysSurviveSuccessfulPopulation()
    {
        var original = new LegacyNativeItemTypePlus { ID = 99, Plus = 1, ItemHP = 77 };
        var slots = new LegacyNativeItemTypePlus[13];
        slots[1] = original;
        var table = new Dictionary<uint, LegacyNativeItemTypePlus[]> { [99] = slots };
        LegacyNativeItemAddTable.PopulateRows(table, new[] { Line(42, 2) });
        Assert.Equal(2, table.Count);
        Assert.Same(slots, table[99]);
        Assert.Same(original, table[99][1]);
        Assert.Equal((ushort)77, original.ItemHP);
        Assert.Equal(42U, table[42][2].ID);
    }

    [Fact]
    public void PreexistingSlotReplacementKeepsArrayAndDoesNotMutateEscapedOldRow()
    {
        var oldRow = new LegacyNativeItemTypePlus { ID = 42, Plus = 2, ItemHP = 11 };
        var otherRow = new LegacyNativeItemTypePlus { ID = 42, Plus = 3, ItemHP = 33 };
        var slots = new LegacyNativeItemTypePlus[13];
        slots[2] = oldRow;
        slots[3] = otherRow;
        var table = new Dictionary<uint, LegacyNativeItemTypePlus[]> { [42] = slots };
        LegacyNativeItemAddTable.PopulateRows(table, new[] { Line(42, 2, 22) });
        Assert.Same(slots, table[42]);
        Assert.NotSame(oldRow, slots[2]);
        Assert.Equal((ushort)22, slots[2].ItemHP);
        Assert.Equal((ushort)11, oldRow.ItemHP);
        Assert.Same(otherRow, slots[3]);
    }

    [Fact]
    public void ValidDuplicateOverwriteRemainsCommittedWhenLaterParsingFaults()
    {
        var table = new Dictionary<uint, LegacyNativeItemTypePlus[]>();
        var badRow = "42@@2@@3@@4@@5@@6@@7@@8@@9@@bad";
        Assert.Throws<FormatException>(() => LegacyNativeItemAddTable.PopulateRows(
            table, new[] { Line(42, 2, 11), Line(42, 2, 22), badRow }));
        Assert.Single(table);
        Assert.Equal((ushort)22, table[42][2].ItemHP);
        Assert.Equal((byte)10, table[42][2].Dodge);
        Assert.Null(table[42][1]);
    }

    [Fact]
    public void NewIdIndexFaultDoesNotPublishItsArrayAndKeepsEarlierEntry()
    {
        var table = new Dictionary<uint, LegacyNativeItemTypePlus[]>();
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeItemAddTable.PopulateRows(
            table, new[] { Line(41, 2, 11), Line(42, 13) }));
        Assert.Single(table);
        Assert.Equal((ushort)11, table[41][2].ItemHP);
        Assert.DoesNotContain(42U, table.Keys);
    }

    [Fact]
    public void ExistingIdIndexFaultKeepsTheExistingArrayAndAllItsSlots()
    {
        var table = CreateTable(new[] { Line(42, 2, 11) });
        var slots = table[42];
        var row = slots[2];
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeItemAddTable.PopulateRows(table, new[] { Line(42, 13) }));
        Assert.Single(table);
        Assert.Same(slots, table[42]);
        Assert.Same(row, table[42][2]);
        Assert.Equal((ushort)11, row.ItemHP);
    }

    [Fact]
    public void EmptyInputDoesNotValidateOrDereferenceAModelOnlyNullDictionary()
    {
        // API fault-order model only: genuine native Loading allocates a nonnull dictionary.
        LegacyNativeItemAddTable.PopulateRows(null!, Array.Empty<string>());
    }

    [Fact]
    public void ModelOnlyNullDictionaryIsReachedAfterTrimAndCompleteParse()
    {
        // These injected null dictionary states are not a native Loading reachability claim.
        Assert.Throws<NullReferenceException>(() => LegacyNativeItemAddTable.PopulateRows(null!, new string[] { null! }));
        Assert.Throws<FormatException>(() => LegacyNativeItemAddTable.PopulateRows(null!, new[] { "bad@@2@@3@@4@@5@@6@@7@@8@@9@@10" }));
        Assert.Throws<OverflowException>(() => LegacyNativeItemAddTable.PopulateRows(null!, new[] { "42@@13@@3@@4@@5@@6@@7@@8@@9@@256" }));
        Assert.Throws<NullReferenceException>(() => LegacyNativeItemAddTable.PopulateRows(null!, new[] { Line(42, 2) }));
    }

    [Fact]
    public void ModelOnlyPreexistingNullArrayIsNotRepairedAndParsingFaultStillWins()
    {
        // Only native-created arrays are reachable in the fresh native local dictionary.
        var table = new Dictionary<uint, LegacyNativeItemTypePlus[]> { [42] = null! };
        Assert.Throws<FormatException>(() => LegacyNativeItemAddTable.PopulateRows(table, new[] { "42@@2@@3@@4@@5@@6@@7@@8@@9@@bad" }));
        Assert.Null(table[42]);
        Assert.Throws<NullReferenceException>(() => LegacyNativeItemAddTable.PopulateRows(table, new[] { Line(42, 2) }));
        Assert.Single(table);
        Assert.Null(table[42]);
    }

    // Fixture-only allocation helper; production exposes only the void population phase.
    private static Dictionary<uint, LegacyNativeItemTypePlus[]> CreateTable(string[] lines)
    {
        var dictionary = new Dictionary<uint, LegacyNativeItemTypePlus[]>();
        LegacyNativeItemAddTable.PopulateRows(dictionary, lines);
        return dictionary;
    }

    private static string Line(uint id, byte plus, ushort itemHp = 3) => string.Join("@@", new[]
    {
        id.ToString(CultureInfo.InvariantCulture), plus.ToString(CultureInfo.InvariantCulture),
        itemHp.ToString(CultureInfo.InvariantCulture), "4", "5", "6", "7", "8", "9", "10"
    });

    private static uint[] Read(LegacyNativeItemTypePlus row) => new uint[]
    {
        row.ID, row.Plus, row.ItemHP, row.MinAttack, row.MaxAttack,
        row.PhysicalDefence, row.MagicAttack, row.MagicDefence, row.Agility, row.Dodge
    };
}
