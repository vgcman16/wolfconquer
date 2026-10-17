using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class GuildWarParserTests
{
    [Fact]
    public void GuildWarInfoParserReadsLiveLikeConductorsAndPreservesTrailingEmptyFields()
    {
        const string content = """
            [Info]
            ID=1363
            Name=zzzzzzz
            LeaderReward=0
            DeputiLeaderReward=7
            [Pole]
            Name=AttackOnTitans
            HitPoints=161116935
            [Condutors]
            GuildConductor1=101620,307,279,1000,1487,2,301,273,1000,
            GuildConductor2=101618,32,53,1005,1477,2,31,47,1005,
            """;

        var data = GuildWarParser.Parse(content, "GuildWarInfo.ini");

        Assert.Equal("GuildWarInfo.ini", data.SourceFile);
        Assert.Equal(["Info", "Pole", "Condutors"], data.Sections.Select(section => section.Name));
        Assert.Empty(data.Diagnostics);

        Assert.NotNull(data.Info);
        Assert.Equal(1363, data.Info.Id);
        Assert.Equal("zzzzzzz", data.Info.Name);
        Assert.Equal(0, data.Info.LeaderReward);
        Assert.Equal(7, data.Info.DeputiLeaderReward);
        Assert.Empty(data.Info.LeaderTop);
        Assert.Empty(data.Info.DeputiTop);

        Assert.NotNull(data.Pole);
        Assert.Equal("AttackOnTitans", data.Pole.Name);
        Assert.Equal(161116935, data.Pole.HitPoints);

        var firstConductor = Assert.Single(data.GetEntries("Condutors", "GuildConductor1"));
        Assert.Equal("GuildConductor1", firstConductor.Key);
        Assert.Equal("101620,307,279,1000,1487,2,301,273,1000,", firstConductor.Value);
        Assert.Equal("GuildConductor1", firstConductor.RawKey);
        Assert.Equal("101620,307,279,1000,1487,2,301,273,1000,", firstConductor.RawValue);
        Assert.Equal(10, firstConductor.LineNumber);
        Assert.Equal("GuildConductor1=101620,307,279,1000,1487,2,301,273,1000,", firstConductor.RawLine);
        Assert.Equal("GuildWarInfo.ini", firstConductor.SourceFile);

        Assert.Equal(2, data.Conductors.Count);
        Assert.Equal("GuildConductor1", data.Conductors[0].Key);
        Assert.Equal(
            ["101620", "307", "279", "1000", "1487", "2", "301", "273", "1000", ""],
            data.Conductors[0].Fields);
    }

    [Fact]
    public void SuperGuildWarInfoParserReadsPoleHitPointsWithoutConductors()
    {
        const string content = """
            [Info]
            ID=1204
            Name=WeAreFamiily
            LeaderReward=1
            DeputiLeaderReward=7
            LeaderTop0=1294500
            DeputiTop10=1261320
            [Pole]
            Name=WeAreFamiily
            HitPoints=800000000
            """;

        var data = GuildWarParser.Parse(content, "SuperGuildWarInfo.ini");

        Assert.Empty(data.Diagnostics);
        Assert.NotNull(data.Info);
        Assert.Equal(1204, data.Info.Id);
        Assert.Equal(1, data.Info.LeaderReward);
        Assert.Equal(7, data.Info.DeputiLeaderReward);
        Assert.Equal(1294500, data.Info.LeaderTop[0]);
        Assert.Equal(1261320, data.Info.DeputiTop[10]);
        Assert.NotNull(data.Pole);
        Assert.Equal(800000000, data.Pole.HitPoints);
        Assert.Empty(data.Conductors);
        Assert.Null(data.GetSection("Condutors"));
    }

    [Fact]
    public void ParserPreservesDuplicateKeysAndTypedAccessUsesLastEntry()
    {
        const string content = """
            [Info]
            ID=1363
            Name=first
            ID=1364
            Name=second
            """;

        var data = GuildWarParser.Parse(content, "GuildWarInfo.ini");

        Assert.NotNull(data.Info);
        Assert.Equal(1364, data.Info.Id);
        Assert.Equal("second", data.Info.Name);
        Assert.Equal(["1363", "1364"], data.GetEntries("Info", "ID").Select(entry => entry.Value));
        Assert.Equal(2, data.GetEntries("Info", "ID")[0].LineNumber);
        Assert.Equal(4, data.GetEntries("Info", "ID")[1].LineNumber);
        Assert.Contains(data.Diagnostics, diagnostic => diagnostic.Code == "DuplicateGuildWarKey");
    }

    [Fact]
    public void MissingSectionsLeaveConvenienceObjectsNullAndEntriesEmpty()
    {
        const string content = """
            [Pole]
            Name=AttackOnTitans
            """;

        var data = GuildWarParser.Parse(content, "GuildWarInfo.ini");

        Assert.Null(data.Info);
        Assert.NotNull(data.Pole);
        Assert.Empty(data.Conductors);
        Assert.Null(data.GetSection("Info"));
        Assert.Null(data.GetEntry("Info", "ID"));
        Assert.Empty(data.GetEntries("Info", "ID"));
        Assert.Empty(data.Diagnostics);
    }

    [Fact]
    public void ParserReportsDiagnosticsForInvalidKnownNumericKeys()
    {
        const string content = """
            [Info]
            ID=bad-id
            LeaderReward=bad-reward
            DeputiLeaderReward=bad-deputi-reward
            LeaderTop0=bad-leader-top
            DeputiTop1=bad-deputi-top
            [Pole]
            HitPoints=bad-hitpoints
            """;

        var data = GuildWarParser.Parse(content, "GuildWarInfo.ini");

        Assert.NotNull(data.Info);
        Assert.Null(data.Info.Id);
        Assert.Null(data.Info.LeaderReward);
        Assert.Null(data.Info.DeputiLeaderReward);
        Assert.Empty(data.Info.LeaderTop);
        Assert.Empty(data.Info.DeputiTop);
        Assert.NotNull(data.Pole);
        Assert.Null(data.Pole.HitPoints);

        var diagnostics = data.Diagnostics.Where(diagnostic => diagnostic.Code == "InvalidGuildWarNumericValue").ToList();
        Assert.Equal(6, diagnostics.Count);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Message.Contains("GuildWarInfo.ini line 2", StringComparison.Ordinal));
        Assert.Contains(diagnostics, diagnostic => diagnostic.Message.Contains("[Pole] HitPoints", StringComparison.Ordinal));
    }
}
