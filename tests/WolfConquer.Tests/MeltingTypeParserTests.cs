using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class MeltingTypeParserTests
{
    [Fact]
    public void ParserReadsLiveLikeRowsAndSkipsComments()
    {
        const string content = """
            ; field comments from live file
            1 700002 0 0 1
            2 38 3330609 3329979
            2 40 730005 3009002 3314255 3311759 3009103 730002 730003
            """;

        var rules = MeltingTypeParser.Parse(content, "MeltingTYpe_3.ini");

        Assert.Equal(3, rules.Count);
        Assert.Equal("MeltingTYpe_3.ini", rules[0].SourceFile);
        Assert.Equal(2, rules[0].LineNumber);
        Assert.Equal(1, rules[0].RuleType);
        Assert.Equal(700002, rules[0].ItemTypeId);
        Assert.Equal([0, 0, 1], rules[0].Parameters);
        Assert.Equal(["1", "700002", "0", "0", "1"], rules[0].Fields);
        Assert.Equal("1 700002 0 0 1", rules[0].RawLine);

        Assert.Equal(2, rules[1].RuleType);
        Assert.Equal(38, rules[1].ItemTypeId);
        Assert.Equal([3330609, 3329979], rules[1].Parameters);

        Assert.Equal(40, rules[2].ItemTypeId);
        Assert.Equal([730005, 3009002, 3314255, 3311759, 3009103, 730002, 730003], rules[2].Parameters);
    }

    [Fact]
    public void RowParserPreservesRawLineWhileTrimmingWhitespaceFields()
    {
        const string row = "  2 12 730005 3009002 3309994 3309999 3320172   ";

        var rule = MeltingTypeParser.ParseRow("MeltingType_0.ini", 12, row);

        Assert.Equal(row, rule.RawLine);
        Assert.Equal(2, rule.RuleType);
        Assert.Equal(12, rule.ItemTypeId);
        Assert.Equal([730005, 3009002, 3309994, 3309999, 3320172], rule.Parameters);
        Assert.Equal(["2", "12", "730005", "3009002", "3309994", "3309999", "3320172"], rule.Fields);
    }

    [Theory]
    [InlineData("1 700002 0")]
    [InlineData("bad 700002 0 0")]
    [InlineData("1 bad 0 0")]
    [InlineData("1 700002 bad 0")]
    public void RowParserRejectsShortRowsAndInvalidNumericFields(string row)
    {
        Assert.Throws<FormatException>(() => MeltingTypeParser.ParseRow("MeltingType.ini", 1, row));
    }

    [Fact]
    public void RowParserRejectsInvalidLineNumbers()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MeltingTypeParser.ParseRow("MeltingType.ini", 0, "1 700002 0 0"));
    }
}
