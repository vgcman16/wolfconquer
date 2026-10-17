using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class JiangHuParserTests
{
    [Fact]
    public void JiangHuAttributeParserReadsCountedLiveRowsAndPreservesMetadata()
    {
        const string content = "Count=2\n1 10 1 5 \n90 15 6 230 \n";

        var attributes = JiangHuParser.ParseAttributes(content, "JianghuAttributes.txt");

        Assert.Equal(2, attributes.Count);

        var first = attributes[0];
        Assert.Equal(1, first.Id);
        Assert.Equal(10, first.AttributeType);
        Assert.Equal(1, first.Level);
        Assert.Equal(5, first.Value);
        Assert.Equal("JianghuAttributes.txt", first.SourceFile);
        Assert.Equal(2, first.LineNumber);
        Assert.Equal("1 10 1 5 ", first.RawLine);
        Assert.Equal(["1", "10", "1", "5"], first.Fields);

        var last = attributes[1];
        Assert.Equal(90, last.Id);
        Assert.Equal(15, last.AttributeType);
        Assert.Equal(6, last.Level);
        Assert.Equal(230, last.Value);
        Assert.Equal("90 15 6 230 ", last.RawLine);
        Assert.Equal(["90", "15", "6", "230"], last.Fields);
    }

    [Fact]
    public void JiangHuCultivateStatusParserReadsCountedLiveRowsAndDeclaredValues()
    {
        const string content = "Count=2\n1 7 3 5 7 11 13 14 15\n9 8 1 4 5 6 7 11 14 15\n";

        var statuses = JiangHuParser.ParseCultivateStatuses(content, "JingHuCultivateStatus.txt");

        Assert.Equal(2, statuses.Count);

        var first = statuses[0];
        Assert.Equal(1, first.Id);
        Assert.Equal(7, first.DeclaredValueCount);
        Assert.Equal([3, 5, 7, 11, 13, 14, 15], first.Values);
        Assert.Equal("JingHuCultivateStatus.txt", first.SourceFile);
        Assert.Equal(2, first.LineNumber);
        Assert.Equal("1 7 3 5 7 11 13 14 15", first.RawLine);
        Assert.Equal(["1", "7", "3", "5", "7", "11", "13", "14", "15"], first.Fields);

        var last = statuses[1];
        Assert.Equal(9, last.Id);
        Assert.Equal(8, last.DeclaredValueCount);
        Assert.Equal([1, 4, 5, 6, 7, 11, 14, 15], last.Values);
        Assert.Equal(["9", "8", "1", "4", "5", "6", "7", "11", "14", "15"], last.Fields);
    }

    [Theory]
    [InlineData("1 10 1")]
    [InlineData("1 10 1 5 9")]
    [InlineData("x 10 1 5")]
    [InlineData("1 bad 1 5")]
    [InlineData("1 10 1 nope")]
    public void JiangHuAttributeParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => JiangHuParser.ParseAttributeRow("JianghuAttributes.txt", 2, row));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1 x 3 5")]
    [InlineData("1 -1")]
    [InlineData("1 3 3 5")]
    [InlineData("1 1 3 5")]
    [InlineData("1 2 3 bad")]
    public void JiangHuCultivateStatusParserRejectsMalformedRows(string row)
    {
        Assert.Throws<FormatException>(() => JiangHuParser.ParseCultivateStatusRow("JingHuCultivateStatus.txt", 2, row));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1 10 1 5\n")]
    [InlineData("Count=x\n1 10 1 5\n")]
    [InlineData("Count=-1\n")]
    [InlineData("Count=2\n1 10 1 5\n")]
    [InlineData("Count=0\n1 10 1 5\n")]
    public void JiangHuAttributeFileParserRejectsMissingMalformedAndMismatchedCountHeaders(string content)
    {
        Assert.Throws<FormatException>(() => JiangHuParser.ParseAttributes(content, "JianghuAttributes.txt"));
    }

    [Fact]
    public void JiangHuCultivateStatusFileParserRejectsMismatchedCountHeader()
    {
        const string content = "Count=1\n1 7 3 5 7 11 13 14 15\n9 8 1 4 5 6 7 11 14 15\n";

        Assert.Throws<FormatException>(() => JiangHuParser.ParseCultivateStatuses(content, "JingHuCultivateStatus.txt"));
    }
}
