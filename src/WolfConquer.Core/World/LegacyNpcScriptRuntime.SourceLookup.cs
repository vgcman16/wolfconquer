namespace WolfConquer.Core.World;

public static partial class LegacyNpcScriptRuntime
{
    private static readonly IReadOnlyDictionary<uint, LegacyNpcScriptSourceHint> SourceHints =
        new Dictionary<uint, LegacyNpcScriptSourceHint>
        {
            [15_641] = new(
                "Teleportall",
                "NpcHandler.Teleportall",
                LegacyNpcScriptRuntimeActionCategory.KnownSourceScript,
                "NpcID.cs:66 Teleportall = 15641u; NpcHandler.cs:4597-4698 [Npc(NpcID.Teleportall)] models option 0 dialog, option 4 submenu, and teleport options."),
            [20_499] = new(
                "IronTong",
                "NpcHandler.IronTong",
                LegacyNpcScriptRuntimeActionCategory.KnownSourceScript,
                "NpcID.cs:112 IronTong = 20499u; NpcHandler.cs:33358-33375 [Npc(NpcID.IronTong)] models option 0 dialog and option 1 client.Teleport(95, 409, 10137u)."),
            [20_508] = new(
                "DragonIslandConductress",
                "NpcHandler.DragonIslandConductress",
                LegacyNpcScriptRuntimeActionCategory.KnownSourceScript,
                "NpcID.cs:178 DragonIslandConductress = 20508; NpcHandler.cs:86688-86727 [Npc(NpcID.DragonIslandConductress)] models option 0 dialog and teleports options 1, 20, 2, and 3."),
            [43_433] = new(
                "joinJopCentr",
                "NpcHandler.joinJopCentr",
                LegacyNpcScriptRuntimeActionCategory.KnownSourceScript,
                "NpcID.cs:36 joinJopCentr = 43433u; NpcHandler.cs:524-574 [Npc(NpcID.joinJopCentr)] models option 0 dialog and option 1 class-dependent teleports."),
            [5] = new(
                "BlackSmith",
                "NpcHandler.BlackSmith",
                LegacyNpcScriptRuntimeActionCategory.KnownSourceScript,
                "NpcID.cs:469 BlackSmith = 5u; NpcHandler.cs:53123-53138 [Npc(NpcID.BlackSmith)] starts BlackSmith dialog, with option 1 shop/open behavior later in the method."),
            [8] = new(
                "WHTwin",
                "NpcHandler.Warehause",
                LegacyNpcScriptRuntimeActionCategory.KnownSourceScript,
                "NpcID.cs:461 WHTwin = 8u; Procesor.cs:235-237 routes warehouse ids to NpcHandler.Warehause; NpcHandler.cs:34557 starts Warehause."),
            [44] = new(
                "WHMarket",
                "NpcHandler.Warehause",
                LegacyNpcScriptRuntimeActionCategory.KnownSourceScript,
                "NpcID.cs:462 WHMarket = 44u; Procesor.cs:235-237 routes warehouse ids to NpcHandler.Warehause; NpcHandler.cs:34557 starts Warehause."),
            [7_762] = new(
                "Warehause_7762",
                "NpcHandler.Warehause",
                LegacyNpcScriptRuntimeActionCategory.KnownSourceScript,
                "Procesor.cs:235-237 routes npc id 7762 to NpcHandler.Warehause; NpcHandler.cs:34557-34647 opens the warehouse for non-WHTwin warehouse NPCs."),
            [10_011] = new(
                "Warehause_10011",
                "NpcHandler.Warehause",
                LegacyNpcScriptRuntimeActionCategory.KnownSourceScript,
                "Procesor.cs:235-237 routes npc id 10011 to NpcHandler.Warehause; NpcHandler.cs:34557-34647 opens the warehouse for non-WHTwin warehouse NPCs."),
            [10_012] = new(
                "Warehause_10012",
                "NpcHandler.Warehause",
                LegacyNpcScriptRuntimeActionCategory.KnownSourceScript,
                "Procesor.cs:235-237 routes npc id 10012 to NpcHandler.Warehause; NpcHandler.cs:34557-34647 opens the warehouse for non-WHTwin warehouse NPCs."),
            [10_027] = new(
                "Warehause_10027",
                "NpcHandler.Warehause",
                LegacyNpcScriptRuntimeActionCategory.KnownSourceScript,
                "Procesor.cs:235-237 routes npc id 10027 to NpcHandler.Warehause; NpcHandler.cs:34557-34647 opens the warehouse for non-WHTwin warehouse NPCs."),
            [10_028] = new(
                "Warehause_10028",
                "NpcHandler.Warehause",
                LegacyNpcScriptRuntimeActionCategory.KnownSourceScript,
                "Procesor.cs:235-237 routes npc id 10028 to NpcHandler.Warehause; NpcHandler.cs:34557-34647 opens the warehouse for non-WHTwin warehouse NPCs."),
        };

    public static bool TryGetSourceHint(uint npcId, out string npcEnumName, out string sourceMethodName)
    {
        if (LegacyNpcScriptDefinition.TryGetByNpcId(npcId, out var definition))
        {
            npcEnumName = definition.NpcEnumName;
            sourceMethodName = definition.SourceMethodName;
            return true;
        }

        if (SourceHints.TryGetValue(npcId, out var hint))
        {
            npcEnumName = hint.NpcEnumName;
            sourceMethodName = hint.SourceMethodName;
            return true;
        }

        npcEnumName = string.Empty;
        sourceMethodName = string.Empty;
        return false;
    }

    private static LegacyNpcScriptRuntimeResolution ResolveMissingDefinition(
        uint npcId,
        byte optionId,
        byte interactType,
        LegacyNpcServerAction serverAction,
        string sourceEvidencePrefix)
    {
        if (SourceHints.TryGetValue(npcId, out var hint))
        {
            return Create(
                LegacyNpcScriptRuntimeStatus.Unsupported,
                hint.ActionCategory,
                LegacyNpcScriptRuntimeUnsupportedReason.MissingRuntimeDefinition,
                npcId,
                optionId,
                interactType,
                serverAction,
                hint.NpcEnumName,
                hint.SourceMethodName,
                dialog: null,
                result: null,
                sourceEvidence: string.Concat(
                    sourceEvidencePrefix,
                    " Source has a legacy handler, but LegacyNpcScriptDefinition does not model it yet. ",
                    hint.SourceEvidence),
                nextSeam: ModelDefinitionNextSeam);
        }

        return Create(
            LegacyNpcScriptRuntimeStatus.Unsupported,
            LegacyNpcScriptRuntimeActionCategory.UnknownSourceScript,
            LegacyNpcScriptRuntimeUnsupportedReason.MissingSourceScript,
            npcId,
            optionId,
            interactType,
            serverAction,
            sourceEvidence: string.Concat(
                sourceEvidencePrefix,
                " No helper catalog entry or bounded source hint exists for this NPC id."),
            nextSeam: SourceTriageNextSeam);
    }

    private sealed record LegacyNpcScriptSourceHint(
        string NpcEnumName,
        string SourceMethodName,
        LegacyNpcScriptRuntimeActionCategory ActionCategory,
        string SourceEvidence);
}
