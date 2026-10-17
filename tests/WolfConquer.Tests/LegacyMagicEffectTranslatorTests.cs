using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class LegacyMagicEffectTranslatorTests
{
    [Fact]
    public void TranslatesVisibleTextRowsWithoutChangingOtherRows()
    {
        var clientText =
            "// live comment\r\n" +
            "[100]\r\n" +
            "Name=\u96f7\u9706\r\n" +
            "Status=145\r\n" +
            "Desc=\u4e2d\u6587\u63cf\u8ff0\r\n" +
            "DescEx=\u4e2d\u6587\u6269\u5c55\r\n" +
            "\r\n" +
            "[200]\n" +
            "\tName = \u706b\u7130\r\n" +
            "Speed=7\r\n" +
            "Desc=\u71c3\u70e7\r\n" +
            "[999]\r\n" +
            "Name=\u672a\u77e5\r\n" +
            "Status=1";
        var referenceText =
            "[100]\r\n" +
            "Name=Thunder\r\n" +
            "Status=999\r\n" +
            "Desc=Upgradable\r\n" +
            "DescEx=Magic~attack\r\n" +
            "[200]\r\n" +
            "Name=Fire\r\n" +
            "Desc=Burning\r\n";

        var result = LegacyMagicEffectTranslator.Translate(clientText, referenceText, out var translatedText);

        Assert.True(result.Changed);
        Assert.Equal(3, result.SectionsScanned);
        Assert.Equal(2, result.MatchedSections);
        Assert.Equal(1, result.UnmatchedSections);
        Assert.Equal(5, result.TranslatedRows);
        Assert.Equal(5, result.ChangedRows);
        Assert.Equal(1, result.UnmatchedRows);
        Assert.Equal(
            "// live comment\r\n" +
            "[100]\r\n" +
            "Name=Thunder\r\n" +
            "Status=145\r\n" +
            "Desc=Upgradable\r\n" +
            "DescEx=Magic~attack\r\n" +
            "\r\n" +
            "[200]\n" +
            "\tName = Fire\r\n" +
            "Speed=7\r\n" +
            "Desc=Burning\r\n" +
            "[999]\r\n" +
            "Name=\u672a\u77e5\r\n" +
            "Status=1",
            translatedText);
    }

    [Fact]
    public void ReapplyingTranslatedMagicEffectTextIsIdempotent()
    {
        var clientText =
            "[100]\r\n" +
            "Name=\u96f7\u9706\r\n" +
            "Desc=\u4e2d\u6587\u63cf\u8ff0\r\n" +
            "DescEx=\u4e2d\u6587\u6269\u5c55\r\n";
        var referenceText =
            "[100]\r\n" +
            "Name=Thunder\r\n" +
            "Desc=Upgradable\r\n" +
            "DescEx=Magic~attack\r\n";

        var firstResult = LegacyMagicEffectTranslator.Translate(clientText, referenceText, out var firstText);
        var secondResult = LegacyMagicEffectTranslator.Translate(firstText, referenceText, out var secondText);

        Assert.True(firstResult.Changed);
        Assert.False(secondResult.Changed);
        Assert.Equal(3, secondResult.TranslatedRows);
        Assert.Equal(0, secondResult.ChangedRows);
        Assert.Equal(firstText, secondText);
    }

    [Fact]
    public void MatchesDuplicateNumericSectionsByOccurrence()
    {
        var clientText =
            "[500]\r\n" +
            "Name=\u7b2c\u4e00\u4e2a\r\n" +
            "[500]\r\n" +
            "Name=\u7b2c\u4e8c\u4e2a\r\n";
        var referenceText =
            "[500]\r\n" +
            "Name=First Spell\r\n" +
            "[500]\r\n" +
            "Name=Second Spell\r\n";

        var result = LegacyMagicEffectTranslator.Translate(clientText, referenceText, out var translatedText);

        Assert.Equal(2, result.SectionsScanned);
        Assert.Equal(2, result.MatchedSections);
        Assert.Equal(2, result.TranslatedRows);
        Assert.Equal(
            "[500]\r\n" +
            "Name=First Spell\r\n" +
            "[500]\r\n" +
            "Name=Second Spell\r\n",
            translatedText);
    }

    [Fact]
    public void SkipsReferenceValuesThatWouldIntroduceMojibakeOrUnsupportedCharacters()
    {
        var clientText =
            "[10]\r\n" +
            "Name=\u96f7\u9706\r\n" +
            "Desc=\u4e2d\u6587\u63cf\u8ff0\r\n";
        var referenceText =
            "[10]\r\n" +
            "Name=Thunder\ud83d\udd25\r\n" +
            "Desc=Good\ufffdText\r\n";

        var result = LegacyMagicEffectTranslator.Translate(clientText, referenceText, out var translatedText);

        Assert.False(result.Changed);
        Assert.Equal(1, result.MatchedSections);
        Assert.Equal(0, result.TranslatedRows);
        Assert.Equal(2, result.UnmatchedRows);
        Assert.Equal(clientText, translatedText);
    }
}
