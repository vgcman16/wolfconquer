using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class SpiritRateParserTests
{
    [Fact]
    public void SpiritRateParserPreservesLiveRowsAndOrderedParameters()
    {
        var entry = SpiritRateParser.ParseRow(
            "spirit_rate.txt",
            1,
            "76@@1@@4200001@@0@@0@@5@@0@@0@@");

        Assert.Equal(76, entry.Id);
        Assert.Equal(1, entry.Type);
        Assert.Equal(4_200_001, entry.ItemTypeId);
        Assert.Equal([0, 0, 5, 0, 0], entry.Parameters);
        Assert.Equal("spirit_rate.txt", entry.SourceFile);
        Assert.Equal(1, entry.LineNumber);
        Assert.Equal("76@@1@@4200001@@0@@0@@5@@0@@0@@", entry.RawLine);
        Assert.Equal(["76", "1", "4200001", "0", "0", "5", "0", "0", ""], entry.Fields);

        var upgrade = SpiritRateParser.ParseRow(
            "spirit_rate.txt",
            76,
            "151@@0@@4200007@@2@@1@@4200008@@0@@0@@");

        Assert.Equal(151, upgrade.Id);
        Assert.Equal(0, upgrade.Type);
        Assert.Equal(4_200_007, upgrade.ItemTypeId);
        Assert.Equal([2, 1, 4_200_008, 0, 0], upgrade.Parameters);
        Assert.Equal("", upgrade.Fields[^1]);
    }

    [Fact]
    public void SpiritRateParserReadsContentAndKeepsSourceLineNumbers()
    {
        const string content = """
        76@@1@@4200001@@0@@0@@5@@0@@0@@

        151@@0@@4200007@@2@@1@@4200008@@0@@0@@
        """;

        var entries = SpiritRateParser.Parse(content, "spirit_rate.txt");

        Assert.Equal(2, entries.Count);
        Assert.Equal(1, entries[0].LineNumber);
        Assert.Equal(3, entries[1].LineNumber);
        Assert.Equal("151@@0@@4200007@@2@@1@@4200008@@0@@0@@", entries[1].RawLine);
    }

    [Theory]
    [InlineData("76@@1@@4200001@@0@@0@@5@@0@@0")]
    [InlineData("76@@1@@4200001@@0@@0@@5@@0@@0@@extra")]
    [InlineData("x@@1@@4200001@@0@@0@@5@@0@@0@@")]
    [InlineData("76@@1@@item@@0@@0@@5@@0@@0@@")]
    [InlineData("76@@1@@4200001@@0@@nope@@5@@0@@0@@")]
    public void SpiritRateParserRejectsWrongFieldCountsMissingTrailingEmptyFieldAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => SpiritRateParser.ParseRow("spirit_rate.txt", 1, row));
    }
}
