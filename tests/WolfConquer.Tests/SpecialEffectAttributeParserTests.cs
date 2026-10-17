using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class SpecialEffectAttributeParserTests
{
    [Fact]
    public void SpecialEffectAttributeParserPreservesFirstLiveRowShape()
    {
        const string row = "40701011@@1@@10@@10@@100@@10@@60@@5@@";

        var attribute = SpecialEffectAttributeParser.ParseRow("speffect_attribute.txt", 1, row);

        Assert.Equal(40_701_011, attribute.Id);
        Assert.Equal(1, attribute.Type);
        Assert.Equal([10, 10, 100, 10, 60, 5], attribute.Values);
        Assert.Equal("speffect_attribute.txt", attribute.SourceFile);
        Assert.Equal(1, attribute.LineNumber);
        Assert.Equal(row, attribute.RawLine);
        Assert.Equal(["40701011", "1", "10", "10", "100", "10", "60", "5", ""], attribute.Fields);
    }

    [Fact]
    public void SpecialEffectAttributeParserPreservesLastLiveRowShape()
    {
        const string row = "40708186@@4@@50@@100@@0@@12000@@72000@@10@@";

        var attribute = SpecialEffectAttributeParser.ParseRow("speffect_attribute.txt", 432, row);

        Assert.Equal(40_708_186, attribute.Id);
        Assert.Equal(4, attribute.Type);
        Assert.Equal([50, 100, 0, 12_000, 72_000, 10], attribute.Values);
        Assert.Equal("speffect_attribute.txt", attribute.SourceFile);
        Assert.Equal(432, attribute.LineNumber);
        Assert.Equal(row, attribute.RawLine);
        Assert.Equal(["40708186", "4", "50", "100", "0", "12000", "72000", "10", ""], attribute.Fields);
    }

    [Theory]
    [InlineData("40701011@@1@@10@@10@@100@@10@@60@@5")]
    [InlineData("40701011@@1@@10@@10@@100@@10@@60@@5@@extra")]
    [InlineData("bad@@1@@10@@10@@100@@10@@60@@5@@")]
    [InlineData("40701011@@1@@10@@bad@@100@@10@@60@@5@@")]
    public void SpecialEffectAttributeParserRejectsWrongShapeAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => SpecialEffectAttributeParser.ParseRow("speffect_attribute.txt", 1, row));
    }
}
