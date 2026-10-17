using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class SourceBackedStrResTranslatorTests
{
    [Fact]
    public void TranslatesExactKeysFromSafeSources()
    {
        var targetText =
            "90001=\u4ea4\u6613\r\n" +
            "strres.custom=\u81ea\u5b9a\u4e49\r\n" +
            "STRRES.CUSTOM=\u4e0d\u5e94\u8be5\u5339\u914d\r\n";
        var sourceText =
            "90001=Trade\r\n" +
            "new(\"strres.custom\", \"Custom Text\"),\r\n";

        var result = SourceBackedStrResTranslator.Translate(
            targetText,
            [sourceText],
            out var updatedText,
            catalogReplacements: null);

        Assert.True(result.Changed);
        Assert.Equal(2, result.SourceRows);
        Assert.Equal(2, result.SourceKeys);
        Assert.Equal(3, result.TargetRows);
        Assert.Equal(3, result.RowsNeedingTranslation);
        Assert.Equal(2, result.TranslatedRows);
        Assert.Equal(1, result.UnmatchedRows);
        Assert.Equal(2, result.ChangedRows);
        Assert.Contains("90001=Trade\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("strres.custom=Custom Text\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("STRRES.CUSTOM=\u4e0d\u5e94\u8be5\u5339\u914d\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void UsesOnlyExplicitSyntheticCatalogReplacements()
    {
        const string target = "sample.label=\u6d4b\u8bd5\r\nsample.count=\u6d4b\u8bd5:%d\r\n";
        var withoutCatalog = SourceBackedStrResTranslator.Translate(target, [], out var unchanged);
        Assert.False(withoutCatalog.Changed);
        Assert.Equal(target, unchanged);

        var result = SourceBackedStrResTranslator.Translate(target, [], out var updated,
            catalogReplacements: [new("sample.label", "Sample label"), new("sample.count", "Sample count:%d")]);
        Assert.Equal(2, result.TranslatedRows);
        Assert.Equal(0, result.UnmatchedRows);
        Assert.Equal("sample.label=Sample label\r\nsample.count=Sample count:%d\r\n", updated);
    }

    [Fact]
    public void SkipsCommentsAndPreservesLayout()
    {
        var targetText =
            ";90001=\u6ce8\u91ca\r\n" +
            "\r\n" +
            "  90001 = \u6d4b\u8bd5\n";
        var sourceText =
            ";90001=Comment Source\r\n" +
            "90001=Translated\r\n";

        var result = SourceBackedStrResTranslator.Translate(
            targetText,
            [sourceText],
            out var updatedText,
            catalogReplacements: null);

        Assert.True(result.Changed);
        Assert.Equal(1, result.SourceRows);
        Assert.Equal(1, result.TargetRows);
        Assert.Equal(
            ";90001=\u6ce8\u91ca\r\n" +
            "\r\n" +
            "  90001 = Translated\n",
            updatedText);
    }

    [Fact]
    public void SkipsCjkAndUnsafeSourceValues()
    {
        var targetText =
            "90001=\u6d4b\u8bd5\r\n" +
            "90002=\u6d4b\u8bd5\r\n" +
            "90003=\u6d4b\u8bd5\r\n";
        var sourceText =
            "90001=\u4ecd\u7136\u662f\u4e2d\u6587\r\n" +
            "90002=Bad \ufffd source\r\n" +
            "90003=Clean Source\r\n";

        var result = SourceBackedStrResTranslator.Translate(
            targetText,
            [sourceText],
            out var updatedText,
            catalogReplacements: null);

        Assert.True(result.Changed);
        Assert.Equal(1, result.SourceRows);
        Assert.Equal(1, result.SourceKeys);
        Assert.Equal(3, result.TargetRows);
        Assert.Equal(1, result.TranslatedRows);
        Assert.Equal(2, result.UnmatchedRows);
        Assert.Equal(2, result.RemainingCjkRows);
        Assert.Contains("90001=\u6d4b\u8bd5\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("90002=\u6d4b\u8bd5\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("90003=Clean Source\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsPlaceholderAndEscapedNewlineMismatches()
    {
        var targetText =
            "90001=%d\u7ea7\r\n" +
            "90002=\u6807\u9898\\n\u5185\u5bb9\r\n" +
            "90003=%s\u7269\u54c1\r\n";
        var sourceText =
            "90001=Level\r\n" +
            "90002=Title Content\r\n" +
            "90003=%s item\r\n";

        var result = SourceBackedStrResTranslator.Translate(
            targetText,
            [sourceText],
            out var updatedText,
            catalogReplacements: null);

        Assert.True(result.Changed);
        Assert.Equal(3, result.SourceRows);
        Assert.Equal(3, result.TargetRows);
        Assert.Equal(1, result.TranslatedRows);
        Assert.Equal(2, result.PlaceholderMismatchRows);
        Assert.Equal(2, result.RemainingCjkRows);
        Assert.Contains("90001=%d\u7ea7\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("90002=\u6807\u9898\\n\u5185\u5bb9\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("90003=%s item\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsLiteralPercentFormatTokenMismatches()
    {
        var targetText =
            "90001=\u9b54\u9632:+%d%%\r\n" +
            "90002=\u7ecf\u9a8c:%0.3f%%\r\n";
        var sourceText =
            "90001=M-defense:+%d\r\n" +
            "90002=Exp:%0.3f%%\r\n";

        var result = SourceBackedStrResTranslator.Translate(
            targetText,
            [sourceText],
            out var updatedText,
            catalogReplacements: null);

        Assert.True(result.Changed);
        Assert.Equal(2, result.SourceRows);
        Assert.Equal(2, result.TargetRows);
        Assert.Equal(1, result.TranslatedRows);
        Assert.Equal(1, result.PlaceholderMismatchRows);
        Assert.Equal(1, result.RemainingCjkRows);
        Assert.Contains("90001=\u9b54\u9632:+%d%%\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("90002=Exp:%0.3f%%\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsConflictingSourceValues()
    {
        var targetText =
            "90001=\u6d4b\u8bd5\r\n" +
            "90002=\u6d4b\u8bd5\r\n";
        var firstSourceText =
            "90001=First\r\n" +
            "90002=Same\r\n";
        var secondSourceText =
            "90001=Second\r\n" +
            "90002=Same\r\n";

        var result = SourceBackedStrResTranslator.Translate(
            targetText,
            [firstSourceText, secondSourceText],
            out var updatedText,
            catalogReplacements: null);

        Assert.True(result.Changed);
        Assert.Equal(4, result.SourceRows);
        Assert.Equal(1, result.SourceKeys);
        Assert.Equal(1, result.ConflictedSourceKeys);
        Assert.Equal(1, result.TranslatedRows);
        Assert.Equal(1, result.UnmatchedRows);
        Assert.Contains("90001=\u6d4b\u8bd5\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("90002=Same\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void PatchFileIsIdempotentAndPreservesCodePage936()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wolfconquer-strres-{Guid.NewGuid():N}");
        var targetPath = Path.Combine(directory, "StrRes.ini");
        var sourcePath = Path.Combine(directory, "reference-StrRes.ini");
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(
                targetPath,
                encoding.GetBytes(
                    "90001=\u6d4b\u8bd5\r\n" +
                    ";90002=\u6ce8\u91ca\r\n" +
                    "90003=Already English\r\n"));
            File.WriteAllBytes(sourcePath, encoding.GetBytes("90001=Translated\r\n"));

            var result = SourceBackedStrResTranslator.PatchFile(
                targetPath,
                [sourcePath],
                includeTargetAsSource: false,
                catalogReplacements: null);

            var patchedBytes = File.ReadAllBytes(targetPath);
            var patchedText = encoding.GetString(patchedBytes);
            Assert.True(result.Changed);
            Assert.Equal(1, result.ChangedRows);
            Assert.Equal(
                "90001=Translated\r\n" +
                ";90002=\u6ce8\u91ca\r\n" +
                "90003=Already English\r\n",
                patchedText);

            var reapplied = SourceBackedStrResTranslator.PatchFile(
                targetPath,
                [sourcePath],
                includeTargetAsSource: false,
                catalogReplacements: null);

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
    public void PatchFileIgnoresBlockedWrongGameSourcePaths()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wolfconquer-strres-fence-{Guid.NewGuid():N}");
        var targetPath = Path.Combine(directory, "StrRes.ini");
        var wrongGameDirectory = Path.Combine(directory, "Destroyer-Online", "ini");
        var wrongGameSourcePath = Path.Combine(wrongGameDirectory, "StrRes.ini");
        var conquerSourcePath = Path.Combine(directory, "ConquerSource", "StrRes.ini");
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);

        try
        {
            Directory.CreateDirectory(wrongGameDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(conquerSourcePath)!);
            File.WriteAllBytes(
                targetPath,
                encoding.GetBytes(
                    "90001=\u9519\u8bef\u6765\u6e90\u4e0d\u5e94\u8be5\u4f7f\u7528\r\n" +
                    "90002=\u6b63\u786e\u6765\u6e90\r\n"));
            File.WriteAllBytes(wrongGameSourcePath, encoding.GetBytes("90001=Wrong Game Text\r\n"));
            File.WriteAllBytes(conquerSourcePath, encoding.GetBytes("90002=Conquer Text\r\n"));

            var result = SourceBackedStrResTranslator.PatchFile(
                targetPath,
                [wrongGameSourcePath, conquerSourcePath],
                includeTargetAsSource: false,
                catalogReplacements: null);

            var patchedText = encoding.GetString(File.ReadAllBytes(targetPath));
            Assert.True(result.Changed);
            Assert.Equal(1, result.SourceRows);
            Assert.Equal(1, result.TranslatedRows);
            Assert.Equal(1, result.UnmatchedRows);
            Assert.Contains("90001=\u9519\u8bef\u6765\u6e90\u4e0d\u5e94\u8be5\u4f7f\u7528\r\n", patchedText, StringComparison.Ordinal);
            Assert.Contains("90002=Conquer Text\r\n", patchedText, StringComparison.Ordinal);
            Assert.DoesNotContain("Wrong Game Text", patchedText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
