using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNpcScriptDefinitionTests
{
    private const uint VipQuestNpcId = 52_002;
    private const uint NobilityDonorNpcId = 2_541;

    private const string VipQuestSourceEvidence =
        "NpcHandler.cs search for NPC ids 2084 and 2255 had no matches; selected NpcHandler.VipQuest because it is absent from WorldPacketHandlers and models only option 0 dialog plus option 1 teleport. Evidence: NpcID.cs:301 VipQuest = 52002u; NpcHandler.cs:26123-26136 [Npc(NpcID.VipQuest)], dialog AddText/AddOption/AddAvatar(105), client.Teleport(208, 255, 11127u).";

    private const string NobilityDonorSourceEvidence =
        "NpcID.cs:119 NobilityDonor = 2541u; NpcHandler.cs:7854-7905 [Npc(NpcID.NobilityDonor)] option 0 AddText/AddInput(\"\u5929\u77f3\u6350\u732e\u91d1\u989d\", 1, 10)/close; option 1 validates uint input and ConquerPoints before confirmation; option 2 subtracts NobilityCps, adds NobilityCps * 500 to Nobility.Donation, sends NobilityIconCreate, and updates Pool.NobilityRanking.";

    public static TheoryData<uint, string, string, string> GlowtownPortalDefinitions => new()
    {
        {
            20_571,
            "GlowtownPortal1",
            "NpcHandler.GlowtownPortal1",
            "NpcID.cs:109 GlowtownPortal1 = 20571u; NpcHandler.cs:33379-33392 [Npc(NpcID.GlowtownPortal1)] option 0 dialog AddText/AddOption/AddAvatar(156), option 1 client.Teleport(93, 408, 10137u)."
        },
        {
            20_572,
            "GlowtownPortal2",
            "NpcHandler.GlowtownPortal2",
            "NpcID.cs:110 GlowtownPortal2 = 20572u; NpcHandler.cs:33396-33409 [Npc(NpcID.GlowtownPortal2)] option 0 dialog AddText/AddOption/AddAvatar(156), option 1 client.Teleport(93, 408, 10137u)."
        },
        {
            20_573,
            "GlowtownPortal3",
            "NpcHandler.GlowtownPortal3",
            "NpcID.cs:111 GlowtownPortal3 = 20573u; NpcHandler.cs:33413-33426 [Npc(NpcID.GlowtownPortal3)] option 0 dialog AddText/AddOption/AddAvatar(156), option 1 client.Teleport(93, 408, 10137u)."
        },
    };

    [Fact]
    public void LookupByNpcIdReturnsVipQuestCatalogEntry()
    {
        Assert.True(LegacyNpcScriptDefinition.TryGetByNpcId(VipQuestNpcId, out var definition));

        Assert.Same(definition, LegacyNpcScriptDefinition.All.Single(entry => entry.NpcId == VipQuestNpcId));
        Assert.Equal(VipQuestNpcId, definition.NpcId);
        Assert.Equal("VipQuest", definition.NpcEnumName);
        Assert.Equal("NpcHandler.VipQuest", definition.SourceMethodName);

        Assert.False(LegacyNpcScriptDefinition.TryGetByNpcId(2_084, out _));
        Assert.False(LegacyNpcScriptDefinition.TryGetByNpcId(2_255, out _));
    }

    [Fact]
    public void InitialDialogLinesAndOptionsMatchLegacySource()
    {
        var definition = GetVipQuestDefinition();

        Assert.Equal(
            [
                "\u6211\u6562\u80af\u5b9a\u4f60\u6b63\u5728\u5bfb\u627e\u4e00\u79cd\u83b7\u5f97VIP 6\u7684\u65b9\u6cd5\uff0c\u597d\u5728\u6709\u4e00\u79cd\u65b9\u6cd5\uff0c\u4f46\u4e0d\u4f1a\u90a3\u4e48\u5bb9\u6613\u3002",
            ],
            definition.InitialDialogLines);
        Assert.Equal((ushort)105, definition.AvatarId);

        Assert.Collection(
            definition.InitialOptions,
            option =>
            {
                Assert.Equal("\u6ca1\u95ee\u9898\u3002", option.Text);
                Assert.Equal(1, option.OptionId);
                Assert.False(option.IsClosedOption);
                Assert.Equal(LegacyNpcScriptOptionResultKind.Teleport, option.Result.Kind);
                Assert.Equal(11_127U, option.Result.MapId);
                Assert.Equal(208U, option.Result.X);
                Assert.Equal(255U, option.Result.Y);
                Assert.Null(option.Result.ShopId);
                Assert.Null(option.Result.UiId);
            },
            option =>
            {
                Assert.Equal("\u6211\u4f1a\u518d\u8003\u8651\u4e00\u4e0b\u3002", option.Text);
                Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
                Assert.True(option.IsClosedOption);
                Assert.Equal(LegacyNpcScriptOptionResultKind.NoOp, option.Result.Kind);
            });
    }

    [Fact]
    public void TryGetInitialOptionHandlesClosedAndMissingOptions()
    {
        var definition = GetVipQuestDefinition();

        Assert.True(definition.TryGetInitialOption(LegacyNpcReplyPacket.ClosedOptionId, out var closedOption));

        Assert.True(closedOption.IsClosedOption);
        Assert.Equal("\u6211\u4f1a\u518d\u8003\u8651\u4e00\u4e0b\u3002", closedOption.Text);
        Assert.Equal(LegacyNpcScriptOptionResultKind.NoOp, closedOption.Result.Kind);
        Assert.Equal(
            "kind=NoOp sourceEvidence=NpcHandler.VipQuest option byte.MaxValue is the close dialog option at NpcHandler.cs:26129-26131; no source case branch mutates state.",
            closedOption.Result.DescribeForLog());

        Assert.False(definition.TryGetInitialOption(99, out var missingOption));
        Assert.Null(missingOption);
    }

    [Fact]
    public void SourceMethodEvidenceAndLogDescriptionAreDeterministic()
    {
        var definition = GetVipQuestDefinition();

        Assert.Equal("NpcHandler.VipQuest", definition.SourceMethodName);
        Assert.Equal(VipQuestSourceEvidence, definition.SourceEvidence);
        Assert.Equal(
            string.Concat(
                "legacyScript=NpcHandler.VipQuest npcId=52002 npcEnum=VipQuest initialLines=1 initialOptions=2 avatar=105 sourceEvidence=",
                VipQuestSourceEvidence),
            definition.DescribeForLog());

        Assert.True(definition.TryGetInitialOption(1, out var teleportOption));
        Assert.Equal(
            "kind=Teleport map=11127 x=208 y=255 sourceEvidence=NpcHandler.VipQuest option 1: client.Teleport(208, 255, 11127u) at NpcHandler.cs:26134-26135.",
            teleportOption.Result.DescribeForLog());
    }

    [Fact]
    public void NobilityDonorDefinitionMatchesLegacyCpDonationPromptAndStatefulConfirmation()
    {
        var definition = GetDefinition(NobilityDonorNpcId);

        Assert.Same(definition, LegacyNpcScriptDefinition.All.Single(entry => entry.NpcId == NobilityDonorNpcId));
        Assert.Equal(NobilityDonorNpcId, definition.NpcId);
        Assert.Equal("NobilityDonor", definition.NpcEnumName);
        Assert.Equal("NpcHandler.NobilityDonor", definition.SourceMethodName);
        Assert.Equal(NobilityDonorSourceEvidence, definition.SourceEvidence);
        Assert.Equal(
            [
                "    \u5728\u6b64\u4f60\u53ef\u4ee5\u5feb\u901f\u8fdb\u884c\u7235\u4f4d\u6350\u732e\u3002\u5929\u77f3\u6bd4\u4f8b\u662f1:50000\u3001\u8fd9\u5c31\u610f\u5473\u77401\u70b9\u5929\u77f3\u62b5\u626350000\u679a\u94f6\u4e24\uff0c\u6700\u4f4e\u9650\u989d\u4e3a1,000,000\u70b9\u5929\u77f3\u3002",
            ],
            definition.InitialDialogLines);
        Assert.Equal((ushort)6, definition.AvatarId);

        var input = Assert.Single(definition.InitialInputs);
        Assert.Equal("\u5929\u77f3\u6350\u732e\u91d1\u989d", input.Label);
        Assert.Equal(1, input.OptionId);
        Assert.Equal(10, input.MaxLength);
        Assert.Contains("AddInput", input.SourceEvidence);
        Assert.True(definition.TryGetInitialInput(1, out var inputByOption));
        Assert.Same(input, inputByOption);

        var closeOption = Assert.Single(definition.InitialOptions);
        Assert.Equal("\u53ea\u662f\u8def\u8fc7\u3002", closeOption.Text);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, closeOption.OptionId);
        Assert.True(closeOption.IsClosedOption);
        Assert.Equal(LegacyNpcScriptOptionResultKind.NoOp, closeOption.Result.Kind);

        Assert.False(definition.TryGetInitialOption(2, out _));
        Assert.True(definition.TryGetScriptOption(2, out var confirmOption));
        Assert.Equal("\u662f\u7684\u3002", confirmOption.Text);
        Assert.Equal(2, confirmOption.OptionId);
        Assert.Equal(LegacyNpcScriptOptionResultKind.Unsupported, confirmOption.Result.Kind);
        Assert.Contains("NobilityCps * 500", confirmOption.Result.SourceEvidence);
        Assert.Contains("kind=Unsupported", confirmOption.Result.DescribeForLog());

        Assert.Collection(
            definition.ContinuationOptions,
            option =>
            {
                Assert.Same(confirmOption, option);
                Assert.Equal("\u662f\u7684\u3002", option.Text);
            },
            option =>
            {
                Assert.Equal("\u8def\u8fc7\u800c\u5df2\u3002", option.Text);
                Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
                Assert.True(option.IsClosedOption);
                Assert.Equal(LegacyNpcScriptOptionResultKind.NoOp, option.Result.Kind);
                Assert.Contains("NobilityCps", option.Result.SourceEvidence);
            });
    }

    [Fact]
    public void PendingMutationResultModelsNobilityDonationIntentWithoutApplyingIt()
    {
        var mutation = LegacyNpcScriptPendingMutation.NobilityDonation(
            400_000,
            "NpcHandler.NobilityDonor option 2 source mutation.");
        var result = LegacyNpcScriptOptionResult.FromPendingMutation(
            mutation,
            "Helper emits only a pending mutation intent.");

        Assert.Equal(LegacyNpcScriptOptionResultKind.PendingMutation, result.Kind);
        Assert.Same(mutation, result.PendingMutation);
        Assert.Equal(LegacyNpcScriptPendingMutationKind.NobilityDonation, mutation.Kind);
        Assert.Equal(400_000U, mutation.ConquerPoints);
        Assert.Equal(200_000_000UL, mutation.Donation);
        Assert.Equal("client.Player.ConquerPoints", mutation.CurrencySource);
        Assert.Equal("client.Player.Nobility.Donation", mutation.MutationTarget);
        Assert.Contains("kind=PendingMutation", result.DescribeForLog());
        Assert.Contains("donation=200000000", result.DescribeForLog());
    }

    [Theory]
    [MemberData(nameof(GlowtownPortalDefinitions))]
    public void GlowtownPortalDefinitionsMatchLegacyDialogAndTeleport(
        uint npcId,
        string npcEnumName,
        string sourceMethodName,
        string sourceEvidence)
    {
        var definition = GetDefinition(npcId);

        Assert.Same(definition, LegacyNpcScriptDefinition.All.Single(entry => entry.NpcId == npcId));
        Assert.Equal(npcId, definition.NpcId);
        Assert.Equal(npcEnumName, definition.NpcEnumName);
        Assert.Equal(sourceMethodName, definition.SourceMethodName);
        Assert.Equal(sourceEvidence, definition.SourceEvidence);
        Assert.Equal(
            [
                "\u4f60\u786e\u5b9a\u8981\u56de\u5230\u57ce\u9547\u5417\uff1f",
            ],
            definition.InitialDialogLines);
        Assert.Equal((ushort)156, definition.AvatarId);

        Assert.Collection(
            definition.InitialOptions,
            option =>
            {
                Assert.Equal("\u662f\u7684\u3002", option.Text);
                Assert.Equal(1, option.OptionId);
                Assert.False(option.IsClosedOption);
                Assert.Equal(LegacyNpcScriptOptionResultKind.Teleport, option.Result.Kind);
                Assert.Equal(10_137U, option.Result.MapId);
                Assert.Equal(93U, option.Result.X);
                Assert.Equal(408U, option.Result.Y);
                Assert.Null(option.Result.ShopId);
                Assert.Null(option.Result.UiId);
                Assert.Contains("client.Teleport(93, 408, 10137u)", option.Result.SourceEvidence);
            },
            option =>
            {
                Assert.Equal("\u518d\u8d70\u8d70\u3002", option.Text);
                Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
                Assert.True(option.IsClosedOption);
                Assert.Equal(LegacyNpcScriptOptionResultKind.NoOp, option.Result.Kind);
                Assert.Contains("byte.MaxValue", option.Result.SourceEvidence);
            });

        Assert.True(definition.TryGetInitialOption(1, out var teleportOption));
        Assert.Same(definition.InitialOptions[0], teleportOption);
        Assert.Equal(
            string.Concat(
                "kind=Teleport map=10137 x=93 y=408 sourceEvidence=",
                teleportOption.Result.SourceEvidence),
            teleportOption.Result.DescribeForLog());
    }

    private static LegacyNpcScriptDefinition GetVipQuestDefinition()
    {
        return GetDefinition(VipQuestNpcId);
    }

    private static LegacyNpcScriptDefinition GetDefinition(uint npcId)
    {
        Assert.True(LegacyNpcScriptDefinition.TryGetByNpcId(npcId, out var definition));
        return definition;
    }
}
