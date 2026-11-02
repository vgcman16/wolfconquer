using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoResolveTests
{
    [Theory]
    [InlineData("-1")] [InlineData("+1")] [InlineData("1.5")] [InlineData("0x1")]
    [InlineData("1e1")] [InlineData("01")] [InlineData("4294967296")] [InlineData("make()")]
    public void SelectedValuesMustBeCanonicalUInt32Literals(string value) =>
        Chinese7937XuanbaoResolveFixture.Refused(Chinese7937XuanbaoResolveFixture.Module(value));

    [Theory]
    [InlineData("CommonNum=7, GoldNum=17,")]
    [InlineData("CommonNum=7, GoldNum=17, MaterialID=123, Extra=1,")]
    [InlineData("CommonNum=7, GoldNum=17, MaterialID=123, CommonNum=8,")]
    [InlineData("CommonNum=7, GoldNum=17, MaterialID=123")]
    [InlineData("['CommonNum']=7, GoldNum=17, MaterialID=123,")]
    public void MissingExtraDuplicateOrAlteredLiteralMembersCannotPublishACompleteResolve(string fields) =>
        Chinese7937XuanbaoResolveFixture.Refused(Chinese7937XuanbaoResolveFixture.ModuleWithFields(fields));

    [Theory]
    [InlineData("GetXuanBaoResolveNum = GetXuanBaoResolveNum,\n")]
    [InlineData("GetXuanBaoResolveDataID = GetXuanBaoResolveDataID,\n")]
    [InlineData("Unload = Unload,\n")]
    [InlineData("XuanBao_GetXuanBaoResolveNum = XuanBao.GetXuanBaoResolveNum\n")]
    [InlineData("XuanBao_GetXuanBaoResolveDataID = XuanBao.GetXuanBaoResolveDataID\n")]
    [InlineData("RuneSys_Unload = XuanBao.Unload\n")]
    [InlineData("XuanBaoResolve = nil\n")]
    [InlineData("return XuanBao\n")]
    public void EverySelectedExportAliasResetAndTerminalReturnIsRequired(string removed)
    {
        var source = Chinese7937XuanbaoResolveFixture.Module();
        Chinese7937XuanbaoResolveFixture.Refused(source.Replace(removed, "", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("return XuanBaoResolve.GoldNum", "return XuanBaoResolve.CommonNum")]
    [InlineData("if bGold then", "if bGold == true then")]
    [InlineData("return XuanBaoResolve.MaterialID", "return 123")]
    [InlineData("local function Unload()\n", "local function Unload()\nreturn\n")]
    [InlineData("XuanBaoResolve = nil", "if true then XuanBaoResolve = nil end")]
    [InlineData("OtherInfo = nil", "OtherInfo = nil\nOtherInfo = nil")]
    [InlineData("RuneSys_Unload = XuanBao.Unload", "RuneSys_Unload = XuanBao.GetUnrelated")]
    [InlineData("Unload = Unload,", "Unload = unrelated,")]
    public void AlteredBranchMaterialOrUnloadLifecycleIsNotTheObservedRelationship(string original, string replacement) =>
        Chinese7937XuanbaoResolveFixture.Refused(Chinese7937XuanbaoResolveFixture.Module().Replace(original, replacement, StringComparison.Ordinal));

    [Theory]
    [InlineData("XuanBaoResolve.GoldNum = 8\n")]
    [InlineData("local XuanBaoResolve = {}\n")]
    [InlineData("local GetXuanBaoResolveNum = other\n")]
    [InlineData("XuanBao_GetXuanBaoResolveNum = XuanBao.GetXuanBaoResolveNum\n")]
    [InlineData("XuanBao['GetXuanBaoResolveNum'] = other\n")]
    [InlineData("mutate(XuanBao)\n")]
    [InlineData("local escaped = XuanBao\n")]
    [InlineData("RuneSys_Unload = other\n")]
    public void AdditionalSelectedUsesShadowingOrComputedModuleMutationRefuse(string inserted)
    {
        var source = Chinese7937XuanbaoResolveFixture.Module().Replace("return XuanBao\n", inserted + "return XuanBao\n", StringComparison.Ordinal);
        Chinese7937XuanbaoResolveFixture.Refused(source);
    }

    [Fact]
    public void DuplicateLiteralMemberReportsItsActualFailureCategory()
    {
        Assert.Equal(Chinese7937XuanbaoInfoFailure.DuplicateField, Chinese7937XuanbaoResolveFixture.Refused(
            Chinese7937XuanbaoResolveFixture.ModuleWithFields("CommonNum=7, GoldNum=17, MaterialID=123, CommonNum=8,")));
    }
}
