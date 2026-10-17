using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class LegacyNpcScriptRuntime
{
    private const uint WarehouseTwinNpcId = 8;
    private const uint WarehouseMarketNpcId = 44;
    private const uint LegacyWarehouseUiId = 4;
    private const string WarehouseSourceMethodName = "NpcHandler.Warehause";
    private const string WarehouseSourceEvidence =
        "NpcID.cs:461 WHTwin = 8u; NpcID.cs:462 WHMarket = 44u; Procesor.cs:235-237 routes warehouse ids to NpcHandler.Warehause; NpcHandler.cs:34557-34647 sends ActionQuery Type=126 dwParam=4 for warehouse open.";
    private const string WarehouseTwinQuestEvidence =
        "NpcHandler.Warehause option 0 for WHTwin has QuestGUI branches for Military Letter, Blacksmith routing, and quest experience before the open-warehouse option.";
    private const string WarehouseOpenEvidence =
        "NpcHandler.Warehause option 1 and the non-WHTwin fallback send ActionQuery Type=126 dwParam=4 with ObjId=client.Player.UID and the player's current coordinates.";
    private const string WarehouseTwinReliableOptionEvidence =
        "NpcHandler.Warehause option 2 finishes finishQuest.MissionId, grants 1400 experience with angelwing effect, and emits the Blacksmith route dialog at NpcHandler.cs:34631-34639; local runtime does not mutate quest or experience state.";
    private const string WarehouseTwinBlacksmithRouteEvidence =
        "NpcHandler.Warehause option 3 accepts finishQuest2 and sends QuestGUI.SendAutoPatcher for the Blacksmith route at NpcHandler.cs:34640-34643; local runtime does not mutate quest state.";
    private const string WarehouseTwinMilitaryLetterEvidence =
        "NpcHandler.Warehause option 10 checks inventory space and item 711235, then may add the Military Letter and increase quest objectives at NpcHandler.cs:34597-34614; local runtime must not grant items without inventory and quest persistence.";
    private const string WarehouseTwinMilitaryLetterRouteEvidence =
        "NpcHandler.Warehause option 11 only sends QuestGUI.SendAutoPatcher toward finishQuest3.FinishNpcId at NpcHandler.cs:34615-34617 and does not mutate inventory, quest, warehouse, bank, or sash state.";

    private static bool IsSourceRoutedWarehouseNpc(uint npcId)
    {
        return npcId is 8 or 44 or 7_762 or 10_011 or 10_012 or 10_027 or 10_028;
    }

    private static string GetWarehouseNpcEnumName(uint npcId)
    {
        return npcId switch
        {
            WarehouseTwinNpcId => "WHTwin",
            WarehouseMarketNpcId => "WHMarket",
            _ => string.Concat("Warehause_", npcId.ToString(CultureInfo.InvariantCulture)),
        };
    }

    private static readonly LegacyNpcScriptOptionDefinition[] WarehouseTwinDialogOptions =
    [
        new("Open~my~Warehouse.", 1, LegacyNpcScriptOptionResult.OpenUi(LegacyWarehouseUiId, WarehouseOpenEvidence)),
        new("Thanks.", LegacyNpcReplyPacket.ClosedOptionId, LegacyNpcScriptOptionResult.NoOp("Warehouse close option only closes the source dialog.")),
    ];

    private static bool TryResolveWarehouse(
        uint npcId,
        byte optionId,
        byte interactType,
        LegacyNpcServerAction serverAction,
        string sourceEvidencePrefix,
        out LegacyNpcScriptRuntimeResolution resolution)
    {
        resolution = default!;

        if (!IsSourceRoutedWarehouseNpc(npcId))
        {
            return false;
        }

        var npcEnumName = GetWarehouseNpcEnumName(npcId);

        if (npcId == WarehouseTwinNpcId && optionId == 0)
        {
            var dialog = new LegacyNpcScriptRuntimeDialog(
                ["I am the most reliable man in Twin City! Your valuable things will be safe in my warehouse!"],
                WarehouseTwinDialogOptions,
                AvatarId: 10,
                string.Concat(WarehouseSourceMethodName, " option 0 emits a warehouse dialog in the normal path. ", WarehouseSourceEvidence));

            resolution = Create(
                LegacyNpcScriptRuntimeStatus.Modeled,
                LegacyNpcScriptRuntimeActionCategory.Dialog,
                LegacyNpcScriptRuntimeUnsupportedReason.None,
                npcId,
                optionId,
                interactType,
                serverAction,
                npcEnumName,
                WarehouseSourceMethodName,
                dialog,
                result: null,
                sourceEvidence: string.Concat(sourceEvidencePrefix, " ", WarehouseSourceEvidence),
                nextSeam: ModeledNextSeam);
            return true;
        }

        if ((npcId == WarehouseTwinNpcId && optionId == 1) ||
            (npcId != WarehouseTwinNpcId && optionId is 0 or 1))
        {
            var result = LegacyNpcScriptOptionResult.OpenUi(LegacyWarehouseUiId, WarehouseOpenEvidence);
            resolution = Create(
                LegacyNpcScriptRuntimeStatus.Modeled,
                LegacyNpcScriptRuntimeActionCategory.OpenUi,
                LegacyNpcScriptRuntimeUnsupportedReason.None,
                npcId,
                optionId,
                interactType,
                serverAction,
                npcEnumName,
                WarehouseSourceMethodName,
                dialog: null,
                result,
                sourceEvidence: string.Concat(sourceEvidencePrefix, " ", WarehouseSourceEvidence),
                nextSeam: ModeledNextSeam);
            return true;
        }

        if (npcId == WarehouseTwinNpcId && optionId == 11)
        {
            var result = LegacyNpcScriptOptionResult.NoOp(WarehouseTwinMilitaryLetterRouteEvidence);
            resolution = Create(
                LegacyNpcScriptRuntimeStatus.Modeled,
                LegacyNpcScriptRuntimeActionCategory.NoOp,
                LegacyNpcScriptRuntimeUnsupportedReason.None,
                npcId,
                optionId,
                interactType,
                serverAction,
                npcEnumName,
                WarehouseSourceMethodName,
                dialog: null,
                result,
                sourceEvidence: string.Concat(sourceEvidencePrefix, " ", WarehouseTwinMilitaryLetterRouteEvidence),
                nextSeam: ModeledNextSeam);
            return true;
        }

        if (npcId == WarehouseTwinNpcId && optionId is 2 or 3 or 10)
        {
            resolution = Create(
                LegacyNpcScriptRuntimeStatus.Unsupported,
                LegacyNpcScriptRuntimeActionCategory.StatefulNpcScript,
                LegacyNpcScriptRuntimeUnsupportedReason.StatefulSourceAction,
                npcId,
                optionId,
                interactType,
                serverAction,
                npcEnumName,
                WarehouseSourceMethodName,
                dialog: null,
                result: null,
                sourceEvidence: string.Concat(
                    sourceEvidencePrefix,
                    " ",
                    GetWarehouseTwinStatefulSourceEvidence(optionId),
                    " option=",
                    optionId.ToString(CultureInfo.InvariantCulture),
                    "."),
                nextSeam: StatefulSourceActionNextSeam);
            return true;
        }

        resolution = Create(
            LegacyNpcScriptRuntimeStatus.Unsupported,
            LegacyNpcScriptRuntimeActionCategory.UnknownOption,
            LegacyNpcScriptRuntimeUnsupportedReason.MissingOption,
            npcId,
            optionId,
            interactType,
            serverAction,
            npcEnumName,
            WarehouseSourceMethodName,
            dialog: null,
            result: null,
            sourceEvidence: string.Concat(
                sourceEvidencePrefix,
                " ",
                WarehouseSourceMethodName,
                " has a source handler, but option ",
                optionId.ToString(CultureInfo.InvariantCulture),
                " is not modeled. ",
                WarehouseSourceEvidence),
            nextSeam: ModelDefinitionNextSeam);
        return true;
    }

    private static string GetWarehouseTwinStatefulSourceEvidence(byte optionId)
    {
        return optionId switch
        {
            2 => WarehouseTwinReliableOptionEvidence,
            3 => WarehouseTwinBlacksmithRouteEvidence,
            10 => WarehouseTwinMilitaryLetterEvidence,
            _ => WarehouseTwinQuestEvidence,
        };
    }
}
