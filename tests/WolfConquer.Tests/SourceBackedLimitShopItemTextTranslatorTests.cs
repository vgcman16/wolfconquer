using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class SourceBackedLimitShopItemTextTranslatorTests
{
    [Fact]
    public void TranslatesExactSameItemIdWhenEnglishSourceExists()
    {
        var shopText =
            "local LimitShopData ={\r\n" +
            "\t[730001] = {\"\u53ef\u4ee3\u66ff\u6240\u6709\u7c7b\u578b\u7684+1\u88c5\u5907\u3002\"},\r\n" +
            "\t[4200001]  = {\"Already English.\"},\r\n" +
            "}\r\n";

        var result = SourceBackedLimitShopItemTextTranslator.Translate(
            shopText,
            [
                new SourceBackedLimitShopItemTextSource(
                    730_001,
                    "Use~it~as~a~minor~item~to~increase~Composition~progress~by~10~points.",
                    "itemtype.txt:description"),
            ],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(2, result.RowCount);
        Assert.Equal(1, result.RowsNeedingTranslation);
        Assert.Equal(1, result.SourceMatchedRows);
        Assert.Equal(0, result.SkippedRowsWithoutSource);
        Assert.Equal(1, result.UniqueItemIdsNeedingTranslation);
        Assert.Equal(1, result.MatchedUniqueItemIds);
        Assert.Empty(result.MissingItemIds);
        Assert.Empty(result.ConflictedItemIds);
        Assert.Equal(1, result.ChangedRows);
        Assert.Contains(
            "\t[730001] = {\"Use it as a minor item to increase Composition progress by 10 points.\"},\r\n",
            updatedText,
            StringComparison.Ordinal);
        Assert.Contains("\t[4200001]  = {\"Already English.\"},\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadsPackageDescriptionsServerKeyTitlesAndSameFormatRows()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wolfconquer-limit-source-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            var serverKeyPath = Path.Combine(directory, "Server_Key.lua");
            var olderLimitShopPath = Path.Combine(directory, "LimitShop.lua");
            File.WriteAllText(
                serverKeyPath,
                "\t[\"STR_ITEM_TYPE_730001\"] = \"Stone\",\r\n" +
                "\t[\"STR_ITEM_ITEMDESC_730001\"] = \"Use it as a minor item.\",\r\n" +
                "\t[\"STR_ID_tSmallLotteryTicketPackInShop[730002]\"] = \"Contains 9 lilies and 99 Peach Garden Jade.\",\r\n" +
                "\t[\"STR_ID_tNested[1][730002]\"] = \"Do not use ambiguous bracket ids.\",\r\n",
                Encoding.UTF8);
            File.WriteAllText(
                olderLimitShopPath,
                "\t[730003] = {\"Same format package description.\"},\r\n",
                Encoding.ASCII);

            var sources = SourceBackedLimitShopItemTextTranslator.ReadSourceTexts(serverKeyPath)
                .Concat(SourceBackedLimitShopItemTextTranslator.ReadSourceTexts(olderLimitShopPath))
                .ToArray();

            Assert.Contains(sources, source => source.ItemId == 730_001 && source.Text == "Stone");
            Assert.Contains(sources, source => source.ItemId == 730_001 && source.Text == "Use it as a minor item.");
            Assert.Contains(sources, source => source.ItemId == 730_002 && source.Text == "Contains 9 lilies and 99 Peach Garden Jade.");
            Assert.Contains(sources, source => source.ItemId == 730_003 && source.Text == "Same format package description.");
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
    public void PatchFileExpandsConquerBundleDirectorySourcesAndStaysIdempotent()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wolfconquer-limit-bundle-source-{Guid.NewGuid():N}");
        try
        {
            var targetDirectory = Path.Combine(directory, "target");
            var sourceDirectory = Path.Combine(directory, "source");
            var sourceIniDirectory = Path.Combine(sourceDirectory, "cqclient", "ini");
            Directory.CreateDirectory(targetDirectory);
            Directory.CreateDirectory(sourceIniDirectory);

            var limitShopPath = Path.Combine(targetDirectory, "LimitShop.lua");
            var sourceLimitShopPath = Path.Combine(sourceIniDirectory, "LimitShop.lua");
            var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
            File.WriteAllBytes(
                limitShopPath,
                encoding.GetBytes("\t[3000002] = {\"\u9700\u8981\u7ffb\u8bd1\u7684\u9650\u65f6\u5546\u5e97\u8bf4\u660e\u3002\"},\r\n"));
            File.WriteAllBytes(
                sourceLimitShopPath,
                Encoding.ASCII.GetBytes("\t[3000002] = {\"Open to receive a source-backed limited item.\"},\r\n"));

            var result = SourceBackedLimitShopItemTextTranslator.PatchFile(limitShopPath, [sourceDirectory]);
            var idempotentResult = SourceBackedLimitShopItemTextTranslator.PatchFile(limitShopPath, [sourceDirectory]);
            var updatedText = encoding.GetString(File.ReadAllBytes(limitShopPath));

            Assert.True(result.Changed);
            Assert.Equal(1, result.SourceMatchedRows);
            Assert.Equal(1, result.ChangedRows);
            Assert.False(idempotentResult.Changed);
            Assert.Equal(0, idempotentResult.RowsNeedingTranslation);
            Assert.Contains(
                "\t[3000002] = {\"Open to receive a source-backed limited item.\"},\r\n",
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
    public void PreservesLuaSyntaxAndEscapesQuotedReplacementText()
    {
        var shopText = "\t[730001] = { \"\u53ef\u83b7\u5f97%d\u4e2a\u9053\u5177\"}, -- keep comment\r\n";

        var result = SourceBackedLimitShopItemTextTranslator.Translate(
            shopText,
            [
                new SourceBackedLimitShopItemTextSource(
                    730_001,
                    "Open \"Blade\" pack \\ use %d times.",
                    "Server_Key.lua:STR_ITEM_ITEMDESC"),
            ],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(1, result.SourceMatchedRows);
        Assert.Contains(
            "\t[730001] = { \"Open \\\"Blade\\\" pack \\\\ use %d times.\"}, -- keep comment\r\n",
            updatedText,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsSourceWhenPlaceholdersWouldBeLost()
    {
        var shopText = "\t[730001] = {\"\u53ef\u83b7\u5f97%d\u4e2a\u9053\u5177\"},\r\n";

        var result = SourceBackedLimitShopItemTextTranslator.Translate(
            shopText,
            [
                new SourceBackedLimitShopItemTextSource(
                    730_001,
                    "Open to receive items.",
                    "Server_Key.lua:STR_ITEM_ITEMDESC"),
            ],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(1, result.RowCount);
        Assert.Equal(1, result.RowsNeedingTranslation);
        Assert.Equal(0, result.SourceMatchedRows);
        Assert.Equal([730_001], result.MissingItemIds);
        Assert.Equal(shopText, updatedText);
    }

    [Fact]
    public void SkipsRowsWhenSourceIsMissingOrNotCleanEnglish()
    {
        var shopText =
            "\t[730001] = {\"\u53ef\u4ee3\u66ff\u6240\u6709\u7c7b\u578b\u7684+1\u88c5\u5907\u3002\"},\r\n" +
            "\t[730002] = {\"\u53ef\u4ee3\u66ff\u6240\u6709\u7c7b\u578b\u7684+2\u88c5\u5907\u3002\"},\r\n";

        var result = SourceBackedLimitShopItemTextTranslator.Translate(
            shopText,
            [
                new SourceBackedLimitShopItemTextSource(
                    730_002,
                    "\u4ecd\u7136\u662f\u4e2d\u6587",
                    "Server_Key.lua:STR_ITEM_ITEMDESC"),
            ],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(2, result.RowCount);
        Assert.Equal(2, result.RowsNeedingTranslation);
        Assert.Equal(0, result.SourceMatchedRows);
        Assert.Equal(2, result.SkippedRowsWithoutSource);
        Assert.Equal([730_001, 730_002], result.MissingItemIds);
        Assert.Equal(shopText, updatedText);
    }

    [Fact]
    public void ConflictingEnglishSourcesDoNotPatchRows()
    {
        var shopText = "\t[730001] = {\"\u53ef\u4ee3\u66ff\u6240\u6709\u7c7b\u578b\u7684+1\u88c5\u5907\u3002\"},\r\n";

        var result = SourceBackedLimitShopItemTextTranslator.Translate(
            shopText,
            [
                new SourceBackedLimitShopItemTextSource(730_001, "First English source.", "itemtype.txt"),
                new SourceBackedLimitShopItemTextSource(730_001, "Second English source.", "Server_Key.lua"),
            ],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(1, result.RowCount);
        Assert.Equal(1, result.RowsNeedingTranslation);
        Assert.Equal(0, result.SourceMatchedRows);
        Assert.Equal(1, result.SkippedRowsWithoutSource);
        Assert.Equal([730_001], result.ConflictedItemIds);
        Assert.Equal([730_001], result.MissingItemIds);
        Assert.Equal(shopText, updatedText);
    }

    [Fact]
    public void ReapplyingTranslatedTextIsIdempotent()
    {
        var shopText = "\t[730001] = {\"Use it as a minor item to increase Composition progress by 10 points.\"},\r\n";

        var result = SourceBackedLimitShopItemTextTranslator.Translate(
            shopText,
            [
                new SourceBackedLimitShopItemTextSource(
                    730_001,
                    "Use it as a minor item to increase Composition progress by 10 points.",
                    "itemtype.txt:description"),
            ],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(1, result.RowCount);
        Assert.Equal(0, result.RowsNeedingTranslation);
        Assert.Equal(0, result.ChangedRows);
        Assert.Equal(shopText, updatedText);
    }
}
