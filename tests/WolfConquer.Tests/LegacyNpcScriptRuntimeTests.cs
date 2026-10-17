using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNpcScriptRuntimeTests
{
    private const uint VipQuestNpcId = 52_002;
    private const uint BackTcNpcId = 10_032;
    private const uint IronTongNpcId = 20_499;
    private const uint BlackSmithNpcId = 5;
    private const uint WarehouseTwinNpcId = 8;
    private const uint WarehouseMarketNpcId = 44;
    private const uint NobilityDonorNpcId = 2_541;
    private const uint TeleportallNpcId = 15_641;

    [Fact]
    public void ResolveInitialScriptClickReturnsModeledDialog()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                VipQuestNpcId,
                optionId: 0,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeStatus.Modeled, resolution.Status);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.Dialog, resolution.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.None, resolution.Reason);
        Assert.Equal("VipQuest", resolution.NpcEnumName);
        Assert.Equal("NpcHandler.VipQuest", resolution.SourceMethodName);
        Assert.NotNull(resolution.Dialog);
        Assert.Equal((ushort)105, resolution.Dialog.AvatarId);
        Assert.Single(resolution.Dialog.Lines);
        Assert.Equal(2, resolution.Dialog.Options.Count);
        Assert.Null(resolution.Result);

        var diagnostic = resolution.DescribeForLog();
        Assert.Contains("status=modeled", diagnostic);
        Assert.Contains("category=dialog", diagnostic);
        Assert.Contains("legacyScript=NpcHandler.VipQuest", diagnostic);
        Assert.Contains("dialogLines=1", diagnostic);
        Assert.Contains("dialogOptions=2", diagnostic);
        Assert.Contains("nextSeam=none", diagnostic);
    }

    [Fact]
    public void ResolveModeledOptionReturnsTeleportAction()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                VipQuestNpcId,
                optionId: 1,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.Teleport, resolution.ActionCategory);
        Assert.NotNull(resolution.Result);
        Assert.Equal(LegacyNpcScriptOptionResultKind.Teleport, resolution.Result.Kind);
        Assert.Equal(11_127U, resolution.Result.MapId);
        Assert.Equal(208U, resolution.Result.X);
        Assert.Equal(255U, resolution.Result.Y);
        Assert.Contains("client.Teleport(208, 255, 11127u)", resolution.SourceEvidence);
        Assert.Contains("kind=Teleport map=11127 x=208 y=255", resolution.DescribeForLog());
    }

    [Fact]
    public void ResolveDialogReplyUsesActiveNpcFallbackForModeledOption()
    {
        var reply = new LegacyNpcDialogRequestPacket(
            NpcId: 0,
            Mesh: 0,
            OptionId: 1,
            InteractType: (byte)LegacyNpcReplyInteractType.Dialog,
            Action: 0,
            Input: string.Empty);

        var resolution = LegacyNpcScriptRuntime.Resolve(reply, activeNpcId: VipQuestNpcId);

        Assert.True(resolution.IsModeled);
        Assert.Equal(VipQuestNpcId, resolution.NpcId);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.Teleport, resolution.ActionCategory);
        Assert.Contains("[Packet(2255)]", resolution.SourceEvidence);
        Assert.Contains("legacyScript=NpcHandler.VipQuest", resolution.DescribeForLog());
    }

    [Fact]
    public void ResolveBackTcInitialClickReturnsSourceBackedDialog()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                BackTcNpcId,
                optionId: 0,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.Dialog, resolution.ActionCategory);
        Assert.Equal("BackTC", resolution.NpcEnumName);
        Assert.Equal("NpcHandler.BackTC", resolution.SourceMethodName);
        Assert.NotNull(resolution.Dialog);
        Assert.Equal((ushort)154, resolution.Dialog.AvatarId);
        var dialogLine = Assert.Single(resolution.Dialog.Lines);
        Assert.Contains("\u8fd4\u56de\u53cc\u9f99\u57ce", dialogLine);

        Assert.Collection(
            resolution.Dialog.Options,
            option =>
            {
                Assert.Equal("\u8bf7\u5e26\u6211\u56de\u53bb\u3002", option.Text);
                Assert.Equal(3, option.OptionId);
            },
            option =>
            {
                Assert.Equal("\u554a\uff0c\u7b49\u7b49", option.Text);
                Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
            });
        Assert.Contains("NpcID.cs:58 BackTC = 10032u", resolution.SourceEvidence);
    }

    [Fact]
    public void ResolveBackTcTeleportOptionReturnsTwinCityTeleport()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                BackTcNpcId,
                optionId: 3,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.Teleport, resolution.ActionCategory);
        Assert.NotNull(resolution.Result);
        Assert.Equal(LegacyNpcScriptOptionResultKind.Teleport, resolution.Result.Kind);
        Assert.Equal(1_002U, resolution.Result.MapId);
        Assert.Equal(410U, resolution.Result.X);
        Assert.Equal(354U, resolution.Result.Y);
        Assert.Contains("client.Teleport(410, 354, 1002u)", resolution.SourceEvidence);
    }

    [Fact]
    public void ResolveTeleportallInitialClickReturnsSourceBackedTravelDialog()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                TeleportallNpcId,
                optionId: 0,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.Dialog, resolution.ActionCategory);
        Assert.Equal("Teleportall", resolution.NpcEnumName);
        Assert.Equal("NpcHandler.Teleportall", resolution.SourceMethodName);
        Assert.NotNull(resolution.Dialog);
        Assert.Equal((ushort)286, resolution.Dialog.AvatarId);
        Assert.Equal(
            "We made it easy the matter for you, from here you can move around the city.",
            Assert.Single(resolution.Dialog.Lines));
        Assert.Contains(resolution.Dialog.Options, option => option.OptionId == 1 && option.Text == "GuildWar.");
        Assert.Contains(resolution.Dialog.Options, option => option.OptionId == 10 && option.Text == "Poker");
        Assert.Contains("NpcID.cs:66 Teleportall = 15641u", resolution.SourceEvidence);
    }

    [Fact]
    public void ResolveTeleportallDirectOptionReturnsSourceTeleport()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                TeleportallNpcId,
                optionId: 6,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.Teleport, resolution.ActionCategory);
        Assert.NotNull(resolution.Result);
        Assert.Equal(LegacyNpcScriptOptionResultKind.Teleport, resolution.Result.Kind);
        Assert.Equal(1_002U, resolution.Result.MapId);
        Assert.Equal(448U, resolution.Result.X);
        Assert.Equal(271U, resolution.Result.Y);
        Assert.Contains("client.Teleport(448, 271, 1002u)", resolution.SourceEvidence);
    }

    [Fact]
    public void ResolveTeleportallSubmenuDestinationReturnsSourceTeleport()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                TeleportallNpcId,
                optionId: 50,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.Teleport, resolution.ActionCategory);
        Assert.NotNull(resolution.Result);
        Assert.Equal(1_015U, resolution.Result.MapId);
        Assert.Equal(706U, resolution.Result.X);
        Assert.Equal(571U, resolution.Result.Y);
        Assert.Contains("client.Teleport(706, 571, 1015u)", resolution.SourceEvidence);
    }

    [Fact]
    public void ResolveNobilityDonorInitialClickReturnsSourceBackedInputPrompt()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                NobilityDonorNpcId,
                optionId: 0,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.Dialog, resolution.ActionCategory);
        Assert.Equal("NobilityDonor", resolution.NpcEnumName);
        Assert.Equal("NpcHandler.NobilityDonor", resolution.SourceMethodName);
        Assert.NotNull(resolution.Dialog);
        Assert.Equal((ushort)6, resolution.Dialog.AvatarId);
        Assert.Contains("\u7235\u4f4d\u6350\u732e", Assert.Single(resolution.Dialog.Lines));

        var input = Assert.Single(resolution.Dialog.Inputs);
        Assert.Equal("\u5929\u77f3\u6350\u732e\u91d1\u989d", input.Label);
        Assert.Equal(1, input.OptionId);
        Assert.Equal(10, input.MaxLength);

        var closeOption = Assert.Single(resolution.Dialog.Options);
        Assert.Equal("\u53ea\u662f\u8def\u8fc7\u3002", closeOption.Text);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, closeOption.OptionId);
        Assert.Contains("NpcID.cs:119 NobilityDonor = 2541u", resolution.SourceEvidence);
        Assert.Contains("dialogInputs=1", resolution.DescribeForLog());

        var statefulAction = Assert.IsType<LegacyNpcScriptStatefulAction>(resolution.StatefulAction);
        Assert.Equal("NobilityDonorDonation", statefulAction.ActionName);
        Assert.Equal(LegacyNpcScriptStatefulActionStep.Prompt, statefulAction.Step);
        Assert.Equal(LegacyNpcScriptStatefulActionStep.AmountInput, statefulAction.NextState.Step);
        Assert.Null(statefulAction.NextState.PendingConquerPoints);
        Assert.Same(resolution.Dialog, statefulAction.Dialog);
    }

    [Fact]
    public void ResolveNobilityDonorInputReplyReturnsModeledConfirmationState()
    {
        var prompt = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                NobilityDonorNpcId,
                optionId: 0,
                LegacyNpcReplyInteractType.Dialog));
        var promptAction = Assert.IsType<LegacyNpcScriptStatefulAction>(prompt.StatefulAction);

        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                NobilityDonorNpcId,
                optionId: 1,
                LegacyNpcReplyInteractType.Input,
                input: "500000"),
            promptAction.NextState);

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.Dialog, resolution.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.None, resolution.Reason);
        Assert.Equal("NobilityDonor", resolution.NpcEnumName);
        Assert.Equal("NpcHandler.NobilityDonor", resolution.SourceMethodName);
        Assert.Equal("none", resolution.NextSeam);
        Assert.Null(resolution.Result);
        Assert.NotNull(resolution.Dialog);
        var statefulAction = Assert.IsType<LegacyNpcScriptStatefulAction>(resolution.StatefulAction);
        Assert.Equal(LegacyNpcScriptStatefulActionStep.AmountInput, statefulAction.Step);
        Assert.Equal(LegacyNpcScriptStatefulActionStep.Confirmation, statefulAction.NextState.Step);
        Assert.Equal(500_000U, statefulAction.NextState.PendingConquerPoints);
        Assert.NotNull(statefulAction.Dialog);
        Assert.Same(resolution.Dialog, statefulAction.Dialog);
        Assert.Contains("500000", Assert.Single(statefulAction.Dialog.Lines));
        Assert.Equal(2, statefulAction.Dialog.Options.Count);
        Assert.Contains("250000000", statefulAction.Dialog.Lines[0]);
        Assert.Equal("none", statefulAction.NextSeam);
        Assert.Contains("pendingConquerPoints=500000", resolution.SourceEvidence);
        Assert.Contains("stores client.Player.NobilityCps", resolution.SourceEvidence);
        Assert.Contains("without mutating currency or nobility", resolution.SourceEvidence);
        Assert.Contains("statefulStep=amountInput", resolution.DescribeForLog());
        Assert.Contains("stateStep=confirmation", resolution.DescribeForLog());
        Assert.DoesNotContain("wire-stateful-npc-script-action", resolution.DescribeForLog());
    }

    [Fact]
    public void ResolveNobilityDonorInvalidInputReturnsModeledRetryPromptWithoutEconomyMutation()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                NobilityDonorNpcId,
                optionId: 1,
                LegacyNpcReplyInteractType.Input,
                input: "not-a-number"),
            new LegacyNpcScriptStatefulActionState(
                "NobilityDonorDonation",
                LegacyNpcScriptStatefulActionStep.AmountInput,
                PendingConquerPoints: null));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.Dialog, resolution.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.None, resolution.Reason);
        Assert.Equal("none", resolution.NextSeam);
        var retryDialog = resolution.Dialog;
        Assert.NotNull(retryDialog);
        Assert.Equal("Please enter the amount again.", Assert.Single(retryDialog.Lines));
        Assert.Null(resolution.Result);
        var statefulAction = Assert.IsType<LegacyNpcScriptStatefulAction>(resolution.StatefulAction);
        Assert.Equal(LegacyNpcScriptStatefulActionStep.AmountInput, statefulAction.Step);
        Assert.Equal(LegacyNpcScriptStatefulActionStep.AmountInput, statefulAction.NextState.Step);
        Assert.Null(statefulAction.NextState.PendingConquerPoints);
        Assert.Same(retryDialog, statefulAction.Dialog);
        var retryInput = Assert.Single(retryDialog.Inputs);
        Assert.Equal("CP donation amount", retryInput.Label);
        Assert.Equal(16, retryInput.MaxLength);
        Assert.Contains("invalid input", statefulAction.SourceEvidence);
        Assert.Contains("does not mutate currency or nobility state", statefulAction.SourceEvidence);
        Assert.Contains("status=modeled", resolution.DescribeForLog());
        Assert.Contains("reason=none", resolution.DescribeForLog());
    }

    [Fact]
    public void ResolveNobilityDonorConfirmationWithPendingAmountEmitsPendingMutation()
    {
        var input = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                NobilityDonorNpcId,
                optionId: 1,
                LegacyNpcReplyInteractType.Input,
                input: "400000"),
            new LegacyNpcScriptStatefulActionState(
                "NobilityDonorDonation",
                LegacyNpcScriptStatefulActionStep.AmountInput,
                PendingConquerPoints: null));
        var inputAction = Assert.IsType<LegacyNpcScriptStatefulAction>(input.StatefulAction);

        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                NobilityDonorNpcId,
                optionId: 2,
                LegacyNpcReplyInteractType.Option),
            inputAction.NextState);

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.StatefulNpcScript, resolution.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.None, resolution.Reason);
        Assert.Equal("none", resolution.NextSeam);
        Assert.Equal("NobilityDonor", resolution.NpcEnumName);
        Assert.Equal("NpcHandler.NobilityDonor", resolution.SourceMethodName);
        var result = Assert.IsType<LegacyNpcScriptOptionResult>(resolution.Result);
        Assert.Equal(LegacyNpcScriptOptionResultKind.PendingMutation, result.Kind);
        var mutation = Assert.IsType<LegacyNpcScriptPendingMutation>(result.PendingMutation);
        Assert.Equal(LegacyNpcScriptPendingMutationKind.NobilityDonation, mutation.Kind);
        Assert.Equal(400_000U, mutation.ConquerPoints);
        Assert.Equal(200_000_000UL, mutation.Donation);
        Assert.Equal("client.Player.ConquerPoints", mutation.CurrencySource);
        Assert.Equal("client.Player.Nobility.Donation", mutation.MutationTarget);
        var statefulAction = Assert.IsType<LegacyNpcScriptStatefulAction>(resolution.StatefulAction);
        Assert.Equal(LegacyNpcScriptStatefulActionStep.PendingMutation, statefulAction.Step);
        Assert.Equal(LegacyNpcScriptStatefulActionStep.None, statefulAction.NextState.Step);
        Assert.Same(result, statefulAction.Result);
        Assert.Contains("client.Player.Nobility.Donation += num", resolution.SourceEvidence);
        Assert.Contains("Pool.NobilityRanking.UpdateRank", resolution.SourceEvidence);
        Assert.Contains("live NobilityDonor packet path rechecks CP", resolution.SourceEvidence);
        Assert.Contains("kind=PendingMutation", resolution.DescribeForLog());
        Assert.Contains("donation=200000000", resolution.DescribeForLog());
        Assert.Contains("category=statefulNpcScript", resolution.DescribeForLog());
    }

    [Fact]
    public void ResolveNobilityDonorConfirmationWithoutPendingAmountRemainsFencedWithParityGap()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                NobilityDonorNpcId,
                optionId: 2,
                LegacyNpcReplyInteractType.Option));

        Assert.True(resolution.IsUnsupported);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.StatefulNpcScript, resolution.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.StatefulSourceAction, resolution.Reason);
        Assert.Equal("wire-stateful-npc-script-action", resolution.NextSeam);
        Assert.Equal("NobilityDonor", resolution.NpcEnumName);
        Assert.Equal("NpcHandler.NobilityDonor", resolution.SourceMethodName);
        var result = Assert.IsType<LegacyNpcScriptOptionResult>(resolution.Result);
        Assert.Equal(LegacyNpcScriptOptionResultKind.Unsupported, result.Kind);
        var statefulAction = Assert.IsType<LegacyNpcScriptStatefulAction>(resolution.StatefulAction);
        Assert.Equal(LegacyNpcScriptStatefulActionStep.UnsupportedMutationPersistence, statefulAction.Step);
        Assert.Equal(LegacyNpcScriptStatefulActionStep.UnsupportedMutationPersistence, statefulAction.NextState.Step);
        Assert.Null(statefulAction.NextState.PendingConquerPoints);
        Assert.Contains("requires the source NobilityCps amount", resolution.SourceEvidence);

        var diagnostic = LegacyParityGapDiagnostics.MissingNpcScript(resolution, mapId: 1_002);
        Assert.Equal("missing-npc-script", diagnostic.Gap);
        Assert.Contains("PARITY-GAP category=npc severity=warning gap=missing-npc-script", diagnostic.Message);
        Assert.Contains("npcId=2541", diagnostic.Message);
        Assert.Contains("optionId=2", diagnostic.Message);
        Assert.Contains("source=NpcHandler.NobilityDonor", diagnostic.Message);
        Assert.Contains("nextSeam=wire-stateful-npc-script-action", diagnostic.Message);
        Assert.Contains("unsupportedReason=StatefulSourceAction", diagnostic.Message);
    }

    [Fact]
    public void ResolveNobilityDonorCancelCloseClearsStatefulPathWithoutMutation()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                NobilityDonorNpcId,
                optionId: LegacyNpcReplyPacket.ClosedOptionId,
                LegacyNpcReplyInteractType.Option),
            new LegacyNpcScriptStatefulActionState(
                "NobilityDonorDonation",
                LegacyNpcScriptStatefulActionStep.Confirmation,
                400_000));

        Assert.True(resolution.IsIgnored);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.CloseDialog, resolution.ActionCategory);
        var statefulAction = Assert.IsType<LegacyNpcScriptStatefulAction>(resolution.StatefulAction);
        Assert.Equal(LegacyNpcScriptStatefulActionStep.CancelClose, statefulAction.Step);
        Assert.Equal(LegacyNpcScriptStatefulActionStep.None, statefulAction.NextState.Step);
        Assert.NotNull(statefulAction.Result);
        Assert.Equal(LegacyNpcScriptOptionResultKind.NoOp, statefulAction.Result.Kind);
        Assert.Contains("abandon", statefulAction.SourceEvidence);
    }

    [Fact]
    public void ResolveIronTongInitialClickReturnsSourceBackedDialog()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                IronTongNpcId,
                optionId: 0,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.Dialog, resolution.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.None, resolution.Reason);
        Assert.Equal("IronTong", resolution.NpcEnumName);
        Assert.Equal("NpcHandler.IronTong", resolution.SourceMethodName);
        Assert.Equal("none", resolution.NextSeam);
        Assert.NotNull(resolution.Dialog);
        Assert.Equal((ushort)55, resolution.Dialog.AvatarId);
        Assert.Equal(4, resolution.Dialog.Lines.Count);
        Assert.Contains("Iron Tong", resolution.Dialog.Lines[0]);
        Assert.Contains("defend Dragon Island", resolution.Dialog.Lines[3]);
        Assert.Collection(
            resolution.Dialog.Options,
            option =>
            {
                Assert.Equal("Send~me~to~Dragon~Island.", option.Text);
                Assert.Equal(1, option.OptionId);
            },
            option =>
            {
                Assert.Equal("I~will~come~back~later.", option.Text);
                Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
            });
        Assert.Contains("NpcID.cs:112 IronTong = 20499u", resolution.SourceEvidence);
        Assert.Contains("dialogLines=4", resolution.DescribeForLog());
        Assert.DoesNotContain("missingRuntimeDefinition", resolution.DescribeForLog());

        Assert.True(LegacyNpcScriptRuntime.TryGetSourceHint(IronTongNpcId, out var npcEnumName, out var sourceMethodName));
        Assert.Equal("IronTong", npcEnumName);
        Assert.Equal("NpcHandler.IronTong", sourceMethodName);
    }

    [Fact]
    public void ResolveIronTongTeleportOptionReturnsDragonIslandTeleport()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                IronTongNpcId,
                optionId: 1,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.Teleport, resolution.ActionCategory);
        Assert.NotNull(resolution.Result);
        Assert.Equal(LegacyNpcScriptOptionResultKind.Teleport, resolution.Result.Kind);
        Assert.Equal(10_137U, resolution.Result.MapId);
        Assert.Equal(95U, resolution.Result.X);
        Assert.Equal(409U, resolution.Result.Y);
        Assert.Contains("client.Teleport(95, 409, 10137u)", resolution.SourceEvidence);
        Assert.Contains("kind=Teleport map=10137 x=95 y=409", resolution.DescribeForLog());
    }

    [Fact]
    public void ResolveIronTongUnknownOptionReportsMissingOptionNotMissingDefinition()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                IronTongNpcId,
                optionId: 99,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsUnsupported);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.UnknownOption, resolution.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.MissingOption, resolution.Reason);
        Assert.Equal("IronTong", resolution.NpcEnumName);
        Assert.Equal("NpcHandler.IronTong", resolution.SourceMethodName);
        Assert.Equal("model-npc-script-definition", resolution.NextSeam);
        Assert.Contains("option 99", resolution.SourceEvidence);
        Assert.DoesNotContain("missingRuntimeDefinition", resolution.DescribeForLog());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ResolveBlackSmithShopOptionsReturnSourceBackedShop(byte optionId)
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                BlackSmithNpcId,
                optionId,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.Shop, resolution.ActionCategory);
        Assert.Equal("BlackSmith", resolution.NpcEnumName);
        Assert.Equal("NpcHandler.BlackSmith", resolution.SourceMethodName);
        Assert.NotNull(resolution.Result);
        Assert.Equal(LegacyNpcScriptOptionResultKind.Shop, resolution.Result.Kind);
        Assert.Equal(32U, resolution.Result.ShopId);
        Assert.Contains("ActionQuery Type=160 dwParam=32", resolution.SourceEvidence);
        Assert.DoesNotContain("missingRuntimeDefinition", resolution.DescribeForLog());
    }

    [Theory]
    [InlineData(2, "Most weapons and equipments are upgrade, you can buy them here.", "Okay", 3)]
    [InlineData(5, "What a pity. I cannot own a good weapon!", "Okay!", 4)]
    public void ResolveBlackSmithSafeQuestContinuationsReturnSourceBackedDialogs(
        byte optionId,
        string expectedText,
        string expectedOptionText,
        byte expectedNextOption)
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                BlackSmithNpcId,
                optionId,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.Dialog, resolution.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.None, resolution.Reason);
        Assert.Equal("BlackSmith", resolution.NpcEnumName);
        Assert.Equal("NpcHandler.BlackSmith", resolution.SourceMethodName);
        Assert.Equal("none", resolution.NextSeam);
        Assert.NotNull(resolution.Dialog);
        Assert.Equal((ushort)10, resolution.Dialog.AvatarId);
        Assert.Contains(expectedText, Assert.Single(resolution.Dialog.Lines));
        var option = Assert.Single(resolution.Dialog.Options);
        Assert.Equal(expectedOptionText, option.Text);
        Assert.Equal(expectedNextOption, option.OptionId);
        Assert.Equal(LegacyNpcScriptOptionResultKind.Unsupported, option.Result.Kind);
        Assert.Contains($"option {optionId}", resolution.SourceEvidence);
        Assert.Contains("QuestGUI-gated", resolution.SourceEvidence);
        Assert.Contains("dialogLines=1", resolution.DescribeForLog());
        Assert.DoesNotContain("PARITY-GAP", resolution.DescribeForLog());
    }

    [Theory]
    [InlineData(3, "GainExpBall(500.0)", "class weapon item")]
    [InlineData(4, "QuestGUI.Accept", "SendAutoPatcher")]
    public void ResolveBlackSmithUnsafeQuestOptionsRemainFencedWithParityGap(
        byte optionId,
        string expectedEvidence,
        string expectedDiagnosticFact)
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                BlackSmithNpcId,
                optionId,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsUnsupported);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.StatefulNpcScript, resolution.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.StatefulSourceAction, resolution.Reason);
        Assert.Equal("NpcHandler.BlackSmith", resolution.SourceMethodName);
        Assert.Equal("wire-stateful-npc-script-action", resolution.NextSeam);
        Assert.Contains(expectedEvidence, resolution.SourceEvidence);

        var diagnostic = LegacyParityGapDiagnostics.MissingNpcScript(resolution, mapId: 1_002);
        Assert.Equal(LegacyParityGapCategory.Npc, diagnostic.Category);
        Assert.Equal("missing-npc-script", diagnostic.Gap);
        Assert.Contains("PARITY-GAP category=npc severity=warning gap=missing-npc-script", diagnostic.Message);
        Assert.Contains("npcId=5", diagnostic.Message);
        Assert.Contains($"optionId={optionId}", diagnostic.Message);
        Assert.Contains("source=NpcHandler.BlackSmith", diagnostic.Message);
        Assert.Contains("unsupportedReason=StatefulSourceAction", diagnostic.Message);
        Assert.Contains(expectedDiagnosticFact, resolution.SourceEvidence);
    }

    [Fact]
    public void ResolveBlackSmithUnknownOptionReportsMissingOptionNotMissingDefinition()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                BlackSmithNpcId,
                optionId: 99,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsUnsupported);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.UnknownOption, resolution.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.MissingOption, resolution.Reason);
        Assert.Equal("BlackSmith", resolution.NpcEnumName);
        Assert.Equal("NpcHandler.BlackSmith", resolution.SourceMethodName);
        Assert.DoesNotContain("missingRuntimeDefinition", resolution.DescribeForLog());
    }

    [Fact]
    public void ResolveWarehouseTwinInitialClickReturnsSourceBackedDialog()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                WarehouseTwinNpcId,
                optionId: 0,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.Dialog, resolution.ActionCategory);
        Assert.Equal("WHTwin", resolution.NpcEnumName);
        Assert.Equal("NpcHandler.Warehause", resolution.SourceMethodName);
        Assert.NotNull(resolution.Dialog);
        Assert.Equal((ushort)10, resolution.Dialog.AvatarId);
        Assert.Single(resolution.Dialog.Lines);
        Assert.Equal(2, resolution.Dialog.Options.Count);
        Assert.Contains("most reliable man in Twin City", resolution.Dialog.Lines[0]);
        Assert.DoesNotContain("missingRuntimeDefinition", resolution.DescribeForLog());
    }

    [Theory]
    [InlineData(WarehouseTwinNpcId, 1, "WHTwin")]
    [InlineData(WarehouseMarketNpcId, 0, "WHMarket")]
    [InlineData(WarehouseMarketNpcId, 1, "WHMarket")]
    public void ResolveWarehouseOpenOptionsReturnSourceBackedOpenUi(uint npcId, byte optionId, string npcEnumName)
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                npcId,
                optionId,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsModeled);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.OpenUi, resolution.ActionCategory);
        Assert.Equal(npcEnumName, resolution.NpcEnumName);
        Assert.Equal("NpcHandler.Warehause", resolution.SourceMethodName);
        Assert.NotNull(resolution.Result);
        Assert.Equal(LegacyNpcScriptOptionResultKind.OpenUi, resolution.Result.Kind);
        Assert.Equal(4U, resolution.Result.UiId);
        Assert.Contains("ActionQuery Type=126 dwParam=4", resolution.SourceEvidence);
        Assert.DoesNotContain("missingRuntimeDefinition", resolution.DescribeForLog());
    }

    [Theory]
    [InlineData(2, "does not mutate quest or experience state")]
    [InlineData(3, "does not mutate quest state")]
    [InlineData(10, "must not grant items without inventory and quest persistence")]
    public void ResolveWarehouseTwinQuestOptionsReportStatefulSourceAction(byte optionId, string expectedEvidence)
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                WarehouseTwinNpcId,
                optionId,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsUnsupported);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.StatefulNpcScript, resolution.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.StatefulSourceAction, resolution.Reason);
        Assert.Equal("NpcHandler.Warehause", resolution.SourceMethodName);
        Assert.Equal("wire-stateful-npc-script-action", resolution.NextSeam);
        Assert.Contains(expectedEvidence, resolution.SourceEvidence);
    }

    [Fact]
    public void ResolveUnknownNpcReportsSourceTriageReason()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                999_001,
                optionId: 0,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsUnsupported);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.UnknownSourceScript, resolution.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.MissingSourceScript, resolution.Reason);
        Assert.Null(resolution.NpcEnumName);
        Assert.Null(resolution.SourceMethodName);
        Assert.Equal("source-triage", resolution.NextSeam);
        Assert.Contains("No helper catalog entry or bounded source hint", resolution.SourceEvidence);
        Assert.Contains("reason=missingSourceScript", resolution.DescribeForLog());
    }

    [Fact]
    public void ResolveKnownDefinitionUnknownOptionReportsMissingOption()
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                VipQuestNpcId,
                optionId: 99,
                LegacyNpcReplyInteractType.Dialog));

        Assert.True(resolution.IsUnsupported);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.UnknownOption, resolution.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.MissingOption, resolution.Reason);
        Assert.Equal("NpcHandler.VipQuest", resolution.SourceMethodName);
        Assert.Equal("model-npc-script-definition", resolution.NextSeam);
        Assert.Contains("option 99 is not modeled", resolution.SourceEvidence);
        Assert.Contains("category=unknownOption", resolution.DescribeForLog());
    }

    [Theory]
    [InlineData(LegacyNpcServerAction.PlaceFurniture, LegacyNpcScriptRuntimeActionCategory.FurniturePickup, "Mode.PlaceFurniture")]
    [InlineData(LegacyNpcServerAction.Statue, LegacyNpcScriptRuntimeActionCategory.StatueRemoval, "Mode.Statue")]
    [InlineData(LegacyNpcServerAction.Cursor, LegacyNpcScriptRuntimeActionCategory.CursorAction, "Mode.Cursor")]
    public void ResolveNonScriptServerActionsReportsSourceBackedUnsupportedAction(
        LegacyNpcServerAction action,
        LegacyNpcScriptRuntimeActionCategory expectedCategory,
        string expectedEvidence)
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            new LegacyNpcServerRequestPacket(
                action,
                NpcId: 3_124,
                OptionId: 0,
                InteractType: (byte)LegacyNpcReplyInteractType.Dialog,
                Input: string.Empty));

        Assert.True(resolution.IsUnsupported);
        Assert.Equal(expectedCategory, resolution.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.UnsupportedServerAction, resolution.Reason);
        Assert.Equal("wire-non-script-npc-action", resolution.NextSeam);
        Assert.Contains(expectedEvidence, resolution.SourceEvidence);
    }

    [Theory]
    [InlineData(99, LegacyNpcScriptRuntimeActionCategory.InvalidRequest, LegacyNpcScriptRuntimeUnsupportedReason.InvalidInteractType, "Invalid(99)")]
    [InlineData((byte)LegacyNpcReplyInteractType.MessageBox, LegacyNpcScriptRuntimeActionCategory.MessageBoxCallback, LegacyNpcScriptRuntimeUnsupportedReason.UnsupportedInteractType, "messageBoxCallback")]
    [InlineData((byte)LegacyNpcReplyInteractType.Avatar, LegacyNpcScriptRuntimeActionCategory.InvalidRequest, LegacyNpcScriptRuntimeUnsupportedReason.UnsupportedInteractType, "unsupportedInteractType")]
    public void ResolveUnsupportedInteractTypesReportsReasonAndLogKey(
        byte interactType,
        LegacyNpcScriptRuntimeActionCategory expectedCategory,
        LegacyNpcScriptRuntimeUnsupportedReason expectedReason,
        string expectedLog)
    {
        var resolution = LegacyNpcScriptRuntime.Resolve(
            LegacyNpcServerRequestPacket.Script(
                VipQuestNpcId,
                optionId: 0,
                interactType: interactType));

        Assert.True(resolution.IsUnsupported);
        Assert.Equal(expectedCategory, resolution.ActionCategory);
        Assert.Equal(expectedReason, resolution.Reason);
        Assert.Contains(expectedLog, resolution.DescribeForLog());
    }

    [Fact]
    public void ResolveDialogReplyCloseAndZeroOptionsAreIgnoredWithSourceEvidence()
    {
        var close = LegacyNpcScriptRuntime.Resolve(
            new LegacyNpcDialogRequestPacket(
                NpcId: VipQuestNpcId,
                Mesh: 0,
                OptionId: LegacyNpcReplyPacket.ClosedOptionId,
                InteractType: (byte)LegacyNpcReplyInteractType.Dialog,
                Action: 0,
                Input: string.Empty),
            activeNpcId: 0);

        Assert.True(close.IsIgnored);
        Assert.Equal(LegacyNpcScriptRuntimeActionCategory.CloseDialog, close.ActionCategory);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.ClosedOptionIgnored, close.Reason);
        Assert.Contains("option != byte.MaxValue and option != 0", close.SourceEvidence);

        var zero = LegacyNpcScriptRuntime.Resolve(
            new LegacyNpcDialogRequestPacket(
                NpcId: 0,
                Mesh: 0,
                OptionId: 0,
                InteractType: (byte)LegacyNpcReplyInteractType.Dialog,
                Action: 0,
                Input: string.Empty),
            activeNpcId: VipQuestNpcId);

        Assert.True(zero.IsIgnored);
        Assert.Equal(VipQuestNpcId, zero.NpcId);
        Assert.Equal(LegacyNpcScriptRuntimeUnsupportedReason.ZeroReplyOptionIgnored, zero.Reason);
        Assert.Contains("zeroReplyOptionIgnored", zero.DescribeForLog());
    }
}
