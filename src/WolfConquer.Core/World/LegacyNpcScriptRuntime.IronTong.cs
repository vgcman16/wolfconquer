namespace WolfConquer.Core.World;

public static partial class LegacyNpcScriptRuntime
{
    private const uint IronTongNpcId = 20_499;
    private const ushort IronTongAvatarId = 55;
    private const string IronTongNpcEnumName = "IronTong";
    private const string IronTongSourceMethodName = "NpcHandler.IronTong";
    private const string IronTongSourceEvidence =
        "NpcID.cs:112 IronTong = 20499u; NpcHandler.cs:33358-33375 [Npc(NpcID.IronTong)] option 0 emits four AddText lines, a Dragon Island option, close option, AddAvatar(55), and option 1 client.Teleport(95, 409, 10137u).";
    private const string IronTongTeleportEvidence =
        "NpcHandler.IronTong option 1: client.Teleport(95, 409, 10137u) at NpcHandler.cs:33374.";

    private static readonly string[] IronTongDialogLines =
    [
        "<tip desc=Long#ago,#five#masters#defeated#the#evil#king#and#destroyed#his#twisted#hell#array#on#Dragon#Island.>Iron Tong</tip>:",
        "The evil queen and her army have suddenly attacked Dragon Island.",
        "I fear she has found a way to reactivate that array. We cannot let that happen.",
        "Please join us and help defend Dragon Island.",
    ];

    private static readonly LegacyNpcScriptOptionDefinition[] IronTongInitialOptions =
    [
        new LegacyNpcScriptOptionDefinition(
            "Send~me~to~Dragon~Island.",
            1,
            LegacyNpcScriptOptionResult.Teleport(
                mapId: 10_137,
                x: 95,
                y: 409,
                IronTongTeleportEvidence)),
        new LegacyNpcScriptOptionDefinition(
            "I~will~come~back~later.",
            LegacyNpcReplyPacket.ClosedOptionId,
            LegacyNpcScriptOptionResult.NoOp(
                "NpcHandler.IronTong option byte.MaxValue is the close dialog option at NpcHandler.cs:33370-33372; no source case branch mutates state.")),
    ];

    private static bool TryResolveIronTong(
        uint npcId,
        byte optionId,
        byte interactType,
        LegacyNpcServerAction serverAction,
        string sourceEvidencePrefix,
        out LegacyNpcScriptRuntimeResolution resolution)
    {
        resolution = default!;
        if (npcId != IronTongNpcId)
        {
            return false;
        }

        if (optionId == 0)
        {
            var dialog = new LegacyNpcScriptRuntimeDialog(
                IronTongDialogLines,
                IronTongInitialOptions,
                IronTongAvatarId,
                string.Concat(IronTongSourceMethodName, " option 0 emits AddText/AddOption/AddAvatar/FinalizeDialog. ", IronTongSourceEvidence));

            resolution = Create(
                LegacyNpcScriptRuntimeStatus.Modeled,
                LegacyNpcScriptRuntimeActionCategory.Dialog,
                LegacyNpcScriptRuntimeUnsupportedReason.None,
                IronTongNpcId,
                optionId,
                interactType,
                serverAction,
                IronTongNpcEnumName,
                IronTongSourceMethodName,
                dialog,
                result: null,
                sourceEvidence: string.Concat(sourceEvidencePrefix, " ", IronTongSourceEvidence),
                nextSeam: ModeledNextSeam);
            return true;
        }

        if (optionId == 1)
        {
            var result = LegacyNpcScriptOptionResult.Teleport(10_137, 95, 409, IronTongTeleportEvidence);
            resolution = Create(
                LegacyNpcScriptRuntimeStatus.Modeled,
                LegacyNpcScriptRuntimeActionCategory.Teleport,
                LegacyNpcScriptRuntimeUnsupportedReason.None,
                IronTongNpcId,
                optionId,
                interactType,
                serverAction,
                IronTongNpcEnumName,
                IronTongSourceMethodName,
                dialog: null,
                result,
                sourceEvidence: string.Concat(sourceEvidencePrefix, " ", IronTongTeleportEvidence),
                nextSeam: ModeledNextSeam);
            return true;
        }

        resolution = Create(
            LegacyNpcScriptRuntimeStatus.Unsupported,
            LegacyNpcScriptRuntimeActionCategory.UnknownOption,
            LegacyNpcScriptRuntimeUnsupportedReason.MissingOption,
            IronTongNpcId,
            optionId,
            interactType,
            serverAction,
            IronTongNpcEnumName,
            IronTongSourceMethodName,
            dialog: null,
            result: null,
            sourceEvidence: string.Concat(
                sourceEvidencePrefix,
                " ",
                IronTongSourceMethodName,
                " is modeled from source, but option ",
                optionId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                " is not present in the decompiled switch. ",
                IronTongSourceEvidence),
            nextSeam: ModelDefinitionNextSeam);
        return true;
    }
}
