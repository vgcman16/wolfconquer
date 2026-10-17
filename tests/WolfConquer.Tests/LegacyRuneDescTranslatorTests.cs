using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class LegacyRuneDescTranslatorTests
{
    [Fact]
    public void TranslatesMatchedRuneDescriptionsWithoutChangingOtherRows()
    {
        var runeDescText =
            "[4010001]\r\n" +
            "Desc=\u88ab\u52a8\u6280\u80fd\r\n" +
            "Name=\u4fdd\u7559\r\n" +
            "[4020101]\n" +
            "  Desc = \u65e7\u63cf\u8ff0\r\n" +
            "[4036601]\r\n" +
            "Desc=\u4e2d\u6587\u6765\u6e90\r\n" +
            "[999]\r\n" +
            "Desc=\u672a\u77e5\r\n" +
            "[4010001]\r\n" +
            "Desc=\u91cd\u590d";
        var serverKeyText =
            "local ITEMDESCKeyValue = {\r\n" +
            "[\"STR_ITEM_ITEMDESC_4010001\"] = \"Only for Trojan. Equip to get Comprehension skill.\",\r\n" +
            "[\"STR_ITEM_ITEMDESC_4020101\"] = \"Only for Trojan. Passive: Fully recover HP.\",\r\n" +
            "[\"STR_ITEM_ITEMDESC_4036601\"] = \"\u7a00\u6709\u9ec4\u8272\u795e\u7eb9\",\r\n" +
            "}\r\n";

        var result = LegacyRuneDescTranslator.Translate(runeDescText, [serverKeyText], out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(3, result.SourceDescriptionsScanned);
        Assert.Equal(2, result.SourceDescriptions);
        Assert.Empty(result.ConflictingSourceIds);
        Assert.Equal(5, result.RowCount);
        Assert.Equal(3, result.TranslatedRows);
        Assert.Equal(2, result.UnmatchedRows);
        Assert.Equal(4, result.UniqueRuneIds);
        Assert.Equal(2, result.MatchedUniqueRuneIds);
        Assert.Equal([999, 4_036_601], result.UnmatchedRuneIds);
        Assert.Equal(3, result.ChangedRows);
        Assert.Equal(
            "[4010001]\r\n" +
            "Desc=Only for Trojan. Equip to get Comprehension skill.\r\n" +
            "Name=\u4fdd\u7559\r\n" +
            "[4020101]\n" +
            "  Desc = Only for Trojan. Passive: Fully recover HP.\r\n" +
            "[4036601]\r\n" +
            "Desc=\u4e2d\u6587\u6765\u6e90\r\n" +
            "[999]\r\n" +
            "Desc=\u672a\u77e5\r\n" +
            "[4010001]\r\n" +
            "Desc=Only for Trojan. Equip to get Comprehension skill.",
            updatedText);
    }

    [Fact]
    public void ReapplyingTranslatedRuneDescriptionsIsIdempotent()
    {
        var runeDescText =
            "[4010001]\r\n" +
            "Desc=Only for Trojan. Equip to get Comprehension skill.\r\n";
        var serverKeyText =
            "[\"STR_ITEM_ITEMDESC_4010001\"] = \"Only for Trojan. Equip to get Comprehension skill.\",\r\n";

        var result = LegacyRuneDescTranslator.Translate(runeDescText, [serverKeyText], out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(1, result.TranslatedRows);
        Assert.Equal(0, result.ChangedRows);
        Assert.Equal(runeDescText, updatedText);
    }

    [Fact]
    public void SkipsConflictingSourcesAndUnescapesLuaValues()
    {
        var runeDescText =
            "[1]\r\n" +
            "Desc=\u51b2\u7a81\r\n" +
            "[2]\r\n" +
            "Desc=\u5f15\u53f7\r\n";
        var firstServerKeyText =
            "[\"STR_ITEM_ITEMDESC_1\"] = \"First source.\",\r\n" +
            "[\"STR_ITEM_ITEMDESC_2\"] = \"Quoted \\\"Blade\\\" value.\",\r\n";
        var secondServerKeyText =
            "[\"STR_ITEM_ITEMDESC_1\"] = \"Different source.\",\r\n";

        var result = LegacyRuneDescTranslator.Translate(
            runeDescText,
            [firstServerKeyText, secondServerKeyText],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(3, result.SourceDescriptionsScanned);
        Assert.Equal(1, result.SourceDescriptions);
        Assert.Equal([1], result.ConflictingSourceIds);
        Assert.Equal(1, result.TranslatedRows);
        Assert.Equal(1, result.UnmatchedRows);
        Assert.Equal([1], result.UnmatchedRuneIds);
        Assert.Equal(
            "[1]\r\n" +
            "Desc=\u51b2\u7a81\r\n" +
            "[2]\r\n" +
            "Desc=Quoted \"Blade\" value.\r\n",
            updatedText);
    }

    [Fact]
    public void SkipsUnsafeSourceBackedDescriptionsAndKeepsConflictsUnmatched()
    {
        var runeDescText =
            "[1]\r\n" +
            "Desc=\u975e\u82f1\u6587\u6765\u6e90\r\n" +
            "[2]\r\n" +
            "Desc=\u51b2\u7a81\r\n" +
            "[3]\r\n" +
            "Desc=\u53ef\u7528\r\n" +
            "[4]\r\n" +
            "Desc=\u635f\u574f\r\n";
        var firstSourceText =
            "[1]\r\n" +
            "Desc=\u4ecd\u7136\u4e2d\u6587\r\n" +
            "[2]\r\n" +
            "Desc=First English source.\r\n" +
            "[4]\r\n" +
            "Desc=Damaged \uFFFD source.\r\n";
        var secondSourceText =
            "[2]\r\n" +
            "Desc=Different English source.\r\n" +
            "[3]\r\n" +
            "Desc=Useful source-backed description.\r\n";

        var result = LegacyRuneDescTranslator.Translate(
            runeDescText,
            [firstSourceText, secondSourceText],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(5, result.SourceDescriptionsScanned);
        Assert.Equal(1, result.SourceDescriptions);
        Assert.Equal([2], result.ConflictingSourceIds);
        Assert.Equal(1, result.TranslatedRows);
        Assert.Equal(3, result.UnmatchedRows);
        Assert.Equal([1, 2, 4], result.UnmatchedRuneIds);
        Assert.Equal(
            "[1]\r\n" +
            "Desc=\u975e\u82f1\u6587\u6765\u6e90\r\n" +
            "[2]\r\n" +
            "Desc=\u51b2\u7a81\r\n" +
            "[3]\r\n" +
            "Desc=Useful source-backed description.\r\n" +
            "[4]\r\n" +
            "Desc=\u635f\u574f\r\n",
            updatedText);
    }

    [Fact]
    public void PatchFileReadsUtf8RuneDescCodePage936ServerKeyAndItemTypeSources()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-rune-desc-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var runeDescPath = Path.Combine(root, "RuneDesc.ini");
            var serverKeyPath = Path.Combine(root, "Server_Key.lua");
            var sourceRoot = Path.Combine(root, "source");
            Directory.CreateDirectory(sourceRoot);
            var sourceRuneDescPath = Path.Combine(sourceRoot, "RuneDesc.ini");
            var itemTypePath = Path.Combine(root, "itemtype.txt");
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            var cp936 = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);

            File.WriteAllBytes(
                runeDescPath,
                utf8.GetBytes(
                    "[4010001]\r\n" +
                    "Desc=\u88ab\u52a8\u6280\u80fd\r\n" +
                    "[900001]\r\n" +
                    "Desc=\u6765\u6e90\u795e\u7eb9\r\n" +
                    "[900002]\r\n" +
                    "Desc=\u7269\u54c1\u8868\u6765\u6e90\r\n" +
                    "[4036601]\r\n" +
                    "Desc=\u4e2d\u6587\u4fdd\u7559\r\n"));
            File.WriteAllBytes(
                serverKeyPath,
                cp936.GetBytes(
                    "[\"STR_ITEM_ITEMDESC_4010001\"] = \"Only for Trojan. Equip to get Comprehension skill.\",\r\n" +
                    "[\"STR_ITEM_ITEMDESC_4036601\"] = \"\u7a00\u6709\u9ec4\u8272\u795e\u7eb9\",\r\n"));
            File.WriteAllBytes(
                sourceRuneDescPath,
                utf8.GetBytes(
                    "[900001]\r\n" +
                    "Desc=Exact RuneDesc source description.\r\n"));
            File.WriteAllBytes(
                itemTypePath,
                cp936.GetBytes(CreateItemTypeLine(
                    900_002,
                    "SourceRune",
                    "Itemtype~source-backed~description.")));

            var result = LegacyRuneDescTranslator.PatchFile(
                runeDescPath,
                [serverKeyPath, sourceRuneDescPath, itemTypePath]);

            var updatedBytes = File.ReadAllBytes(runeDescPath);
            var updatedText = utf8.GetString(updatedBytes);
            Assert.True(result.Changed);
            Assert.Equal(4, result.SourceDescriptionsScanned);
            Assert.Equal(3, result.SourceDescriptions);
            Assert.Equal(4, result.RowCount);
            Assert.Equal(3, result.TranslatedRows);
            Assert.Equal(1, result.UnmatchedRows);
            Assert.False(updatedBytes.Length >= 3 &&
                updatedBytes[0] == 0xEF &&
                updatedBytes[1] == 0xBB &&
                updatedBytes[2] == 0xBF);
            Assert.Contains("Desc=Only for Trojan. Equip to get Comprehension skill.\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("Desc=Exact RuneDesc source description.\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("Desc=Itemtype source-backed description.\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("Desc=\u4e2d\u6587\u4fdd\u7559\r\n", updatedText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
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
