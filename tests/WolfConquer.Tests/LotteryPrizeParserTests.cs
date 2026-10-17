using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class LotteryPrizeParserTests
{
    [Fact]
    public void LotteryPrizeParserReadsCountedLiveRowsAndPreservesMetadata()
    {
        const string content = "Count=3\n" +
            "1 70 WavyHeavyRing+8 151159 2 0 8\n" +
            "1 70 PureBracelet~2~Sockets 152049 2 2 0\n" +
            "1 70 SkyFlame(+8) 624009 2 0 8\n";

        var prizes = LotteryPrizeParser.Parse(content, "lottery.ini");

        Assert.Equal(3, prizes.Count);

        var first = prizes[0];
        Assert.Equal(1, first.Type);
        Assert.Equal(70, first.Level);
        Assert.Equal("WavyHeavyRing+8", first.ItemName);
        Assert.Equal("151159", first.ItemTypeRaw);
        Assert.Equal(151_159, first.ItemTypeId);
        Assert.Equal(2, first.SocketOne);
        Assert.Equal(0, first.SocketTwo);
        Assert.Equal(8, first.PlusLevel);
        Assert.Equal("lottery.ini", first.SourceFile);
        Assert.Equal(2, first.LineNumber);
        Assert.Equal("1 70 WavyHeavyRing+8 151159 2 0 8", first.RawLine);
        Assert.Equal(["1", "70", "WavyHeavyRing+8", "151159", "2", "0", "8"], first.Fields);

        var second = prizes[1];
        Assert.Equal("PureBracelet~2~Sockets", second.ItemName);
        Assert.Equal(152_049, second.ItemTypeId);
        Assert.Equal(2, second.SocketTwo);

        var third = prizes[2];
        Assert.Equal("SkyFlame(+8)", third.ItemName);
        Assert.Equal(624_009, third.ItemTypeId);
        Assert.Equal(8, third.PlusLevel);
    }

    [Fact]
    public void LotteryPrizeParserReadsLiveRowsWithSpacedNamesAndShortGemRows()
    {
        const string content = "Count=2\n" +
            "2 70 FoxCoat 2 Socket~2~Sockets 133019 2 2 0\n" +
            "4 70 SuperRainbowGem 700033\n";

        var prizes = LotteryPrizeParser.Parse(content, "lottery.ini");

        Assert.Equal("FoxCoat 2 Socket~2~Sockets", prizes[0].ItemName);
        Assert.Equal("133019", prizes[0].ItemTypeRaw);
        Assert.Equal(133_019, prizes[0].ItemTypeId);
        Assert.Equal(2, prizes[0].SocketOne);
        Assert.Equal(2, prizes[0].SocketTwo);
        Assert.Equal(0, prizes[0].PlusLevel);
        Assert.Equal(["2", "70", "FoxCoat", "2", "Socket~2~Sockets", "133019", "2", "2", "0"], prizes[0].Fields);

        Assert.Equal("SuperRainbowGem", prizes[1].ItemName);
        Assert.Equal("700033", prizes[1].ItemTypeRaw);
        Assert.Equal(700_033, prizes[1].ItemTypeId);
        Assert.Null(prizes[1].SocketOne);
        Assert.Null(prizes[1].SocketTwo);
        Assert.Null(prizes[1].PlusLevel);
    }

    [Fact]
    public void LotteryPrizeParserPreservesNonNumericLiveItemTypeTokens()
    {
        var prize = LotteryPrizeParser.ParseRow(
            "lottery.ini",
            102,
            "2 70 EmeraldPlume 142019~2~Sockets 0 2 0");

        Assert.Equal("EmeraldPlume", prize.ItemName);
        Assert.Equal("142019~2~Sockets", prize.ItemTypeRaw);
        Assert.Null(prize.ItemTypeId);
        Assert.Equal(0, prize.SocketOne);
        Assert.Equal(2, prize.SocketTwo);
        Assert.Equal(0, prize.PlusLevel);
    }

    [Fact]
    public void LotteryPrizeParserPreservesTrailingSpacesInRawLine()
    {
        var prize = LotteryPrizeParser.ParseRow(
            "lottery.ini",
            370,
            "1 70 SkyFlame(+8) 624009 2 0 8   ");

        Assert.Equal("1 70 SkyFlame(+8) 624009 2 0 8   ", prize.RawLine);
        Assert.Equal(["1", "70", "SkyFlame(+8)", "624009", "2", "0", "8"], prize.Fields);
    }

    [Fact]
    public void LotteryPrizeParserReadsFileAndUsesFileNameAsSourceFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "lottery-" + Guid.NewGuid() + ".ini");
        File.WriteAllText(path, "Count=1\n1 70 SkyFlame(+8) 624009 2 0 8\n");

        try
        {
            var prize = Assert.Single(LotteryPrizeParser.ParseFile(path));

            Assert.Equal(Path.GetFileName(path), prize.SourceFile);
            Assert.Equal(2, prize.LineNumber);
            Assert.Equal("SkyFlame(+8)", prize.ItemName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("1 70 WavyHeavyRing+8 151159 2 0 8\n")]
    [InlineData("count=1\n1 70 WavyHeavyRing+8 151159 2 0 8\n")]
    [InlineData("Count=x\n1 70 WavyHeavyRing+8 151159 2 0 8\n")]
    [InlineData("Count=-1\n")]
    public void LotteryPrizeParserRejectsMissingAndMalformedCountHeaders(string content)
    {
        Assert.Throws<FormatException>(() => LotteryPrizeParser.Parse(content, "lottery.ini"));
    }

    [Fact]
    public void LotteryPrizeParserCanRejectOrPreserveStaleLiveCountHeaders()
    {
        const string content = "Count=2\n1 70 WavyHeavyRing+8 151159 2 0 8\n";

        var prize = Assert.Single(LotteryPrizeParser.Parse(content, "lottery.ini"));
        Assert.Equal("WavyHeavyRing+8", prize.ItemName);

        Assert.Throws<FormatException>(() => LotteryPrizeParser.Parse(content, "lottery.ini", requireCountMatch: true));
    }

    [Theory]
    [InlineData("1 70 WavyHeavyRing+8")]
    [InlineData("1 70 WavyHeavyRing+8 151159 2")]
    [InlineData("1 70 WavyHeavyRing+8 151159 2 0")]
    public void LotteryPrizeParserRejectsWrongFieldCounts(string row)
    {
        Assert.Throws<FormatException>(() => LotteryPrizeParser.ParseRow("lottery.ini", 2, row));
    }

    [Theory]
    [InlineData("x 70 WavyHeavyRing+8 151159 2 0 8")]
    [InlineData("1 level WavyHeavyRing+8 151159 2 0 8")]
    [InlineData("1 70 WavyHeavyRing+8 151159 socket 0 8")]
    [InlineData("1 70 WavyHeavyRing+8 151159 2 socket 8")]
    [InlineData("1 70 WavyHeavyRing+8 151159 2 0 plus")]
    public void LotteryPrizeParserRejectsInvalidNumericFields(string row)
    {
        Assert.Throws<FormatException>(() => LotteryPrizeParser.ParseRow("lottery.ini", 2, row));
    }
}
