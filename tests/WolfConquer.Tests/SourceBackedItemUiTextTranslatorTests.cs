using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class SourceBackedItemUiTextTranslatorTests
{
    [Fact]
    public void TranslatesItemLuaShowNameRowsFromExactItemNameSource()
    {
        var itemLua =
            "local ShowName = {\r\n" +
            "\t[730001] = \"\u8d64\u70bc\u77f3\", -- keep comment\r\n" +
            "\t[730002] = \"Already English\",\r\n" +
            "}\r\n" +
            "local Other = {\r\n" +
            "\t[730001] = \"\u4e0d\u5e94\u8be5\u52a8\",\r\n" +
            "}\r\n";

        var result = SourceBackedItemUiTextTranslator.Translate(
            itemLua,
            "Item.lua",
            [
                new SourceBackedItemUiTextSource(
                    730_001,
                    SourceBackedItemUiTextTranslator.NameField,
                    "Stone",
                    "itemtype.txt:itemtype.name"),
            ],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(2, result.RowCount);
        Assert.Equal(1, result.RowsNeedingTranslation);
        Assert.Equal(1, result.SourceMatchedRows);
        Assert.Equal(1, result.ChangedRows);
        Assert.Empty(result.BlockedRows);
        Assert.Contains("\t[730001] = \"Stone\", -- keep comment\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[730001] = \"\u4e0d\u5e94\u8be5\u52a8\",\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void TranslatesItemLuaShowNameRowsFromRuneFamilyItemNameSource()
    {
        var itemLua =
            "local ShowName = {\r\n" +
            "\t[40100] = \"\u6b66\u706b\u901a\",\r\n" +
            "\t[40364] = \"\u5929\u5e55\",\r\n" +
            "}\r\n";

        var result = SourceBackedItemUiTextTranslator.Translate(
            itemLua,
            "Item.lua",
            [
                new SourceBackedItemUiTextSource(
                    4_010_001,
                    SourceBackedItemUiTextTranslator.NameField,
                    "Comprehension",
                    "itemtype.txt:itemtype.name"),
                new SourceBackedItemUiTextSource(
                    4_036_401,
                    SourceBackedItemUiTextTranslator.NameField,
                    "Sky Veil (+1)",
                    "itemtype.txt:itemtype.name"),
            ],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(2, result.RowCount);
        Assert.Equal(2, result.RowsNeedingTranslation);
        Assert.Equal(2, result.SourceMatchedRows);
        Assert.Equal(2, result.ChangedRows);
        Assert.Empty(result.BlockedRows);
        Assert.Contains("\t[40100] = \"Comprehension\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[40364] = \"Sky Veil\",\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void TranslatesItemQuenchRowsOnlyWhenSameIdFieldSourcePreservesPlaceholders()
    {
        var quenchText =
            "[320]\n" +
            "Name=\u9b54\u9632\u589e\u52a0\n" +
            "Num=%d\u7ea7\u9b54\u9632\u589e\u52a0:+%d%%\uff0c\n" +
            "Note=\u5269\u4f59\u65f6\u95f4:%02u\u5929%02u\u5c0f\u65f6%02u\u5206%02u\u79d2\n" +
            "Level=6\n";

        var result = SourceBackedItemUiTextTranslator.Translate(
            quenchText,
            "ItemQuenchTipInfo.ini",
            [
                new SourceBackedItemUiTextSource(320, "Name", "Magic Defense Increase", "english-quench:Name"),
                new SourceBackedItemUiTextSource(320, "Num", "Level %d Magic Defense:+%d%%,", "english-quench:Num"),
                new SourceBackedItemUiTextSource(320, "Note", "Remaining time:%02u days %02u hours %02u minutes %02u seconds", "english-quench:Note"),
            ],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(3, result.RowCount);
        Assert.Equal(3, result.RowsNeedingTranslation);
        Assert.Equal(3, result.SourceMatchedRows);
        Assert.Equal(3, result.ChangedRows);
        Assert.DoesNotContain("\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Name=Magic Defense Increase\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Num=Level %d Magic Defense:+%d%%,\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Note=Remaining time:%02u days %02u hours %02u minutes %02u seconds\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Level=6\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void TranslatesItemQuenchDeterministicLabelsWithoutSourcePreservingPlaceholders()
    {
        var quenchText =
            "[320]\r\n" +
            "Name=\u9b54\u9632\u589e\u52a0\r\n" +
            "Num=%d\u7ea7\u9b54\u9632\u589e\u52a0:+%d%%\uff0c\r\n" +
            "Note=\u5269\u4f59\u65f6\u95f4:%02u\u5929%02u\u5c0f\u65f6%02u\u5206%02u\u79d2\r\n" +
            "AppendNum=%s\u6700\u7ec8\u6cd5\u4f24\u51cf\u514d\uff1a+%d\r\n" +
            "[321]\r\n" +
            "Name=\u6cd5\u672f\u7a7f\u900f\r\n" +
            "Num=%d\u7ea7\u6cd5\u672f\u7a7f\u900f:+%d\r\n";

        var result = SourceBackedItemUiTextTranslator.Translate(
            quenchText,
            "ItemQuenchTipInfo.ini",
            [],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(6, result.RowCount);
        Assert.Equal(6, result.RowsNeedingTranslation);
        Assert.Equal(6, result.SourceMatchedRows);
        Assert.Equal(6, result.ChangedRows);
        Assert.Empty(result.BlockedRows);
        Assert.Contains("Name=Magic Defense Increase\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Num=Level %d Magic Defense Increase:+%d%%,\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Note=Remaining time:%02u days %02u hours %02u minutes %02u seconds\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("AppendNum=%sFinal Magic Damage Reduction:+%d\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Name=Magic Penetration\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Num=Level %d Magic Penetration:+%d\r\n", updatedText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\u9b54\u9632\u589e\u52a0", "Magic Defense Increase")]
    [InlineData("\u6cd5\u672f\u7a7f\u900f", "Magic Penetration")]
    [InlineData("\u76fe\u724c\u683c\u6321", "Shield Block")]
    [InlineData("\u751f\u547d\u589e\u6548", "Health Boost")]
    [InlineData("\u6700\u7ec8\u7269\u7406\u4f24\u5bb3\u51cf\u514d", "Final Physical Damage Reduction")]
    [InlineData("\u6700\u7ec8\u7269\u7406\u4f24\u5bb3\u589e\u6548", "Final Physical Damage Boost")]
    [InlineData("\u6bd2\u4f24\u51cf\u514d", "Poison Damage Reduction")]
    [InlineData("\u6700\u7ec8\u6cd5\u672f\u4f24\u5bb3\u589e\u6548", "Final Magic Damage Boost")]
    [InlineData("\u6700\u7ec8\u6cd5\u672f\u4f24\u5bb3\u51cf\u514d", "Final Magic Damage Reduction")]
    [InlineData("\u7269\u7406\u66b4\u51fb", "Physical Critical")]
    [InlineData("\u6cd5\u672f\u66b4\u51fb", "Magic Critical")]
    [InlineData("\u514d\u66b4\u673a\u7387", "Critical Immunity Rate")]
    [InlineData("\u7c89\u788e\u653b\u51fb", "Smash Attack")]
    [InlineData("\u575a\u56fa\u9632\u5fa1", "Sturdy Defense")]
    [InlineData("\u91d1\u5c5e\u6027\u6297\u6027", "Metal Resistance")]
    [InlineData("\u6728\u5c5e\u6027\u6297\u6027", "Wood Resistance")]
    [InlineData("\u6c34\u5c5e\u6027\u6297\u6027", "Water Resistance")]
    [InlineData("\u706b\u5c5e\u6027\u6297\u6027", "Fire Resistance")]
    [InlineData("\u571f\u5c5e\u6027\u6297\u6027", "Earth Resistance")]
    public void TranslatesRequiredItemQuenchNameGlossaryTerms(string sourceTerm, string expectedTerm)
    {
        var quenchText =
            "[330]\r\n" +
            $"Name={sourceTerm}\r\n";

        var result = SourceBackedItemUiTextTranslator.Translate(
            quenchText,
            "ItemQuenchTipInfo.ini",
            [],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(1, result.SourceMatchedRows);
        Assert.Equal($"[330]\r\nName={expectedTerm}\r\n", updatedText);
    }

    [Fact]
    public void LeavesUnsupportedItemQuenchProseUnchangedWithoutSource()
    {
        var quenchText =
            "[400]\r\n" +
            "Note=\u4efb\u610f\u5267\u60c5\u63cf\u8ff0\r\n";

        var result = SourceBackedItemUiTextTranslator.Translate(
            quenchText,
            "ItemQuenchTipInfo.ini",
            [],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(1, result.RowCount);
        Assert.Equal(1, result.RowsNeedingTranslation);
        Assert.Equal(1, result.SkippedRowsWithoutSource);
        Assert.Equal([new SourceBackedItemUiTextBlockedRow("ItemQuenchTipInfo.ini", 400, "Note", "missing-source")], result.BlockedRows);
        Assert.Equal(quenchText, updatedText);
    }

    [Fact]
    public void DoesNotApplyItemQuenchBuiltInLabelsOutsideItemQuenchTipInfo()
    {
        var storageText =
            "[1-1]\r\n" +
            "ID=320\r\n" +
            "Name=\u9b54\u9632\u589e\u52a0\r\n";

        var result = SourceBackedItemUiTextTranslator.Translate(
            storageText,
            "Storage.ini",
            [],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(1, result.RowCount);
        Assert.Equal(1, result.RowsNeedingTranslation);
        Assert.Equal(1, result.SkippedRowsWithUnsupportedField);
        Assert.Equal([new SourceBackedItemUiTextBlockedRow("Storage.ini", 320, "Name", "unsupported-field")], result.BlockedRows);
        Assert.Equal(storageText, updatedText);
    }

    [Fact]
    public void ItemQuenchBuiltInLabelTranslationIsIdempotent()
    {
        var quenchText =
            "[500]\r\n" +
            "Name=\u706b\u5c5e\u6027\u6297\u6027\r\n" +
            "Num=%d\u7ea7\u706b\u5c5e\u6027\u6297\u6027:+%d%%\r\n";

        var firstResult = SourceBackedItemUiTextTranslator.Translate(
            quenchText,
            "ItemQuenchTipInfo.ini",
            [],
            out var firstUpdatedText);
        var secondResult = SourceBackedItemUiTextTranslator.Translate(
            firstUpdatedText,
            "ItemQuenchTipInfo.ini",
            [],
            out var secondUpdatedText);

        Assert.True(firstResult.Changed);
        Assert.False(secondResult.Changed);
        Assert.Equal(0, secondResult.RowsNeedingTranslation);
        Assert.Equal(firstUpdatedText, secondUpdatedText);
    }

    [Fact]
    public void TranslatesStorageRowsUsingIdWithinUnitSection()
    {
        var storageText =
            "[Storage]\r\n" +
            "StorageTypeCount=1\r\n" +
            "[3-1]\r\n" +
            "ID=6007\r\n" +
            "Intro=\u7d2b\u9704\u9cb2\u9e4f\u7ffc\\n\u9cb2\u9e4f\u4e4b\u7ffc\uff0c\u98ce\u52a8\u4e5d\u9704\u3002\r\n" +
            "DelTip=\u9601\u4e0b\u5931\u53bb\u4e86\u8fd9\u4ef6\u7269\u54c1\uff01\r\n";

        var result = SourceBackedItemUiTextTranslator.Translate(
            storageText,
            "Storage.ini",
            [
                new SourceBackedItemUiTextSource(6_007, "Intro", "Sky Wing\\nThe wind moves through the ninth heaven.", "english-storage:Intro"),
            ],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(2, result.RowCount);
        Assert.Equal(2, result.RowsNeedingTranslation);
        Assert.Equal(1, result.SourceMatchedRows);
        Assert.Equal(1, result.SkippedRowsWithoutSource);
        Assert.Equal([new SourceBackedItemUiTextBlockedRow("Storage.ini", 6_007, "DelTip", "missing-source")], result.BlockedRows);
        Assert.Contains("Intro=Sky Wing\\nThe wind moves through the ninth heaven.\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("DelTip=\u9601\u4e0b\u5931\u53bb\u4e86\u8fd9\u4ef6\u7269\u54c1\uff01\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsReplacementWhenPlaceholdersOrColorsWouldBeLost()
    {
        var storageText =
            "[1-1]\r\n" +
            "ID=730001\r\n" +
            "Intro=<color=0xffe4d5b2>\u5269\u4f59:%d</color>\r\n";

        var result = SourceBackedItemUiTextTranslator.Translate(
            storageText,
            "Storage.ini",
            [
                new SourceBackedItemUiTextSource(
                    730_001,
                    SourceBackedItemUiTextTranslator.DescriptionField,
                    "Remaining:%d",
                    "itemtype.txt:itemtype.description"),
            ],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(1, result.RowCount);
        Assert.Equal(1, result.RowsNeedingTranslation);
        Assert.Equal(0, result.SourceMatchedRows);
        Assert.Equal(1, result.SkippedRowsWithUnsafeReplacement);
        Assert.Equal([new SourceBackedItemUiTextBlockedRow("Storage.ini", 730_001, "Intro", "unsafe-replacement")], result.BlockedRows);
        Assert.Equal(storageText, updatedText);
    }

    [Fact]
    public void ConflictingEnglishSourcesDoNotPatchRows()
    {
        var quenchText =
            "[730001]\r\n" +
            "Name=\u8d64\u70bc\u77f3\r\n";

        var result = SourceBackedItemUiTextTranslator.Translate(
            quenchText,
            "ItemQuenchTipInfo.ini",
            [
                new SourceBackedItemUiTextSource(730_001, "Name", "Stone", "itemtype.txt:itemtype.name"),
                new SourceBackedItemUiTextSource(730_001, "Name", "Meteor Stone", "other-source:Name"),
            ],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(1, result.RowCount);
        Assert.Equal(1, result.RowsNeedingTranslation);
        Assert.Equal(1, result.SkippedRowsWithConflictingSource);
        Assert.Equal([new SourceBackedItemUiTextConflict(730_001, "Name")], result.Conflicts);
        Assert.Equal([new SourceBackedItemUiTextBlockedRow("ItemQuenchTipInfo.ini", 730_001, "Name", "conflicting-source")], result.BlockedRows);
        Assert.Equal(quenchText, updatedText);
    }

    [Fact]
    public void ReadsItemTypeServerKeyAndSameFormatSources()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wolfconquer-itemui-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            var itemTypePath = Path.Combine(directory, "itemtype.txt");
            var serverKeyPath = Path.Combine(directory, "Server_Key.lua");
            var storagePath = Path.Combine(directory, "Storage.ini");
            File.WriteAllText(itemTypePath, BuildItemTypeLine(730_001, "Stone", "Use~it~for~composition."), Encoding.ASCII);
            File.WriteAllText(serverKeyPath, "[\"STR_ITEM_ITEMDESC_730002\"] = \"Server key description.\"\r\n", Encoding.ASCII);
            File.WriteAllText(
                storagePath,
                "[1-1]\r\nID=730003\r\nIntro=Clean storage intro.\r\n",
                Encoding.ASCII);

            var itemTypeSources = SourceBackedItemUiTextTranslator.ReadSourceTexts(itemTypePath);
            var serverKeySources = SourceBackedItemUiTextTranslator.ReadSourceTexts(serverKeyPath);
            var storageSources = SourceBackedItemUiTextTranslator.ReadSourceTexts(storagePath);

            Assert.Contains(itemTypeSources, source => source.ItemId == 730_001 && source.Field == "Name" && source.Text == "Stone");
            Assert.Contains(itemTypeSources, source => source.ItemId == 730_001 && source.Field == "Description" && source.Text == "Use~it~for~composition.");
            Assert.Contains(serverKeySources, source => source.ItemId == 730_002 && source.Field == "Description" && source.Text == "Server key description.");
            Assert.Contains(storageSources, source => source.ItemId == 730_003 && source.Field == "Intro" && source.Text == "Clean storage intro.");
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
    public void PatchFileDetectsCp936ItemLuaAndUtf8StorageTempCopies()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wolfconquer-itemui-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            var itemTypePath = Path.Combine(directory, "itemtype.txt");
            var itemLuaPath = Path.Combine(directory, "Item.lua");
            var storagePath = Path.Combine(directory, "Storage.ini");
            var cp936 = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
            var utf8 = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8);

            File.WriteAllBytes(
                itemTypePath,
                cp936.GetBytes(BuildItemTypeLine(730_001, "Stone", "Use~it~as~a~minor~item.")));
            File.WriteAllBytes(
                itemLuaPath,
                cp936.GetBytes(
                    "local ShowName = {\r\n" +
                    "\t[730001] = \"\u8d64\u70bc\u77f3\",\r\n" +
                    "}\r\n"));
            File.WriteAllBytes(
                storagePath,
                utf8.GetBytes(
                    "[1-1]\n" +
                    "ID=730001\n" +
                    "Intro=\u7528\u4e8e\u8ffd\u52a0\u7684\u77f3\u5934\n"));

            var itemResult = SourceBackedItemUiTextTranslator.PatchFile(itemLuaPath, [itemTypePath]);
            var storageResult = SourceBackedItemUiTextTranslator.PatchFile(storagePath, [itemTypePath]);

            Assert.True(itemResult.Changed);
            Assert.True(storageResult.Changed);
            Assert.Equal("local ShowName = {\r\n\t[730001] = \"Stone\",\r\n}\r\n", cp936.GetString(File.ReadAllBytes(itemLuaPath)));
            Assert.Equal("[1-1]\nID=730001\nIntro=Use it as a minor item.\n", utf8.GetString(File.ReadAllBytes(storagePath)));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static string BuildItemTypeLine(int itemId, string name, string description)
    {
        var fields = Enumerable.Repeat("0", 54).ToArray();
        fields[0] = itemId.ToString();
        fields[1] = name;
        fields[52] = "RareItem";
        fields[53] = description;
        return string.Join("@@", fields) + "\r\n";
    }
}
