using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoAttributeCatalogTests
{
    [Theory]
    [InlineData("[10] = { [1] = { ScoreLimit0=1, ScoreLimit0=2, }, },")]
    [InlineData("[10] = { [1] = { ScoreLimit0=1, }, [1] = { ScoreLimit1=2, }, },")]
    [InlineData("[10] = { [1] = { ScoreLimit0=1, }, }, [10] = { [2] = { ScoreLimit1=2, }, },")]
    public void DuplicateScoreKeysRefuse(string rows) => Refuses(
        Chinese7937XuanbaoAttributeFixture.Module(scoreRows: rows), Chinese7937XuanbaoInfoFailure.DuplicateField);

    [Theory]
    [InlineData("[10] = { [1] = true, [1] = false, },")]
    [InlineData("[10] = { [1] = true, }, [10] = { [2] = true, },")]
    public void DuplicateGoldKeysRefuse(string rows) => Refuses(
        Chinese7937XuanbaoAttributeFixture.Module(goldRows: rows), Chinese7937XuanbaoInfoFailure.DuplicateField);

    [Theory]
    [InlineData("[10] = { [1] = { ScoreLimit0=-1, }, },")]
    [InlineData("[10] = { [1] = { ScoreLimit0=4294967296, }, },")]
    [InlineData("[10] = { [1] = { ScoreLimit0=01, }, },")]
    [InlineData("[10] = { [1] = { ScoreLimit0=0x10, }, },")]
    [InlineData("[10] = { [1] = { ScoreLimit0=1+2, }, },")]
    [InlineData("[10] = { [1] = { ScoreLimit9=1, }, },")]
    [InlineData("[10] = { [1] = { scoreLimit0=1, }, },")]
    [InlineData("[10] = { [1] = { [\"ScoreLimit0\"]=1, }, },")]
    [InlineData("[10] = { [1] = { ScoreLimit0=1 }, },")]
    [InlineData("[10] = { [1] = { }, },")]
    [InlineData("[10] = { },")]
    [InlineData("")]
    [InlineData("[4294967296] = { [1] = { ScoreLimit0=1, }, },")]
    [InlineData("[10] = { [4294967296] = { ScoreLimit0=1, }, },")]
    [InlineData("[\"10\"] = { [1] = { ScoreLimit0=1, }, },")]
    public void UnsupportedScoreSyntaxDoesNotBecomeAValueOrPartialRow(string rows) =>
        Refuses(Chinese7937XuanbaoAttributeFixture.Module(scoreRows: rows));

    [Theory]
    [InlineData("[10] = { [1] = 1, },")]
    [InlineData("[10] = { [1] = nil, },")]
    [InlineData("[10] = { [1] = callback(), },")]
    [InlineData("[10] = { [1] = true or false, },")]
    [InlineData("[10] = { [1] = true },")]
    [InlineData("[10] = { },")]
    [InlineData("")]
    public void UnsupportedGoldSyntaxDoesNotBecomeAnAcceptingFlag(string rows) =>
        Refuses(Chinese7937XuanbaoAttributeFixture.Module(goldRows: rows));

    [Fact]
    public void EveryGetterExportAliasAndResetMustKeepItsDirectRelationship()
    {
        var module = Chinese7937XuanbaoAttributeFixture.Module();
        foreach (var pair in new[]
        {
            ("local t1 = XuanBaoScoreGradeStep[nItemType]", "local t1 = Other[nItemType]"),
            ("local t3 = t2[nIndex]", "local t3 = t2[other]"),
            ("return t3\nend", "return t3 or 0\nend"),
            ("local res = t[nAttriType]", "local res = t[other]"),
            ("return res\nend", "return true\nend"),
            ("GetXuanBaoScoreGradeStep = GetXuanBaoScoreGradeStep,", "GetXuanBaoScoreGradeStep = Other,"),
            ("GetXuanBaoGoldLimitAttri = GetXuanBaoGoldLimitAttri,", "GetXuanBaoGoldLimitAttri = Other,"),
            ("XuanBao_GetXuanBaoScoreGradeStep = XuanBao.GetXuanBaoScoreGradeStep", ""),
            ("XuanBao_GetXuanBaoGoldLimitAttri = XuanBao.GetXuanBaoGoldLimitAttri", ""),
            ("XuanBaoScoreGradeStep = nil", "Other = nil"),
            ("XuanBaoLimitAttri = nil", "Other = nil"),
        }) Refuses(module.Replace(pair.Item1, pair.Item2, StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateShadowedConditionalAndExtraSelectedWritesCannotBorrowValidRelations()
    {
        var module = Chinese7937XuanbaoAttributeFixture.Module();
        foreach (var suffix in new[]
        {
            "XuanBaoLimitAttri[10][1] = false\n", "XuanBaoScoreGradeStep = nil\n",
            "GetXuanBaoGoldLimitAttri = nil\n", "XuanBao = nil\n",
            "local function Hidden()\nXuanBaoScoreGradeStep = nil\nend\n",
            "XuanBao[\"GetXuanBaoGoldLimitAttri\"] = Other\n", "rawset(XuanBao, key, Other)\n",
            "XuanBao.Other = function() end\n", "local function Hidden()\nUse(XuanBao)\nend\n",
            Chinese7937XuanbaoAttributeFixture.ScoreGetter, Chinese7937XuanbaoAttributeFixture.Aliases,
        }) Refuses(module + suffix);
        Refuses("local function Hidden()\n" + module + "end\n");
        Refuses(module.Replace("XuanBaoLimitAttri = nil", "if true then\nXuanBaoLimitAttri = nil\nend", StringComparison.Ordinal));
        Refuses(module.Replace("local XuanBaoScoreGradeStep =", "local unrelated = { local XuanBaoScoreGradeStep =", StringComparison.Ordinal) + "}\n");
        Refuses(module.Replace("Unload = Unload,", "[\"GetXuanBaoGoldLimitAttri\"] = Other,\nUnload = Unload,", StringComparison.Ordinal));
        Refuses(module.Replace("Unload = Unload,", "GetXuanBaoScoreGradeStep = GetXuanBaoScoreGradeStep,\nUnload = Unload,", StringComparison.Ordinal));
    }

    [Fact]
    public void SelectedDeclarationOrderAndExpressionBoundariesCannotBeExtended()
    {
        var module = Chinese7937XuanbaoAttributeFixture.Module();
        Refuses(Chinese7937XuanbaoAttributeFixture.ScoreGetter + module.Replace(Chinese7937XuanbaoAttributeFixture.ScoreGetter, "", StringComparison.Ordinal));
        foreach (var suffix in new[] { "()", "\n+1", "\nand Other" }) Refuses(module.TrimEnd() + suffix + "\n");
        foreach (var suffix in new[] { "local x='unfinished", "local x=[=[unfinished", "local function unfinished()", "local x={" })
            Refuses(module + suffix);
    }

    [Fact]
    public void StrictDeclaredVersionAndEncodingDoNotRepairUnsupportedInput()
    {
        var bytes = Chinese7937XuanbaoAttributeFixture.Gbk(Chinese7937XuanbaoAttributeFixture.Module());
        Assert.False(Chinese7937DecodedXuanbaoInfo.TryParseAttributeCatalog(bytes,
            Chinese7937XuanbaoInfoVersion.Catalog7931, out var catalog, out var failure));
        Assert.Null(catalog); Assert.Equal(Chinese7937XuanbaoInfoFailure.UnsupportedVersion, failure);
        Refuses([], Chinese7937XuanbaoInfoFailure.InputBounds);
        Refuses([0x81], Chinese7937XuanbaoInfoFailure.InvalidGbk);
        Refuses([0xef, 0xbb, 0xbf, .. bytes], Chinese7937XuanbaoInfoFailure.InvalidGbk);
        Refuses([0xff, 0xfe, .. bytes], Chinese7937XuanbaoInfoFailure.InvalidGbk);
        Refuses([.. bytes, 0], Chinese7937XuanbaoInfoFailure.UnsupportedSyntax);
    }

    private static void Refuses(string text, Chinese7937XuanbaoInfoFailure? expected = null) =>
        Refuses(Chinese7937XuanbaoAttributeFixture.Gbk(text), expected);

    private static void Refuses(byte[] bytes, Chinese7937XuanbaoInfoFailure? expected = null)
    {
        Assert.False(Chinese7937DecodedXuanbaoInfo.TryParseAttributeCatalog(bytes, Current, out var catalog, out var failure));
        Assert.Null(catalog); Assert.NotEqual(Chinese7937XuanbaoInfoFailure.None, failure);
        if (expected.HasValue) Assert.Equal(expected.Value, failure);
    }
}
