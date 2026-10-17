using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class GouyuImmortalParserTests
{
    [Fact]
    public void GouyuImmortalParserPreservesFirstLiveRowShape()
    {
        const string row = "8@@8@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@";

        var entry = GouyuImmortalParser.ParseRow("gouyu_immortal.txt", 1, row);

        Assert.Equal(8, entry.Id);
        Assert.Equal([8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], entry.Values);
        Assert.Equal("gouyu_immortal.txt", entry.SourceFile);
        Assert.Equal(1, entry.LineNumber);
        Assert.Equal(row, entry.RawLine);
        Assert.Equal(["8", "8", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", ""], entry.Fields);
    }

    [Fact]
    public void GouyuImmortalParserPreservesLastLiveRowShape()
    {
        const string row = "17@@72@@4000@@1000@@1000@@600@@5800@@10@@5000@@5000@@0@@0@@3000@@2000@@1500@@9@@";

        var entry = GouyuImmortalParser.ParseRow("gouyu_immortal.txt", 10, row);

        Assert.Equal(17, entry.Id);
        Assert.Equal([72, 4000, 1000, 1000, 600, 5800, 10, 5000, 5000, 0, 0, 3000, 2000, 1500, 9], entry.Values);
        Assert.Equal("gouyu_immortal.txt", entry.SourceFile);
        Assert.Equal(10, entry.LineNumber);
        Assert.Equal(row, entry.RawLine);
        Assert.Equal(["17", "72", "4000", "1000", "1000", "600", "5800", "10", "5000", "5000", "0", "0", "3000", "2000", "1500", "9", ""], entry.Fields);
    }

    [Theory]
    [InlineData("8@@8@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0")]
    [InlineData("8@@8@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@extra")]
    [InlineData("x@@8@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@")]
    [InlineData("8@@8@@0@@bad@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@")]
    public void GouyuImmortalParserRejectsBadShapesAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => GouyuImmortalParser.ParseRow("gouyu_immortal.txt", 1, row));
    }
}
