using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class ActivityObjectParserTests
{
    [Fact]
    public void PokerTableParserPreservesLiveFirstAndLastRows()
    {
        const string firstRow = "102130 194 128 7247567 013 1 0 10000 1";

        var first = ActivityObjectParser.ParsePokerTableRow("PokerTables.txt", 1, firstRow);

        Assert.Equal(102130, first.Id);
        Assert.Equal([194, 128, 7247567, 13, 1, 0, 10000, 1], first.Values);
        Assert.Equal("PokerTables.txt", first.SourceFile);
        Assert.Equal(1, first.LineNumber);
        Assert.Equal(firstRow, first.RawLine);
        Assert.Equal(["102130", "194", "128", "7247567", "013", "1", "0", "10000", "1"], first.Fields);

        const string lastRow = "102230 326 0 1002967 099 1 0 50000000 1";

        var last = ActivityObjectParser.ParsePokerTableRow("PokerTables.txt", 50, lastRow);

        Assert.Equal(102230, last.Id);
        Assert.Equal([326, 0, 1002967, 99, 1, 0, 50000000, 1], last.Values);
        Assert.Equal("PokerTables.txt", last.SourceFile);
        Assert.Equal(50, last.LineNumber);
        Assert.Equal(lastRow, last.RawLine);
        Assert.Equal(["102230", "326", "0", "1002967", "099", "1", "0", "50000000", "1"], last.Fields);
    }

    [Fact]
    public void RouletteTableParserPreservesLiveFirstAndLastRows()
    {
        const string firstRow = "102183,6,1,3852,60,85,30236";

        var first = ActivityObjectParser.ParseRouletteTableRow("Roulettes.txt", 1, firstRow);

        Assert.Equal(102183, first.Id);
        Assert.Equal([6, 1, 3852, 60, 85, 30236], first.Values);
        Assert.Equal("Roulettes.txt", first.SourceFile);
        Assert.Equal(1, first.LineNumber);
        Assert.Equal(firstRow, first.RawLine);
        Assert.Equal(["102183", "6", "1", "3852", "60", "85", "30236"], first.Fields);

        const string lastRow = "102095,8,1,3852,94,85,30236";

        var last = ActivityObjectParser.ParseRouletteTableRow("Roulettes.txt", 20, lastRow);

        Assert.Equal(102095, last.Id);
        Assert.Equal([8, 1, 3852, 94, 85, 30236], last.Values);
        Assert.Equal("Roulettes.txt", last.SourceFile);
        Assert.Equal(20, last.LineNumber);
        Assert.Equal(lastRow, last.RawLine);
        Assert.Equal(["102095", "8", "1", "3852", "94", "85", "30236"], last.Fields);
    }

    [Fact]
    public void NpcTimeParserPreservesLiveRow()
    {
        const string row = "78156,EpicPirate,2,8940,5000,42,33,0,0,1,11";

        var entry = ActivityObjectParser.ParseNpcTimeRow("NpcTime.txt", 1, row);

        Assert.Equal(78156, entry.Id);
        Assert.Equal("EpicPirate", entry.Label);
        Assert.Equal([2, 8940, 5000, 42, 33, 0, 0, 1, 11], entry.Values);
        Assert.Equal("NpcTime.txt", entry.SourceFile);
        Assert.Equal(1, entry.LineNumber);
        Assert.Equal(row, entry.RawLine);
        Assert.Equal(["78156", "EpicPirate", "2", "8940", "5000", "42", "33", "0", "0", "1", "11"], entry.Fields);
    }

    [Theory]
    [InlineData("102130 194 128 7247567 013 1 0 10000")]
    [InlineData("102130 194 128 7247567 013 1 0 10000 1 0")]
    [InlineData("102130 194 128 7247567 x 1 0 10000 1")]
    public void PokerTableParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => ActivityObjectParser.ParsePokerTableRow("PokerTables.txt", 1, row));
    }

    [Theory]
    [InlineData("102183,6,1,3852,60,85")]
    [InlineData("102183,6,1,3852,60,85,30236,0")]
    [InlineData("102183,6,1,3852,x,85,30236")]
    public void RouletteTableParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => ActivityObjectParser.ParseRouletteTableRow("Roulettes.txt", 1, row));
    }

    [Theory]
    [InlineData("78156,EpicPirate,2,8940,5000,42,33,0,0,1")]
    [InlineData("78156,EpicPirate,2,8940,5000,42,33,0,0,1,11,0")]
    [InlineData("78156,EpicPirate,2,8940,5000,42,bad,0,0,1,11")]
    [InlineData("78156,2,8940,5000,42,33,0,0,1,11")]
    public void NpcTimeParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => ActivityObjectParser.ParseNpcTimeRow("NpcTime.txt", 1, row));
    }
}
