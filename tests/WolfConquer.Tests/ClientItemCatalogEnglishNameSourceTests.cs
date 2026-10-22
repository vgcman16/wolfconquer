using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientItemCatalogEnglishNameSourceTests
{
    [Fact]
    public void MixedEndingsTrackAllRowsButExposeOnlyEligibleAsciiNamesWithoutMutableAliases()
    {
        var input = Join(
            Row("1", Ascii("English Potion"), 67, "\r\n"),
            Row("2", [0xB5, 0xA5], 67, "\n"),
            Row("3", [], 67, "\r"),
            Row("4", Ascii("12345"), 67, "\n"),
            Row("5", Ascii("@Leading Name"), 67, ""));
        var expected = input.ToArray();

        var source = ClientItemCatalogEnglishNameSource.Parse(input);
        Array.Fill(input, (byte)'X');
        foreach (var name in source.Names.Values) CorruptReturnedMemory(name);

        Assert.Equal(5, source.SourceRowCount);
        Assert.Equal(3, source.SkippedNameCount);
        Assert.Equal(new uint[] { 1, 5 }, source.Names.Keys.Order());
        Assert.Equal(Ascii("English Potion"), source.Names[1].ToArray());
        Assert.Equal(Ascii("@Leading Name"), source.Names[5].ToArray());
        Assert.Equal(5, ClientItemCatalogEnglishNameSource.Parse(expected).SourceRowCount);
    }

    [Theory]
    [InlineData("English Potion", true)]
    [InlineData("1234 Sword", true)]
    [InlineData("[Quest] #A", true)]
    [InlineData("@Leading", true)]
    [InlineData("A@B", true)]
    [InlineData("A ~", true)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("12345", false)]
    [InlineData("123 45#!", false)]
    [InlineData("\tName", false)]
    [InlineData("Name\u001F", false)]
    [InlineData("Name\u007F", false)]
    public void NameEligibilityRequiresPrintableAsciiAndAtLeastOneLatinLetter(string name, bool eligible)
    {
        var source = ClientItemCatalogEnglishNameSource.Parse(Row("1", Ascii(name), 67, "\n"));

        Assert.Equal(1, source.SourceRowCount);
        Assert.Equal(eligible ? 0 : 1, source.SkippedNameCount);
        Assert.Equal(eligible, source.Names.ContainsKey(1));
        if (eligible) Assert.Equal(Ascii(name), source.Names[1].ToArray());
    }

    [Fact]
    public void NonAsciiNameBytesAreSkippedRatherThanLossilyConvertedToEnglish()
    {
        var source = ClientItemCatalogEnglishNameSource.Parse(Join(
            Row("1", [0xB5, 0xA5], 67, "\r\n"),
            Row("2", [(byte)'A', 0x80], 67, "")));

        Assert.Equal(2, source.SourceRowCount);
        Assert.Equal(2, source.SkippedNameCount);
        Assert.Empty(source.Names);
    }

    [Fact]
    public void IdZeroMaximumAndLeadingZeroSpellingUseTheirNumericIdentities()
    {
        var source = ClientItemCatalogEnglishNameSource.Parse(Join(
            Row("0000", Ascii("Zero"), 67, "\n"),
            Row("04294967295", Ascii("Maximum"), 67, "")));

        Assert.Equal(new uint[] { 0, uint.MaxValue }, source.Names.Keys.Order());
        Assert.Equal(Ascii("Zero"), source.Names[0].ToArray());
        Assert.Equal(Ascii("Maximum"), source.Names[uint.MaxValue].ToArray());
    }

    [Theory]
    [InlineData(65)]
    [InlineData(66)]
    [InlineData(68)]
    public void DonorSchemaRequiresExactlySixtySevenFieldsNotTheNativeSixtyFive(int fields)
    {
        Assert.Throws<InvalidDataException>(() => ClientItemCatalogEnglishNameSource.Parse(Row("1", Ascii("Name"), fields, "\n")));
    }

    [Theory]
    [InlineData("1", "1")]
    [InlineData("01", "1")]
    [InlineData("0", "000")]
    public void DuplicateNumericIdsFailEvenWhenTheFirstNameWouldBeSkipped(string first, string second)
    {
        var input = Join(Row(first, [], 67, "\r\n"), Row(second, Ascii("English"), 67, ""));

        Assert.Throws<InvalidDataException>(() => ClientItemCatalogEnglishNameSource.Parse(input));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(66)]
    public void NulInAnyDonorFieldRejectsTheWholeSource(int field)
    {
        var values = Fields("1", Ascii("English"), 67);
        values[field] = Join(values[field], [0]);

        Assert.Throws<InvalidDataException>(() => ClientItemCatalogEnglishNameSource.Parse(Render(values, "\n")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData(" 1")]
    [InlineData("4294967296")]
    public void MalformedIdsFailEvenWhenTheirNamesWouldBeSkipped(string id)
    {
        Assert.Throws<InvalidDataException>(() => ClientItemCatalogEnglishNameSource.Parse(Row(id, [], 67, "")));
    }

    [Fact]
    public void EmptyRowsAndMissingTrailingDelimiterFailClosed()
    {
        var valid = Row("1", Ascii("English"), 67, "\n");
        Assert.Throws<InvalidDataException>(() => ClientItemCatalogEnglishNameSource.Parse([]));
        Assert.Throws<InvalidDataException>(() => ClientItemCatalogEnglishNameSource.Parse(Ascii("\r\n")));
        Assert.Throws<InvalidDataException>(() => ClientItemCatalogEnglishNameSource.Parse(Join(valid, Ascii("\n"))));
        var missingTrailer = Row("1", Ascii("English"), 67, "")[..^2];
        Assert.Throws<InvalidDataException>(() => ClientItemCatalogEnglishNameSource.Parse(missingTrailer));
    }

    [Fact]
    public void BatchNamesPreserveOtherSixtyFourFieldsOrderEndingsIdsAndOriginalObjects()
    {
        var first = Fields("0001", [0xB5, 0xA5], 65);
        first[40] = [0x81, 0x80, 0xFF];
        var second = Fields("2", Ascii("Old Two"), 65);
        var third = Fields("4294967295", Ascii("Unchanged"), 65);
        var input = Join(Render(first, "\r\n"), Render(second, "\n"), Render(third, ""));
        var original = ClientItemCatalogDocument.Parse(input);
        var oldRows = original.Rows.ToArray();
        var one = Ascii("English One");
        var two = Ascii("English Two");
        var names = new Dictionary<uint, ReadOnlyMemory<byte>> { [2] = two, [1] = one };

        var edited = original.WithNames(names);
        one[0] = (byte)'X';
        two[0] = (byte)'Y';
        names.Clear();

        Assert.Equal(new uint[] { 1, 2, uint.MaxValue }, edited.Rows.Select(static row => row.ItemId));
        Assert.Equal(Ascii("English One"), edited.Rows[0].GetField(1).ToArray());
        Assert.Equal(Ascii("English Two"), edited.Rows[1].GetField(1).ToArray());
        Assert.Equal(third[1], edited.Rows[2].GetField(1).ToArray());
        for (var row = 0; row < oldRows.Length; ++row)
        {
            Assert.Equal(oldRows[row].LineEnding.ToArray(), edited.Rows[row].LineEnding.ToArray());
            for (var field = 0; field < 65; ++field)
                if (field != 1) Assert.Equal(oldRows[row].GetField(field).ToArray(), edited.Rows[row].GetField(field).ToArray());
        }
        Assert.Equal(first[1], oldRows[0].GetField(1).ToArray());
        Assert.Equal(second[1], oldRows[1].GetField(1).ToArray());
        Assert.Equal(input, original.ToArray());
        first[1] = Ascii("English One");
        second[1] = Ascii("English Two");
        var expected = Join(Render(first, "\r\n"), Render(second, "\n"), Render(third, ""));
        Assert.Equal(expected, edited.ToArray());
        Assert.Equal(expected, ClientItemCatalogDocument.Parse(edited.ToArray()).ToArray());
    }

    [Theory]
    [InlineData("Bad\0Name")]
    [InlineData("Bad\rName")]
    [InlineData("Bad\nName")]
    [InlineData("Bad@@Name")]
    [InlineData("Bad@")]
    public void UnsafeLaterBatchNameIsAtomicAndDoesNotMutateEarlierSourceRows(string unsafeName)
    {
        var input = Join(Row("1", Ascii("Old One"), 65, "\r\n"), Row("2", Ascii("Old Two"), 65, ""));
        var preserved = input.ToArray();
        var original = ClientItemCatalogDocument.Parse(input);
        var names = new Dictionary<uint, ReadOnlyMemory<byte>> { [1] = Ascii("Valid First"), [2] = Ascii(unsafeName) };

        Assert.ThrowsAny<ArgumentException>(() => original.WithNames(names));

        Assert.Equal(preserved, input);
        Assert.Equal(preserved, original.ToArray());
        Assert.Equal(Ascii("Old One"), original.Rows[0].GetField(1).ToArray());
        Assert.Equal(Ascii("Old Two"), original.Rows[1].GetField(1).ToArray());
    }

    [Fact]
    public void UnknownLaterBatchIdIsAtomicRatherThanInsertedOrIgnoredByTheDocumentEditor()
    {
        var input = Row("1", Ascii("Old"), 65, "\n");
        var original = ClientItemCatalogDocument.Parse(input);
        var names = new Dictionary<uint, ReadOnlyMemory<byte>> { [1] = Ascii("Valid First"), [999] = Ascii("Unknown") };

        Assert.Throws<KeyNotFoundException>(() => original.WithNames(names));
        Assert.Equal(input, original.ToArray());
        Assert.Single(original.Rows);
    }

    [Fact]
    public void EmptyBatchIsByteExactAndDonorNamesCanBeUsedWithoutSharingMutableValues()
    {
        var input = Row("1", Ascii("Old"), 65, "\n");
        var original = ClientItemCatalogDocument.Parse(input);
        var source = ClientItemCatalogEnglishNameSource.Parse(Row("1", Ascii("English"), 67, "\r\n"));

        Assert.Equal(input, original.WithNames(new Dictionary<uint, ReadOnlyMemory<byte>>()).ToArray());
        var names = source.Names;
        var edited = original.WithNames(names);
        CorruptReturnedMemory(names[1]);

        Assert.Equal(Ascii("English"), edited.Rows[0].GetField(1).ToArray());
        Assert.Equal(Ascii("English"), source.Names[1].ToArray());
        Assert.Equal(input, original.ToArray());
    }

    private static byte[][] Fields(string id, byte[] name, int count)
    {
        var fields = Enumerable.Range(0, count).Select(index => Ascii(index == 0 ? id : $"field{index:D2}")).ToArray();
        fields[1] = name.ToArray();
        return fields;
    }

    private static byte[] Row(string id, byte[] name, int count, string ending) => Render(Fields(id, name, count), ending);

    private static byte[] Render(IReadOnlyList<byte[]> fields, string ending)
    {
        using var output = new MemoryStream();
        foreach (var field in fields)
        {
            output.Write(field);
            output.Write("@@"u8);
        }
        output.Write(Ascii(ending));
        return output.ToArray();
    }

    private static byte[] Join(params byte[][] parts)
    {
        using var output = new MemoryStream();
        foreach (var part in parts) output.Write(part);
        return output.ToArray();
    }

    private static void CorruptReturnedMemory(ReadOnlyMemory<byte> memory)
    {
        if (!memory.IsEmpty && MemoryMarshal.TryGetArray(memory, out var segment))
            Array.Fill(segment.Array!, (byte)'X', segment.Offset, segment.Count);
    }

    private static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);
}
