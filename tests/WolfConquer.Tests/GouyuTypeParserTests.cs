using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class GouyuTypeParserTests
{
    [Fact]
    public void GouyuTypeParserPreservesLiveBoundaryRowsAndOrderedValues()
    {
        var first = GouyuTypeParser.ParseRow(
            "gouyu_type.txt",
            1,
            "1@@100@@1@@50@@10537@@0@@16520@@16530@@0@@0@@0@@100@@10@@");

        Assert.Equal(1, first.Id);
        Assert.Equal(100, first.Type);
        Assert.Equal(1, first.Level);
        Assert.Equal([50, 10_537, 0, 16_520, 16_530, 0, 0, 0, 100, 10], first.Values);
        Assert.Equal("gouyu_type.txt", first.SourceFile);
        Assert.Equal(1, first.LineNumber);
        Assert.Equal("1@@100@@1@@50@@10537@@0@@16520@@16530@@0@@0@@0@@100@@10@@", first.RawLine);
        Assert.Equal(["1", "100", "1", "50", "10537", "0", "16520", "16530", "0", "0", "0", "100", "10", ""], first.Fields);

        var last = GouyuTypeParser.ParseRow(
            "gouyu_type.txt",
            315,
            "315@@604@@9@@0@@37008@@0@@0@@16390@@0@@0@@0@@2000@@8370@@");

        Assert.Equal(315, last.Id);
        Assert.Equal(604, last.Type);
        Assert.Equal(9, last.Level);
        Assert.Equal([0, 37_008, 0, 0, 16_390, 0, 0, 0, 2_000, 8_370], last.Values);
        Assert.Equal("gouyu_type.txt", last.SourceFile);
        Assert.Equal(315, last.LineNumber);
        Assert.Equal("315@@604@@9@@0@@37008@@0@@0@@16390@@0@@0@@0@@2000@@8370@@", last.RawLine);
        Assert.Equal(["315", "604", "9", "0", "37008", "0", "0", "16390", "0", "0", "0", "2000", "8370", ""], last.Fields);
    }

    [Theory]
    [InlineData("1@@100@@1@@50@@10537@@0@@16520@@16530@@0@@0@@0@@100@@10")]
    [InlineData("1@@100@@1@@50@@10537@@0@@16520@@16530@@0@@0@@0@@100@@10@@extra")]
    public void GouyuTypeParserRejectsMalformedFieldCountAndTrailingFieldRows(string row)
    {
        Assert.Throws<FormatException>(() => GouyuTypeParser.ParseRow("gouyu_type.txt", 1, row));
    }

    [Theory]
    [InlineData("x@@100@@1@@50@@10537@@0@@16520@@16530@@0@@0@@0@@100@@10@@")]
    [InlineData("1@@100@@1@@bad@@10537@@0@@16520@@16530@@0@@0@@0@@100@@10@@")]
    public void GouyuTypeParserRejectsNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => GouyuTypeParser.ParseRow("gouyu_type.txt", 1, row));
    }
}
