using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientItemCatalogDocumentTests
{
    private const int NativeFieldCount = 65;
    private static readonly byte[] Delimiter = [(byte)'@', (byte)'@'];

    [Fact]
    public void MixedLineEndingsAndUnterminatedTailRoundTripWithoutTranscoding()
    {
        var firstFields = Fields("0", [0xD2, 0xA9, 0xC6, 0xB7]);
        firstFields[40] = [0x81, 0x80, 0xFE, 0xFF];
        firstFields[64] = [0xB2, 0xA9];
        var rows = new[]
        {
            Row(firstFields, "\r\n"),
            Row(Fields("00042", Ascii("Second")), "\n"),
            Row(Fields("1001000", Ascii("Third")), "\r"),
            Row(Fields("4294967295", Ascii("Final")), ""),
        };
        var source = Concat(rows);

        var document = ClientItemCatalogDocument.Parse(source);

        Assert.Equal(NativeFieldCount, ClientItemCatalogDocument.FieldCount);
        Assert.Equal(new uint[] { 0, 42, 1001000, uint.MaxValue }, document.Rows.Select(static row => row.ItemId));
        Assert.Equal(new[] { "\r\n", "\n", "\r", "" }, document.Rows.Select(static row => Encoding.ASCII.GetString(row.LineEnding.Span)));
        Assert.All(document.Rows, row => Assert.Equal(NativeFieldCount, row.FieldCount));
        Assert.Equal(firstFields[1], document.Rows[0].GetField(1).ToArray());
        Assert.Equal(firstFields[40], document.Rows[0].GetField(40).ToArray());
        Assert.Equal(firstFields[64], document.Rows[0].GetField(64).ToArray());
        Assert.Equal(source, document.ToArray());
        Assert.Equal(source, RebuildFromFields(document));
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("")]
    public void AllNonIdFieldsMayBeEmptyWithAnySupportedLineEnding(string ending)
    {
        var fields = Enumerable.Range(0, NativeFieldCount).Select(static _ => Array.Empty<byte>()).ToArray();
        fields[0] = Ascii("4294967295");
        var source = Row(fields, ending);

        var document = ClientItemCatalogDocument.Parse(source);

        var row = Assert.Single(document.Rows);
        Assert.Equal(uint.MaxValue, row.ItemId);
        Assert.Equal(NativeFieldCount, row.Fields.Count);
        Assert.All(row.Fields.Skip(1), field => Assert.True(field.IsEmpty));
        Assert.Equal(Ascii(ending), row.LineEnding.ToArray());
        Assert.Equal(source, document.ToArray());
    }

    [Fact]
    public void NoOpEditsUsingEveryParsedNonIdFieldReconstructTheExactOriginalBytes()
    {
        var source = Concat(Row(Fields("00000001", [0xB5, 0xA5]), "\r\n"), Row(Fields("2"), ""));
        var original = ClientItemCatalogDocument.Parse(source);
        var rebuilt = original;

        foreach (var row in original.Rows)
        {
            for (var index = 1; index < NativeFieldCount; ++index)
            {
                rebuilt = rebuilt.WithField(row.ItemId, index, row.GetField(index).Span);
            }
        }

        Assert.Equal(source, rebuilt.ToArray());
        Assert.Equal(source, RebuildFromFields(rebuilt));
        Assert.Equal(source, original.ToArray());
        Assert.Equal(Ascii("00000001"), rebuilt.Rows[0].GetField(0).ToArray());
    }

    [Fact]
    public void TargetedNameEditPreservesAllOtherFieldsRowOrderLineEndingsAndOriginalObjects()
    {
        var oldFields = Fields("0001001000", [0xD2, 0xA9, 0xC6, 0xB7]);
        oldFields[9] = [];
        oldFields[40] = [0x81, 0x80, 0xFF];
        var source = Concat(Row(Fields("7"), "\r\n"), Row(oldFields, "\n"), Row(Fields("9"), ""));
        var preservedInput = source.ToArray();
        var original = ClientItemCatalogDocument.Parse(source);
        var oldRow = original.Rows[1];
        var replacement = Ascii("English Potion");
        var expectedFields = oldFields.Select(static field => field.ToArray()).ToArray();
        expectedFields[1] = replacement.ToArray();
        var expected = Concat(Row(Fields("7"), "\r\n"), Row(expectedFields, "\n"), Row(Fields("9"), ""));

        var edited = original.WithField(1001000, 1, replacement);
        replacement[0] = (byte)'X';

        Assert.Equal(expected, edited.ToArray());
        Assert.Equal(new uint[] { 7, 1001000, 9 }, edited.Rows.Select(static row => row.ItemId));
        Assert.Equal(source, original.ToArray());
        Assert.Equal(preservedInput, source);
        Assert.Equal(oldFields[1], oldRow.GetField(1).ToArray());
        for (var index = 0; index < NativeFieldCount; ++index)
        {
            if (index != 1)
            {
                Assert.Equal(oldFields[index], edited.Rows[1].GetField(index).ToArray());
            }
        }
        Assert.Equal(original.Rows[0].LineEnding.ToArray(), edited.Rows[0].LineEnding.ToArray());
        Assert.Equal(oldRow.LineEnding.ToArray(), edited.Rows[1].LineEnding.ToArray());
        Assert.Equal(original.Rows[2].LineEnding.ToArray(), edited.Rows[2].LineEnding.ToArray());
        Assert.Equal(expected, ClientItemCatalogDocument.Parse(edited.ToArray()).ToArray());
    }

    [Fact]
    public void ParsingAndGettersDoNotExposeMutableAliasesToStoredBytes()
    {
        var source = Row(Fields("1001000", [0xB5, 0xA5]), "\r\n");
        var expected = source.ToArray();
        var document = ClientItemCatalogDocument.Parse(source);
        var row = Assert.Single(document.Rows);
        Array.Fill(source, (byte)'X');

        CorruptReturnedMemory(row.GetField(0));
        CorruptReturnedMemory(row.GetField(1));
        CorruptReturnedMemory(row.LineEnding);
        foreach (var field in row.Fields)
        {
            CorruptReturnedMemory(field);
        }
        var output = document.ToArray();
        Array.Fill(output, (byte)'Y');

        Assert.Equal(1001000U, row.ItemId);
        Assert.Equal(Ascii("1001000"), row.GetField(0).ToArray());
        Assert.Equal(new byte[] { 0xB5, 0xA5 }, row.GetField(1).ToArray());
        Assert.Equal(Ascii("\r\n"), row.LineEnding.ToArray());
        Assert.Equal(expected, document.ToArray());
    }

    [Fact]
    public void RowEditAndReplaceFieldAliasDoNotMutateTheSourceRowOrDocument()
    {
        var source = Row(Fields("1", Ascii("Old")), "\r");
        var original = ClientItemCatalogDocument.Parse(source);
        var oldRow = Assert.Single(original.Rows);
        var replacement = Ascii("New");

        var newRow = oldRow.WithField(1, replacement);
        var documentEdit = original.ReplaceField(1, 1, replacement);
        replacement[0] = (byte)'X';

        Assert.Equal(Ascii("Old"), oldRow.GetField(1).ToArray());
        Assert.Equal(Ascii("New"), newRow.GetField(1).ToArray());
        Assert.Equal(Ascii("New"), documentEdit.Rows[0].GetField(1).ToArray());
        Assert.Equal(oldRow.LineEnding.ToArray(), newRow.LineEnding.ToArray());
        Assert.Equal(source, original.ToArray());
        Assert.Equal(1U, newRow.ItemId);
    }

    [Fact]
    public void EmptyAndLeadingSingleAtSignEditsRemainRepresentable()
    {
        var original = ClientItemCatalogDocument.Parse(Row(Fields("1"), "\n"));

        var empty = original.WithField(1, 1, []);
        var leading = original.WithField(1, 1, Ascii("@Leading"));

        Assert.True(empty.Rows[0].GetField(1).IsEmpty);
        Assert.Equal(Ascii("@Leading"), leading.Rows[0].GetField(1).ToArray());
        Assert.Equal(empty.ToArray(), ClientItemCatalogDocument.Parse(empty.ToArray()).ToArray());
        Assert.Equal(Ascii("@Leading"), ClientItemCatalogDocument.Parse(leading.ToArray()).Rows[0].GetField(1).ToArray());
    }

    [Fact]
    public void WriteReconstructsBytesAtCurrentStreamPositionWithoutClosingTheStream()
    {
        var source = Concat(Row(Fields("0", [0xFE, 0xFF]), "\r\n"), Row(Fields("4294967295"), ""));
        var document = ClientItemCatalogDocument.Parse(source);
        using var output = new MemoryStream();
        var prefix = Ascii("prefix:");
        output.Write(prefix);

        document.Write(output);

        Assert.True(output.CanWrite);
        Assert.Equal(Concat(prefix, source), output.ToArray());
        Assert.Equal(source, document.ToArray());
    }

    [Theory]
    [InlineData(64)]
    [InlineData(66)]
    [InlineData(67)]
    public void RejectsAnyNonNativeFieldCountIncludingTheEnglish7770Schema(int fieldCount)
    {
        var source = Row(Fields("1001000", fieldCount: fieldCount), "\r\n");

        Assert.Throws<InvalidDataException>(() => ClientItemCatalogDocument.Parse(source));
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("")]
    public void RequiresATrailingDelimiterAfterTheSixtyFifthField(string ending)
    {
        var source = Row(Fields("1"), ending, trailingDelimiter: false);

        Assert.Throws<InvalidDataException>(() => ClientItemCatalogDocument.Parse(source));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(64)]
    public void RejectsNulAnywhereInFieldBytes(int fieldIndex)
    {
        var fields = Fields("1");
        fields[fieldIndex] = Concat(fields[fieldIndex], [0]);
        var source = Row(fields, "\n");

        Assert.Throws<InvalidDataException>(() => ClientItemCatalogDocument.Parse(source));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\r\n")]
    [InlineData("\n")]
    [InlineData("\r")]
    public void RejectsEmptyDocumentsAndRows(string source)
    {
        Assert.Throws<InvalidDataException>(() => ClientItemCatalogDocument.Parse(Ascii(source)));
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    [InlineData("\r")]
    public void RejectsBlankRowsBeforeBetweenOrAfterCatalogRows(string ending)
    {
        var first = Row(Fields("1"), ending);
        var second = Row(Fields("2"), ending);

        Assert.Throws<InvalidDataException>(() => ClientItemCatalogDocument.Parse(Concat(Ascii(ending), first)));
        Assert.Throws<InvalidDataException>(() => ClientItemCatalogDocument.Parse(Concat(first, Ascii(ending), second)));
        Assert.Throws<InvalidDataException>(() => ClientItemCatalogDocument.Parse(Concat(first, Ascii(ending))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("1.0")]
    [InlineData("1e3")]
    [InlineData("0x1")]
    [InlineData("1a")]
    [InlineData("4294967296")]
    [InlineData("999999999999999999999999")]
    public void RejectsEmptyNonnumericOrOverflowingItemIds(string itemId)
    {
        var source = Row(Fields(itemId), "\n");

        Assert.Throws<InvalidDataException>(() => ClientItemCatalogDocument.Parse(source));
    }

    [Fact]
    public void RejectsNonAsciiItemIdBytes()
    {
        var fields = Fields("1");
        fields[0] = [0xB5, 0xA5];

        Assert.Throws<InvalidDataException>(() => ClientItemCatalogDocument.Parse(Row(fields, "")));
    }

    [Theory]
    [InlineData("1", "1")]
    [InlineData("01", "1")]
    [InlineData("0", "0000")]
    [InlineData("4294967295", "04294967295")]
    public void RejectsDuplicateNumericIdentitiesEvenWithDifferentRawIdSpelling(string firstId, string secondId)
    {
        var source = Concat(Row(Fields(firstId), "\r\n"), Row(Fields(secondId), ""));

        Assert.Throws<InvalidDataException>(() => ClientItemCatalogDocument.Parse(source));
    }

    [Theory]
    [InlineData("\0")]
    [InlineData("line\rbreak")]
    [InlineData("line\nbreak")]
    [InlineData("field@@break")]
    [InlineData("trailing@")]
    [InlineData("@")]
    public void RejectsUnsafeFieldEditsWithoutMutatingOldRowsOrInput(string replacement)
    {
        var source = Row(Fields("1", Ascii("Old")), "\r\n");
        var preservedInput = source.ToArray();
        var original = ClientItemCatalogDocument.Parse(source);
        var oldRow = Assert.Single(original.Rows);
        var bytes = Ascii(replacement);

        Assert.ThrowsAny<ArgumentException>(() => original.WithField(1, 1, bytes));
        Assert.ThrowsAny<ArgumentException>(() => oldRow.WithField(1, bytes));

        Assert.Equal(preservedInput, source);
        Assert.Equal(preservedInput, original.ToArray());
        Assert.Equal(Ascii("Old"), oldRow.GetField(1).ToArray());
    }

    [Fact]
    public void ItemIdEditsAreRejectedEvenWhenTheReplacementWouldBeUnchanged()
    {
        var source = Row(Fields("0001"), "\n");
        var original = ClientItemCatalogDocument.Parse(source);
        var row = Assert.Single(original.Rows);

        Assert.ThrowsAny<ArgumentException>(() => original.WithField(1, 0, Ascii("2")));
        Assert.ThrowsAny<ArgumentException>(() => original.WithField(1, 0, Ascii("0001")));
        Assert.ThrowsAny<ArgumentException>(() => row.WithField(0, Ascii("2")));
        Assert.Equal(source, original.ToArray());
        Assert.Equal(1U, row.ItemId);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65)]
    public void OutOfRangeFieldIndicesAreRejected(int index)
    {
        var source = Row(Fields("1"), "");
        var original = ClientItemCatalogDocument.Parse(source);
        var row = Assert.Single(original.Rows);

        Assert.Throws<ArgumentOutOfRangeException>(() => original.WithField(1, index, Ascii("New")));
        Assert.Throws<ArgumentOutOfRangeException>(() => row.WithField(index, Ascii("New")));
        Assert.Throws<ArgumentOutOfRangeException>(() => row.GetField(index));
        Assert.Equal(source, original.ToArray());
    }

    [Fact]
    public void MissingItemIdIsNotSilentlyAddedOrAppliedToAnotherRow()
    {
        var source = Row(Fields("1"), "");
        var original = ClientItemCatalogDocument.Parse(source);

        Assert.Throws<KeyNotFoundException>(() => original.WithField(2, 1, Ascii("New")));
        Assert.Equal(source, original.ToArray());
    }

    private static byte[][] Fields(string itemId, byte[]? name = null, int fieldCount = NativeFieldCount)
    {
        var fields = Enumerable.Range(0, fieldCount).Select(index => Ascii(index == 0 ? itemId : $"field{index:D2}")).ToArray();
        if (name is not null && fields.Length > 1)
        {
            fields[1] = name.ToArray();
        }
        return fields;
    }

    private static byte[] Row(IReadOnlyList<byte[]> fields, string ending, bool trailingDelimiter = true)
    {
        using var output = new MemoryStream();
        for (var index = 0; index < fields.Count; ++index)
        {
            output.Write(fields[index]);
            if (trailingDelimiter || index + 1 < fields.Count)
            {
                output.Write(Delimiter);
            }
        }
        output.Write(Ascii(ending));
        return output.ToArray();
    }

    private static byte[] RebuildFromFields(ClientItemCatalogDocument document)
    {
        using var output = new MemoryStream();
        foreach (var row in document.Rows)
        {
            foreach (var field in row.Fields)
            {
                output.Write(field.Span);
                output.Write(Delimiter);
            }
            output.Write(row.LineEnding.Span);
        }
        return output.ToArray();
    }

    private static byte[] Concat(params byte[][] pieces)
    {
        using var output = new MemoryStream();
        foreach (var piece in pieces)
        {
            output.Write(piece);
        }
        return output.ToArray();
    }

    private static void CorruptReturnedMemory(ReadOnlyMemory<byte> memory)
    {
        if (!memory.IsEmpty && MemoryMarshal.TryGetArray(memory, out var segment))
        {
            Array.Fill(segment.Array!, (byte)'X', segment.Offset, segment.Count);
        }
    }

    private static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);
}
