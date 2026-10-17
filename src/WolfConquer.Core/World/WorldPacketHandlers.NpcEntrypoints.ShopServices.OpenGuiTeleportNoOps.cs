namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly LegacyShopServiceOpenGui[] LegacyShopServiceOpenGuiNpcs =
    [
        new(LegacyTheStorekeeperNpcId, "NpcHandler.TheStorekeeper", "53328-53419", UseMapIdField: true),
        new(LegacyPharmacistNpcId, "NpcHandler.Pharmacist", "53233-53326", UseMapIdField: false),
        new(LegacyArmorerNpcId, "NpcHandler.Armorer", "53420-53530", UseMapIdField: true),
    ];

    private static readonly LegacyShopServiceTeleport[] LegacyShopServiceTeleports =
    [
        new(
            LegacyFurnitureStoreNpcId,
            OptionId: 1,
            new LegacyNpcTeleportDestination(1_511, 52, 70, "NpcHandler.FurnitureStore"),
            "38431-38455"),
    ];

    private static readonly LegacyShopServiceNoOp[] LegacyShopServiceNoOps =
    [
        new(LegacyTinterNpcId, "NpcHandler.Tinter", "38457-38527", [10, 11, 12]),
        new(LegacyPharmacistNpcId, "NpcHandler.Pharmacist", "53233-53326", [4]),
        new(LegacyTheStorekeeperNpcId, "NpcHandler.TheStorekeeper", "53328-53419", [7]),
        new(LegacyArmorerNpcId, "NpcHandler.Armorer", "53420-53530", [8, 9]),
        new(LegacySurgeonMiracleNpcId, "NpcHandler.SurgeonMiracle", "38636-38680", [0]),
    ];

    private static bool TryGetLegacyShopServiceOpenGui(
        uint npcId,
        byte optionId,
        out LegacyShopServiceOpenGui openGui)
    {
        openGui = default!;
        if (optionId is not 0 and not 1)
        {
            return false;
        }

        foreach (var candidate in LegacyShopServiceOpenGuiNpcs)
        {
            if (candidate.NpcId == npcId)
            {
                openGui = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetLegacyShopServiceTeleport(
        uint npcId,
        byte optionId,
        out LegacyShopServiceTeleport teleport)
    {
        teleport = default!;
        foreach (var candidate in LegacyShopServiceTeleports)
        {
            if (candidate.NpcId == npcId && candidate.OptionId == optionId)
            {
                teleport = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetLegacyShopServiceNoOp(
        uint npcId,
        byte optionId,
        out LegacyShopServiceNoOp noOp)
    {
        noOp = default!;
        foreach (var candidate in LegacyShopServiceNoOps)
        {
            if (candidate.NpcId == npcId && ContainsOption(candidate.OptionIds, optionId))
            {
                noOp = candidate;
                return true;
            }
        }

        return false;
    }

    private sealed record LegacyShopServiceOpenGui(
        uint NpcId,
        string SourceMethod,
        string SourceLines,
        bool UseMapIdField);

    private sealed record LegacyShopServiceTeleport(
        uint NpcId,
        byte OptionId,
        LegacyNpcTeleportDestination Destination,
        string SourceLines);

    private sealed record LegacyShopServiceNoOp(
        uint NpcId,
        string SourceMethod,
        string SourceLines,
        byte[] OptionIds);
}
