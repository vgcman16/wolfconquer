using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class FateParserTests
{
    [Fact]
    public void FateParserPreservesLiveAtAtRows()
    {
        var first = FateParser.ParseRow(
            "fate_exp.txt",
            1,
            "1@@1@@1@@500@@60000@@120000@@130000@@90000@@0@@0@@0@@10@@0@@0@@3300576@@");

        Assert.Equal(1, first.Id);
        Assert.Equal(1, first.Type);
        Assert.Equal(1, first.Level);
        Assert.Equal(500, first.Experience);
        Assert.Equal([60000L, 120000L, 130000L, 90000L, 0L, 0L, 0L, 10L, 0L, 0L, 3300576L], first.Values);
        Assert.Equal("fate_exp.txt", first.SourceFile);
        Assert.Equal(1, first.LineNumber);
        Assert.Equal("1@@1@@1@@500@@60000@@120000@@130000@@90000@@0@@0@@0@@10@@0@@0@@3300576@@", first.RawLine);
        Assert.Equal(
            ["1", "1", "1", "500", "60000", "120000", "130000", "90000", "0", "0", "0", "10", "0", "0", "3300576", ""],
            first.Fields);

        var last = FateParser.ParseRow(
            "fate_exp.txt",
            396,
            "396@@4@@99@@6063000@@40080@@50080@@30040@@0@@85@@90@@5000@@3000@@1000@@5000@@3008202@@");

        Assert.Equal(396, last.Id);
        Assert.Equal(4, last.Type);
        Assert.Equal(99, last.Level);
        Assert.Equal(6_063_000, last.Experience);
        Assert.Equal([40080L, 50080L, 30040L, 0L, 85L, 90L, 5000L, 3000L, 1000L, 5000L, 3008202L], last.Values);
        Assert.Equal("", last.Fields[^1]);
        Assert.Equal(396, last.LineNumber);
    }

    [Theory]
    [InlineData("1@@1@@1@@500@@60000@@120000@@130000@@90000@@0@@0@@0@@10@@0@@0@@3300576")]
    [InlineData("1@@1@@1@@500@@60000@@120000@@130000@@90000@@0@@0@@0@@10@@0@@0@@3300576@@extra")]
    [InlineData("x@@1@@1@@500@@60000@@120000@@130000@@90000@@0@@0@@0@@10@@0@@0@@3300576@@")]
    [InlineData("1@@1@@1@@500@@60000@@oops@@130000@@90000@@0@@0@@0@@10@@0@@0@@3300576@@")]
    public void FateParserRejectsWrongFieldCountsMissingTrailingAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => FateParser.ParseRow("fate_exp.txt", 1, row));
    }
}
