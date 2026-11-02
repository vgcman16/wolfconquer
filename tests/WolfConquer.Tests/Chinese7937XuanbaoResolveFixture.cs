using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

// Synthetic scalar data and selected relationship forms; no captured client rows.
internal static class Chinese7937XuanbaoResolveFixture
{
    internal const string NumberGetter = "local function GetXuanBaoResolveNum(bGold)\n" +
        "if bGold then\nreturn XuanBaoResolve.GoldNum\nelse\nreturn XuanBaoResolve.CommonNum\nend\nend\n";
    internal const string MaterialGetter = "local function GetXuanBaoResolveDataID()\nreturn XuanBaoResolve.MaterialID\nend\n";
    internal const string Unload = "local function Unload()\nOtherInfo = nil\nXuanBaoResolve = nil\nend\n";
    internal const string Exports = "local XuanBao = {\nGetXuanBaoResolveNum = GetXuanBaoResolveNum,\n" +
        "GetXuanBaoResolveDataID = GetXuanBaoResolveDataID,\nUnload = Unload,\n}\n";
    internal const string Aliases = "XuanBao_GetXuanBaoResolveNum = XuanBao.GetXuanBaoResolveNum\n" +
        "XuanBao_GetXuanBaoResolveDataID = XuanBao.GetXuanBaoResolveDataID\nRuneSys_Unload = XuanBao.Unload\n";

    internal static string Module(string common = "7", string gold = "17", string material = "123") =>
        ModuleWithFields($"CommonNum={common},\nGoldNum={gold},\nMaterialID={material},");

    internal static string ModuleWithFields(string fields) => "local XuanBaoResolve = {\n" + fields + "\n}\n" +
        NumberGetter + MaterialGetter + Unload + Exports + Aliases + "return XuanBao\n";

    internal static byte[] Gbk(string text) => ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936).GetBytes(text);

    internal static Chinese7937XuanbaoResolveSnapshot Parse(string text)
    {
        Assert.True(Chinese7937DecodedXuanbaoInfo.TryParseResolve(Gbk(text), Chinese7937XuanbaoInfoVersion.Catalog7937,
            out var snapshot, out var failure), failure.ToString());
        return Assert.IsType<Chinese7937XuanbaoResolveSnapshot>(snapshot);
    }

    internal static Chinese7937XuanbaoInfoFailure Refused(string text)
    {
        Assert.False(Chinese7937DecodedXuanbaoInfo.TryParseResolve(Gbk(text), Chinese7937XuanbaoInfoVersion.Catalog7937,
            out var snapshot, out var failure));
        Assert.Null(snapshot); Assert.NotEqual(Chinese7937XuanbaoInfoFailure.None, failure);
        return failure;
    }
}
