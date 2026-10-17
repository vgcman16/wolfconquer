using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class SyndicateSkillTypeParserTests
{
    [Fact]
    public void SyndicateSkillTypeParserPreservesLiveFirstAndLastRows()
    {
        var first = SyndicateSkillTypeParser.ParseRow(
            "syn_skill_type.txt",
            1,
            "1@@1@@1@@500@@75050@@50000@@50@@");

        Assert.Equal(1, first.Id);
        Assert.Equal(1, first.SkillType);
        Assert.Equal(1, first.Level);
        Assert.Equal([500, 75050, 50000, 50], first.Values);
        Assert.Equal("syn_skill_type.txt", first.SourceFile);
        Assert.Equal(1, first.LineNumber);
        Assert.Equal("1@@1@@1@@500@@75050@@50000@@50@@", first.RawLine);
        Assert.Equal(["1", "1", "1", "500", "75050", "50000", "50", ""], first.Fields);

        var last = SyndicateSkillTypeParser.ParseRow(
            "syn_skill_type.txt",
            1400,
            "1400@@14@@100@@5000@@18000@@100000@@1000@@");

        Assert.Equal(1400, last.Id);
        Assert.Equal(14, last.SkillType);
        Assert.Equal(100, last.Level);
        Assert.Equal([5000, 18000, 100000, 1000], last.Values);
        Assert.Equal("syn_skill_type.txt", last.SourceFile);
        Assert.Equal(1400, last.LineNumber);
        Assert.Equal("1400@@14@@100@@5000@@18000@@100000@@1000@@", last.RawLine);
        Assert.Equal(["1400", "14", "100", "5000", "18000", "100000", "1000", ""], last.Fields);
    }

    [Theory]
    [InlineData("1@@1@@1@@500@@75050@@50000@@50")]
    [InlineData("1@@1@@1@@500@@75050@@50000@@50@@extra")]
    [InlineData("bad@@1@@1@@500@@75050@@50000@@50@@")]
    [InlineData("1@@1@@1@@500@@oops@@50000@@50@@")]
    public void SyndicateSkillTypeParserRejectsBadRows(string row)
    {
        Assert.Throws<FormatException>(() => SyndicateSkillTypeParser.ParseRow("syn_skill_type.txt", 1, row));
    }
}
