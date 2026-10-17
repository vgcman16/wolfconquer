using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class CnResSourceBackedTranslatorTests
{
    [Fact]
    public void TranslatesOnlyRowsWithConflictFreeLocalSourceValues()
    {
        var targetText =
            "STR_BATTLEPASS_Lev=%d\u7ea7\r\n" +
            "STR_BATTLEPASS_Lev=Lv%d\r\n" +
            "STR_DAOIST_LUCK_DRAW_PRICE=%d \u4e94\u884c\u7075\u6c14\r\n" +
            "STR_DAOIST_LUCK_DRAW_PRICE=%d &139&\r\n" +
            "STR_SYSTEM_NAME=\u7cfb\u7edf\r\n" +
            "STR_SYSTEM_NAME=System\r\n" +
            "STR_CASE_Only=%d\u7ea7\r\n" +
            "STR_CASE_ONLY=Lv%d\r\n" +
            "STR_CONFLICT=\u6d4b\u8bd5\r\n" +
            "STR_BLOCKED=\u4ecd\u7136\u6ca1\u6709\u82f1\u6587\r\n";
        var sourceText =
            "STR_CONFLICT=First\r\n" +
            "STR_CONFLICT=Second\r\n" +
            "STR_FROM_REFERENCE=Reference Text\r\n";

        var result = CnResSourceBackedTranslator.Translate(targetText, [targetText, sourceText], out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(7, result.SourceRows);
        Assert.Equal(5, result.SourceKeys);
        Assert.Equal(1, result.ConflictedSourceKeys);
        Assert.Equal(10, result.TargetRows);
        Assert.Equal(3, result.TranslatedRows);
        Assert.Equal(3, result.UnmatchedRows);
        Assert.Equal(3, result.ChangedRows);
        Assert.Contains("STR_BATTLEPASS_Lev=Lv%d\r\n", updatedText, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(updatedText, "STR_BATTLEPASS_Lev=Lv%d"));
        Assert.Contains("STR_DAOIST_LUCK_DRAW_PRICE=%d &139&\r\n", updatedText, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(updatedText, "STR_DAOIST_LUCK_DRAW_PRICE=%d &139&"));
        Assert.Contains("STR_SYSTEM_NAME=System\r\n", updatedText, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(updatedText, "STR_SYSTEM_NAME=System"));
        Assert.Contains("STR_CASE_Only=%d\u7ea7\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("STR_CASE_ONLY=Lv%d\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("STR_CONFLICT=\u6d4b\u8bd5\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("STR_BLOCKED=\u4ecd\u7136\u6ca1\u6709\u82f1\u6587\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void ReapplyingTranslatedRowsIsIdempotent()
    {
        var targetText =
            "STR_SYSTEM_NAME=System\r\n" +
            "STR_SYSTEM_NAME=System\r\n";

        var result = CnResSourceBackedTranslator.Translate(targetText, [targetText], out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(2, result.SourceRows);
        Assert.Equal(1, result.SourceKeys);
        Assert.Equal(0, result.ConflictedSourceKeys);
        Assert.Equal(2, result.TargetRows);
        Assert.Equal(0, result.TranslatedRows);
        Assert.Equal(0, result.UnmatchedRows);
        Assert.Equal(0, result.ChangedRows);
        Assert.Equal(targetText, updatedText);
    }

    [Fact]
    public void UsesOnlyKnownSourceBackedKeyAliases()
    {
        var targetText =
            "STR_BATTLEPASS_Lev=%d\u7ea7\r\n" +
            "STR_CASE_Only=%d\u7ea7\r\n";
        var sourceText =
            "STR_BATTLEPASS_LEV=Lv%d\r\n" +
            "STR_CASE_ONLY=Lv%d\r\n";

        var result = CnResSourceBackedTranslator.Translate(targetText, [sourceText], out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(2, result.SourceRows);
        Assert.Equal(3, result.SourceKeys);
        Assert.Equal(2, result.TargetRows);
        Assert.Equal(1, result.TranslatedRows);
        Assert.Equal(1, result.UnmatchedRows);
        Assert.Equal(1, result.ChangedRows);
        Assert.Contains("STR_BATTLEPASS_Lev=Lv%d\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("STR_CASE_Only=%d\u7ea7\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsSourceValuesThatStillNeedTranslation()
    {
        var targetText =
            "STR_BLOCKED=\u6d4b\u8bd5\r\n" +
            "STR_GARBLED=\u6d4b\u8bd5\r\n" +
            "STR_CLEAN=\u6d4b\u8bd5\r\n";
        var sourceText =
            "STR_BLOCKED=\u4ecd\u7136\u662f\u4e2d\u6587\r\n" +
            "STR_GARBLED=Bad \ufffd source\r\n" +
            "STR_CLEAN=Clean Source\r\n";

        var result = CnResSourceBackedTranslator.Translate(targetText, [sourceText], out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(1, result.SourceRows);
        Assert.Equal(1, result.SourceKeys);
        Assert.Equal(3, result.TargetRows);
        Assert.Equal(1, result.TranslatedRows);
        Assert.Equal(2, result.UnmatchedRows);
        Assert.Contains("STR_BLOCKED=\u6d4b\u8bd5\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("STR_GARBLED=\u6d4b\u8bd5\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("STR_CLEAN=Clean Source\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void UsesLuaStringAssignmentsForSameResourceKeys()
    {
        var targetText =
            "STR_OFFICIAL_TYPE_KING=\u56fd\u738b\r\n" +
            "STR_TRAP_ID_1310=\u673a\u5173\r\n" +
            "STR_TEXT_WITH_ESCAPED_QUOTE=\u6d4b\u8bd5\r\n" +
            "STR_TEXT_WITH_CONTROL=\u6d4b\u8bd5\r\n";
        var sourceText =
            "\t[\"STR_OFFICIAL_TYPE_KING\"] = \"King\",\r\n" +
            "\t[\"STR_TRAP_ID_1310\"] = \"trap\" -- comment\r\n" +
            "\t[\"STR_TEXT_WITH_ESCAPED_QUOTE\"] = \"He said \\\"go\\\"\",\r\n" +
            "\t[\"STR_TEXT_WITH_CONTROL\"] = \"Line one\\nLine two\",\r\n";

        var result = CnResSourceBackedTranslator.Translate(targetText, [sourceText], out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(4, result.SourceRows);
        Assert.Equal(4, result.SourceKeys);
        Assert.Equal(4, result.TargetRows);
        Assert.Equal(3, result.TranslatedRows);
        Assert.Equal(1, result.UnmatchedRows);
        Assert.Equal(3, result.ChangedRows);
        Assert.Contains("STR_OFFICIAL_TYPE_KING=King\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("STR_TRAP_ID_1310=trap\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("STR_TEXT_WITH_ESCAPED_QUOTE=He said \"go\"\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("STR_TEXT_WITH_CONTROL=\u6d4b\u8bd5\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void PreservesEscapedNewlineSourceValuesWhenTargetHasEscapedNewline()
    {
        var targetText =
            "STR_MAIL_CONTENT=\u7269\u54c1\uff1a[%s]\\n\u91d1\u989d\uff1a[%d]\r\n" +
            "STR_MAIL_TITLE=\u6807\u9898\r\n";
        var sourceText =
            "\t[\"STR_MAIL_CONTENT\"] = \"Item: [%s]\\nAmount: [%d]\",\r\n" +
            "\t[\"STR_MAIL_TITLE\"] = \"Mail Title\",\r\n";

        var result = CnResSourceBackedTranslator.Translate(targetText, [sourceText], out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(2, result.SourceRows);
        Assert.Equal(2, result.SourceKeys);
        Assert.Equal(2, result.TranslatedRows);
        Assert.Contains("STR_MAIL_CONTENT=Item: [%s]\\nAmount: [%d]\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("STR_MAIL_TITLE=Mail Title\r\n", updatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Item: [%s]\nAmount", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void UsesCSharpClientTextReplacementRowsAndStringConstantsForExactKeys()
    {
        var targetText =
            "STR_BAG_FULL=\u80cc\u5305\u5df2\u6ee1\uff01\r\n" +
            "10009=\u662f\u5426\u786e\u5b9a\u79bb\u5f00\u5e2e\u6d3e\uff1f\r\n" +
            "STR_CONSTANT=\u5e38\u91cf\r\n";
        var sourceText =
            "        new(\"STR_BAG_FULL\", \"Your bag is full!\"),\r\n" +
            "        new ClientTextReplacement(\"10009\", \"Are you sure you want to leave the guild?\"),\r\n" +
            "        public const string STR_CONSTANT = \"Constant source\";\r\n";

        var result = CnResSourceBackedTranslator.Translate(targetText, [sourceText], out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(3, result.SourceRows);
        Assert.Equal(3, result.SourceKeys);
        Assert.Equal(3, result.TranslatedRows);
        Assert.Equal(3, result.ChangedRows);
        Assert.Contains("STR_BAG_FULL=Your bag is full!\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("10009=Are you sure you want to leave the guild?\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("STR_CONSTANT=Constant source\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsExactSourcesWithMismatchedPlaceholdersOrTargetTags()
    {
        var targetText =
            "STR_PRICE=%d \u4e94\u884c\u7075\u6c14\r\n" +
            "STR_HASH_TAG=#77\u5143\u5b9d\r\n" +
            "STR_COLOR_TAG=&139&\u79ef\u5206\r\n" +
            "STR_SAFE_EXTRA_TAG=%d \u7075\u6c14\r\n";
        var sourceText =
            "new(\"STR_PRICE\", \"Spirit price\"),\r\n" +
            "new(\"STR_HASH_TAG\", \"CPs\"),\r\n" +
            "new(\"STR_COLOR_TAG\", \"&140&Points\"),\r\n" +
            "new(\"STR_SAFE_EXTRA_TAG\", \"%d &139& Spirit\"),\r\n";

        var result = CnResSourceBackedTranslator.Translate(targetText, [sourceText], out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(4, result.SourceRows);
        Assert.Equal(4, result.SourceKeys);
        Assert.Equal(1, result.TranslatedRows);
        Assert.Equal(3, result.UnmatchedRows);
        Assert.Equal(1, result.ChangedRows);
        Assert.Contains("STR_PRICE=%d \u4e94\u884c\u7075\u6c14\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("STR_HASH_TAG=#77\u5143\u5b9d\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("STR_COLOR_TAG=&139&\u79ef\u5206\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("STR_SAFE_EXTRA_TAG=%d &139& Spirit\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void PatchUtf8FilePreservesUtf8NoBomAndLineEndings()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wolfconquer-cnres-{Guid.NewGuid():N}");
        var targetPath = Path.Combine(directory, "Server_Cn_Res.ini");
        var sourcePath = Path.Combine(directory, "reference.ini");
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8);

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(
                targetPath,
                encoding.GetBytes(
                    "STR_SYSTEM_NAME=\u7cfb\u7edf\r\n" +
                    "STR_UNMATCHED=\u6d4b\u8bd5"));
            File.WriteAllBytes(sourcePath, encoding.GetBytes("STR_SYSTEM_NAME=System\r\n"));

            var result = CnResSourceBackedTranslator.PatchUtf8File(
                targetPath,
                [sourcePath],
                includeTargetAsSource: false);

            var patchedBytes = File.ReadAllBytes(targetPath);
            var patchedText = encoding.GetString(patchedBytes);
            Assert.True(result.Changed);
            Assert.Equal(1, result.ChangedRows);
            Assert.False(patchedBytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
            Assert.Equal("STR_SYSTEM_NAME=System\r\nSTR_UNMATCHED=\u6d4b\u8bd5", patchedText);
            Assert.DoesNotContain("\n", patchedText.Replace("\r\n", string.Empty, StringComparison.Ordinal));

            var reapplied = CnResSourceBackedTranslator.PatchUtf8File(
                targetPath,
                [sourcePath],
                includeTargetAsSource: false);

            Assert.False(reapplied.Changed);
            Assert.Equal(patchedBytes, File.ReadAllBytes(targetPath));
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
    public void PatchUtf8FileToleratesInvalidUtf8SourceBytes()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wolfconquer-cnres-{Guid.NewGuid():N}");
        var targetPath = Path.Combine(directory, "Cn_Res.ini");
        var sourcePath = Path.Combine(directory, "Server_Key.lua");
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8);

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(
                targetPath,
                encoding.GetBytes(
                    "STR_SYSTEM_NAME=\u7cfb\u7edf\r\n" +
                    "STR_BAD_SOURCE=\u6d4b\u8bd5\r\n"));
            File.WriteAllBytes(
                sourcePath,
                [
                    .. Encoding.ASCII.GetBytes("STR_SYSTEM_NAME=System\r\nSTR_BAD_SOURCE="),
                    0xd0,
                    .. Encoding.ASCII.GetBytes("\r\n")
                ]);

            var result = CnResSourceBackedTranslator.PatchUtf8File(
                targetPath,
                [sourcePath],
                includeTargetAsSource: false);

            Assert.True(result.Changed);
            Assert.Equal(1, result.SourceRows);
            Assert.Equal(1, result.ChangedRows);
            Assert.Equal(
                "STR_SYSTEM_NAME=System\r\nSTR_BAD_SOURCE=\u6d4b\u8bd5\r\n",
                encoding.GetString(File.ReadAllBytes(targetPath)));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
