using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record VictoryChestPlan(VictoryChestView Before, WorldSession Updated,
        LegacyDragonSkinInventory Inventory, LegacyVictoryQuestCapture Quests,
        LegacyVictoryChestPersistence.Prepared Files, byte[][] Frames, string Persisted, string Success);

    private static bool TryPrepareVictoryChest(WorldPacketHandlerContext context, VictoryChestView view,
        LegacyDragonSkinInventory beforeItems, out VictoryChestPlan plan, out string reason)
    {
        plan = null!;
        reason = "victoryChestClaimChanged";
        var session = view.Binding.Expected;
        if (!IsCurrentVictoryChestView(view) || !IsLegacyVictoryChestObjectiveReady(view.Claim) ||
            !beforeItems.IsCurrent(session.PlayerItems) || !HasLegacyInventorySpace(session.PlayerItems, 1) ||
            !LegacyVictoryChestPersistence.TryPrepare(view.Binding, beforeItems, view.Quests, out var files, out reason)) return false;

        // Preserve the actual source pool/one draw; missing required ownership or
        // capacity has already refused. A pure RNG draw is not rolled back on a
        // later serialization/storage refusal.
        var rewardId = LegacyVictoryChestRewards[Random.Shared.Next(0, LegacyVictoryChestRewards.Length)];
        if (!TryPrepareDeityChestReward(context, session.PlayerItems, rewardId, out var reward, out reason)) return false;
        var items = session.PlayerItems with { InventoryItems = session.PlayerItems.InventoryItems.Concat([reward]).ToArray() };
        if (!LegacyVictoryQuestCapture.TryParseImage(view.Quests.CopyImage(), out var frozen, out reason) ||
            !frozen.TryGetQuest(LegacyDeityLandQuestId, out var frozenClaim)) return false;
        var finished = frozenClaim with { Status = PlayerQuestStatus.Finished };
        var oldRows = session.Character.PlayerQuests.Quests;
        var rows = new PlayerQuestSnapshotEntry[oldRows.Count];
        for (var index = 0; index < rows.Length; index++)
            rows[index] = oldRows[index].Uid == LegacyDeityLandQuestId ? finished : oldRows[index];
        var updated = WithPlayerQuests(session with { PlayerItems = items }, new PlayerQuestSnapshot(rows));
        if (!LegacyDragonSkinInventory.TryCapture(items, out var capturedItems, out reason) ||
            !LegacyVictoryQuestCapture.TryCapture(updated.Character.PlayerQuests, out var capturedQuests, out reason)) return false;
        var frames = new List<byte[]>
        {
            LegacyQuestListPacket.CreateSingleQuestFrame(LegacyQuestListMode.FinishQuest, ToLegacyQuestListItem(finished)),
            LegacyGameItemPacket.FromSnapshotItem(reward).ToFrameBytes(),
        };
        var now = DateTime.Now;
        if (LegacyItemExtraPacket.TryCreate(reward, now, out var extra)) frames.Add(extra.ToFrameBytes());
        if (LegacyItemLockPacket.TryCreateUnlockDate(reward, now, out var locked)) frames.Add(locked.ToFrameBytes());
        var updatedContext = context with { Session = updated };
        var persisted = "WorldServer persisted LegacyPlayerQuests " + $"{FormatSession(updatedContext)} " +
            "source=NpcHandler.VictoryChest action=victory-chest-unlock " +
            $"path={updated.Character.Uid.ToString(CultureInfo.InvariantCulture)}.bin " +
            $"questId={finished.Uid.ToString(CultureInfo.InvariantCulture)} " +
            $"questCount={rows.Length.ToString(CultureInfo.InvariantCulture)}.";
        var success = "WorldServer handled LegacyNpc VictoryChest unlock " + $"{FormatSession(updatedContext)} " +
            $"source=NpcHandler.VictoryChest questId={LegacyDeityLandQuestId} objective0={frozenClaim.Intentions[0]} itemId={rewardId}.";
        var prepared = new VictoryChestPlan(view, updated, capturedItems, capturedQuests, files, frames.ToArray(), persisted, success);
        if (!IsPreparedVictoryChest(prepared)) { reason = "victoryChestPreparedOwnerChanged"; return false; }
        plan = prepared;
        reason = string.Empty;
        return true;
    }
}
