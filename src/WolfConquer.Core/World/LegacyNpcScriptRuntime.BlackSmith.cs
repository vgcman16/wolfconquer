namespace WolfConquer.Core.World;

public static partial class LegacyNpcScriptRuntime
{
    private const uint BlackSmithNpcId = 5;
    private const uint BlackSmithShopGuiId = 32;
    private const string BlackSmithNpcEnumName = "BlackSmith";
    private const string BlackSmithSourceMethodName = "NpcHandler.BlackSmith";
    private const string BlackSmithSourceEvidence =
        "NpcID.cs:469 BlackSmith = 5u; NpcHandler.cs:53123-53230 [Npc(NpcID.BlackSmith)] option 1 and the non-quest option-0 fallback send ActionQuery Type=160 dwParam=32 with ActiveNpc.";
    private const string BlackSmithQuestEvidence =
        "NpcHandler.BlackSmith option 0 has QuestGUI-gated dialog branches at NpcHandler.cs:53133-53155; local runtime keeps option 0 on the existing non-quest shop fallback until player quest state is wired.";
    private const string BlackSmithTeachWeaponsEvidence =
        "NpcHandler.BlackSmith option 2 emits a one-line weapons dialog with option 3 and AddAvatar(10) at NpcHandler.cs:53156-53159.";
    private const string BlackSmithGiftPromptEvidence =
        "NpcHandler.BlackSmith option 5 emits the weapon-gift prompt with option 4 and AddAvatar(10) at NpcHandler.cs:53209-53212.";
    private const string BlackSmithRewardEvidence =
        "NpcHandler.BlackSmith option 3 checks inventory space, calls QuestGUI.FinishQuest, GainExpBall(500.0), and adds a class weapon item before a follow-up dialog at NpcHandler.cs:53160-53208.";
    private const string BlackSmithQuestAcceptEvidence =
        "NpcHandler.BlackSmith option 4 calls QuestGUI.Accept(finishQuest2, 0u) and SendAutoPatcher to route the player to the TC Captain at NpcHandler.cs:53213-53216.";

    private static bool TryResolveBlackSmith(
        uint npcId,
        byte optionId,
        byte interactType,
        LegacyNpcServerAction serverAction,
        string sourceEvidencePrefix,
        out LegacyNpcScriptRuntimeResolution resolution)
    {
        resolution = default!;
        if (npcId != BlackSmithNpcId)
        {
            return false;
        }

        if (optionId is 0 or 1)
        {
            var result = LegacyNpcScriptOptionResult.Shop(
                BlackSmithShopGuiId,
                "NpcHandler.BlackSmith sends ActionQuery Type=160 dwParam=32 for option 1 and the generic option-0 fallback at NpcHandler.cs:53217-53230.");
            resolution = Create(
                LegacyNpcScriptRuntimeStatus.Modeled,
                LegacyNpcScriptRuntimeActionCategory.Shop,
                LegacyNpcScriptRuntimeUnsupportedReason.None,
                BlackSmithNpcId,
                optionId,
                interactType,
                serverAction,
                BlackSmithNpcEnumName,
                BlackSmithSourceMethodName,
                dialog: null,
                result,
                sourceEvidence: string.Concat(sourceEvidencePrefix, " ", BlackSmithSourceEvidence),
                nextSeam: ModeledNextSeam);
            return true;
        }

        if (optionId == 2)
        {
            resolution = CreateBlackSmithDialogResolution(
                optionId,
                interactType,
                serverAction,
                sourceEvidencePrefix,
                dialogText: "Most weapons and equipments are upgrade, you can buy them here.",
                optionText: "Okay",
                nextOptionId: 3,
                dialogEvidence: BlackSmithTeachWeaponsEvidence,
                optionResultEvidence: BlackSmithRewardEvidence);
            return true;
        }

        if (optionId == 5)
        {
            resolution = CreateBlackSmithDialogResolution(
                optionId,
                interactType,
                serverAction,
                sourceEvidencePrefix,
                dialogText: "What a pity. I cannot own a good weapon! But that doesn`t mean you have to be the same! Please take this weapon as a gift. Well",
                optionText: "Okay!",
                nextOptionId: 4,
                dialogEvidence: BlackSmithGiftPromptEvidence,
                optionResultEvidence: BlackSmithQuestAcceptEvidence);
            return true;
        }

        if (optionId is 3 or 4)
        {
            var sourceEvidence = optionId == 3 ? BlackSmithRewardEvidence : BlackSmithQuestAcceptEvidence;
            resolution = Create(
                LegacyNpcScriptRuntimeStatus.Unsupported,
                LegacyNpcScriptRuntimeActionCategory.StatefulNpcScript,
                LegacyNpcScriptRuntimeUnsupportedReason.StatefulSourceAction,
                BlackSmithNpcId,
                optionId,
                interactType,
                serverAction,
                BlackSmithNpcEnumName,
                BlackSmithSourceMethodName,
                dialog: null,
                result: null,
                sourceEvidence: string.Concat(sourceEvidencePrefix, " ", sourceEvidence),
                nextSeam: StatefulSourceActionNextSeam);
            return true;
        }

        resolution = Create(
            LegacyNpcScriptRuntimeStatus.Unsupported,
            LegacyNpcScriptRuntimeActionCategory.UnknownOption,
            LegacyNpcScriptRuntimeUnsupportedReason.MissingOption,
            BlackSmithNpcId,
            optionId,
            interactType,
            serverAction,
            BlackSmithNpcEnumName,
            BlackSmithSourceMethodName,
            dialog: null,
            result: null,
            sourceEvidence: string.Concat(
                sourceEvidencePrefix,
                " ",
                BlackSmithSourceMethodName,
                " is modeled from source, but option ",
                optionId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                " is not present in the decompiled switch. ",
                BlackSmithSourceEvidence),
            nextSeam: ModelDefinitionNextSeam);
        return true;
    }

    private static LegacyNpcScriptRuntimeResolution CreateBlackSmithDialogResolution(
        byte optionId,
        byte interactType,
        LegacyNpcServerAction serverAction,
        string sourceEvidencePrefix,
        string dialogText,
        string optionText,
        byte nextOptionId,
        string dialogEvidence,
        string optionResultEvidence)
    {
        var dialog = new LegacyNpcScriptRuntimeDialog(
            [dialogText],
            [
                new LegacyNpcScriptOptionDefinition(
                    optionText,
                    nextOptionId,
                    LegacyNpcScriptOptionResult.Unsupported(optionResultEvidence)),
            ],
            10,
            dialogEvidence);

        return Create(
            LegacyNpcScriptRuntimeStatus.Modeled,
            LegacyNpcScriptRuntimeActionCategory.Dialog,
            LegacyNpcScriptRuntimeUnsupportedReason.None,
            BlackSmithNpcId,
            optionId,
            interactType,
            serverAction,
            BlackSmithNpcEnumName,
            BlackSmithSourceMethodName,
            dialog,
            result: null,
            sourceEvidence: string.Concat(sourceEvidencePrefix, " ", BlackSmithQuestEvidence, " ", dialogEvidence),
            nextSeam: ModeledNextSeam);
    }
}
