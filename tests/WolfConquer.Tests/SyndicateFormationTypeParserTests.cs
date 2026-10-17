using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class SyndicateFormationTypeParserTests
{
    [Fact]
    public void SyndicateFormationTypeParserPreservesLiveFirstAndLastRows()
    {
        var first = SyndicateFormationTypeParser.ParseRow(
            "syn_formtype.txt",
            1,
            "1@@100@@1@@100000000@@120000000@@700@@1000@@0@@");

        Assert.Equal(1, first.Id);
        Assert.Equal(100, first.Type);
        Assert.Equal(1, first.Level);
        Assert.Equal([100_000_000L, 120_000_000L, 700L, 1000L, 0L], first.Values);
        Assert.Equal("syn_formtype.txt", first.SourceFile);
        Assert.Equal(1, first.LineNumber);
        Assert.Equal("1@@100@@1@@100000000@@120000000@@700@@1000@@0@@", first.RawLine);
        Assert.Equal(["1", "100", "1", "100000000", "120000000", "700", "1000", "0", ""], first.Fields);

        var last = SyndicateFormationTypeParser.ParseRow(
            "syn_formtype.txt",
            187,
            "187@@500@@15@@0@@0@@0@@0@@0@@");

        Assert.Equal(187, last.Id);
        Assert.Equal(500, last.Type);
        Assert.Equal(15, last.Level);
        Assert.Equal([0L, 0L, 0L, 0L, 0L], last.Values);
        Assert.Equal("syn_formtype.txt", last.SourceFile);
        Assert.Equal(187, last.LineNumber);
        Assert.Equal("187@@500@@15@@0@@0@@0@@0@@0@@", last.RawLine);
        Assert.Equal(["187", "500", "15", "0", "0", "0", "0", "0", ""], last.Fields);
    }

    [Theory]
    [InlineData("1@@100@@1@@100000000@@120000000@@700@@1000@@0")]
    [InlineData("1@@100@@1@@100000000@@120000000@@700@@1000@@0@@extra")]
    [InlineData("bad@@100@@1@@100000000@@120000000@@700@@1000@@0@@")]
    [InlineData("1@@100@@1@@100000000@@oops@@700@@1000@@0@@")]
    public void SyndicateFormationTypeParserRejectsBadRows(string row)
    {
        Assert.Throws<FormatException>(() => SyndicateFormationTypeParser.ParseRow("syn_formtype.txt", 1, row));
    }
}
