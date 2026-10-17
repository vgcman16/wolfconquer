using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class DisdainRuleParserTests
{
    [Fact]
    public void DisdainRuleParserPreservesLiveFirstAndLastRows()
    {
        var first = DisdainRuleParser.ParseRow(
            "cq_disdain.txt",
            1,
            "1,0,400,2800,0,60000,60000,-90,-90,1,2000,100,1,2000,100,1,2000,100,1,2000,100");

        Assert.Equal(1, first.Id);
        Assert.Equal([0, 400, 2800, 0, 60000, 60000, -90, -90, 1, 2000, 100, 1, 2000, 100, 1, 2000, 100, 1, 2000, 100], first.Values);
        Assert.Equal("cq_disdain.txt", first.SourceFile);
        Assert.Equal(1, first.LineNumber);
        Assert.Equal("1,0,400,2800,0,60000,60000,-90,-90,1,2000,100,1,2000,100,1,2000,100,1,2000,100", first.RawLine);
        Assert.Equal(
            ["1", "0", "400", "2800", "0", "60000", "60000", "-90", "-90", "1", "2000", "100", "1", "2000", "100", "1", "2000", "100", "1", "2000", "100"],
            first.Fields);

        var last = DisdainRuleParser.ParseRow(
            "cq_disdain.txt",
            801,
            "801,0,-400,0,0,60000,60000,30,30,1,111,18,1,191,18,1,191,18,1,191,18");

        Assert.Equal(801, last.Id);
        Assert.Equal([0, -400, 0, 0, 60000, 60000, 30, 30, 1, 111, 18, 1, 191, 18, 1, 191, 18, 1, 191, 18], last.Values);
        Assert.Equal("cq_disdain.txt", last.SourceFile);
        Assert.Equal(801, last.LineNumber);
        Assert.Equal("801,0,-400,0,0,60000,60000,30,30,1,111,18,1,191,18,1,191,18,1,191,18", last.RawLine);
        Assert.Equal(
            ["801", "0", "-400", "0", "0", "60000", "60000", "30", "30", "1", "111", "18", "1", "191", "18", "1", "191", "18", "1", "191", "18"],
            last.Fields);
    }

    [Theory]
    [InlineData("1,0,400")]
    [InlineData("1,0,400,2800,0,60000,60000,-90,-90,1,2000,100,1,2000,100,1,2000,100,1,2000,100,0")]
    [InlineData("bad,0,400,2800,0,60000,60000,-90,-90,1,2000,100,1,2000,100,1,2000,100,1,2000,100")]
    [InlineData("1,0,oops,2800,0,60000,60000,-90,-90,1,2000,100,1,2000,100,1,2000,100,1,2000,100")]
    [InlineData("1,0,,2800,0,60000,60000,-90,-90,1,2000,100,1,2000,100,1,2000,100,1,2000,100")]
    public void DisdainRuleParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => DisdainRuleParser.ParseRow("cq_disdain.txt", 1, row));
    }
}
