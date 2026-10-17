using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class HundredWeaponParserTests
{
    [Fact]
    public void HundredWeaponParserPreservesFirstLiveRowShape()
    {
        const string row = "1@@410@@1@@0@@100@@10100@@20050@@30030@@15870@@0@@0@@";

        var entry = HundredWeaponParser.ParseRow("hundred_weapon.txt", 1, row);

        Assert.Equal(1, entry.Id);
        Assert.Equal(410, entry.WeaponType);
        Assert.Equal(1, entry.Level);
        Assert.Equal([0, 100, 10100, 20050, 30030, 15870, 0, 0], entry.Values);
        Assert.Equal("hundred_weapon.txt", entry.SourceFile);
        Assert.Equal(1, entry.LineNumber);
        Assert.Equal(row, entry.RawLine);
        Assert.Equal(["1", "410", "1", "0", "100", "10100", "20050", "30030", "15870", "0", "0", ""], entry.Fields);
    }

    [Fact]
    public void HundredWeaponParserPreservesLastLiveRowShape()
    {
        const string row = "81@@490@@9@@12@@0@@10500@@20300@@30200@@15900@@3@@1@@";

        var entry = HundredWeaponParser.ParseRow("hundred_weapon.txt", 81, row);

        Assert.Equal(81, entry.Id);
        Assert.Equal(490, entry.WeaponType);
        Assert.Equal(9, entry.Level);
        Assert.Equal([12, 0, 10500, 20300, 30200, 15900, 3, 1], entry.Values);
        Assert.Equal("hundred_weapon.txt", entry.SourceFile);
        Assert.Equal(81, entry.LineNumber);
        Assert.Equal(row, entry.RawLine);
        Assert.Equal(["81", "490", "9", "12", "0", "10500", "20300", "30200", "15900", "3", "1", ""], entry.Fields);
    }

    [Theory]
    [InlineData("1@@410@@1@@0@@100@@10100@@20050@@30030@@15870@@0@@0")]
    [InlineData("1@@410@@1@@0@@100@@10100@@20050@@30030@@15870@@0@@0@@extra")]
    [InlineData("x@@410@@1@@0@@100@@10100@@20050@@30030@@15870@@0@@0@@")]
    [InlineData("1@@410@@1@@0@@bad@@10100@@20050@@30030@@15870@@0@@0@@")]
    public void HundredWeaponParserRejectsMalformedRows(string row)
    {
        Assert.Throws<FormatException>(() => HundredWeaponParser.ParseRow("hundred_weapon.txt", 1, row));
    }
}
