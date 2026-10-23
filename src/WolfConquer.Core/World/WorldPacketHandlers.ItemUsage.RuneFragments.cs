using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyDirectRuneFragmentCombineCount = 20;
    private const uint LegacyDirectYellowRuneFragmentFallbackItemId = 4_032_001;
    private const string LegacyDirectRuneFragmentSource = "MsgItemUsuagePacket.UseItem";
    private const string LegacyDirectRuneFragmentLines = "4260-4362";

    private static async ValueTask HandleLegacyDirectRuneFragmentUseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerItemSnapshotItem item,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyDirectRuneFragmentSpec(item.ItemId, out var spec))
        {
            return;
        }

        if (CountLegacyRuneFragmentsForCombine(session.PlayerItems, item.ItemId, spec.RequiredBound) <
            LegacyDirectRuneFragmentCombineCount)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(
                            $"You don't have {LegacyDirectRuneFragmentCombineCount} {spec.FragmentName}{(spec.RewardBound ? "(B)" : "")}!.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage direct rune fragment use " +
                    $"{FormatSession(context)} source={LegacyDirectRuneFragmentSource} " +
                    $"sourceLines={LegacyDirectRuneFragmentLines} itemUid={item.Uid} itemId={item.ItemId} " +
                    $"requiredBound={spec.RequiredBound}; fragments missing.")
                .ConfigureAwait(false);
            return;
        }

        if (!TrySelectLegacyDirectRuneFragmentReward(
                context,
                session,
                spec.EquipmentPosition,
                out var rewardItemType,
                out var poolIndex,
                out var pendingReason))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP MsgItemUsage direct rune fragment use " +
                    $"{FormatSession(context)} source={LegacyDirectRuneFragmentSource} " +
                    $"sourceLines={LegacyDirectRuneFragmentLines} itemUid={item.Uid} itemId={item.ItemId}; {pendingReason}")
                .ConfigureAwait(false);
            return;
        }

        var selectedRewardItemId = CheckedUInt32(rewardItemType!.Id, nameof(rewardItemType.Id));
        var rewardItemId = spec.IsYellow &&
            IsLegacyDirectYellowRuneFragmentBlacklistedReward(selectedRewardItemId)
                ? LegacyDirectYellowRuneFragmentFallbackItemId
                : selectedRewardItemId;
        if (rewardItemId != selectedRewardItemId &&
            !TryReadItemType(context.WorldData, rewardItemId, out rewardItemType))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP MsgItemUsage direct rune fragment use " +
                    $"{FormatSession(context)} source={LegacyDirectRuneFragmentSource} " +
                    $"sourceLines={LegacyDirectRuneFragmentLines} itemUid={item.Uid} itemId={item.ItemId}; " +
                    $"yellow fallback item {rewardItemId} is missing from itemtype catalog.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryConsumeLegacyBoundInventoryItemCount(
                session.PlayerItems,
                item.ItemId,
                spec.RequiredBound,
                LegacyDirectRuneFragmentCombineCount,
                out var updatedItems,
                out var consumeFrames))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP MsgItemUsage direct rune fragment use " +
                    $"{FormatSession(context)} source={LegacyDirectRuneFragmentSource} " +
                    $"sourceLines={LegacyDirectRuneFragmentLines} itemUid={item.Uid} itemId={item.ItemId}; " +
                    "source inventory count passed but local consume could not remove 20 matching fragments.")
                .ConfigureAwait(false);
            return;
        }

        var rewardItem = CreatePurchasedItems(
            updatedItems,
            rewardItemType!,
            count: 1,
            isBound: spec.RewardBound)[0];
        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems.Concat([rewardItem]).ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await TryPersistLegacyItemUsageMutationAsync(
                updatedContext,
                updatedSession,
                "direct rune fragment use",
                characterValues: null,
                persistItems: true)
            .ConfigureAwait(false);

        foreach (var frame in consumeFrames)
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(
                        rewardItem,
                        LegacyGameItemPacket.CreateRuneSpellOverride(rewardItem, context.WorldData?.ItemsById),
                        LegacyGameItemPacket.AddItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket
                    .SystemTalk(
                        "You have got 1 " + rewardItemType!.Name + (spec.RewardBound ? "(B)!." : "!."))
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage direct rune fragment use " +
                $"{FormatSession(updatedContext)} source={LegacyDirectRuneFragmentSource} " +
                $"sourceLines={LegacyDirectRuneFragmentLines} itemUid={item.Uid} itemId={item.ItemId} " +
                $"requiredBound={spec.RequiredBound} rewardItemId={rewardItem.ItemId} " +
                $"selectedRewardItemId={selectedRewardItemId} poolIndex={poolIndex} bound={rewardItem.Bound}.")
            .ConfigureAwait(false);
    }

    private static bool TrySelectLegacyDirectRuneFragmentReward(
        WorldPacketHandlerContext context,
        WorldSession session,
        ushort equipmentPosition,
        out ItemType? itemType,
        out int poolIndex,
        out string pendingReason)
    {
        itemType = null;
        poolIndex = -1;
        if (context.WorldData is not { } worldData)
        {
            pendingReason = "itemtype catalog is not loaded, so the random complete-rune reward pool is unavailable";
            return false;
        }

        var pool = worldData.ItemsById
            .Values
            .SelectMany(items => items)
            .Where(candidate => candidate.Id > 0 &&
                candidate.Id % 100 == 1 &&
                ItemType.GetLegacyEquipmentPosition(checked((uint)candidate.Id)) == equipmentPosition)
            .OrderBy(candidate => candidate.Id)
            .ToArray();
        if (pool.Length == 0)
        {
            pendingReason = $"itemtype catalog has no complete rune rows for equipment position {equipmentPosition}";
            return false;
        }

        poolIndex = SelectLegacyRuneFragmentCompleteRunePoolIndex(session.Character, pool.Length);
        itemType = pool[poolIndex];
        pendingReason = "";
        return true;
    }

    private static bool TryConsumeLegacyBoundInventoryItemCount(
        PlayerItemSnapshot items,
        uint itemId,
        byte bound,
        ushort count,
        out PlayerItemSnapshot updatedItems,
        out IReadOnlyList<byte[]> frames)
    {
        updatedItems = items;
        frames = [];
        if (count == 0)
        {
            return true;
        }

        var inventoryItems = items.InventoryItems.ToList();
        var mutationFrames = new List<byte[]>();
        var remaining = count;
        while (remaining > 0)
        {
            var remainingBeforePass = remaining;
            for (var index = 0; index < inventoryItems.Count && remaining > 0; index++)
            {
                var candidate = inventoryItems[index];
                if (candidate.ItemId != itemId || candidate.Bound != bound)
                {
                    continue;
                }

                if (candidate.StackSize > 1)
                {
                    var updatedItem = candidate.WithStackSize(checked((ushort)(candidate.StackSize - 1)));
                    inventoryItems[index] = updatedItem;
                    mutationFrames.Add(
                        LegacyGameItemPacket
                            .FromSnapshotItem(updatedItem, mode: LegacyGameItemPacket.UpdateItemMode)
                            .ToFrameBytes());
                }
                else
                {
                    mutationFrames.Add(
                        new MsgItemUsagePacket(
                            candidate.Uid,
                            DwParam: 0,
                            MsgItemUsageAction.RemoveInventory,
                            Timestamp: 0,
                            DwParam2: 0,
                            DwParam3: 0,
                            DwParam4: 0).ToFrameBytes());
                    inventoryItems.RemoveAt(index);
                    index--;
                }

                remaining--;
            }

            if (remaining == remainingBeforePass)
            {
                return false;
            }
        }

        updatedItems = items with { InventoryItems = inventoryItems.ToArray() };
        frames = mutationFrames;
        return true;
    }

    private static bool IsLegacyDirectRuneFragmentItem(uint itemId)
    {
        return itemId is 3_306_366 or 3_306_367 or 3_306_368 or 3_306_369 or 3_306_370 or 3_306_371;
    }

    private static bool IsLegacyDirectYellowRuneFragmentBlacklistedReward(uint itemId)
    {
        return itemId is
            4_034_801 or
            4_035_101 or
            4_035_001 or
            4_034_201 or
            4_034_301 or
            4_032_101 or
            4_032_301 or
            4_032_601 or
            4_033_701 or
            4_034_701 or
            4_034_501;
    }

    private static bool TryGetLegacyDirectRuneFragmentSpec(uint itemId, out LegacyDirectRuneFragmentSpec spec)
    {
        spec = itemId switch
        {
            3_306_366 => new("RedRuneFragment", EquipmentPosition: 101, RequiredBound: 0, RewardBound: false, IsYellow: false),
            3_306_367 => new("YellowRuneFragment", EquipmentPosition: 103, RequiredBound: 0, RewardBound: false, IsYellow: true),
            3_306_368 => new("BlueRuneFragment", EquipmentPosition: 102, RequiredBound: 0, RewardBound: false, IsYellow: false),
            3_306_369 => new("RedRuneFragment", EquipmentPosition: 101, RequiredBound: 1, RewardBound: true, IsYellow: false),
            3_306_370 => new("YellowRuneFragment", EquipmentPosition: 103, RequiredBound: 1, RewardBound: true, IsYellow: true),
            3_306_371 => new("BlueRuneFragment", EquipmentPosition: 102, RequiredBound: 1, RewardBound: true, IsYellow: false),
            _ => default,
        };
        return !string.IsNullOrEmpty(spec.FragmentName);
    }

    private readonly record struct LegacyDirectRuneFragmentSpec(
        string FragmentName,
        ushort EquipmentPosition,
        byte RequiredBound,
        bool RewardBound,
        bool IsYellow);
}
