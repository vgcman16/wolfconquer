using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class GouYuSkillTextSourceBackedTranslatorTests
{
    [Fact]
    public void TranslatesOnlySameIdSkillNamesFromConflictFreeSources()
    {
        var gouYuText =
            "tGouYuItemTipText[20200][\"Text1\"] = \"<color=0xffffa800>\u539a\u571f\u5b88\u62a4</color>\"\r\n" +
            "tGouYuItemTipText[20200][\"Text4\"] = \"\u53d7\u5230\u4f24\u5bb3\u5fc5\u5b9a\u89e6\u53d1\u201c\u539a\u571f\u5b88\u62a4\u201d\u72b6\u6001\"\r\n" +
            "tGouYuItemTipText[20210][\"Text1\"] = \"<color=0xffffa800>\u67d4\u6c34\u8bc0</color>\"\r\n" +
            "tGouYuItemTipText[20210][\"Text4\"] = \"\u539a\u571f\u5b88\u62a4 should not be touched in another row\"\r\n";
        var magicEffectText =
            "[2020000]\r\n" +
            "Name=SolidBulwark\r\n" +
            "[2021000]\r\n" +
            "Name=FlowKnack\r\n";
        var magicTypeText =
            "20200@@20200@@6@@SolidBulwark@@1@@\r\n" +
            "20210@@20210@@6@@FlowKnack@@1@@\r\n";

        var result = GouYuSkillTextSourceBackedTranslator.Translate(
            gouYuText,
            [magicEffectText],
            [magicTypeText],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(4, result.TextRowsScanned);
        Assert.Equal(2, result.UniqueTextIds);
        Assert.Equal(2, result.SourceSkillNames);
        Assert.Equal(2, result.MatchedTextIds);
        Assert.Equal(3, result.TranslatedRows);
        Assert.Equal(3, result.ChangedRows);
        Assert.Empty(result.ConflictingSourceIds);
        Assert.Empty(result.MissingSourceIds);
        Assert.Contains("tGouYuItemTipText[20200][\"Text1\"] = \"<color=0xffffa800>SolidBulwark</color>\"", updatedText);
        Assert.Contains("\u53d7\u5230\u4f24\u5bb3\u5fc5\u5b9a\u89e6\u53d1\u201cSolidBulwark\u201d\u72b6\u6001", updatedText);
        Assert.Contains("tGouYuItemTipText[20210][\"Text1\"] = \"<color=0xffffa800>FlowKnack</color>\"", updatedText);
        Assert.Contains("\u539a\u571f\u5b88\u62a4 should not be touched in another row", updatedText);
    }

    [Fact]
    public void UsesGouYuTypeMagicIdsForLiveShortIdNamesAndSkillPhrases()
    {
        var gouYuText =
            "tGouYuItemTipText[100][\"Text1\"] = \"<color=0xffffa800>\u8c6a\u706b\u6cd5\u5370</color>\"\r\n" +
            "tGouYuItemTipText[100][\"Text2\"] = \"<color=0xfffbec8a>\u7c7b\u578b\uff1a\u706b\u9041\u6cd5\u5370</color>\"\r\n" +
            "tGouYuItemTipText[100][\"Text3\"] = \"<color=0xfffbec8a>\u7b49\u7ea7\uff1a</color><color=0xffffa900>%u\u7ea7</color>\"\r\n" +
            "tGouYuItemTipText[100][\"Text5\"] = \"<color=0xffcec1a8>learn </color><color=0xfffa800>\u8c6a\u706b\u7403\u4e4b\u672f</color>\"\r\n" +
            "tGouYuItemTipText[100][\"Text6\"] = \"<color=0xffcec1a8>\u53d7\u5230\u8c6a\u706b\u7403\u4e4b\u672f\u653b\u51fb</color><color=0xff54b052>%u%%</color>\"\r\n" +
            "tGouYuItemTipText[101][\"Text1\"] = \"<color=0xffffa800>\u8c6a\u706b\u6cd5\u5370\u00b7\u711a\u5929</color>\"\r\n" +
            "tGouYuItemTipText[101][\"Text6\"] = \"<color=0xffcec1a8>\u53d7\u5230\u8c6a\u706b\u7403\u4e4b\u672f\u653b\u51fb</color><color=0xff54b052>%u%%</color>\"\r\n";
        var magicTypeText =
            "16520@@16520@@90@@WildFireball@@1@@\r\n";
        var gouYuTypeText =
            "1@@100@@1@@50@@10537@@0@@16520@@16530@@0@@0@@0@@100@@10@@\r\n";

        var result = GouYuSkillTextSourceBackedTranslator.Translate(
            gouYuText,
            [],
            [magicTypeText],
            [gouYuTypeText],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(1, result.MatchedTextIds);
        Assert.Equal([101], result.MissingSourceIds);
        Assert.Contains("tGouYuItemTipText[100][\"Text1\"] = \"<color=0xffffa800>WildFireball</color>\"", updatedText);
        Assert.Contains("tGouYuItemTipText[100][\"Text2\"] = \"<color=0xfffbec8a>Type: Fire Release Seal</color>\"", updatedText);
        Assert.Contains("tGouYuItemTipText[100][\"Text3\"] = \"<color=0xfffbec8a>Level: </color><color=0xffffa900>%u</color>\"", updatedText);
        Assert.Contains("<color=0xffcec1a8>learn </color><color=0xfffa800>WildFireball</color>", updatedText);
        Assert.Contains("<color=0xffcec1a8>\u53d7\u5230WildFireball\u653b\u51fb</color><color=0xff54b052>%u%%</color>", updatedText);
        Assert.Contains("tGouYuItemTipText[101][\"Text1\"] = \"<color=0xffffa800>\u8c6a\u706b\u6cd5\u5370\u00b7\u711a\u5929</color>\"", updatedText);
    }

    [Fact]
    public void TranslatesText6ExperienceLabel()
    {
        var gouYuText =
            "tGouYuItemTipText[100][\"Text6\"] = \"\u7ecf\u9a8c\uff1a%.3f%%\"\r\n";

        var result = GouYuSkillTextSourceBackedTranslator.Translate(
            gouYuText,
            [],
            [],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(1, result.TextRowsScanned);
        Assert.Equal(1, result.TranslatedRows);
        Assert.Equal(1, result.ChangedRows);
        Assert.Equal(0, result.RemainingCjkRows);
        Assert.Equal("tGouYuItemTipText[100][\"Text6\"] = \"EXP: %.3f%%\"\r\n", updatedText);
    }

    [Fact]
    public void TranslatesIndexedText3ExperienceLabelWithoutTranslatingProse()
    {
        var gouYuText =
            "tGouYuItemTipText[9000][\"Text3\"] = \"\u7ecf\u9a8c\uff1a%.3f%%\"\r\n" +
            "tGouYuItemTipText[9001][\"Text3\"] = \"<color=0xfff4e372>\u6280\u80fdcd\uff1a3\u79d2</color>\"\r\n" +
            "tGouYuItemTipText[9002][\"Text3\"] = \"<color=0xffcec1a8>\u6280\u80fdcd\uff1a3\u79d2\u53ea\u662f\u8bf4\u660e\uff0c\u4e0d\u5e94\u8be5\u88ab\u5f53\u4f5c\u6807\u7b7e\u7ffb\u8bd1\u3002</color>\"\r\n" +
            "tGouYuItemTipText[9003][\"Text3\"] = \"<color=0xffcec1a8>\u88ab\u52a8\u6280\u80fd\uff0c\u7b49\u7ea7\uff1a0\u7ea7\u53ea\u4fdd\u7559\u6545\u4e8b\u6027\u63cf\u8ff0\u3002</color>\"\r\n";

        var result = GouYuSkillTextSourceBackedTranslator.Translate(
            gouYuText,
            [],
            [],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(2, result.TranslatedRows);
        Assert.Equal(2, result.ChangedRows);
        Assert.Contains("tGouYuItemTipText[9000][\"Text3\"] = \"EXP: %.3f%%\"", updatedText);
        Assert.Contains("tGouYuItemTipText[9001][\"Text3\"] = \"<color=0xfff4e372>Cooldown: 3 sec</color>\"", updatedText);
        Assert.Contains("tGouYuItemTipText[9002][\"Text3\"] = \"<color=0xffcec1a8>\u6280\u80fdcd\uff1a3\u79d2\u53ea\u662f\u8bf4\u660e\uff0c\u4e0d\u5e94\u8be5\u88ab\u5f53\u4f5c\u6807\u7b7e\u7ffb\u8bd1\u3002</color>\"", updatedText);
        Assert.Contains("tGouYuItemTipText[9003][\"Text3\"] = \"<color=0xffcec1a8>\u88ab\u52a8\u6280\u80fd\uff0c\u7b49\u7ea7\uff1a0\u7ea7\u53ea\u4fdd\u7559\u6545\u4e8b\u6027\u63cf\u8ff0\u3002</color>\"", updatedText);
    }

    [Fact]
    public void TranslatesText2NumberLevelLabel()
    {
        var gouYuText =
            "tGouYuItemTipText[100][\"Text2\"] = \"<color=0xfffbec8a>\u7b49\u7ea7\uff1a0\u7ea7</color>\"\n";

        var result = GouYuSkillTextSourceBackedTranslator.Translate(
            gouYuText,
            [],
            [],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(1, result.TranslatedRows);
        Assert.Equal(1, result.ChangedRows);
        Assert.Equal(0, result.RemainingCjkRows);
        Assert.Equal("tGouYuItemTipText[100][\"Text2\"] = \"<color=0xfffbec8a>Level: 0</color>\"\n", updatedText);
    }

    [Fact]
    public void TranslatesLiveLuaTableLabelVariantsAndIsIdempotent()
    {
        var gouYuText =
            "Text2=\"\u7b49\u7ea7\uff1a10\u7ea7\",\r\n" +
            "Text3=  \"\u7ecf\u9a8c\uff1a%.3f%%\",\r\n" +
            "Text3=\"<color=0xffcec1a8>\u957f\u63cf\u8ff0\u7b49\u7ea7\uff1a0\u7ea7\u4e0d\u5e94\u8be5\u88ab\u5f53\u4f5c\u6807\u7b7e\u7ffb\u8bd1\u3002</color>\",\r\n" +
            "Text4=\"<color=0xffffa800>\u89e3\u9501\u6761\u4ef6\uff1a\u5f00\u542f\u53cc\u67aa\u6218\u88c5</color>\",\r\n" +
            "Text6=\"\u7ecf\u9a8c\uff1a%.3f%%\",\r\n" +
            "Text6=\"\u4e0d\u53ef\u5347\u7ea7\",\r\n" +
            "\ttext = \"\u706b\u9041\",    -- \u672c\u884c\u540d\u79f0\r\n";

        var first = GouYuSkillTextSourceBackedTranslator.Translate(
            gouYuText,
            [],
            [],
            out var firstText);
        var second = GouYuSkillTextSourceBackedTranslator.Translate(
            firstText,
            [],
            [],
            out var secondText);

        Assert.True(first.Changed);
        Assert.Equal(6, first.TranslatedRows);
        Assert.Equal(6, first.ChangedRows);
        Assert.Contains("Text2=\"Level: 10\",", firstText);
        Assert.Contains("Text3=  \"EXP: %.3f%%\",", firstText);
        Assert.Contains("Text3=\"<color=0xffcec1a8>\u957f\u63cf\u8ff0\u7b49\u7ea7\uff1a0\u7ea7\u4e0d\u5e94\u8be5\u88ab\u5f53\u4f5c\u6807\u7b7e\u7ffb\u8bd1\u3002</color>\",", firstText);
        Assert.Contains("Text4=\"<color=0xffffa800>Unlock requirement: activate Dual-Gun Combat Gear</color>\",", firstText);
        Assert.Contains("Text6=\"EXP: %.3f%%\",", firstText);
        Assert.Contains("Text6=\"Unupgradable\",", firstText);
        Assert.Contains("\ttext = \"Fire Release\",    -- \u672c\u884c\u540d\u79f0", firstText);
        Assert.False(second.Changed);
        Assert.Equal(firstText, secondText);
    }

    [Fact]
    public void LeavesUnsupportedProseUnchangedWhenSourceBlocked()
    {
        var gouYuText =
            "tGouYuItemTipText[900][\"Text1\"] = \"<color=0xffffa800>\u672a\u77e5\u672f</color>\"\r\n" +
            "tGouYuItemTipText[900][\"Text6\"] = \"\u7ecf\u9a8c\uff1a%.3f%%\u53ea\u662f\u8bf4\u660e\uff0c\u7b49\u7ea7\uff1a0\u7ea7\u4e5f\u53ea\u662f\u8bf4\u660e\"\r\n" +
            "Text6=\"\u4e0d\u53ef\u5347\u7ea7\u7684\u957f\u8bf4\u660e\",\r\n";

        var result = GouYuSkillTextSourceBackedTranslator.Translate(
            gouYuText,
            [],
            [],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(gouYuText, updatedText);
        Assert.Equal([900], result.MissingSourceIds);
        Assert.Equal(0, result.TranslatedRows);
    }

    [Fact]
    public void UsesSecondaryGouYuTypeMagicIdWhenPrimaryIsEmpty()
    {
        var gouYuText =
            "tGouYuItemTipText[600][\"Text1\"] = \"<color=0xffffa800>\u5f0f\u7eb8\u4e4b\u821e</color>\"\r\n" +
            "tGouYuItemTipText[600][\"Text2\"] = \"<color=0xfffbec8a>\u7c7b\u578b\uff1a\u8840\u7ee7\u6cd5\u5370</color>\"\r\n";
        var magicTypeText =
            "16360@@16360@@0@@PaperDance@@0@@\r\n";
        var gouYuTypeText =
            "271@@600@@1@@50@@37008@@0@@0@@16360@@0@@0@@0@@100@@10@@\r\n";

        var result = GouYuSkillTextSourceBackedTranslator.Translate(
            gouYuText,
            [],
            [magicTypeText],
            [gouYuTypeText],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(1, result.MatchedTextIds);
        Assert.Empty(result.MissingSourceIds);
        Assert.Contains("tGouYuItemTipText[600][\"Text1\"] = \"<color=0xffffa800>PaperDance</color>\"", updatedText);
        Assert.Contains("tGouYuItemTipText[600][\"Text2\"] = \"<color=0xfffbec8a>Type: Bloodline Seal</color>\"", updatedText);
    }

    [Fact]
    public void LeavesRowsUnchangedWhenLocalSourcesConflict()
    {
        var gouYuText =
            "tGouYuItemTipText[8001][\"Text1\"] = \"<color=0xffffa800>\u6563\u5c04</color>\"\n";
        var magicEffectText =
            "[800100]\n" +
            "Name=Scatter\n";
        var magicTypeText =
            "8001@@8001@@1@@ScatterFire@@1@@\n";

        var result = GouYuSkillTextSourceBackedTranslator.Translate(
            gouYuText,
            [magicEffectText],
            [magicTypeText],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(gouYuText, updatedText);
        Assert.Equal([8001], result.ConflictingSourceIds);
        Assert.Equal([8001], result.MissingSourceIds);
        Assert.Equal(0, result.TranslatedRows);
    }

    [Fact]
    public void SkipsMissingOrChineseSourceRows()
    {
        var gouYuText =
            "tGouYuItemTipText[22790][\"Text1\"] = \"<color=0xffffa800>\u9e4f\u7a0b\u4e07\u91cc</color>\"\r\n" +
            "tGouYuItemTipText[99999][\"Text1\"] = \"<color=0xffffa800>\u672a\u77e5</color>\"\r\n";
        var magicEffectText =
            "[2279000]\r\n" +
            "Name=\u9e4f\u7a0b\u4e07\u91cc\r\n";
        var magicTypeText =
            "22790@@22790@@1@@\u9e4f\u7a0b\u4e07\u91cc@@1@@\r\n";

        var result = GouYuSkillTextSourceBackedTranslator.Translate(
            gouYuText,
            [magicEffectText],
            [magicTypeText],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(gouYuText, updatedText);
        Assert.Empty(result.ConflictingSourceIds);
        Assert.Equal([22790, 99999], result.MissingSourceIds);
        Assert.Equal(0, result.SourceSkillNames);
    }

    [Fact]
    public void ReapplyingTranslatedTextIsIdempotent()
    {
        var gouYuText =
            "tGouYuItemTipText[20200][\"Text1\"] = \"<color=0xffffa800>\u539a\u571f\u5b88\u62a4</color>\"\r\n";
        var magicEffectText =
            "[2020000]\r\n" +
            "Name=SolidBulwark\r\n";
        var magicTypeText =
            "20200@@20200@@6@@SolidBulwark@@1@@\r\n";

        var first = GouYuSkillTextSourceBackedTranslator.Translate(
            gouYuText,
            [magicEffectText],
            [magicTypeText],
            out var firstText);
        var second = GouYuSkillTextSourceBackedTranslator.Translate(
            firstText,
            [magicEffectText],
            [magicTypeText],
            out var secondText);

        Assert.True(first.Changed);
        Assert.False(second.Changed);
        Assert.Equal(firstText, secondText);
        Assert.Equal(0, second.TranslatedRows);
    }

    [Fact]
    public void PatchFilePreservesCodePage936Encoding()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-gouyu-translator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var gouYuPath = Path.Combine(root, "GouYu.lua");
            var magicEffectPath = Path.Combine(root, "MagicEffect.ini");
            var magicTypePath = Path.Combine(root, "magictype.txt");
            var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
            File.WriteAllBytes(
                gouYuPath,
                encoding.GetBytes("tGouYuItemTipText[20200][\"Text1\"] = \"<color=0xffffa800>\u539a\u571f\u5b88\u62a4</color>\"\r\n"));
            File.WriteAllBytes(
                magicEffectPath,
                encoding.GetBytes("[2020000]\r\nName=SolidBulwark\r\n"));
            File.WriteAllBytes(
                magicTypePath,
                encoding.GetBytes("20200@@20200@@6@@SolidBulwark@@1@@\r\n"));

            var result = GouYuSkillTextSourceBackedTranslator.PatchFile(
                gouYuPath,
                [magicEffectPath],
                [magicTypePath]);
            var updatedText = encoding.GetString(File.ReadAllBytes(gouYuPath));

            Assert.True(result.Changed);
            Assert.Contains("<color=0xffffa800>SolidBulwark</color>", updatedText);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
