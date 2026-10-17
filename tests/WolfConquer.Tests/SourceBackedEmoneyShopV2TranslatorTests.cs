using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class SourceBackedEmoneyShopV2TranslatorTests
{
    [Fact]
    public void TranslatesOnlyCjkDisplayTextWhenEnglishSourceExists()
    {
        var shopText =
            "ID=2888\r\n" +
            "3319473 other|new \u53cc\u8f6e\u6697\u85cf\u98ce\u706b\u4e4b\u52bf\u3002\r\n" +
            "Item0=4200001 item|hot Already English.\r\n" +
            "Item1=9999999 item|new \u672a\u77e5\u7269\u54c1\r\n";

        var result = SourceBackedEmoneyShopV2Translator.Translate(
            shopText,
            [
                new SourceBackedEmoneyShopV2TextSource(
                    3_319_473,
                    "Use it in the tower to move up 1 floor.",
                    "itemtype.txt:description"),
                new SourceBackedEmoneyShopV2TextSource(
                    4_200_001,
                    "The precious stone contains everlasting vital power.",
                    "Server_Key.lua:STR_ITEM_ITEMDESC"),
            ],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(3, result.RowCount);
        Assert.Equal(2, result.RowsNeedingTranslation);
        Assert.Equal(1, result.SourceMatchedRows);
        Assert.Equal(1, result.SkippedRowsWithoutSource);
        Assert.Equal(2, result.UniqueItemIdsNeedingTranslation);
        Assert.Equal(1, result.MatchedUniqueItemIds);
        Assert.Equal([9_999_999], result.MissingItemIds);
        Assert.Equal(1, result.ChangedRows);
        Assert.Contains("3319473 other|new Use it in the tower to move up 1 floor.\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Item0=4200001 item|hot Already English.\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Item1=9999999 item|new \u672a\u77e5\u7269\u54c1\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void ReapplyingTranslatedTextIsIdempotent()
    {
        var shopText =
            "ID=2888\r\n" +
            "3319473 other|new Use it in the tower to move up 1 floor.\r\n";

        var result = SourceBackedEmoneyShopV2Translator.Translate(
            shopText,
            [
                new SourceBackedEmoneyShopV2TextSource(
                    3_319_473,
                    "Use it in the tower to move up 1 floor.",
                    "itemtype.txt:description"),
            ],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(1, result.RowCount);
        Assert.Equal(0, result.RowsNeedingTranslation);
        Assert.Equal(0, result.ChangedRows);
        Assert.Equal(shopText, updatedText);
    }

    [Fact]
    public void ReadsPackageDescriptionsServerKeyTitlesAndSameFormatRows()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wolfconquer-emoney-source-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            var serverKeyPath = Path.Combine(directory, "Server_Key.lua");
            var olderShopPath = Path.Combine(directory, "emoneyshop.ini");
            File.WriteAllText(
                serverKeyPath,
                "\t[\"STR_ITEM_TYPE_727808\"] = \"Brocade Feather Martial Attire Pack\",\r\n" +
                "\t[\"STR_ITEM_ITEMDESC_727808\"] = \"Open to get a 1% blessed garment.\",\r\n" +
                "\t[\"STR_ID_tSmallLotteryTicketPackInShop[3200504]\"] = \"Contains 9 lilies and 99 Peach Garden Jade.\",\r\n" +
                "\t[\"STR_ID_tNested[1][3200504]\"] = \"Do not use ambiguous bracket ids.\",\r\n",
                Encoding.UTF8);
            File.WriteAllText(
                olderShopPath,
                "Item0=727792 hot Contains the Sacred Jade Hare Garment [Collector's Edition], Brocade Feather Martial Attire Pack, 1 Dragon Ball, 1 Focus Pill Mega Pack, 2 Small Praying Stones, and 1 Meteor Scroll.\r\n",
                Encoding.ASCII);

            var sources = SourceBackedEmoneyShopV2Translator.ReadSourceTexts(serverKeyPath)
                .Concat(SourceBackedEmoneyShopV2Translator.ReadSourceTexts(olderShopPath))
                .ToArray();

            Assert.Contains(sources, source => source.ItemId == 727_808 && source.Text == "Brocade Feather Martial Attire Pack");
            Assert.Contains(sources, source => source.ItemId == 727_808 && source.Text == "Open to get a 1% blessed garment.");
            Assert.Contains(sources, source => source.ItemId == 3_200_504 && source.Text == "Contains 9 lilies and 99 Peach Garden Jade.");
            Assert.Contains(sources, source => source.ItemId == 727_792 && source.Text.StartsWith("Contains the Sacred Jade Hare", StringComparison.Ordinal));
            Assert.DoesNotContain(sources, source => source.Text == "Do not use ambiguous bracket ids.");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void PatchFileReadsSiblingLegacyEmoneyShopRowsWhenTargetShopIsSource()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wolfconquer-emoney-sibling-source-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            var shopPath = Path.Combine(directory, "emoneyShopV2.ini");
            var siblingShopPath = Path.Combine(directory, "emoneyshop.ini");
            var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);

            File.WriteAllBytes(
                shopPath,
                encoding.GetBytes(
                    "ID=2888\r\n" +
                    "727792 item|hot \u5185\u6709\u5723\u7075\u7389\u5154\u5916\u5957[\u73cd\u85cf\u7248]\u4e0e\u9526\u7fce\u6b66\u8863\u793c\u5305\u3001\u9f99\u73e01\u9897\u3002\r\n" +
                    "Item1=9999999 item|new \u672a\u77e5\u7269\u54c1\r\n"));
            File.WriteAllBytes(
                siblingShopPath,
                encoding.GetBytes(
                    "Item0=727792 hot Contains the Sacred Jade Hare Garment [Collector's Edition], Brocade Feather Martial Attire Pack, and 1 Dragon Ball.\r\n"));

            var result = SourceBackedEmoneyShopV2Translator.PatchFile(shopPath, [shopPath]);
            var updatedText = encoding.GetString(File.ReadAllBytes(shopPath));

            Assert.True(result.Changed);
            Assert.Equal(2, result.RowCount);
            Assert.Equal(2, result.RowsNeedingTranslation);
            Assert.Equal(1, result.SourceMatchedRows);
            Assert.Equal(1, result.SkippedRowsWithoutSource);
            Assert.Equal([9_999_999], result.MissingItemIds);
            Assert.Contains(
                "727792 item|hot Contains the Sacred Jade Hare Garment [Collector's Edition], Brocade Feather Martial Attire Pack, and 1 Dragon Ball.\r\n",
                updatedText,
                StringComparison.Ordinal);
            Assert.Contains("Item1=9999999 item|new \u672a\u77e5\u7269\u54c1\r\n", updatedText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void PatchFileExpandsConquerBundleDirectorySourcesAndStaysIdempotent()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wolfconquer-emoney-bundle-source-{Guid.NewGuid():N}");
        try
        {
            var targetDirectory = Path.Combine(directory, "target");
            var sourceDirectory = Path.Combine(directory, "source");
            var sourceIniDirectory = Path.Combine(sourceDirectory, "cqclient", "ini");
            Directory.CreateDirectory(targetDirectory);
            Directory.CreateDirectory(sourceIniDirectory);

            var shopPath = Path.Combine(targetDirectory, "emoneyShopV2.ini");
            var sourceShopPath = Path.Combine(sourceIniDirectory, "emoneyShopV2.ini");
            var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
            File.WriteAllBytes(
                shopPath,
                encoding.GetBytes(
                    "ID=2888\r\n" +
                    "3000001 item|new \u9700\u8981\u7ffb\u8bd1\u7684\u9053\u5177\u8bf4\u660e\u3002\r\n"));
            File.WriteAllBytes(
                sourceShopPath,
                Encoding.ASCII.GetBytes(
                    "3000001 item|new Open to receive a source-backed bundle item.\r\n"));

            var result = SourceBackedEmoneyShopV2Translator.PatchFile(shopPath, [sourceDirectory]);
            var idempotentResult = SourceBackedEmoneyShopV2Translator.PatchFile(shopPath, [sourceDirectory]);
            var updatedText = encoding.GetString(File.ReadAllBytes(shopPath));

            Assert.True(result.Changed);
            Assert.Equal(1, result.SourceMatchedRows);
            Assert.Equal(1, result.ChangedRows);
            Assert.False(idempotentResult.Changed);
            Assert.Equal(0, idempotentResult.RowsNeedingTranslation);
            Assert.Contains(
                "3000001 item|new Open to receive a source-backed bundle item.\r\n",
                updatedText,
                StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void SameFormatRowsUseRowContextWhenSameItemIdHasDistinctDescriptions()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wolfconquer-emoney-row-context-source-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            var sourcePath = Path.Combine(directory, "emoneyShopV2.ini");
            File.WriteAllText(
                sourcePath,
                "3320290 item|new Open to get 1 24-hour Blessed Star(Splendor) (B) Garment.\r\n" +
                "item12=3320290 new Open to get 1 24-hour Blessed Star (Splendor) (B) Garment.\r\n",
                Encoding.ASCII);

            var shopText =
                "3320290 item|new \u6253\u5f00\u53ef\u83b7\u5f9724\u5c0f\u65f6\u798f\u661f\u5929\u964d\u3010\u534e\u5f69\u7248\u3011\uff08\u8d60\uff09\u5916\u5957\u3002\r\n" +
                "item12=3320290 new \u6253\u5f00\u53ef\u83b7\u5f9724\u5c0f\u65f6\u798f\u661f\u5929\u964d\u3010\u534e\u5f69\u7248\u3011\uff08\u8d60\uff09\u5916\u5957\u3002\r\n";

            var result = SourceBackedEmoneyShopV2Translator.Translate(
                shopText,
                SourceBackedEmoneyShopV2Translator.ReadSourceTexts(sourcePath),
                out var updatedText);

            Assert.True(result.Changed);
            Assert.Equal(2, result.SourceMatchedRows);
            Assert.Equal(2, result.ChangedRows);
            Assert.Contains(
                "3320290 item|new Open to get 1 24-hour Blessed Star(Splendor) (B) Garment.\r\n",
                updatedText,
                StringComparison.Ordinal);
            Assert.Contains(
                "item12=3320290 new Open to get 1 24-hour Blessed Star (Splendor) (B) Garment.\r\n",
                updatedText,
                StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void PreservesMarkersWhileReplacingOnlyTitleOrDescriptionText()
    {
        var shopText =
            "ID=2888\r\n" +
            "Item0=727792 hot \u63a8\u8350\u56db\r\n" +
            "3319473 other|new \u53ef\u83b7\u5f97%d\u4e2a\u9053\u5177\u3002\r\n";

        var result = SourceBackedEmoneyShopV2Translator.Translate(
            shopText,
            [
                new SourceBackedEmoneyShopV2TextSource(
                    727_792,
                    "Sacred Jade Hare Pack",
                    "Server_Key.lua:STR_ITEM_TYPE"),
                new SourceBackedEmoneyShopV2TextSource(
                    3_319_473,
                    "Open to receive %d items.",
                    "Server_Key.lua:STR_ITEM_ITEMDESC"),
            ],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(2, result.SourceMatchedRows);
        Assert.Contains("Item0=727792 hot Sacred Jade Hare Pack\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("3319473 other|new Open to receive %d items.\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsSourceWhenPlaceholdersWouldBeLost()
    {
        var shopText = "3319473 item|new \u53ef\u83b7\u5f97%d\u4e2a\u9053\u5177\u3002\r\n";

        var result = SourceBackedEmoneyShopV2Translator.Translate(
            shopText,
            [
                new SourceBackedEmoneyShopV2TextSource(
                    3_319_473,
                    "Open to receive items.",
                    "Server_Key.lua:STR_ITEM_ITEMDESC"),
            ],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(1, result.RowCount);
        Assert.Equal(1, result.RowsNeedingTranslation);
        Assert.Equal(0, result.SourceMatchedRows);
        Assert.Equal([3_319_473], result.MissingItemIds);
        Assert.Equal(shopText, updatedText);
    }

    [Fact]
    public void SkipsRowsWhenSourceIsMissingOrStillCjk()
    {
        var shopText =
            "ID=2888\r\n" +
            "3319473 other|new \u53cc\u8f6e\u6697\u85cf\u98ce\u706b\u4e4b\u52bf\u3002\r\n" +
            "Item0=3200504 item|new \u5185\u542b9\u6735\u767e\u5408\u82b1\u3002\r\n";

        var result = SourceBackedEmoneyShopV2Translator.Translate(
            shopText,
            [
                new SourceBackedEmoneyShopV2TextSource(
                    3_200_504,
                    "\u6e05\u96c5\u767e\u5408\u5927\u793c\u5305",
                    "Server_Key.lua:STR_ITEM_ITEMDESC"),
            ],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(2, result.RowCount);
        Assert.Equal(2, result.RowsNeedingTranslation);
        Assert.Equal(0, result.SourceMatchedRows);
        Assert.Equal(2, result.SkippedRowsWithoutSource);
        Assert.Equal([3_200_504, 3_319_473], result.MissingItemIds);
        Assert.Equal(shopText, updatedText);
    }

    [Fact]
    public void ConflictingEnglishSourcesDoNotPatchRows()
    {
        var shopText =
            "ID=2888\r\n" +
            "3319473 other|new \u53cc\u8f6e\u6697\u85cf\u98ce\u706b\u4e4b\u52bf\u3002\r\n";

        var result = SourceBackedEmoneyShopV2Translator.Translate(
            shopText,
            [
                new SourceBackedEmoneyShopV2TextSource(3_319_473, "First English source.", "itemtype.txt"),
                new SourceBackedEmoneyShopV2TextSource(3_319_473, "Second English source.", "Server_Key.lua"),
            ],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal([3_319_473], result.ConflictedItemIds);
        Assert.Equal([3_319_473], result.MissingItemIds);
        Assert.Equal(shopText, updatedText);
    }
}
