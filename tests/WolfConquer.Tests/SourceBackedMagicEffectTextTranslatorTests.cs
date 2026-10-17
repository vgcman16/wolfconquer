using System.Text.RegularExpressions;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class SourceBackedMagicEffectTextTranslatorTests
{
    [Fact]
    public void TranslatesVisibleRowsFromSameNumericSectionAndKey()
    {
        var targetText =
            "[100]\r\n" +
            "Name=\\u96f7\\u9706\r\n" +
            "Name1=\\u5929\\u96f7\r\n" +
            "Desc=\\u9020\\u6210%d%%\\u4f24\\u5bb3\r\n" +
            "DescEx=\\u6d88\\u8017%d\\u70b9\\u6cd5\\u529b\r\n" +
            "Status=145\r\n" +
            "\r\n" +
            "[101]\r\n" +
            "Name=\\u706b\\u7130\r\n";
        targetText = DecodeEscapes(targetText);
        var sourceText =
            "[100]\r\n" +
            "Name=Thunder\r\n" +
            "Name1=Storm Call\r\n" +
            "Desc=Deals %d%% damage\r\n" +
            "DescEx=Costs %d mana\r\n" +
            "Status=999\r\n" +
            "\r\n" +
            "[101]\r\n" +
            "Name=\\u706b\\u7130\r\n";
        sourceText = DecodeEscapes(sourceText);

        var sources = SourceBackedMagicEffectTextTranslator.ReadMagicEffectTextSourcesFromText(
            sourceText,
            "DBServer/MagicEffect.ini");
        var result = SourceBackedMagicEffectTextTranslator.Translate(
            targetText,
            sources,
            [],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(4, result.SourceRows);
        Assert.Equal(4, result.SourceKeys);
        Assert.Equal(5, result.TargetRows);
        Assert.Equal(5, result.RowsNeedingTranslation);
        Assert.Equal(4, result.SameKeyMatchedRows);
        Assert.Equal(0, result.SkillNameFallbackRows);
        Assert.Equal(1, result.UnmatchedRows);
        Assert.Equal(4, result.ChangedRows);
        Assert.Equal(1, result.RemainingCjkRows);
        Assert.Empty(result.ConflictingSourceKeys);
        Assert.Contains("Name=Thunder\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Name1=Storm Call\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Desc=Deals %d%% damage\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("DescEx=Costs %d mana\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("[101]\r\nName=" + DecodeEscapes("\\u706b\\u7130") + "\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void UsesMagicTypeNameFallbackForNameLikeRows()
    {
        var targetText =
            "[21190]\n" +
            "Name=\\u4e0a\\u5584\\u82e5\\u6c34\n" +
            "Name2=\\u8f6c\\u4e16\\u6280\\u80fd\n" +
            "Desc=\\u4e0a\\u5584\\u82e5\\u6c34\\u63cf\\u8ff0\n";
        targetText = DecodeEscapes(targetText);
        var magicTypeText =
            "0@@21190@@6@@WaterAegis(Rebirth)@@0@@\n" +
            "1@@21190@@6@@WaterAegis(Rebirth)@@0@@\n";
        var skillSources = SourceBackedMagicEffectTextTranslator.ReadMagicTypeSkillNameSourcesFromText(
            magicTypeText,
            "magictype.txt");

        var result = SourceBackedMagicEffectTextTranslator.Translate(
            targetText,
            [],
            skillSources,
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(2, result.SkillNameSourceRows);
        Assert.Equal(1, result.SkillNameSourceIds);
        Assert.Equal(3, result.TargetRows);
        Assert.Equal(2, result.SkillNameFallbackRows);
        Assert.Equal(1, result.UnmatchedRows);
        Assert.Equal(2, result.ChangedRows);
        Assert.Contains("Name=WaterAegis(Rebirth)\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Name2=WaterAegis(Rebirth)\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Desc=" + DecodeEscapes("\\u4e0a\\u5584\\u82e5\\u6c34\\u63cf\\u8ff0") + "\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsDescWhenPlaceholderTokensWouldChange()
    {
        var targetText =
            "[100]\n" +
            "Desc=\\u9020\\u6210%d%%\\u4f24\\u5bb3\n" +
            "DescEx=\\u6d88\\u8017{0}\\u70b9\\u6cd5\\u529b\n";
        targetText = DecodeEscapes(targetText);
        var sources = new[]
        {
            new SourceBackedMagicEffectTextSource(100, "Desc", "Deals heavy damage", "DBServer/MagicEffect.ini"),
            new SourceBackedMagicEffectTextSource(100, "DescEx", "Costs {1} mana", "DBServer/MagicEffect.ini")
        };

        var result = SourceBackedMagicEffectTextTranslator.Translate(
            targetText,
            sources,
            [],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(targetText, updatedText);
        Assert.Equal(2, result.PlaceholderMismatchRows);
        Assert.Equal(2, result.RemainingCjkRows);
        Assert.Equal(0, result.ChangedRows);
    }

    [Fact]
    public void SkipsRowsWhenMultipleSourcesDisagree()
    {
        var targetText =
            "[100]\r\n" +
            "Name=\\u96f7\\u9706\r\n" +
            "[200]\r\n" +
            "Name=\\u706b\\u7130\r\n";
        targetText = DecodeEscapes(targetText);
        var sources = new[]
        {
            new SourceBackedMagicEffectTextSource(100, "Name", "Thunder", "MagicEffect.a.ini"),
            new SourceBackedMagicEffectTextSource(100, "Name", "Lightning", "MagicEffect.b.ini"),
            new SourceBackedMagicEffectTextSource(200, "Name", "Fire", "MagicEffect.a.ini")
        };
        var skillSources = new[]
        {
            new SourceBackedMagicEffectSkillNameSource(200, "Flame", "magictype.txt")
        };

        var result = SourceBackedMagicEffectTextTranslator.Translate(
            targetText,
            sources,
            skillSources,
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(targetText, updatedText);
        Assert.Equal(["100:NAME"], result.ConflictingSourceKeys);
        Assert.Equal(2, result.ConflictRows);
        Assert.Equal(0, result.UnmatchedRows);
        Assert.Equal(2, result.RemainingCjkRows);
    }

    [Fact]
    public void PatchFilePreservesCodePage936EncodingAndIsIdempotent()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-magic-effect-translator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
            var magicEffectPath = Path.Combine(root, "MagicEffect.ini");
            var referencePath = Path.Combine(root, "ReferenceMagicEffect.ini");
            var magicTypePath = Path.Combine(root, "magictype.txt");
            File.WriteAllBytes(
                magicEffectPath,
                encoding.GetBytes(DecodeEscapes(
                    "[100]\r\n" +
                    "Name=\\u96f7\\u9706\r\n" +
                    "Desc=\\u9020\\u6210%d%%\\u4f24\\u5bb3\r\n" +
                    "[21190]\r\n" +
                    "Name=\\u4e0a\\u5584\\u82e5\\u6c34\r\n")));
            File.WriteAllBytes(
                referencePath,
                encoding.GetBytes(
                    "[100]\r\n" +
                    "Name=Thunder\r\n" +
                    "Desc=Deals %d%% damage\r\n"));
            File.WriteAllBytes(
                magicTypePath,
                encoding.GetBytes("0@@21190@@6@@WaterAegis(Rebirth)@@0@@\r\n"));

            var firstResult = SourceBackedMagicEffectTextTranslator.PatchFile(
                magicEffectPath,
                referencePath,
                [magicTypePath]);
            var firstBytes = File.ReadAllBytes(magicEffectPath);
            var firstText = encoding.GetString(firstBytes);
            var secondResult = SourceBackedMagicEffectTextTranslator.PatchFile(
                magicEffectPath,
                referencePath,
                [magicTypePath]);

            Assert.True(firstResult.Changed);
            Assert.False(secondResult.Changed);
            Assert.Equal(firstBytes, File.ReadAllBytes(magicEffectPath));
            Assert.Contains("Name=Thunder\r\n", firstText, StringComparison.Ordinal);
            Assert.Contains("Desc=Deals %d%% damage\r\n", firstText, StringComparison.Ordinal);
            Assert.Contains("Name=WaterAegis(Rebirth)\r\n", firstText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string DecodeEscapes(string value)
    {
        return Regex.Unescape(value);
    }
}
