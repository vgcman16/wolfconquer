using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class SubProfessionParserTests
{
    private const string MinimalCatalog = """
        //title sub-profession class name
        [1]
        title=Martial Artist
        intro=Critical Strike +0%
        req_lv=70
        req_meto=0
        req_Profession=
        Promotion=
        suit=0
        UplimitLev=9
        UpLevEffect=LevelUp
        UpStepEffect=LevelUp
        BigClass=2
        AccessionMapID=6072
        AccessionNpcID=7778
        AccessionPosX=33
        AccessionPosY=46
        StepUpMapID=6072
        StepUpNpcID=7778
        StepUpPosX=33
        StepUpPosY=46
        AniSection=FitWork_MartialartsPic

        [11]
        title=Martial Artist
        intro=Critical Strike +1%
        req_lv=70
        req_meto=0
        req_Profession=41,31
        Promotion=or 800 StudyPoints
        req_joblv=0
        suit=0
        UplimitLev=9
        req_joblv=1
        EffectProportion=1
        UpLevEffect=LevelUp
        UpStepEffect=LevelUp
        SubSpecial=None
        """;

    [Fact]
    public void ParserReadsNumericSectionsAndTypedFields()
    {
        var catalog = SubProfessionParser.Parse(MinimalCatalog);

        Assert.Equal("SubProfessionInfo.ini", catalog.SourceFile);
        Assert.Equal(2, catalog.Entries.Count);

        var baseEntry = catalog.Entries[0];
        Assert.Equal(1, baseEntry.Id);
        Assert.Equal("Martial Artist", baseEntry.Title);
        Assert.Equal("Critical Strike +0%", baseEntry.Intro);
        Assert.Equal(70, baseEntry.RequiredLevel);
        Assert.Equal(0, baseEntry.RequiredMetempsychosis);
        Assert.Empty(baseEntry.RequiredProfessions);
        Assert.Equal("", baseEntry.Promotion);
        Assert.Equal(0, baseEntry.Suit);
        Assert.Equal(9, baseEntry.LevelLimit);
        Assert.Equal("LevelUp", baseEntry.UpLevelEffect);
        Assert.Equal("LevelUp", baseEntry.UpStepEffect);
        Assert.Equal(2, baseEntry.BigClass);
        Assert.Equal(new SubProfessionNpcRoute(6072, 7778, 33, 46), baseEntry.AccessionRoute);
        Assert.Equal(new SubProfessionNpcRoute(6072, 7778, 33, 46), baseEntry.StepUpRoute);
        Assert.Equal("FitWork_MartialartsPic", baseEntry.AnimationSection);
        Assert.Equal("1", baseEntry.RawSectionName);
        Assert.Equal(2, baseEntry.LineNumber);

        var levelEntry = catalog.Entries[1];
        Assert.Equal(11, levelEntry.Id);
        Assert.Equal([41, 31], levelEntry.RequiredProfessions);
        Assert.Equal("or 800 StudyPoints", levelEntry.Promotion);
        Assert.Equal(1, levelEntry.RequiredJobLevel);
        Assert.Equal(1, levelEntry.EffectProportion);
        Assert.Equal("None", levelEntry.SubSpecial);
        var requiredJobLevelEntries = levelEntry.Entries
            .Where(entry => entry.Key == "req_joblv")
            .ToArray();
        Assert.Equal(["req_joblv=0", "req_joblv=1"], requiredJobLevelEntries.Select(entry => entry.RawLine).ToArray());
    }

    [Fact]
    public void ParseFileUsesCp936AndFileNameAsSource()
    {
        var path = Path.Combine(Path.GetTempPath(), "SubProfessionInfo-" + Guid.NewGuid() + ".ini");
        try
        {
            File.WriteAllText(path, MinimalCatalog);

            var catalog = SubProfessionParser.ParseFile(path);

            Assert.Equal(Path.GetFileName(path), catalog.SourceFile);
            Assert.All(catalog.Entries, entry => Assert.Equal(Path.GetFileName(path), entry.SourceFile));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ParserRejectsDuplicateSectionIds()
    {
        var text = MinimalCatalog + Environment.NewLine + "[11]" + Environment.NewLine + "title=Duplicate";

        var ex = Assert.Throws<FormatException>(() => SubProfessionParser.Parse(text));
        Assert.Contains("duplicate sub-profession section id 11", ex.Message);
    }

    [Fact]
    public void ParserRejectsPartialRouteFields()
    {
        var text = MinimalCatalog.Replace("AccessionNpcID=7778", "");

        var ex = Assert.Throws<FormatException>(() => SubProfessionParser.Parse(text));
        Assert.Contains("has a partial Accession route", ex.Message);
    }

    [Theory]
    [InlineData("req_lv=70", "req_lv=x")]
    [InlineData("req_Profession=41,31", "req_Profession=41,x")]
    [InlineData("UplimitLev=9", "UplimitLev=nine")]
    public void ParserRejectsMalformedNumericFields(string oldValue, string newValue)
    {
        var text = MinimalCatalog.Replace(oldValue, newValue);

        Assert.Throws<FormatException>(() => SubProfessionParser.Parse(text));
    }
}
