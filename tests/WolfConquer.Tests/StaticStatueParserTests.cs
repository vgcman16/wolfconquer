using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class StaticStatueParserTests
{
    private const string LiveLikeRow =
        "298/34/1/179/8/8/160/183/104/16/251/141/79/24/195/9/32/1/40/0/48/0/48/0/48/0/48/0/48/0/48/0/" +
        "48/0/48/0/48/0/48/0/48/0/48/0/48/0/48/0/48/0/48/0/48/0/48/0/64/133/247/8/72/157/247/11/" +
        "80/141/161/8/88/247/207/37/96/247/207/37/104/245/254/21/112/245/254/21/120/224/167/18/" +
        "128/1/155/160/12/136/1/233/185/12/144/1/12/152/1/128/137/122/160/1/255/150/128/1/168/1/" +
        "208/14/184/1/195/219/5/208/1/0/216/1/173/2/224/1/141/1/232/1/238/31/240/1/7/248/1/250/1/" +
        "144/2/2/152/2/140/1/160/2/0/168/2/0/176/2/15/208/2/144/78/216/2/9/232/2/12/240/2/" +
        "174/206/184/4/168/3/193/2/176/3/100/192/3/16/216/3/15/248/3/0/128/4/233/134/50/136/4/" +
        "183/150/50/144/4/217/234/48/152/4/217/234/48/160/4/191/214/208/173/3/168/4/0/176/4/" +
        "205/101/184/4/213/15/192/4/187/39/200/4/8/216/4/248/3/248/4/1/160/5/195/9/200/5/" +
        "161/216/207/9/208/5/244/3/216/5/213/165/219/28/218/6/0/218/6/7/71/111/124/72/111/109/101/" +
        "224/6/0/240/6/0/248/6/0/128/7/0/136/7/0/84/81/83/101/114/118/101/114/105176/301/141/" +
        "1002/936350/936350/";

    [Fact]
    public void ParseReadsLiveLikeCountedRowAndPreservesMetadata()
    {
        var entry = Assert.Single(StaticStatueParser.Parse(
            "Count=1\n" + LiveLikeRow + "\n",
            "StaticStatue.txt"));

        Assert.Equal(298, entry.RecordId);
        Assert.Equal(305, entry.Values.Count);
        Assert.Equal(298, entry.Values[0]);
        Assert.Equal(936_350, entry.Values[^1]);
        Assert.Equal("StaticStatue.txt", entry.SourceFile);
        Assert.Equal(2, entry.LineNumber);
        Assert.Equal(LiveLikeRow, entry.RawLine);
        Assert.Equal(306, entry.Fields.Count);
        Assert.Equal("298", entry.Fields[0]);
        Assert.Equal("", entry.Fields[^1]);
    }

    [Fact]
    public void ParseFileUsesInputFileNameAsSource()
    {
        var path = Path.Combine(Path.GetTempPath(), "StaticStatue-" + Guid.NewGuid(), "StaticStatue.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            File.WriteAllText(path, "Count=1\n" + LiveLikeRow + "\n");

            var entry = Assert.Single(StaticStatueParser.ParseFile(path));

            Assert.Equal("StaticStatue.txt", entry.SourceFile);
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
        var ex = Assert.Throws<FormatException>(() => StaticStatueParser.Parse(
            "Count=2\n" + LiveLikeRow + "\n",
            "StaticStatue.txt"));

        Assert.Contains("Count", ex.Message);
        Assert.Contains("2", ex.Message);
        Assert.Contains("1", ex.Message);
    }

    [Fact]
    public void ParseRejectsMissingMalformedOrNegativeCountHeaders()
    {
        var inputs = new[]
        {
            "",
            LiveLikeRow,
            "Total=1\n" + LiveLikeRow + "\n",
            "Count=not-a-number\n" + LiveLikeRow + "\n",
            "Count=-1\n"
        };

        foreach (var text in inputs)
        {
            Assert.Throws<FormatException>(() => StaticStatueParser.Parse(text, "StaticStatue.txt"));
        }
    }

    [Fact]
    public void ParseRowRejectsWrongFieldCounts()
    {
        Assert.Throws<FormatException>(() => StaticStatueParser.ParseRow(
            "StaticStatue.txt",
            2,
            LiveLikeRow[..^1]));

        Assert.Throws<FormatException>(() => StaticStatueParser.ParseRow(
            "StaticStatue.txt",
            2,
            LiveLikeRow + "extra/"));
    }

    [Fact]
    public void ParseRowRejectsRowsWithoutTrailingSlashField()
    {
        var ex = Assert.Throws<FormatException>(() => StaticStatueParser.ParseRow(
            "StaticStatue.txt",
            2,
            LiveLikeRow[..^1] + "/1"));

        Assert.Contains("trailing", ex.Message);
    }

    [Fact]
    public void ParseRowReportsInvalidNumericFields()
    {
        var ex = Assert.Throws<FormatException>(() => StaticStatueParser.ParseRow(
            "StaticStatue.txt",
            2,
            LiveLikeRow.Replace("/34/", "/not-a-number/", StringComparison.Ordinal)));

        Assert.Contains("static statue field 2", ex.Message);
        Assert.Contains("not-a-number", ex.Message);
    }

    [Fact]
    public void ParseRowValidatesSourceAndLineNumber()
    {
        Assert.Throws<ArgumentException>(() => StaticStatueParser.ParseRow("", 2, LiveLikeRow));
        Assert.Throws<ArgumentOutOfRangeException>(() => StaticStatueParser.ParseRow("StaticStatue.txt", 0, LiveLikeRow));
    }
}
