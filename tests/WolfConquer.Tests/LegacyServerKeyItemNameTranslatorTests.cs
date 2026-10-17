using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class LegacyServerKeyItemNameTranslatorTests
{
    [Fact]
    public void TranslatesMatchedItemNameKeysWithoutChangingLuaStructure()
    {
        var serverKeyText =
            "local Text = {\r\n" +
            "\t[\"STR_ITEM_TYPE_50000\"] = \"\u7a7f\u4e91\u7bad\",\r\n" +
            "\t[\"STR_ITEM_TYPE_730001\"] = \"\u8d64\u70bc\u77f3\",\r\n" +
            "\t[\"STR_ITEM_TYPE_9999999\"] = \"\u672a\u77e5\u7269\u54c1\",\r\n" +
            "\t\"STR_ITEM_TYPE_\",\r\n" +
            "}\r\n";

        var result = LegacyServerKeyItemNameTranslator.Translate(
            serverKeyText,
            [
                CreateItemTypeLine(50_000, "SpeedArrow"),
                CreateItemTypeLine(730_001, "Stone"),
            ],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(3, result.KeyCount);
        Assert.Equal(2, result.TranslatedKeys);
        Assert.Equal(1, result.UnmatchedKeys);
        Assert.Equal(2, result.ChangedKeys);
        Assert.Equal(3, result.UniqueItemIds);
        Assert.Equal(2, result.MatchedUniqueItemIds);
        Assert.Equal([9_999_999], result.UnmatchedItemIds);
        Assert.Empty(result.ConflictingItemIds);
        Assert.Contains("\t[\"STR_ITEM_TYPE_50000\"] = \"SpeedArrow\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_ITEM_TYPE_730001\"] = \"Stone\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_ITEM_TYPE_9999999\"] = \"\u672a\u77e5\u7269\u54c1\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t\"STR_ITEM_TYPE_\",\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void ReapplyingTranslatedItemNamesIsIdempotent()
    {
        var serverKeyText =
            "local Text = {\r\n" +
            "\t[\"STR_ITEM_TYPE_50000\"] = \"SpeedArrow\",\r\n" +
            "}\r\n";

        var result = LegacyServerKeyItemNameTranslator.Translate(
            serverKeyText,
            [CreateItemTypeLine(50_000, "SpeedArrow")],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(1, result.KeyCount);
        Assert.Equal(1, result.TranslatedKeys);
        Assert.Equal(0, result.UnmatchedKeys);
        Assert.Equal(0, result.ChangedKeys);
        Assert.Empty(result.ConflictingItemIds);
        Assert.Equal(serverKeyText, updatedText);
    }

    [Fact]
    public void EscapesLuaStringValues()
    {
        var serverKeyText = "\t[\"STR_ITEM_TYPE_1\"] = \"\u6d4b\u8bd5\",\r\n";

        var result = LegacyServerKeyItemNameTranslator.Translate(
            serverKeyText,
            [CreateItemTypeLine(1, "Quoted \"Blade\" \\ Test")],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Contains("\t[\"STR_ITEM_TYPE_1\"] = \"Quoted \\\"Blade\\\" \\\\ Test\",\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void TranslatesMatchedItemDescriptionsAndShootItemNames()
    {
        var serverKeyText =
            "local Text = {\r\n" +
            "\t[\"STR_ITEM_ITEMDESC_4010001\"] = \"\u52c7\u58eb\u4e13\u5c5e\",\r\n" +
            "\t[\"STR_ITEM_ITEMDESC_4021927\"] = \"\u706b\u9053\u4e13\u5c5e\",\r\n" +
            "\t[\"STR_SHOOT_3003125_0\"] = \"\u901a\u795e\u4e39\",\r\n" +
            "\t[\"STR_SHOOT_AWARD_TYPE_1\"] = \"\u94f6\u4e24\",\r\n" +
            "}\r\n";

        var result = LegacyServerKeyItemNameTranslator.Translate(
            serverKeyText,
            [
                CreateItemTypeLine(4_010_001, "Comprehension", "Only~for~Trojan.~Equip~to~get~Comprehension."),
                CreateItemTypeLine(4_021_927, "RiseofTaoism(+27)", "Only~for~Fire~Taoist~for~40\uFFFD\uB863"),
                CreateItemTypeLine(3_003_125, "SpecialTrainingPill", "Use~it~to~increase~the~Ultra~attribute~rate."),
            ],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(3, result.KeyCount);
        Assert.Equal(3, result.TranslatedKeys);
        Assert.Equal(3, result.ChangedKeys);
        Assert.Empty(result.ConflictingItemIds);
        Assert.Contains("\t[\"STR_ITEM_ITEMDESC_4010001\"] = \"Only for Trojan. Equip to get Comprehension.\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_ITEM_ITEMDESC_4021927\"] = \"Only for Fire Taoist for 40 sec.\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_SHOOT_3003125_0\"] = \"SpecialTrainingPill\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_SHOOT_AWARD_TYPE_1\"] = \"\u94f6\u4e24\",\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsItemTextSourcesThatStillContainChinese()
    {
        var serverKeyText =
            "\t[\"STR_ITEM_TYPE_1\"] = \"\u6d4b\u8bd5\",\r\n" +
            "\t[\"STR_ITEM_ITEMDESC_1\"] = \"\u63cf\u8ff0\",\r\n";

        var result = LegacyServerKeyItemNameTranslator.Translate(
            serverKeyText,
            [CreateItemTypeLine(1, "\u6d4b\u8bd5", "\u63cf\u8ff0")],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(2, result.KeyCount);
        Assert.Equal(0, result.TranslatedKeys);
        Assert.Equal(2, result.UnmatchedKeys);
        Assert.Equal(serverKeyText, updatedText);
    }

    [Fact]
    public void PatchFileUsesExactServerKeyAndRuneDescSourcesWithoutReplacingEnglishTargets()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-server-key-item-source-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var targetPath = Path.Combine(root, "Target_Server_Key.lua");
            var sourceServerKeyPath = Path.Combine(root, "Source_Server_Key.lua");
            var runeDescPath = Path.Combine(root, "RuneDesc.ini");
            var cp936 = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

            File.WriteAllBytes(
                targetPath,
                cp936.GetBytes(
                    "\t[\"STR_ITEM_TYPE_900001\"] = \"\u6d4b\u8bd5\u7269\u54c1\",\r\n" +
                    "\t[\"STR_ITEM_TYPE_900002\"] = \"AlreadyEnglish\",\r\n" +
                    "\t[\"STR_ITEM_ITEMDESC_900003\"] = \"\u6d4b\u8bd5\u63cf\u8ff0\",\r\n"));
            File.WriteAllBytes(
                sourceServerKeyPath,
                cp936.GetBytes(
                    "\t[\"STR_ITEM_TYPE_900001\"] = \"ExactSourceName\",\r\n" +
                    "\t[\"STR_ITEM_TYPE_900002\"] = \"DifferentSourceName\",\r\n"));
            File.WriteAllBytes(
                runeDescPath,
                utf8.GetBytes(
                    "[900003]\r\n" +
                    "Desc=Exact rune description.\r\n"));

            var result = LegacyServerKeyItemNameTranslator.PatchFile(
                targetPath,
                [sourceServerKeyPath, runeDescPath]);

            var updatedText = cp936.GetString(File.ReadAllBytes(targetPath));
            Assert.True(result.Changed);
            Assert.Equal(3, result.KeyCount);
            Assert.Equal(3, result.TranslatedKeys);
            Assert.Equal(2, result.ChangedKeys);
            Assert.Empty(result.ConflictingItemIds);
            Assert.Contains("\t[\"STR_ITEM_TYPE_900001\"] = \"ExactSourceName\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_ITEM_TYPE_900002\"] = \"AlreadyEnglish\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_ITEM_ITEMDESC_900003\"] = \"Exact rune description.\",\r\n", updatedText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ConflictingExactEnglishSourcesDoNotPatchRows()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-server-key-item-conflict-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var targetPath = Path.Combine(root, "Target_Server_Key.lua");
            var firstSourcePath = Path.Combine(root, "First_Server_Key.lua");
            var secondSourcePath = Path.Combine(root, "Second_Server_Key.lua");
            var cp936 = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);

            File.WriteAllBytes(
                targetPath,
                cp936.GetBytes("\t[\"STR_ITEM_TYPE_900001\"] = \"\u6d4b\u8bd5\u7269\u54c1\",\r\n"));
            File.WriteAllBytes(
                firstSourcePath,
                cp936.GetBytes("\t[\"STR_ITEM_TYPE_900001\"] = \"FirstSourceName\",\r\n"));
            File.WriteAllBytes(
                secondSourcePath,
                cp936.GetBytes("\t[\"STR_ITEM_TYPE_900001\"] = \"SecondSourceName\",\r\n"));

            var result = LegacyServerKeyItemNameTranslator.PatchFile(
                targetPath,
                [firstSourcePath, secondSourcePath]);

            var updatedText = cp936.GetString(File.ReadAllBytes(targetPath));
            Assert.False(result.Changed);
            Assert.Equal(1, result.KeyCount);
            Assert.Equal(0, result.TranslatedKeys);
            Assert.Equal(1, result.UnmatchedKeys);
            Assert.Equal([900_001], result.UnmatchedItemIds);
            Assert.Equal([900_001], result.ConflictingItemIds);
            Assert.Equal("\t[\"STR_ITEM_TYPE_900001\"] = \"\u6d4b\u8bd5\u7269\u54c1\",\r\n", updatedText);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static string CreateItemTypeLine(int itemId, string name, string description = "")
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemId.ToString();
        fields[1] = name;
        fields[53] = description;
        return string.Join("@@", fields);
    }
}
