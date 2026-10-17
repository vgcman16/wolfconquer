using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class StateCountFileParserTests
{
    [Fact]
    public void ParsesCountOnlyFileWithoutTrailingNewline()
    {
        var file = StateCountFileParser.Parse("WarriorRank.txt", "Count=0");

        Assert.Equal("WarriorRank.txt", file.SourceFile);
        Assert.Equal(0, file.DeclaredCount);
        Assert.Empty(file.Entries);
        Assert.Equal(["Count=0"], file.RawLines);
    }

    [Fact]
    public void ParsesSlashDelimitedEntriesAndPreservesTrailingEmptyField()
    {
        var file = StateCountFileParser.Parse(
            "Votes.txt",
            "Count=1\r\n1296463/198.51.100.15/638379291346047487/\r\n");

        var entry = Assert.Single(file.Entries);
        Assert.Equal(1, file.DeclaredCount);
        Assert.Equal(2, entry.SourceLine);
        Assert.Equal("1296463/198.51.100.15/638379291346047487/", entry.RawLine);
        Assert.Equal(["1296463", "198.51.100.15", "638379291346047487", ""], entry.Fields);
    }

    [Fact]
    public void ParsesWhitespaceDelimitedEntries()
    {
        var file = StateCountFileParser.Parse(
            "BanIp.txt",
            "Count=1\r\n198.51.100.15 638379291346047487\r\n");

        var entry = Assert.Single(file.Entries);
        Assert.Equal(["198.51.100.15", "638379291346047487"], entry.Fields);
    }

    [Fact]
    public void ThrowsWhenCountHeaderIsMissing()
    {
        var exception = Assert.Throws<FormatException>(() => StateCountFileParser.Parse("BanIp.txt", "198.51.100.15\r\n"));

        Assert.Contains("BanIp.txt line 1", exception.Message);
        Assert.Contains("Count", exception.Message);
    }

    [Fact]
    public void ThrowsWhenDeclaredCountDoesNotMatchEntryCount()
    {
        var exception = Assert.Throws<FormatException>(() => StateCountFileParser.Parse("BanIp.txt", "Count=2\r\n198.51.100.15\r\n"));

        Assert.Contains("BanIp.txt", exception.Message);
        Assert.Contains("declares 2", exception.Message);
        Assert.Contains("parsed 1", exception.Message);
    }
}
