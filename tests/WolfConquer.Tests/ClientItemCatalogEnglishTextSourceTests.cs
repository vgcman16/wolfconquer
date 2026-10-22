using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientItemCatalogEnglishTextSourceTests
{
    [Fact]
    public void MixedEndingSourceTracksEachTextFieldIndependentlyAndOwnsAllExposedBytes()
    {
        var input = Join(
            Render(Fields("1", "English One", "Medicine", "Restores HP", 67), "\r\n"),
            Render(Fields("2", "", "123", "Use on ally.", 67), "\n"),
            Render(Fields("3", "", "Weapon", "", 67), "\r"),
            Render(Fields("4", "Name Four", "", "", 67), ""));

        var source = ClientItemCatalogEnglishTextSource.Parse(input);
        Array.Fill(input, (byte)'X');
        foreach (var dictionary in new[] { source.Names, source.Categories, source.Descriptions })
            foreach (var value in dictionary.Values) Corrupt(value);

        Assert.Equal(4, source.SourceRowCount);
        Assert.Equal(2, source.SkippedNameCount);
        Assert.Equal(2, source.SkippedCategoryCount);
        Assert.Equal(2, source.SkippedDescriptionCount);
        Assert.Equal(new uint[] { 1, 4 }, source.Names.Keys.Order());
        Assert.Equal(new uint[] { 1, 3 }, source.Categories.Keys.Order());
        Assert.Equal(new uint[] { 1, 2 }, source.Descriptions.Keys.Order());
        Assert.Equal(Ascii("English One"), source.Names[1].ToArray());
        Assert.Equal(Ascii("Medicine"), source.Categories[1].ToArray());
        Assert.Equal(Ascii("Restores HP"), source.Descriptions[1].ToArray());
    }

    [Theory]
    [InlineData(52, "empty")]
    [InlineData(52, "numeric")]
    [InlineData(52, "control")]
    [InlineData(52, "non-ascii")]
    [InlineData(53, "empty")]
    [InlineData(53, "numeric")]
    [InlineData(53, "control")]
    [InlineData(53, "non-ascii")]
    public void CategoryAndDescriptionApplyTheEnglishPolicyWithoutSuppressingOtherEligibleText(int field, string scenario)
    {
        var fields = Fields("1", "English Name", "English Category", "English Description", 67);
        fields[field] = scenario switch
        {
            "empty" => [],
            "numeric" => Ascii("12345"),
            "control" => Ascii("Bad\tText"),
            "non-ascii" => new byte[] { 0xB5, 0xA5 },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

        var source = ClientItemCatalogEnglishTextSource.Parse(Render(fields, "\n"));

        Assert.Single(source.Names);
        Assert.Equal(0, source.SkippedNameCount);
        Assert.Equal(field == 52 ? 1 : 0, source.SkippedCategoryCount);
        Assert.Equal(field == 53 ? 1 : 0, source.SkippedDescriptionCount);
        Assert.Equal(field != 52, source.Categories.ContainsKey(1));
        Assert.Equal(field != 53, source.Descriptions.ContainsKey(1));
    }

    [Theory]
    [InlineData("65-fields")]
    [InlineData("skipped-duplicate")]
    [InlineData("nul-description")]
    [InlineData("missing-trailer")]
    public void StrictDonorValidationStillCoversSkippedRowsAndAllTextFields(string scenario)
    {
        var fields = Fields("1", "Name", "Category", "Description", scenario == "65-fields" ? 65 : 67);
        if (scenario == "nul-description") fields[53] = Ascii("Bad\0Description");
        var bytes = scenario switch
        {
            "skipped-duplicate" => Join(
                Render(Fields("01", "", "", "", 67), "\n"),
                Render(Fields("1", "Name", "Category", "Description", 67), "")),
            "missing-trailer" => Render(fields, "")[..^2],
            _ => Render(fields, "\n"),
        };

        Assert.Throws<InvalidDataException>(() => ClientItemCatalogEnglishTextSource.Parse(bytes));
    }

    [Fact]
    public void SourceRetainsPrintablePercentDescriptionsForTheSeparateCliSafetyGate()
    {
        var source = ClientItemCatalogEnglishTextSource.Parse(Join(
            Render(Fields("1", "One", "Category", "Format %s", 67), "\n"),
            Render(Fields("2", "Two", "Category", "Literal %% text", 67), "")));

        Assert.Equal(0, source.SkippedDescriptionCount);
        Assert.Equal(Ascii("Format %s"), source.Descriptions[1].ToArray());
        Assert.Equal(Ascii("Literal %% text"), source.Descriptions[2].ToArray());
    }

    [Fact]
    public void TypedBatchClonesValuesAndChangesOnlyThreeFieldsWithNullMeaningLeaveUnchanged()
    {
        var first = Fields("0001", "Old Name", "Old Category", "Old Description", 65);
        first[40] = [0x81, 0x80, 0xFF];
        var second = Fields("2", "Keep Name", "Old Two", "Keep Description", 65);
        var third = Fields("3", "Unchanged", "Unchanged", "Unchanged", 65);
        var input = Join(Render(first, "\r\n"), Render(second, "\n"), Render(third, ""));
        var original = ClientItemCatalogDocument.Parse(input);
        var name = Ascii("New Name");
        var category = Ascii("New Category");
        var description = Ascii("New Description");
        var changes = new Dictionary<uint, ClientItemCatalogTextFields>
        {
            [1] = new(name: name, category: category, description: description),
            [2] = new(category: Ascii("New Two")),
        };
        Array.Fill(name, (byte)'X');
        Array.Fill(category, (byte)'Y');
        Array.Fill(description, (byte)'Z');
        Corrupt(changes[1].Name!.Value);
        Corrupt(changes[1].Category!.Value);
        Corrupt(changes[1].Description!.Value);

        var edited = original.WithTextFields(changes);
        changes.Clear();

        Assert.Equal(new uint[] { 1, 2, 3 }, edited.Rows.Select(static row => row.ItemId));
        for (var row = 0; row < original.Rows.Count; ++row)
        {
            Assert.Equal(original.Rows[row].LineEnding.ToArray(), edited.Rows[row].LineEnding.ToArray());
            for (var field = 0; field < 65; ++field)
                if (field is not (1 or 52 or 53))
                    Assert.Equal(original.Rows[row].GetField(field).ToArray(), edited.Rows[row].GetField(field).ToArray());
        }
        Assert.Equal(Ascii("New Name"), edited.Rows[0].GetField(1).ToArray());
        Assert.Equal(Ascii("New Category"), edited.Rows[0].GetField(52).ToArray());
        Assert.Equal(Ascii("New Description"), edited.Rows[0].GetField(53).ToArray());
        Assert.Equal(second[1], edited.Rows[1].GetField(1).ToArray());
        Assert.Equal(Ascii("New Two"), edited.Rows[1].GetField(52).ToArray());
        Assert.Equal(second[53], edited.Rows[1].GetField(53).ToArray());
        Assert.Equal(input, original.ToArray());
        first[1] = Ascii("New Name");
        first[52] = Ascii("New Category");
        first[53] = Ascii("New Description");
        second[52] = Ascii("New Two");
        Assert.Equal(Join(Render(first, "\r\n"), Render(second, "\n"), Render(third, "")), edited.ToArray());
    }

    [Fact]
    public void NullTextMembersAreNoOpsButAnExplicitEmptyValueClearsOnlyThatField()
    {
        var input = Render(Fields("1", "Name", "Category", "Description", 65), "\n");
        var original = ClientItemCatalogDocument.Parse(input);
        var leaveAll = new ClientItemCatalogTextFields();
        Assert.Null(leaveAll.Name);
        Assert.Null(leaveAll.Category);
        Assert.Null(leaveAll.Description);
        Assert.Equal(input, original.WithTextFields(new Dictionary<uint, ClientItemCatalogTextFields> { [1] = leaveAll }).ToArray());

        var cleared = original.WithTextFields(new Dictionary<uint, ClientItemCatalogTextFields>
        {
            [1] = new(category: ReadOnlyMemory<byte>.Empty),
        });

        Assert.True(cleared.Rows[0].GetField(52).IsEmpty);
        Assert.Equal(Ascii("Name"), cleared.Rows[0].GetField(1).ToArray());
        Assert.Equal(Ascii("Description"), cleared.Rows[0].GetField(53).ToArray());
        Assert.Equal(input, original.ToArray());
    }

    [Theory]
    [InlineData(52, "Bad@@Text")]
    [InlineData(52, "Bad\0Text")]
    [InlineData(53, "Bad@")]
    [InlineData(53, "Bad\nText")]
    public void UnsafeLaterTextEditCannotPartiallyMutateEarlierSourceRows(int field, string value)
    {
        var input = Join(Render(Fields("1", "One", "Cat One", "Desc One", 65), "\r\n"),
            Render(Fields("2", "Two", "Cat Two", "Desc Two", 65), ""));
        var original = ClientItemCatalogDocument.Parse(input);
        var unsafeFields = field == 52
            ? new ClientItemCatalogTextFields(category: Ascii(value))
            : new ClientItemCatalogTextFields(description: Ascii(value));
        var changes = new Dictionary<uint, ClientItemCatalogTextFields>
        {
            [1] = new(name: Ascii("Valid First")),
            [2] = unsafeFields,
        };

        Assert.ThrowsAny<ArgumentException>(() => original.WithTextFields(changes));
        Assert.Equal(input, original.ToArray());
        Assert.Equal(Ascii("One"), original.Rows[0].GetField(1).ToArray());
    }

    [Fact]
    public void UnknownIdsAndNullBatchValuesAreRejectedWithoutChangingTheDocument()
    {
        var input = Render(Fields("1", "Name", "Category", "Description", 65), "");
        var original = ClientItemCatalogDocument.Parse(input);
        Assert.Throws<KeyNotFoundException>(() => original.WithTextFields(new Dictionary<uint, ClientItemCatalogTextFields>
        {
            [1] = new(name: Ascii("Valid First")), [999] = new(category: Ascii("Unknown")),
        }));
        Assert.ThrowsAny<ArgumentException>(() => original.WithTextFields(new Dictionary<uint, ClientItemCatalogTextFields>
        {
            [1] = null!,
        }));
        Assert.Equal(input, original.ToArray());
    }

    private static byte[][] Fields(string id, string name, string category, string description, int count)
    {
        var fields = Enumerable.Range(0, count).Select(index => Ascii(index == 0 ? id : $"stat{index:D2}")).ToArray();
        fields[1] = Ascii(name);
        fields[52] = Ascii(category);
        fields[53] = Ascii(description);
        return fields;
    }

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

    private static void Corrupt(ReadOnlyMemory<byte> memory)
    {
        if (!memory.IsEmpty && MemoryMarshal.TryGetArray(memory, out var segment))
            Array.Fill(segment.Array!, (byte)'X', segment.Offset, segment.Count);
    }

    private static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);
}
