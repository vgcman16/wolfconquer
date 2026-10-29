using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoInfoTests
{
    [Theory]
    [InlineData("[\"alpha\"] = 1, [\"alpha\"] = 2,", Chinese7937XuanbaoInfoFailure.DuplicateField)]
    [InlineData("[\"alpha\"] = 4294967296,", Chinese7937XuanbaoInfoFailure.NumericWidth)]
    [InlineData("[\"alpha\"] = -1,", Chinese7937XuanbaoInfoFailure.UnsupportedSyntax)]
    [InlineData("[\"alpha\"] = 01,", Chinese7937XuanbaoInfoFailure.UnsupportedSyntax)]
    [InlineData("[\"alpha\"] = 0x10,", Chinese7937XuanbaoInfoFailure.UnsupportedSyntax)]
    [InlineData("[\"alpha\"] = 1.0,", Chinese7937XuanbaoInfoFailure.UnsupportedSyntax)]
    [InlineData("[\"alpha\"] = 1+2,", Chinese7937XuanbaoInfoFailure.UnsupportedSyntax)]
    [InlineData("[\"alpha\"] = f(),", Chinese7937XuanbaoInfoFailure.UnsupportedSyntax)]
    [InlineData("[\"alpha\"] = {},", Chinese7937XuanbaoInfoFailure.UnsupportedSyntax)]
    [InlineData("[\"alpha\"] = 1", Chinese7937XuanbaoInfoFailure.UnsupportedSyntax)]
    [InlineData("alpha = 1,", Chinese7937XuanbaoInfoFailure.UnsupportedSyntax)]
    [InlineData("['alpha'] = 1,", Chinese7937XuanbaoInfoFailure.UnsupportedSyntax)]
    [InlineData("[\"a\\108pha\"] = 1,", Chinese7937XuanbaoInfoFailure.UnsupportedSyntax)]
    [InlineData("[1] = 1,", Chinese7937XuanbaoInfoFailure.UnsupportedSyntax)]
    [InlineData("", Chinese7937XuanbaoInfoFailure.UnsupportedSyntax)]
    public void UnsupportedOrDuplicateSelectedLiteralNeverProducesPartialFields(string fields, Chinese7937XuanbaoInfoFailure expected) =>
        Refuses(Module(fields), expected);

    [Fact]
    public void EverySelectedRelationshipEdgeIsRequiredAndDirect()
    {
        foreach (var text in new[]
        {
            Module().Replace(Literal("[\"alpha\"] = 7,"), "", StringComparison.Ordinal),
            Module().Replace("return XuanBaoEchoInfo[key]", "return Other[key]", StringComparison.Ordinal),
            Module().Replace("GetXuanBaoEchoInfo = GetXuanBaoEchoInfo,", "GetXuanBaoEchoInfo = Other,", StringComparison.Ordinal),
            Module().Replace(Alias, "", StringComparison.Ordinal),
            Module().Replace("XuanBaoEchoInfo = nil", "Other = nil", StringComparison.Ordinal),
            Module().Replace("local function GetXuanBaoEchoInfo(key)", "local function GetXuanBaoEchoInfo(other)", StringComparison.Ordinal),
            Module().Replace("return XuanBaoEchoInfo[key]", "return XuanBaoEchoInfo[key] or 0", StringComparison.Ordinal),
            Module().Replace(Alias, "XuanBao_GetXuanBaoEchoInfo = function(key) return XuanBao.GetXuanBaoEchoInfo(key) end\n", StringComparison.Ordinal),
        }) Refuses(text);
    }

    [Fact]
    public void DuplicateDeclarationsAndSelectedMutationOrReferencesAreAmbiguous()
    {
        foreach (var suffix in new[] { Literal("[\"extra\"] = 1,"), Getter, Alias,
            "XuanBaoEchoInfo = nil\n", "XuanBaoEchoInfo[\"alpha\"] = 99\n", "GetXuanBaoEchoInfo(\"alpha\")\n",
            "GetXuanBaoEchoInfo = nil\n", "XuanBao = nil\n",
            "local function MutateModule()\n XuanBao = nil\nend\n" }) Refuses(Module() + suffix);
        Refuses(Module().Replace("GetXuanBaoEchoInfo = GetXuanBaoEchoInfo,",
            "GetXuanBaoEchoInfo = GetXuanBaoEchoInfo, GetXuanBaoEchoInfo = GetXuanBaoEchoInfo,", StringComparison.Ordinal));
    }

    [Fact]
    public void ComputedOrQuotedModuleMembersCannotHideAnOverwritingSelectedExport()
    {
        foreach (var member in new[] { "[\"GetXuanBaoEchoInfo\"] = Other,", "[\"GetXuanBaoEchoInf\\111\"] = Other,", "[computed()] = Other," })
            Refuses(Module().Replace("    Unload = Unload,", "    " + member + "\n    Unload = Unload,", StringComparison.Ordinal));
    }

    [Fact]
    public void ScopeForwardBindingConditionalResetAndContinuationsCannotImpersonateTheObservedRelationship()
    {
        Refuses("local function Shadow()\n" + Module() + "end\n");
        Refuses(Getter + Literal("[\"alpha\"] = 7,") + Unload + Export + Alias);
        Refuses(Module().Replace("    XuanBaoEchoInfo = nil", "    if true then\n        XuanBaoEchoInfo = nil\n    end", StringComparison.Ordinal));
        Refuses(Module().Replace("local XuanBaoEchoInfo = {", "local Hidden = { local XuanBaoEchoInfo = {", StringComparison.Ordinal) + "}\n");
        foreach (var suffix in new[] { "()", "\n+ 1", "\nand Other" }) Refuses(Module().TrimEnd() + suffix + "\n");
    }

    [Theory]
    [InlineData("--[==[ unfinished")]
    [InlineData("local unrelated = 'unfinished")]
    [InlineData("local unrelated = [=[unfinished")]
    [InlineData("local function unrelated()")]
    [InlineData("local unrelated = {")]
    public void AmbiguousTrailingLexicalBoundariesNeverGetIgnored(string suffix) => Refuses(Module() + suffix);
}
