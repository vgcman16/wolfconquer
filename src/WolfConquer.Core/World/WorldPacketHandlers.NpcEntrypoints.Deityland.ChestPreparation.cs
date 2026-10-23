using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record DeityChestPlan(WorldSession Updated, LegacyDragonSkinInventory Items,
        LegacyDeityChestPersistence.Prepared Files, byte[][] Frames, string Success);

    private static bool TryPrepareDeityChest(WorldPacketHandlerContext context, DeityChestOwner owner,
        string sourceMethod, uint keyItemId, uint rewardItemId, uint cpReward,
        out DeityChestPlan plan, out byte[]? refusal, out string reason)
    {
        plan = null!; refusal = null;
        reason = "deityChestRewardInvalid";
        if (keyItemId != LegacyRareDeitylandChestKeyItemId && keyItemId != LegacyEpicDeitylandChestUnlockKeyItemId ||
            (cpReward == 0 ? !LegacyRareDeitylandChestRewards.Contains(rewardItemId) && !LegacyEpicDeitylandChestRewards.Contains(rewardItemId)
                : cpReward != LegacyEpicDeitylandChestConquerPointReward || rewardItemId != 0)) return false;
        var session = owner.Session;
        if (!LegacyDeityChestPersistence.TryPrepare(session, context.PlayerItemsRoot, owner.Items,
            keyItemId, rewardItemId, cpReward, out var files, out reason)) return false;
        var keyIndex = FindLegacyPositiveDeitylandChestKey(session.PlayerItems, keyItemId);
        if (keyIndex < 0) { reason = "key missing"; return false; }
        if (session.PlayerItems.InventoryItems[keyIndex].DepositedItems.Count != 0)
        { reason = "deityChestKeyHasDeposits"; return false; }
        var frames = new List<byte[]>();
        var items = PrepareLegacyDeitylandChestKeyDebit(session.PlayerItems, keyIndex, frames);
        if (rewardItemId != 0 && !HasLegacyInventorySpace(items, 1))
        {
            refusal = LegacyNpcReplyPacket.ForMessageBox("Please make 1 more space in your inventory.").ToFrameBytes();
            reason = "inventory is full";
            return false;
        }
        WorldSession updated;
        string success;
        if (cpReward != 0)
        {
            if (session.Character.ConquerPoints is < 0 or > uint.MaxValue || cpReward > uint.MaxValue - session.Character.ConquerPoints)
            { reason = "deityChestConquerPointsOverflow"; return false; }
            var character = session.Character with { ConquerPoints = checked(session.Character.ConquerPoints + cpReward) };
            updated = session with { Character = character, PlayerItems = items };
            frames.Add(new LegacyMsgUpdatePacket(checked((uint)character.Uid),
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.ConquerPoints, checked((ulong)character.ConquerPoints))]).ToFrameBytes());
            success = "WorldServer handled LegacyNpc Deityland chest CP reward " +
                $"{FormatSession(context with { Session = updated })} source={sourceMethod} keyItemId={keyItemId} conquerPoints={cpReward}.";
        }
        else
        {
            if (!TryPrepareDeityChestReward(context, items, rewardItemId, out var reward, out reason)) return false;
            items = items with { InventoryItems = items.InventoryItems.Concat([reward]).ToArray() };
            updated = session with { PlayerItems = items };
            frames.Add(LegacyGameItemPacket.FromSnapshotItem(reward).ToFrameBytes());
            var now = DateTime.Now;
            if (LegacyItemExtraPacket.TryCreate(reward, now, out var extra)) frames.Add(extra.ToFrameBytes());
            if (LegacyItemLockPacket.TryCreateUnlockDate(reward, now, out var locked)) frames.Add(locked.ToFrameBytes());
            success = "WorldServer handled LegacyNpc Deityland chest item reward " +
                $"{FormatSession(context with { Session = updated })} source={sourceMethod} keyItemId={keyItemId} itemId={rewardItemId}.";
        }
        if (!LegacyDragonSkinInventory.TryCapture(items, out var after, out reason)) return false;
        plan = new(updated, after, files, frames.ToArray(), success);
        if (IsPreparedDeityChestOwner(context, owner, plan)) { reason = string.Empty; return true; }
        plan = null!;
        reason = "deityChestPreparedOwnerChanged";
        return false;
    }

    private static bool TryPrepareDeityChestReward(WorldPacketHandlerContext context, PlayerItemSnapshot items,
        uint itemId, out PlayerItemSnapshotItem reward, out string reason)
    {
        reward = null!;
        reason = "deityChestRewardMetadataInvalid";
        ushort durability = 0;
        // Missing metadata is an existing optional zero-durability path. A
        // present malformed/foreign row is not evidence for that fallback.
        if (context.WorldData is { } data && data.ItemsById.TryGetValue(checked((int)itemId), out var rows) &&
            (rows is null || rows.Count != 0 && (rows[0] is not { } row || row.Id != itemId ||
                !row.TryGetLegacyDurability(out durability)))) return false;
        reward = CreateLegacyDeitylandChestRewardItem(context, items, itemId);
        if (reward.Durability != durability || reward.MaximumDurability != durability) return false;
        reason = string.Empty;
        return true;
    }
}
