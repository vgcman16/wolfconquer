using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class NeiGongParserTests
{
    private const string MinimalCatalog = """
        [NeiGong]
        Num=1
        Attri0Tip=Potency~Cost
        Attri14Tip=Anti-break

        [1]
        id=1
        Name=Universal~Concept
        AniSection=NeiGong_HeartSkill14Btn
        NeiGongNum=1
        AttriType=7-9
        AttriValue=100-100
        GetWayContent=PS:~Claim~the~book~[Universal~Concept]~from~the~Celestial~Sage~at~(TwinCity~202,416).

        [1-1]
        Type=1
        Name=Universal~Concept~(A)
        AniSection=NeiGong_HeartSkill14Btn
        MaxLev=5
        AttriType=2-4
        AttriValue=100-100
        NeiGongValue=1500-1500-1500-1500-1500
        Condition=<item 3005365>[Universal~Concept~(A)]</item>
        ReqLev=2015
        ReqNeiGongValue=0
        ReqPreNeiGong=0
        ReqItemType=3005365
        """;

    [Fact]
    public void ParserReadsHeaderSecretsAndTypes()
    {
        var catalog = NeiGongParser.Parse(MinimalCatalog);

        Assert.Equal(1, catalog.Header.SecretCount);
        Assert.Equal(2, catalog.Header.AttributeTips.Count);
        Assert.Equal(new NeiGongAttributeTip(0, "Potency~Cost"), catalog.Header.AttributeTips[0]);
        Assert.Equal(new NeiGongAttributeTip(14, "Anti-break"), catalog.Header.AttributeTips[1]);
        Assert.Equal("NeiGongInfo.ini", catalog.Header.SourceFile);
        Assert.Equal(1, catalog.Header.LineNumber);
        Assert.Single(catalog.Secrets);
        Assert.Single(catalog.Types);

        var secret = catalog.Secrets.Single();
        Assert.Equal(1, secret.Id);
        Assert.Equal("Universal~Concept", secret.Name);
        Assert.Equal("NeiGong_HeartSkill14Btn", secret.AniSection);
        Assert.Equal(1, secret.NeiGongCount);
        Assert.Equal([7, 9], secret.AttributeTypes);
        Assert.Equal([100, 100], secret.AttributeValues);
        Assert.StartsWith("PS:~Claim", secret.GetWayContent, StringComparison.Ordinal);
        Assert.Equal("1", secret.RawSectionName);
        Assert.Equal("Universal~Concept", secret.Fields["Name"]);

        var type = catalog.Types.Single();
        Assert.Equal(1, type.SecretId);
        Assert.Equal(1, type.Slot);
        Assert.Equal(1, type.TypeId);
        Assert.Equal("Universal~Concept~(A)", type.Name);
        Assert.Equal(5, type.MaxLevel);
        Assert.Equal([2, 4], type.AttributeTypes);
        Assert.Equal([100, 100], type.AttributeValues);
        Assert.Equal([1500, 1500, 1500, 1500, 1500], type.CultureValues);
        Assert.Equal("<item 3005365>[Universal~Concept~(A)]</item>", type.Condition);
        Assert.Equal(2015, type.RequiredLevel);
        Assert.Equal(0, type.RequiredNeiGongValue);
        Assert.Equal(0, type.RequiredPrerequisiteNeiGong);
        Assert.Equal(3_005_365, type.RequiredItemTypeId);
        Assert.Equal("1-1", type.RawSectionName);
    }

    [Fact]
    public void ParseFileUsesFileNameAsSource()
    {
        var path = Path.Combine(Path.GetTempPath(), "NeiGongInfo-" + Guid.NewGuid() + ".ini");
        try
        {
            File.WriteAllText(path, MinimalCatalog);

            var catalog = NeiGongParser.ParseFile(path);

            Assert.Equal(Path.GetFileName(path), catalog.Header.SourceFile);
            Assert.Equal(Path.GetFileName(path), catalog.Secrets.Single().SourceFile);
            Assert.Equal(Path.GetFileName(path), catalog.Types.Single().SourceFile);
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
    public void ParserRejectsSecretCountMismatch()
    {
        var text = MinimalCatalog.Replace("Num=1", "Num=2");

        var ex = Assert.Throws<FormatException>(() => NeiGongParser.Parse(text));
        Assert.Contains("declares 2 secrets but parsed 1", ex.Message);
    }

    [Fact]
    public void ParserRejectsSecretTypeCountMismatch()
    {
        var text = MinimalCatalog.Replace("NeiGongNum=1", "NeiGongNum=2");

        var ex = Assert.Throws<FormatException>(() => NeiGongParser.Parse(text));
        Assert.Contains("declares 2 types but parsed 1", ex.Message);
    }

    [Theory]
    [InlineData("AttriType=7-9", "AttriType=7-x")]
    [InlineData("AttriValue=100-100", "AttriValue=100")]
    [InlineData("MaxLev=5", "MaxLev=4")]
    public void ParserRejectsMalformedNumericLists(string oldValue, string newValue)
    {
        var text = MinimalCatalog.Replace(oldValue, newValue);

        Assert.Throws<FormatException>(() => NeiGongParser.Parse(text));
    }
}
