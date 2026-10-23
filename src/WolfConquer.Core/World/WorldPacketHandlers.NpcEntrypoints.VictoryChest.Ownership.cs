using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record VictoryChestView(LegacyVictoryChestPersistence.Binding Binding,
        LegacyVictoryQuestCapture Quests, PlayerQuestSnapshotEntry? Claim);

    private static bool TryCaptureVictoryChestView(WorldPacketHandlerContext context, WorldSession expected,
        out VictoryChestView view, out string reason)
    {
        view = null!;
        if (!LegacyVictoryChestPersistence.TryCaptureBinding(context, expected, out var binding, out reason) ||
            !LegacyVictoryQuestCapture.TryCapture(expected.Character.PlayerQuests, out var quests, out reason)) return false;
        expected.Character.PlayerQuests.TryGetQuest(LegacyDeityLandQuestId, out var claim);
        var captured = new VictoryChestView(binding, quests, claim);
        if (!IsCurrentVictoryChestView(captured)) { reason = "victoryChestQuestOwnerChanged"; return false; }
        view = captured;
        reason = string.Empty;
        return true;
    }

    private static bool IsCurrentVictoryChestView(VictoryChestView view) =>
        view.Quests.IsCurrent(view.Binding.Expected.Character.PlayerQuests) &&
        LegacyVictoryChestPersistence.AttributesCurrent(view.Binding) &&
        LegacyVictoryChestPersistence.IsCurrent(view.Binding, view.Binding.Expected);

    private static bool IsPreparedVictoryChest(VictoryChestPlan plan) =>
        plan.Before.Quests.IsCurrent(plan.Before.Binding.Expected.Character.PlayerQuests) &&
        plan.Quests.IsCurrent(plan.Updated.Character.PlayerQuests) &&
        plan.Inventory.IsCurrent(plan.Updated.PlayerItems) &&
        plan.Files.Inventory.IsCurrent(plan.Before.Binding.Expected.PlayerItems) &&
        LegacyVictoryChestPersistence.GrantOwnersCurrent(plan.Before.Binding);
}
