using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class ActivityTaskParserTests
{
    [Fact]
    public void ParserReadsMetadataAndTaskSectionsWhilePreservingRawEvidence()
    {
        const string content = """
        // DBServer evidence; client patching uses a different ActivityTask.ini.
        [ActivityTask]
        New Activity System
        Num=2

        [7]
        id=1007
        Title=Daily Gift
        Type=1
        Progress=3
        ActiveValue=25
        CloseLev=0141
        Tip=Claim it from Welfare.
        OpenLev=0005
        Intro=Finish three rounds.
        AniSection=Sparkle
        Param=[npc,Guide,9534,1002,301,529]

        // second task deliberately omits optional values
        [2]
        id=1002
        Title=Training
        Type=2
        Progress=1
        ActiveValue=10
        CloseLev=120
        Tip=Defeat a trainer.
        """;

        var catalog = ActivityTaskParser.Parse(content, "ActivityTask.ini");

        Assert.Equal(2, catalog.Metadata.DeclaredCount);
        Assert.Equal("ActivityTask", catalog.Metadata.SourceSection);
        Assert.Equal(2, catalog.Metadata.SourceLine);
        Assert.Equal("Num=2", Assert.Single(catalog.Metadata.Entries).RawLine);

        Assert.Collection(
            catalog.Tasks,
            first =>
            {
                Assert.Equal("7", first.SourceSection);
                Assert.Equal(6, first.SourceLine);
                Assert.Equal(7, first.SectionId);
                Assert.Equal(1007, first.Id);
                Assert.Equal("Daily Gift", first.Title);
                Assert.Equal(1, first.Type);
                Assert.Equal(3, first.Progress);
                Assert.Equal(25, first.ActiveValue);
                Assert.Equal("0141", first.CloseLevelRaw);
                Assert.Equal(141, first.CloseLevel);
                Assert.Equal("0005", first.OpenLevelRaw);
                Assert.Equal(5, first.OpenLevel);
                Assert.Equal("Finish three rounds.", first.Intro);
                Assert.Equal("Sparkle", first.AnimationSection);
                Assert.Equal("[npc,Guide,9534,1002,301,529]", first.Param);
                Assert.Equal("CloseLev=0141", first.Entries.Single(entry => entry.Key == "CloseLev").RawLine);
            },
            second =>
            {
                Assert.Equal("2", second.SourceSection);
                Assert.Equal(20, second.SourceLine);
                Assert.Equal(2, second.SectionId);
                Assert.Equal(1002, second.Id);
                Assert.Equal(120, second.CloseLevel);
                Assert.Equal("120", second.CloseLevelRaw);
                Assert.Null(second.OpenLevelRaw);
                Assert.Null(second.OpenLevel);
                Assert.Null(second.Intro);
                Assert.Null(second.AnimationSection);
                Assert.Null(second.Param);
            });
    }

    [Fact]
    public void ParserRejectsDeclaredCountMismatch()
    {
        const string content = """
        [ActivityTask]
        Num=2
        [1]
        id=1
        Title=Only One
        Type=1
        Progress=1
        ActiveValue=1
        CloseLev=1
        Tip=One
        """;

        var ex = Assert.Throws<FormatException>(() => ActivityTaskParser.Parse(content));

        Assert.Contains("declares 2 tasks but has 1 numeric task sections", ex.Message);
    }

    [Fact]
    public void ParserRejectsMissingRequiredKeys()
    {
        const string content = """
        [ActivityTask]
        Num=1
        [1]
        id=1
        Title=Broken
        Type=1
        Progress=1
        ActiveValue=1
        CloseLev=1
        """;

        var ex = Assert.Throws<KeyNotFoundException>(() => ActivityTaskParser.Parse(content));

        Assert.Contains("Tip", ex.Message);
    }

    [Theory]
    [InlineData("id", "abc")]
    [InlineData("Type", "abc")]
    [InlineData("Progress", "abc")]
    [InlineData("ActiveValue", "abc")]
    [InlineData("CloseLev", "abc")]
    [InlineData("OpenLev", "abc")]
    public void ParserRejectsInvalidNumericValues(string key, string value)
    {
        var content = $"""
        [ActivityTask]
        Num=1
        [1]
        id={(key == "id" ? value : "1")}
        Title=Broken
        Type={(key == "Type" ? value : "1")}
        Progress={(key == "Progress" ? value : "1")}
        ActiveValue={(key == "ActiveValue" ? value : "1")}
        CloseLev={(key == "CloseLev" ? value : "1")}
        Tip=One
        OpenLev={(key == "OpenLev" ? value : "5")}
        """;

        Assert.Throws<FormatException>(() => ActivityTaskParser.Parse(content));
    }
}
