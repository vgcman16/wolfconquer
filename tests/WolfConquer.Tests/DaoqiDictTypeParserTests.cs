using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class DaoqiDictTypeParserTests
{
    [Fact]
    public void DaoqiDictTypeParserPreservesLiveFirstAndLastRows()
    {
        var first = DaoqiDictTypeParser.ParseRow(
            "daoqi_dict_type.txt",
            1,
            "113@@0@@1@@100@@0@@0@@100@@1000@@900@@113@@1@@0@@0@@0@@0@@");

        Assert.Equal(113, first.Id);
        Assert.Equal(0, first.Type);
        Assert.Equal(1, first.Level);
        Assert.Equal([100, 0, 0, 100, 1000, 900, 113, 1, 0, 0, 0, 0], first.Values);
        Assert.Equal("daoqi_dict_type.txt", first.SourceFile);
        Assert.Equal(1, first.LineNumber);
        Assert.Equal("113@@0@@1@@100@@0@@0@@100@@1000@@900@@113@@1@@0@@0@@0@@0@@", first.RawLine);
        Assert.Equal(
            ["113", "0", "1", "100", "0", "0", "100", "1000", "900", "113", "1", "0", "0", "0", "0", ""],
            first.Fields);

        var last = DaoqiDictTypeParser.ParseRow(
            "daoqi_dict_type.txt",
            326,
            "26907@@1@@24@@21820009@@0@@0@@600@@0@@0@@26007@@10@@0@@0@@10@@0@@");

        Assert.Equal(26907, last.Id);
        Assert.Equal(1, last.Type);
        Assert.Equal(24, last.Level);
        Assert.Equal([21_820_009, 0, 0, 600, 0, 0, 26_007, 10, 0, 0, 10, 0], last.Values);
        Assert.Equal("daoqi_dict_type.txt", last.SourceFile);
        Assert.Equal(326, last.LineNumber);
        Assert.Equal("26907@@1@@24@@21820009@@0@@0@@600@@0@@0@@26007@@10@@0@@0@@10@@0@@", last.RawLine);
        Assert.Equal(
            ["26907", "1", "24", "21820009", "0", "0", "600", "0", "0", "26007", "10", "0", "0", "10", "0", ""],
            last.Fields);
    }

    [Theory]
    [InlineData("113@@0@@1@@100@@0@@0@@100@@1000@@900@@113@@1@@0@@0@@0@@0")]
    [InlineData("113@@0@@1@@100@@0@@0@@100@@1000@@900@@113@@1@@0@@0@@0@@0@@extra")]
    [InlineData("bad@@0@@1@@100@@0@@0@@100@@1000@@900@@113@@1@@0@@0@@0@@0@@")]
    [InlineData("113@@0@@1@@bad@@0@@0@@100@@1000@@900@@113@@1@@0@@0@@0@@0@@")]
    public void DaoqiDictTypeParserRejectsMalformedTrailingAndNonnumericRows(string row)
    {
        Assert.Throws<FormatException>(() => DaoqiDictTypeParser.ParseRow("daoqi_dict_type.txt", 1, row));
    }
}
