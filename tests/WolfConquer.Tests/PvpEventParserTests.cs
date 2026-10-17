using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class PvpEventParserTests
{
    [Fact]
    public void ArenaParserReadsCountedLiveLikeFirstAndLastRowsAndPreservesTrailingEmptyField()
    {
        const string firstRow = "1296459/Test/100/8000/1671008/4000/0/0/0/0/0/0/4000/0/0/1/";
        const string lastRow = "1294419/Riva/140/7006/3452007/4000/0/0/0/0/0/0/4000/0/0/16/";
        const string content = $"Count=2\n{firstRow}\n{lastRow}\n";

        var records = PvpEventParser.ParseArena(content, "Arena.ini");

        Assert.Equal(2, records.Count);

        var first = records[0];
        Assert.Equal(1296459, first.PlayerId);
        Assert.Equal("Test", first.PlayerName);
        Assert.Equal(100, first.Level);
        Assert.Equal(8000, first.Mesh);
        Assert.Equal(1671008, first.Lookface);
        Assert.Equal(4000, first.Points);
        Assert.Equal(4000, first.Prize);
        Assert.Equal(1, first.Rank);
        Assert.Equal("Arena.ini", first.SourceFile);
        Assert.Equal(2, first.LineNumber);
        Assert.Equal(firstRow, first.RawLine);
        Assert.Equal(["1296459", "Test", "100", "8000", "1671008", "4000", "0", "0", "0", "0", "0", "0", "4000", "0", "0", "1", ""], first.Fields);

        var last = records[1];
        Assert.Equal(1294419, last.PlayerId);
        Assert.Equal("Riva", last.PlayerName);
        Assert.Equal(140, last.Level);
        Assert.Equal(7006, last.Mesh);
        Assert.Equal(3452007, last.Lookface);
        Assert.Equal(16, last.Rank);
        Assert.Equal(lastRow, last.RawLine);
        Assert.Equal("", last.Fields[^1]);
    }

    [Fact]
    public void TeamArenaParserReadsLiveLikeRowUsingArenaSlashShape()
    {
        const string row = "1293600/name/140/7051/1541008/4000/0/0/0/0/0/0/0/0/0/0/";
        const string content = $"Count=1\n{row}\n";

        var records = PvpEventParser.ParseTeamArena(content, "TeamArena.ini");

        var record = Assert.Single(records);
        Assert.Equal(1293600, record.PlayerId);
        Assert.Equal("name", record.PlayerName);
        Assert.Equal(140, record.Level);
        Assert.Equal(7051, record.Mesh);
        Assert.Equal(1541008, record.Lookface);
        Assert.Equal(4000, record.Points);
        Assert.Equal(0, record.Prize);
        Assert.Equal(0, record.Rank);
        Assert.Equal("TeamArena.ini", record.SourceFile);
        Assert.Equal(2, record.LineNumber);
        Assert.Equal(row, record.RawLine);
        Assert.Equal(["1293600", "name", "140", "7051", "1541008", "4000", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", ""], record.Fields);
    }

    [Fact]
    public void PkTournamentParserReadsEliteAndSkillTeamRows()
    {
        const string firstRow = "0/0/0/None/0/0/0/";
        const string lastRow = "3/7/37/$El#3Shrey/1401008/1/1296148/";
        const string content = $"Count=2\n{firstRow}\n{lastRow}\n";

        var records = PvpEventParser.ParsePkTournament(content, "SkillTeamPK.ini");

        Assert.Equal(2, records.Count);

        var first = records[0];
        Assert.Equal(0, first.Group);
        Assert.Equal(0, first.Slot);
        Assert.Equal(0, first.Level);
        Assert.Equal("None", first.PlayerName);
        Assert.Equal(0, first.Lookface);
        Assert.Equal(0, first.Status);
        Assert.Equal(0, first.PlayerId);
        Assert.Equal("SkillTeamPK.ini", first.SourceFile);
        Assert.Equal(2, first.LineNumber);
        Assert.Equal(firstRow, first.RawLine);
        Assert.Equal(["0", "0", "0", "None", "0", "0", "0", ""], first.Fields);

        var last = records[1];
        Assert.Equal(3, last.Group);
        Assert.Equal(7, last.Slot);
        Assert.Equal(37, last.Level);
        Assert.Equal("$El#3Shrey", last.PlayerName);
        Assert.Equal(1401008, last.Lookface);
        Assert.Equal(1, last.Status);
        Assert.Equal(1296148, last.PlayerId);
        Assert.Equal(3, last.LineNumber);
        Assert.Equal(lastRow, last.RawLine);
        Assert.Equal("", last.Fields[^1]);
    }

    [Fact]
    public void ClassPkWarParserReadsCountedLiveLikeFirstAndLastRows()
    {
        const string firstRow = "0/0/0/0/";
        const string lastRow = "10/3/0/0/";
        const string content = $"Count=2\n{firstRow}\n{lastRow}\n";

        var records = PvpEventParser.ParseClassPkWar(content, "ClassPkWar.ini");

        Assert.Equal(2, records.Count);

        var first = records[0];
        Assert.Equal(0, first.ClassId);
        Assert.Equal(0, first.Slot);
        Assert.Equal(0, first.PlayerId);
        Assert.Equal(0, first.Points);
        Assert.Equal("ClassPkWar.ini", first.SourceFile);
        Assert.Equal(2, first.LineNumber);
        Assert.Equal(firstRow, first.RawLine);
        Assert.Equal(["0", "0", "0", "0", ""], first.Fields);

        var last = records[1];
        Assert.Equal(10, last.ClassId);
        Assert.Equal(3, last.Slot);
        Assert.Equal(0, last.PlayerId);
        Assert.Equal(0, last.Points);
        Assert.Equal(lastRow, last.RawLine);
        Assert.Equal("", last.Fields[^1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1296459/Test/100/8000/1671008/4000/0/0/0/0/0/0/4000/0/0/1/\n")]
    [InlineData("Total=1\n1296459/Test/100/8000/1671008/4000/0/0/0/0/0/0/4000/0/0/1/\n")]
    [InlineData("Count=x\n1296459/Test/100/8000/1671008/4000/0/0/0/0/0/0/4000/0/0/1/\n")]
    [InlineData("Count=-1\n")]
    [InlineData("Count=2\n1296459/Test/100/8000/1671008/4000/0/0/0/0/0/0/4000/0/0/1/\n")]
    [InlineData("Count=0\n1296459/Test/100/8000/1671008/4000/0/0/0/0/0/0/4000/0/0/1/\n")]
    public void ArenaParserRejectsMissingMalformedAndMismatchedCountHeaders(string content)
    {
        Assert.Throws<FormatException>(() => PvpEventParser.ParseArena(content, "Arena.ini"));
    }

    [Theory]
    [InlineData("1296459/Test/100/8000/1671008/4000/0/0/0/0/0/0/4000/0/0/1")]
    [InlineData("1296459/Test/100/8000/1671008/4000/0/0/0/0/0/0/4000/0/0/1/0/")]
    [InlineData("bad/Test/100/8000/1671008/4000/0/0/0/0/0/0/4000/0/0/1/")]
    public void ArenaRowParserRejectsFieldCountMismatchAndInvalidNumericFields(string row)
    {
        Assert.Throws<FormatException>(() => PvpEventParser.ParseArenaRow("Arena.ini", 2, row));
    }

    [Theory]
    [InlineData("0/0/0/None/0/0/0")]
    [InlineData("0/0/0/None/0/0/0/extra/")]
    [InlineData("0/0/bad/None/0/0/0/")]
    public void PkTournamentRowParserRejectsFieldCountMismatchAndInvalidNumericFields(string row)
    {
        Assert.Throws<FormatException>(() => PvpEventParser.ParsePkTournamentRow("ElitePk.ini", 2, row));
    }

    [Theory]
    [InlineData("0/0/0/0")]
    [InlineData("0/0/0/0/extra/")]
    [InlineData("0/bad/0/0/")]
    public void ClassPkWarRowParserRejectsFieldCountMismatchAndInvalidNumericFields(string row)
    {
        Assert.Throws<FormatException>(() => PvpEventParser.ParseClassPkWarRow("ClassPkWar.ini", 2, row));
    }
}
