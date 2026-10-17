using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class RuneRankingParserTests
{
    [Fact]
    public void ParseReadsLiveLikeCountedRowAndPreservesMetadata()
    {
        var entry = Assert.Single(RuneRankingParser.Parse(
            "Count=1\nRuneMaster[PM]/314800/1293600/\n",
            "RuneRanking.txt"));

        Assert.Equal("RuneMaster[PM]", entry.CharacterName);
        Assert.Equal(314_800, entry.Score);
        Assert.Equal(1_293_600, entry.CharacterId);
        Assert.Equal("RuneRanking.txt", entry.SourceFile);
        Assert.Equal(2, entry.LineNumber);
        Assert.Equal("RuneMaster[PM]/314800/1293600/", entry.RawLine);
        Assert.Equal(["RuneMaster[PM]", "314800", "1293600", ""], entry.Fields);
    }

    [Fact]
    public void ParseFileUsesInputFileNameAsSource()
    {
        var path = Path.Combine(Path.GetTempPath(), "RuneRanking-" + Guid.NewGuid(), "RuneRanking.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            File.WriteAllText(path, "Count=1\nRuneMaster[PM]/314800/1293600/\n");

            var entry = Assert.Single(RuneRankingParser.ParseFile(path));

            Assert.Equal("RuneRanking.txt", entry.SourceFile);
            Assert.Equal(2, entry.LineNumber);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void ParseRejectsDeclaredCountMismatch()
    {
        var ex = Assert.Throws<FormatException>(() => RuneRankingParser.Parse(
            "Count=2\nRuneMaster[PM]/314800/1293600/\n",
            "RuneRanking.txt"));

        Assert.Contains("Count", ex.Message);
        Assert.Contains("2", ex.Message);
        Assert.Contains("1", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("RuneMaster[PM]/314800/1293600/")]
    [InlineData("Total=1\nRuneMaster[PM]/314800/1293600/")]
    [InlineData("Count=not-a-number\nRuneMaster[PM]/314800/1293600/")]
    [InlineData("Count=-1\n")]
    public void ParseRejectsMissingMalformedOrNegativeCountHeaders(string text)
    {
        Assert.Throws<FormatException>(() => RuneRankingParser.Parse(text, "RuneRanking.txt"));
    }

    [Theory]
    [InlineData("RuneMaster[PM]/314800/1293600")]
    [InlineData("RuneMaster[PM]/314800/1293600/extra/")]
    public void ParseRowRejectsWrongFieldCounts(string row)
    {
        Assert.Throws<FormatException>(() => RuneRankingParser.ParseRow("RuneRanking.txt", 2, row));
    }

    [Theory]
    [InlineData("RuneMaster[PM]/not-score/1293600/", "rune ranking score", "not-score")]
    [InlineData("RuneMaster[PM]/314800/not-id/", "rune ranking character id", "not-id")]
    public void ParseRowReportsInvalidNumericFields(string row, string label, string value)
    {
        var ex = Assert.Throws<FormatException>(() => RuneRankingParser.ParseRow("RuneRanking.txt", 2, row));

        Assert.Contains(label, ex.Message);
        Assert.Contains(value, ex.Message);
    }

    [Fact]
    public void ParseRowValidatesSourceAndLineNumber()
    {
        Assert.Throws<ArgumentException>(() => RuneRankingParser.ParseRow("", 2, "RuneMaster[PM]/314800/1293600/"));
        Assert.Throws<ArgumentOutOfRangeException>(() => RuneRankingParser.ParseRow("RuneRanking.txt", 0, "RuneMaster[PM]/314800/1293600/"));
    }
}
