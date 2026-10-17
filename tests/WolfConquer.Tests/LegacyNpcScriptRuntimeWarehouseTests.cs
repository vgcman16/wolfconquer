using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNpcScriptRuntimeWarehouseTests
{
    private const uint WarehouseTwinNpcId = 8;

    public static TheoryData<uint, string> SourceRoutedWarehouseNpcIds => new()
    {
        { 44, "WHMarket" },
        { 7_762, "Warehause_7762" },
        { 10_011, "Warehause_10011" },
        { 10_012, "Warehause_10012" },
        { 10_027, "Warehause_10027" },
        { 10_028, "Warehause_10028" },
    };

    [Theory]
    [MemberData(nameof(SourceRoutedWarehouseNpcIds))]
    public void ResolveSourceRoutedWarehouseNpcInitialClickOpensWarehouse(uint npcId, string npcEnumName)
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                npcId,
                optionId: 0,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.OpenUi, resolution.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.None, resolution.Reason);
        Assert.Equal(npcEnumName, resolution.NpcEnumName);
        Assert.Equal("NpcHandler.Warehause", resolution.SourceMethodName);
        Assert.Null(resolution.Dialog);

        var result = Assert.IsType<LegacyNpcScriptOptionResult>(resolution.Result);
        Assert.Equal(LegacyNpcScriptOptionResultKind.OpenUi, result.Kind);
        Assert.Equal(4U, result.UiId);
        Assert.Contains("ActionQuery Type=126 dwParam=4", resolution.SourceEvidence);
        Assert.Contains("NpcHandler.Warehause", resolution.SourceEvidence);
        Assert.DoesNotContain("missingRuntimeDefinition", resolution.DescribeForLog());

        Assert.True(LegacyNpcScriptRuntime.TryGetSourceHint(npcId, out var hintName, out var sourceMethod));
        Assert.Equal(npcEnumName, hintName);
        Assert.Equal("NpcHandler.Warehause", sourceMethod);
    }

    [Theory]
    [MemberData(nameof(SourceRoutedWarehouseNpcIds))]
    public void ResolveSourceRoutedWarehouseNpcOpenOptionOpensWarehouse(uint npcId, string npcEnumName)
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                npcId,
                optionId: 1,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.OpenUi, resolution.ActionCategory);
        Assert.Equal(npcEnumName, resolution.NpcEnumName);

        var result = Assert.IsType<LegacyNpcScriptOptionResult>(resolution.Result);
        Assert.Equal(LegacyNpcScriptOptionResultKind.OpenUi, result.Kind);
        Assert.Equal(4U, result.UiId);
    }

    [Fact]
    public void ResolveWarehouseTwinMilitaryLetterRouteModelsSafeNoMutationBranch()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                WarehouseTwinNpcId,
                optionId: 11,
                LegacyNpcReplyInteractType.Option));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.NoOp, resolution.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.None, resolution.Reason);
        Assert.Equal("WHTwin", resolution.NpcEnumName);
        Assert.Equal("NpcHandler.Warehause", resolution.SourceMethodName);
        Assert.Equal("none", resolution.NextSeam);
        Assert.Null(resolution.Dialog);

        var result = Assert.IsType<LegacyNpcScriptOptionResult>(resolution.Result);
        Assert.Equal(LegacyNpcScriptOptionResultKind.NoOp, result.Kind);
        Assert.Contains("option 11 only sends QuestGUI.SendAutoPatcher", resolution.SourceEvidence);
        Assert.Contains("does not mutate inventory, quest, warehouse, bank, or sash state", resolution.SourceEvidence);
        Assert.Contains("kind=NoOp", resolution.DescribeForLog());
    }

    [Fact]
    public void ResolveWarehouseTwinMilitaryLetterGrantRemainsFencedToAvoidDuplicateItems()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                WarehouseTwinNpcId,
                optionId: 10,
                LegacyNpcReplyInteractType.Option));

        Assert.True(resolution.IsUnsupported);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.StatefulNpcScript, resolution.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.StatefulSourceAction, resolution.Reason);
        Assert.Equal("wire-stateful-npc-script-action", resolution.NextSeam);
        Assert.Null(resolution.Result);
        Assert.Contains("option 10 checks inventory space and item 711235", resolution.SourceEvidence);
        Assert.Contains("must not grant items without inventory and quest persistence", resolution.SourceEvidence);
    }
}
