using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class RefineryParserTests
{
    private const string FirstMaterialRow = "724350 Detoxication(Unique)Material 11 7 0 0 0 0 0 0 0 0 0 9074942 0 0 0 0 0 0 0 1 1 0 0 0 0 0 0 0 0 0 0 0 0 0 1 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 Necklace Level~3~Refinery~item.~Right~click~to~refine~a~Necklace.~Reduces~11%~Poison~Damage. 7 0 0 0 13010 500 ";
    private const string HossuMaterialRow = "3006165 NormalHossuHair 100 10 0 0 0 0 0 0 0 0 0 98513000 0 0 0 0 0 0 0 1 1 0 0 0 0 0 0 0 0 0 0 0 0 0 1 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 (Hossu)(MaxHP)Material Level~1~refinery~material.~Right~click~it~to~refine~Taoist`s~Hossu~and~increase~the~user`s~Max~HP~by~100~points. 5 0 0 0 0 500 0 0 0 0 0 ";

    [Fact]
    public void RefineryMaterialParserReadsLiveRowAndPreservesRawFields()
    {
        var material = RefineryParser.ParseMaterialRow("Rifinery.txt", 1, FirstMaterialRow);

        Assert.Equal(724_350, material.ItemType);
        Assert.Equal("Detoxication(Unique)Material", material.Name);
        Assert.Equal(11, material.PrimaryValue);
        Assert.Equal(7, material.EffectType);
        Assert.Equal(51, material.Parameters.Count);
        Assert.Equal(11, material.Parameters[0]);
        Assert.Equal(7, material.Parameters[1]);
        Assert.Equal("Necklace", material.Target);
        Assert.StartsWith("Level~3~Refinery~item.", material.Description);
        Assert.Equal([7, 0, 0, 0, 13_010, 500], material.TailValues);
        Assert.Equal(61, material.Fields.Count);
        Assert.Equal("Rifinery.txt", material.SourceFile);
        Assert.Equal(1, material.LineNumber);
        Assert.Equal(FirstMaterialRow, material.RawLine);
    }

    [Fact]
    public void RefineryMaterialParserReadsHossuRowsWithExtendedTailValues()
    {
        var material = RefineryParser.ParseMaterialRow("Rifinery.txt", 172, HossuMaterialRow);

        Assert.Equal(3_006_165, material.ItemType);
        Assert.Equal("NormalHossuHair", material.Name);
        Assert.Equal(100, material.PrimaryValue);
        Assert.Equal(10, material.EffectType);
        Assert.Equal("(Hossu)(MaxHP)Material", material.Target);
        Assert.StartsWith("Level~1~refinery~material.", material.Description);
        Assert.Equal([5, 0, 0, 0, 0, 500, 0, 0, 0, 0, 0], material.TailValues);
        Assert.Equal(66, material.Fields.Count);
    }

    [Fact]
    public void RefineryMaterialParserSkipsBlankLinesAndPreservesPhysicalLineNumbers()
    {
        var content = FirstMaterialRow + Environment.NewLine + Environment.NewLine + HossuMaterialRow + Environment.NewLine;

        var materials = RefineryParser.ParseMaterials(content, "Rifinery.txt");

        Assert.Equal(2, materials.Count);
        Assert.Equal(1, materials[0].LineNumber);
        Assert.Equal(3, materials[1].LineNumber);
        Assert.Equal(1, materials[0].Fields.Count(field => field == "Necklace"));
    }

    [Fact]
    public void RefineryMaterialParserRejectsWrongFieldCounts()
    {
        var ex = Assert.Throws<FormatException>(() => RefineryParser.ParseMaterialRow("Rifinery.txt", 1, "1 Material 2"));

        Assert.Contains("has 3 fields; expected 61, 65, or 66", ex.Message);
    }

    [Fact]
    public void RefineryMaterialParserRejectsNonNumericParameterFields()
    {
        var row = FirstMaterialRow.Replace(" 11 ", " nope ");

        var ex = Assert.Throws<FormatException>(() => RefineryParser.ParseMaterialRow("Rifinery.txt", 1, row));

        Assert.Contains("field 3", ex.Message);
    }

    [Fact]
    public void RefineryBoxParserReadsCountedRowsAndPreservesRawFields()
    {
        const string content = """
        Count=2
        723130,6,9,1
        3004280,4,3,1
        """;

        var boxes = RefineryParser.ParseBoxes(content, "databaserefineryboxes.txt");

        Assert.Equal(2, boxes.Count);

        var first = boxes[0];
        Assert.Equal(723_130, first.ItemType);
        Assert.Equal([6, 9, 1], first.Values);
        Assert.Equal("databaserefineryboxes.txt", first.SourceFile);
        Assert.Equal(2, first.LineNumber);
        Assert.Equal("723130,6,9,1", first.RawLine);
        Assert.Equal(["723130", "6", "9", "1"], first.Fields);

        var last = boxes[1];
        Assert.Equal(3_004_280, last.ItemType);
        Assert.Equal([4, 3, 1], last.Values);
        Assert.Equal(3, last.LineNumber);
        Assert.Equal("3004280,4,3,1", last.RawLine);
    }

    [Fact]
    public void RefinerySoulParserReadsCountedRowsAndDerivesSoulLevelFromFileName()
    {
        const string content = """
        Count=2
        MonsterSaber 800020
        Flashaxe 827013
        """;

        var souls = RefineryParser.ParseSouls(content, "Souls7.ini");

        Assert.Equal(2, souls.Count);

        var first = souls[0];
        Assert.Equal(7, first.SoulLevel);
        Assert.Equal("MonsterSaber", first.EquipmentName);
        Assert.Equal(800_020, first.ItemType);
        Assert.Equal("Souls7.ini", first.SourceFile);
        Assert.Equal(2, first.LineNumber);
        Assert.Equal("MonsterSaber 800020", first.RawLine);
        Assert.Equal(["MonsterSaber", "800020"], first.Fields);

        var last = souls[1];
        Assert.Equal(7, last.SoulLevel);
        Assert.Equal("Flashaxe", last.EquipmentName);
        Assert.Equal(827_013, last.ItemType);
        Assert.Equal(3, last.LineNumber);
        Assert.Equal("Flashaxe 827013", last.RawLine);
    }

    [Fact]
    public void RefinerySoulParserReadsLevelOneHeaderExample()
    {
        const string content = """
        Count=1
        Headgear 724353
        """;

        var soul = Assert.Single(RefineryParser.ParseSouls(content, "Souls1.ini"));

        Assert.Equal(1, soul.SoulLevel);
        Assert.Equal("Headgear", soul.EquipmentName);
        Assert.Equal(724_353, soul.ItemType);
    }

    [Theory]
    [InlineData("Count=2\n723130,6,9,1")]
    [InlineData("Count=x\n723130,6,9,1")]
    [InlineData("723130,6,9,1")]
    public void RefineryBoxParserRejectsMissingMalformedAndMismatchedCountHeaders(string content)
    {
        Assert.Throws<FormatException>(() => RefineryParser.ParseBoxes(content, "databaserefineryboxes.txt"));
    }

    [Theory]
    [InlineData("723130,6,9")]
    [InlineData("723130,6,9,1,0")]
    [InlineData("item,6,9,1")]
    [InlineData("723130,6,nope,1")]
    public void RefineryBoxParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => RefineryParser.ParseBoxRow("databaserefineryboxes.txt", 2, row));
    }

    [Theory]
    [InlineData("Count=2\nHeadgear 724353")]
    [InlineData("Count=x\nHeadgear 724353")]
    [InlineData("Headgear 724353")]
    public void RefinerySoulParserRejectsMissingMalformedAndMismatchedCountHeaders(string content)
    {
        Assert.Throws<FormatException>(() => RefineryParser.ParseSouls(content, "Souls1.ini"));
    }

    [Theory]
    [InlineData("Headgear")]
    [InlineData("Headgear 724353 extra")]
    [InlineData("Headgear item")]
    public void RefinerySoulParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => RefineryParser.ParseSoulRow("Souls1.ini", 2, row, soulLevel: 1));
    }

    [Fact]
    public void RefinerySoulParserRejectsFileNamesWithoutLevelSuffix()
    {
        const string content = """
        Count=1
        Headgear 724353
        """;

        Assert.Throws<FormatException>(() => RefineryParser.ParseSouls(content, "Souls.ini"));
    }
}
