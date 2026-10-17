using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class LegacyShopNameTranslatorTests
{
    [Fact]
    public void TranslateUsesEnglishClientShopNameForMatchingChineseDbShopId()
    {
        var target =
            "[Header]\r\n" +
            "Amount=2\r\n" +
            "\r\n" +
            "[Shop0]\r\n" +
            "ID=10\r\n" +
            "Name=云门关武器店\r\n" +
            "ItemAmount=0\r\n" +
            "\r\n" +
            "[Shop1]\r\n" +
            "ID=11\r\n" +
            "Name=Armorer\r\n" +
            "ItemAmount=0\r\n";
        var reference =
            "[Shop0]\r\n" +
            "ID=10\r\n" +
            "Name=Cloud Gate Weapon Shop\r\n" +
            "\r\n" +
            "[Shop1]\r\n" +
            "ID=11\r\n" +
            "Name=Phoenix Castle Equipment Shop\r\n";

        var result = LegacyShopNameTranslator.Translate(target, reference, out var updated);

        Assert.True(result.Changed);
        Assert.Equal(2, result.ReferenceShopsScanned);
        Assert.Equal(2, result.TargetShopsScanned);
        Assert.Equal(1, result.MatchedTargetShops);
        Assert.Equal(1, result.TranslatedNames);
        Assert.Equal(1, result.ChangedNames);
        Assert.Equal(0, result.UnmatchedNames);
        Assert.Contains("Name=Cloud Gate Weapon Shop\r\n", updated);
        Assert.Contains("Name=Armorer\r\n", updated);
    }

    [Fact]
    public void TranslateTracksChineseDbShopNameWithoutReferenceAsUnmatched()
    {
        var target =
            "[Shop0]\n" +
            "ID=99\n" +
            "Name=未知商店\n";
        var reference =
            "[Shop0]\n" +
            "ID=10\n" +
            "Name=Cloud Gate Weapon Shop\n";

        var result = LegacyShopNameTranslator.Translate(target, reference, out var updated);

        Assert.False(result.Changed);
        Assert.Equal(1, result.UnmatchedNames);
        Assert.Equal(target, updated);
    }

    [Fact]
    public void TranslateUsesSourceBackedTransformShopNameFallback()
    {
        var target =
            "[Shop0]\r\n" +
            "ID=9836\r\n" +
            "Name=叫天鸡外形双龙城杂货店\r\n" +
            "\r\n" +
            "[Shop1]\r\n" +
            "ID=9841\r\n" +
            "Name = 鬼斧王外形鸣凤堡药店\r\n" +
            "\r\n" +
            "[Shop2]\r\n" +
            "ID=9846\r\n" +
            "Name=火鼠外形云门关武器店\r\n" +
            "\r\n" +
            "[Shop3]\r\n" +
            "ID=9851\r\n" +
            "Name=翼蛇外形黑虎城装备店\r\n";

        var result = LegacyShopNameTranslator.Translate(target, string.Empty, out var updated);

        Assert.True(result.Changed);
        Assert.Equal(0, result.ReferenceShopsScanned);
        Assert.Equal(4, result.TargetShopsScanned);
        Assert.Equal(4, result.MatchedTargetShops);
        Assert.Equal(4, result.TranslatedNames);
        Assert.Equal(4, result.ChangedNames);
        Assert.Equal(0, result.UnmatchedNames);
        Assert.Contains("Name=Pheasant Transform Twin City Grocery\r\n", updated);
        Assert.Contains("Name = Blade Ghost Transform Phoenix Castle Pharmacy\r\n", updated);
        Assert.Contains("Name=Fire Rat Transform Cloud Gate Weapon Shop\r\n", updated);
        Assert.Contains("Name=Winged Snake Transform Black Tiger City Equipment Shop\r\n", updated);
    }

    [Fact]
    public void TranslateLeavesUnsupportedTransformShopNameUnmatched()
    {
        var target =
            "[Shop0]\n" +
            "ID=25744\n" +
            "Name=特瑞\n" +
            "\n" +
            "[Shop1]\n" +
            "ID=99999\n" +
            "Name=未知外形双龙城杂货店\n";

        var result = LegacyShopNameTranslator.Translate(target, string.Empty, out var updated);

        Assert.False(result.Changed);
        Assert.Equal(2, result.TargetShopsScanned);
        Assert.Equal(0, result.MatchedTargetShops);
        Assert.Equal(0, result.TranslatedNames);
        Assert.Equal(2, result.UnmatchedNames);
        Assert.Equal(target, updated);
    }

    [Fact]
    public void ReapplyingSourceBackedTransformShopNameFallbackIsIdempotent()
    {
        var target =
            "[Shop0]\r\n" +
            "ID=9842\r\n" +
            "Name=火鼠外形鸣凤堡药店\r\n";

        var first = LegacyShopNameTranslator.Translate(target, string.Empty, out var firstUpdate);
        var second = LegacyShopNameTranslator.Translate(firstUpdate, string.Empty, out var secondUpdate);

        Assert.True(first.Changed);
        Assert.False(second.Changed);
        Assert.Equal(1, first.TranslatedNames);
        Assert.Equal(0, second.TranslatedNames);
        Assert.Equal(firstUpdate, secondUpdate);
        Assert.Contains("Name=Fire Rat Transform Phoenix Castle Pharmacy\r\n", secondUpdate);
    }
}
