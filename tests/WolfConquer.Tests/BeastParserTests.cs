using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class BeastParserTests
{
    [Fact]
    public void BeastParserPreservesFirstLiveRowShape()
    {
        const string row = "1@@2400@@200@@200@@500@@100@@100@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@";

        var beast = BeastParser.ParseRow("beasts_attr.txt", 1, row);

        Assert.Equal(1, beast.Level);
        Assert.Equal([2400, 200, 200, 500, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], beast.Parameters);
        Assert.Equal("beasts_attr.txt", beast.SourceFile);
        Assert.Equal(1, beast.LineNumber);
        Assert.Equal(row, beast.RawLine);
        Assert.Equal(["1", "2400", "200", "200", "500", "100", "100", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", ""], beast.Fields);
    }

    [Fact]
    public void BeastParserPreservesLaterLiveRowShape()
    {
        const string row = "100@@48000@@1200@@2000@@3500@@600@@600@@1000@@1200@@1500@@1000@@0@@0@@0@@0@@0@@4@@0@@";

        var beast = BeastParser.ParseRow("beasts_attr.txt", 100, row);

        Assert.Equal(100, beast.Level);
        Assert.Equal([48000, 1200, 2000, 3500, 600, 600, 1000, 1200, 1500, 1000, 0, 0, 0, 0, 0, 4, 0], beast.Parameters);
        Assert.Equal("beasts_attr.txt", beast.SourceFile);
        Assert.Equal(100, beast.LineNumber);
        Assert.Equal(row, beast.RawLine);
        Assert.Equal("", beast.Fields[^1]);
    }

    [Theory]
    [InlineData("1@@2400@@200@@200@@500@@100@@100@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0")]
    [InlineData("1@@2400@@200@@200@@500@@100@@100@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@extra")]
    [InlineData("x@@2400@@200@@200@@500@@100@@100@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@")]
    [InlineData("1@@2400@@bad@@200@@500@@100@@100@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@")]
    public void BeastParserRejectsWrongFieldCountsTrailingFieldsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => BeastParser.ParseRow("beasts_attr.txt", 1, row));
    }
}
