using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class LegacyEmoneyShopTranslatorTests
{
    [Fact]
    public void TranslatesMatchedRowsWithoutChangingShopStructure()
    {
        var shopText =
            "[recommend]\r\n" +
            "ID=1\r\n" +
            "Amount=4\r\n" +
            "MoneyType=1\r\n" +
            "3325756 other|new \u6253\u5f00\u540e\u53ef\u968f\u673a\u83b7\u5f97\r\n" +
            "Item0=4200001 item|hot \u73cd\u8d35\u5b9d\u77f3\r\n" +
            "item1=3319473 new\u53cc\u8f6e\u793c\u5305\r\n" +
            "Item2=9999999 item|new \u672a\u77e5\u7269\u54c1\r\n";

        var result = LegacyEmoneyShopTranslator.Translate(
            shopText,
            [
                CreateItemTypeLine(3_325_756, "L1 Mythsoul Pack", "Open~to~get~1~L1~Mythsoul~of~a~random~part."),
                CreateItemTypeLine(4_200_001, "P1Anima", "The~precious~stone~contains~everlasting~vital~power."),
            ],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(3, result.RowCount);
        Assert.Equal(2, result.TranslatedRows);
        Assert.Equal(1, result.UnmatchedRows);
        Assert.Equal(2, result.ChangedRows);
        Assert.Equal(3, result.UniqueItemIds);
        Assert.Equal(2, result.MatchedUniqueItemIds);
        Assert.Equal([9_999_999], result.UnmatchedItemIds);
        Assert.Contains("3325756 other|new Open to get 1 L1 Mythsoul of a random part.\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Item0=4200001 item|hot The precious stone contains everlasting vital power.\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("item1=3319473 new\u53cc\u8f6e\u793c\u5305\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Item2=9999999 item|new \u672a\u77e5\u7269\u54c1\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void ReapplyingTranslatedRowsIsIdempotent()
    {
        var shopText =
            "[recommend]\r\n" +
            "ID=1\r\n" +
            "Amount=1\r\n" +
            "3325756 other|new Open to get 1 L1 Mythsoul of a random part.\r\n";

        var result = LegacyEmoneyShopTranslator.Translate(
            shopText,
            [CreateItemTypeLine(3_325_756, "L1 Mythsoul Pack", "Open~to~get~1~L1~Mythsoul~of~a~random~part.")],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(1, result.RowCount);
        Assert.Equal(1, result.TranslatedRows);
        Assert.Equal(0, result.UnmatchedRows);
        Assert.Equal(0, result.ChangedRows);
        Assert.Equal(shopText, updatedText);
    }

    private static string CreateItemTypeLine(int itemId, string name, string description)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemId.ToString();
        fields[1] = name;
        fields[53] = description;
        return string.Join("@@", fields);
    }
}
