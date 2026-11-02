using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

internal static class Chinese7937XuanbaoAttributeFixture
{
    internal const string ScoreGetter = "local function GetXuanBaoScoreGradeStep(nItemType, nAttriType, nIndex, default)\n" +
        "local t1 = XuanBaoScoreGradeStep[nItemType]\nif (t1 == nil) then\nreturn default\nend\n" +
        "local t2 = t1[nAttriType]\nif (t2 == nil) then\nreturn default\nend\n" +
        "local t3 = t2[nIndex]\nif (t3 == nil) then\nreturn default\nend\nreturn t3\nend\n";
    internal const string GoldGetter = "local function GetXuanBaoGoldLimitAttri(nItemType, nAttriType, default)\n" +
        "local t = XuanBaoLimitAttri[nItemType]\nif (t == nil) then\nreturn default\nend\n" +
        "local res = t[nAttriType]\nif (res == nil) then\nreturn default\nend\nreturn res\nend\n";
    internal const string Unload = "local function Unload()\nXuanBaoScoreGradeStep = nil\nXuanBaoLimitAttri = nil\nend\n";
    internal const string Exports = "local XuanBao = {\nGetXuanBaoScoreGradeStep = GetXuanBaoScoreGradeStep,\n" +
        "GetXuanBaoGoldLimitAttri = GetXuanBaoGoldLimitAttri,\nUnload = Unload,\n}\n";
    internal const string Aliases = "XuanBao_GetXuanBaoScoreGradeStep = XuanBao.GetXuanBaoScoreGradeStep\n" +
        "XuanBao_GetXuanBaoGoldLimitAttri = XuanBao.GetXuanBaoGoldLimitAttri\n";

    internal static string Module(string scoreRows = "[10] = { [1] = { ScoreLimit0=0, ScoreLimit8=100, }, },",
        string goldRows = "[10] = { [1] = true, [2] = false, },") =>
        "local XuanBaoScoreGradeStep = {\n" + scoreRows + "\n}\n" + ScoreGetter +
        "local XuanBaoLimitAttri = {\n" + goldRows + "\n}\n" + GoldGetter + Unload + Exports + Aliases + "return XuanBao\n";

    internal static byte[] Gbk(string text) => ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936).GetBytes(text);

    internal static Chinese7937XuanbaoAttributeCatalog Parse(string text)
    {
        Assert.True(Chinese7937DecodedXuanbaoInfo.TryParseAttributeCatalog(Gbk(text),
            Chinese7937XuanbaoInfoVersion.Catalog7937, out var catalog, out var failure), failure.ToString());
        return Assert.IsType<Chinese7937XuanbaoAttributeCatalog>(catalog);
    }
}
