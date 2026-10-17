using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class LegacyNpcScriptRuntime
{
    private const uint NobilityDonorNpcId = 2_541;
    private const string NobilityDonorActionName = "NobilityDonorDonation";
    private const string NobilityDonorOption1ConfirmationEvidence =
        "NpcHandler.NobilityDonor option 1 parses Input with uint.TryParse, checks client.Player.ConquerPoints >= result, stores client.Player.NobilityCps = result, and emits a confirmation dialog at NpcHandler.cs:7869-7879.";
    private const string NobilityDonorOption1RetryEvidence =
        "NpcHandler.NobilityDonor option 1 invalid input emits AddText(\"Please enter the amount again.\")/AddInput(\"CP donation amount\", 1, 16)/close at NpcHandler.cs:7884-7889.";
    private const string NobilityDonorOption2MutationEvidence =
        "NpcHandler.NobilityDonor option 2 subtracts client.Player.NobilityCps from client.Player.ConquerPoints, computes num = NobilityCps * 500, applies client.Player.Nobility.Donation += num, sends NobilityIconCreate, updates Pool.NobilityRanking.UpdateRank, and clears NobilityCps at NpcHandler.cs:7893-7902.";

    private static LegacyNpcScriptStatefulAction? CreateNobilityDonorPromptActionIfMatched(
        LegacyNpcScriptDefinition definition,
        LegacyNpcScriptRuntimeDialog dialog,
        string sourceEvidencePrefix)
    {
        if (definition.NpcId != NobilityDonorNpcId)
        {
            return null;
        }

        var sourceEvidence = string.Concat(
            sourceEvidencePrefix,
            " ",
            definition.SourceEvidence,
            " Stateful helper step prompt maps option 0 to the amount-input state and does not mutate currency.");

        return new LegacyNpcScriptStatefulAction(
            NobilityDonorActionName,
            LegacyNpcScriptStatefulActionStep.Prompt,
            new LegacyNpcScriptStatefulActionState(
                NobilityDonorActionName,
                LegacyNpcScriptStatefulActionStep.AmountInput,
                PendingConquerPoints: null),
            dialog,
            null,
            sourceEvidence,
            ModeledNextSeam);
    }

    private static LegacyNpcScriptStatefulAction? CreateNobilityDonorCancelCloseActionIfMatched(
        uint npcId,
        string sourceEvidencePrefix,
        LegacyNpcScriptStatefulActionState? statefulActionState)
    {
        if (npcId != NobilityDonorNpcId &&
            !IsNobilityDonorState(statefulActionState))
        {
            return null;
        }

        const string sourceEvidence =
            "NpcHandler.NobilityDonor resets client.Player.NobilityCps for non-option-2 requests at NpcHandler.cs:7858-7861; byte.MaxValue close/cancel options therefore abandon the pending donation path without mutation.";

        return new LegacyNpcScriptStatefulAction(
            NobilityDonorActionName,
            LegacyNpcScriptStatefulActionStep.CancelClose,
            LegacyNpcScriptStatefulActionState.None,
            null,
            LegacyNpcScriptOptionResult.NoOp(sourceEvidence),
            string.Concat(sourceEvidencePrefix, " ", sourceEvidence),
            ModeledNextSeam);
    }

    private static LegacyNpcScriptRuntimeResolution CreateNobilityDonorAmountInputResolution(
        LegacyNpcScriptDefinition definition,
        byte optionId,
        byte interactType,
        LegacyNpcServerAction serverAction,
        string input,
        string sourceEvidencePrefix,
        LegacyNpcScriptStatefulActionState? statefulActionState)
    {
        _ = statefulActionState;

        if (!uint.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var conquerPoints))
        {
            var retryDialog = new LegacyNpcScriptRuntimeDialog(
                ["Please enter the amount again."],
                definition.InitialOptions,
                definition.AvatarId,
                NobilityDonorOption1RetryEvidence)
            {
                Inputs =
                [
                    new LegacyNpcScriptInputDefinition(
                        "CP donation amount",
                        1,
                        16,
                        NobilityDonorOption1RetryEvidence),
                ],
            };
            var action = new LegacyNpcScriptStatefulAction(
                NobilityDonorActionName,
                LegacyNpcScriptStatefulActionStep.AmountInput,
                new LegacyNpcScriptStatefulActionState(
                    NobilityDonorActionName,
                    LegacyNpcScriptStatefulActionStep.AmountInput,
                    PendingConquerPoints: null),
                retryDialog,
                null,
                string.Concat(
                    sourceEvidencePrefix,
                    " ",
                    NobilityDonorOption1RetryEvidence,
                    " Helper parsed no uint amount, emits only the retry dialog, keeps the path at amount input, and does not mutate currency or nobility state."),
                ModeledNextSeam);

            return Create(
                LegacyNpcScriptRuntimeStatus.Modeled,
                LegacyNpcScriptRuntimeActionCategory.Dialog,
                LegacyNpcScriptRuntimeUnsupportedReason.None,
                definition.NpcId,
                optionId,
                interactType,
                serverAction,
                definition.NpcEnumName,
                definition.SourceMethodName,
                retryDialog,
                result: null,
                sourceEvidence: action.SourceEvidence,
                nextSeam: ModeledNextSeam,
                statefulAction: action);
        }

        var donation = checked((ulong)conquerPoints * 500UL);
        var confirmationDialog = new LegacyNpcScriptRuntimeDialog(
            [FormatNobilityDonorConfirmationText(conquerPoints, donation)],
            definition.ContinuationOptions,
            definition.AvatarId,
            NobilityDonorOption1ConfirmationEvidence);
        var confirmationState = new LegacyNpcScriptStatefulActionState(
            NobilityDonorActionName,
            LegacyNpcScriptStatefulActionStep.Confirmation,
            conquerPoints);
        var statefulAction = new LegacyNpcScriptStatefulAction(
            NobilityDonorActionName,
            LegacyNpcScriptStatefulActionStep.AmountInput,
            confirmationState,
            confirmationDialog,
            null,
            string.Concat(
                sourceEvidencePrefix,
                " ",
                NobilityDonorOption1ConfirmationEvidence,
                " Helper parsed pendingConquerPoints=",
                conquerPoints.ToString(CultureInfo.InvariantCulture),
                " donation=",
                donation.ToString(CultureInfo.InvariantCulture),
                " and emits only confirmation state without mutating currency or nobility; the live NobilityDonor packet path rechecks CP before accepting the pending donation."),
            ModeledNextSeam);

        return Create(
            LegacyNpcScriptRuntimeStatus.Modeled,
            LegacyNpcScriptRuntimeActionCategory.Dialog,
            LegacyNpcScriptRuntimeUnsupportedReason.None,
            definition.NpcId,
            optionId,
            interactType,
            serverAction,
            definition.NpcEnumName,
            definition.SourceMethodName,
            confirmationDialog,
            result: null,
            sourceEvidence: statefulAction.SourceEvidence,
            nextSeam: ModeledNextSeam,
            statefulAction: statefulAction);
    }

    private static LegacyNpcScriptRuntimeResolution CreateNobilityDonorConfirmationResolution(
        LegacyNpcScriptDefinition definition,
        LegacyNpcScriptOptionDefinition option,
        byte optionId,
        byte interactType,
        LegacyNpcServerAction serverAction,
        string sourceEvidencePrefix,
        LegacyNpcScriptStatefulActionState? statefulActionState)
    {
        if (!TryGetNobilityDonorPendingConquerPoints(statefulActionState, out var conquerPoints))
        {
            var missingStateAction = new LegacyNpcScriptStatefulAction(
                NobilityDonorActionName,
                LegacyNpcScriptStatefulActionStep.UnsupportedMutationPersistence,
                new LegacyNpcScriptStatefulActionState(
                    NobilityDonorActionName,
                    LegacyNpcScriptStatefulActionStep.UnsupportedMutationPersistence,
                    PendingConquerPoints: null),
                null,
                option.Result,
                string.Concat(
                    sourceEvidencePrefix,
                    " ",
                    option.Result.SourceEvidence,
                    " Helper cannot emit a pending mutation because option 2 requires the source NobilityCps amount captured by option 1."),
                StatefulSourceActionNextSeam);

            return Create(
                LegacyNpcScriptRuntimeStatus.Unsupported,
                LegacyNpcScriptRuntimeActionCategory.StatefulNpcScript,
                LegacyNpcScriptRuntimeUnsupportedReason.StatefulSourceAction,
                definition.NpcId,
                optionId,
                interactType,
                serverAction,
                definition.NpcEnumName,
                definition.SourceMethodName,
                dialog: null,
                result: option.Result,
                sourceEvidence: missingStateAction.SourceEvidence,
                nextSeam: StatefulSourceActionNextSeam,
                statefulAction: missingStateAction);
        }

        var pendingMutation = LegacyNpcScriptPendingMutation.NobilityDonation(
            conquerPoints,
            NobilityDonorOption2MutationEvidence);
        var result = LegacyNpcScriptOptionResult.FromPendingMutation(
            pendingMutation,
            string.Concat(
                NobilityDonorOption2MutationEvidence,
                " Helper emits the source-backed mutation contract; the live NobilityDonor packet path rechecks CP, routes the donation through LegacyNobilityDonationRuntime, persists CP/nobility state, and sends the nobility icon packet."));
        var statefulAction = new LegacyNpcScriptStatefulAction(
            NobilityDonorActionName,
            LegacyNpcScriptStatefulActionStep.PendingMutation,
            LegacyNpcScriptStatefulActionState.None,
            null,
            result,
            string.Concat(sourceEvidencePrefix, " ", result.SourceEvidence),
            ModeledNextSeam);

        return Create(
            LegacyNpcScriptRuntimeStatus.Modeled,
            LegacyNpcScriptRuntimeActionCategory.StatefulNpcScript,
            LegacyNpcScriptRuntimeUnsupportedReason.None,
            definition.NpcId,
            optionId,
            interactType,
            serverAction,
            definition.NpcEnumName,
            definition.SourceMethodName,
            dialog: null,
            result: result,
            sourceEvidence: statefulAction.SourceEvidence,
            nextSeam: ModeledNextSeam,
            statefulAction: statefulAction);
    }

    private static bool TryGetNobilityDonorPendingConquerPoints(
        LegacyNpcScriptStatefulActionState? statefulActionState,
        out uint conquerPoints)
    {
        conquerPoints = 0;
        if (statefulActionState is not
            {
                ActionName: NobilityDonorActionName,
                Step: LegacyNpcScriptStatefulActionStep.Confirmation,
                PendingConquerPoints: { } pendingConquerPoints,
            })
        {
            return false;
        }

        conquerPoints = pendingConquerPoints;
        return true;
    }

    private static bool IsNobilityDonorState(LegacyNpcScriptStatefulActionState? statefulActionState)
    {
        return string.Equals(
            statefulActionState?.ActionName,
            NobilityDonorActionName,
            StringComparison.Ordinal);
    }

    private static string FormatNobilityDonorConfirmationText(uint conquerPoints, ulong donation)
    {
        return string.Concat(
            "Are you sure you want to donate ",
            conquerPoints.ToString(CultureInfo.InvariantCulture),
            " CPs? I will count it as ",
            donation.ToString(CultureInfo.InvariantCulture),
            " silver toward nobility.");
    }
}
