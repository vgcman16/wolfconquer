using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyRuneDecomposeRequiredInventorySpaces = 10;
    private const ushort LegacyRuneDecomposeFragmentStack = 10;
    private const uint LegacyBlueRuneEssenceItemId = 4_040_001;
    private const uint LegacyYellowRuneEssenceItemId = 4_050_001;
    private const uint LegacyRedRuneEssenceItemId = 4_060_001;
    private const uint LegacyUnboundRedRuneFragmentItemId = 3_306_366;
    private const uint LegacyUnboundYellowRuneFragmentItemId = 3_306_367;
    private const uint LegacyUnboundBlueRuneFragmentItemId = 3_306_368;
    private const uint LegacyBoundRedRuneFragmentItemId = 3_306_369;
    private const uint LegacyBoundYellowRuneFragmentItemId = 3_306_370;
    private const uint LegacyBoundBlueRuneFragmentItemId = 3_306_371;
    private const string LegacyRuneDecomposeSource = "MsgItemUsuagePacket.DecomposeRune";
    private const string LegacyRuneDecomposeLines = "1981-2048";

    private static async ValueTask HandleLegacyDecomposeRuneAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemUsagePacket packet,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        var args = ReadLegacyItemUsageArgs(frame);
        if (args.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage decompose rune " +
                    $"{FormatSession(context)} source={LegacyRuneDecomposeSource} " +
                    $"sourceLines={LegacyRuneDecomposeLines}; source args[0] rune UID is missing.")
                .ConfigureAwait(false);
            return;
        }

        var runeUid = args[0];
        if (!TryGetRuneItem(session.PlayerItems, runeUid, out var rune))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage decompose rune " +
                    $"{FormatSession(context)} source={LegacyRuneDecomposeSource} " +
                    $"sourceLines={LegacyRuneDecomposeLines} itemUid={runeUid}; rune item was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, LegacyRuneDecomposeRequiredInventorySpaces))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .BoxDialog("Please make 10 free spaces in your inventory.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage decompose rune " +
                    $"{FormatSession(context)} source={LegacyRuneDecomposeSource} " +
                    $"sourceLines={LegacyRuneDecomposeLines} itemUid={runeUid}; " +
                    $"source requires {LegacyRuneDecomposeRequiredInventorySpaces} free inventory spaces.")
                .ConfigureAwait(false);
            return;
        }

        var runeFamily = GetLegacyRuneDecomposeFamily(rune.ItemId);
        var runeLevel = rune.ItemId % 100;
        if (runeFamily > int.MaxValue ||
            runeLevel > int.MaxValue ||
            context.WorldData is not { } worldData ||
            !worldData.RuneLevelExperienceByRuneTypeAndLevel.TryGetValue(
                (checked((int)runeFamily), checked((int)runeLevel)),
                out var levelExperience) ||
            levelExperience.Experience < 0 ||
            levelExperience.Experience > uint.MaxValue ||
            levelExperience.Experience + rune.RuneExperience > uint.MaxValue)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedItemUse(
                        rune.ItemId,
                        action: (uint)packet.Action,
                        actionName: packet.Action.ToString(),
                        itemUid: runeUid,
                        source: LegacyRuneDecomposeSource,
                        reason: "rune level experience metadata is missing or outside the legacy uint range"))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "PARITY-GAP MsgItemUsage decompose rune missing rune level experience " +
                    $"{FormatSession(context)} source={LegacyRuneDecomposeSource} " +
                    $"sourceLines={LegacyRuneDecomposeLines} itemUid={runeUid} itemId={rune.ItemId} " +
                    $"runeFamily={runeFamily} runeLevel={runeLevel} runeExp={rune.RuneExperience}.")
                .ConfigureAwait(false);
            return;
        }

        var fragmentItemId = GetLegacyRuneDecomposeFragmentItemId(rune.ItemId, rune.Bound > 0);
        if (!TryReadItemType(context.WorldData, fragmentItemId, out var fragmentItemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedItemUse(
                        fragmentItemId,
                        action: (uint)packet.Action,
                        actionName: packet.Action.ToString(),
                        itemUid: runeUid,
                        source: LegacyRuneDecomposeSource,
                        reason: "rune fragment reward itemtype metadata is not loaded"))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "PARITY-GAP MsgItemUsage decompose rune missing fragment itemtype " +
                    $"{FormatSession(context)} source={LegacyRuneDecomposeSource} " +
                    $"sourceLines={LegacyRuneDecomposeLines} itemUid={runeUid} " +
                    $"fragmentItemId={fragmentItemId}.")
                .ConfigureAwait(false);
            return;
        }

        var essenceItemId = GetLegacyRuneDecomposeEssenceItemId(runeFamily);
        var totalProgress = checked((uint)(levelExperience.Experience + rune.RuneExperience));
        var recycledProgress = (uint)((double)totalProgress / 1.25);
        var essenceStack = unchecked((ushort)(recycledProgress / 10));
        var updatedItems = RemoveLegacyRuneItem(session.PlayerItems, rune);
        var essenceItem = CreateLegacyRuneDecomposeEssenceItem(updatedItems, essenceItemId, essenceStack);
        var addedEssence = TryAddLegacyRuneDecomposeEssence(
            updatedItems,
            essenceItem,
            out updatedItems);
        updatedItems = AddOrStackLegacyBoundInventoryItem(
            updatedItems,
            fragmentItemType,
            LegacyRuneDecomposeFragmentStack,
            out var fragmentItem,
            out var fragmentMode);

        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await TryPersistLegacyItemUsageMutationAsync(
                updatedContext,
                updatedSession,
                "decompose rune",
                characterValues: null,
                persistItems: true)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                new MsgItemUsagePacket(
                    rune.Uid,
                    DwParam: 0,
                    MsgItemUsageAction.RemoveInventory,
                    Timestamp: 0,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        if (addedEssence)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket
                        .FromSnapshotItem(
                            essenceItem,
                            LegacyGameItemPacket.CreateRuneSpellOverride(essenceItem, context.WorldData?.ItemsById))
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(fragmentItem, mode: fragmentMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage decompose rune " +
                $"{FormatSession(updatedContext)} source={LegacyRuneDecomposeSource} " +
                $"sourceLines={LegacyRuneDecomposeLines} itemUid={runeUid} itemId={rune.ItemId} " +
                $"runeFamily={runeFamily} runeLevel={runeLevel} recycledProgress={recycledProgress} " +
                $"essenceItemId={essenceItemId} essenceStack={essenceStack} addedEssence={addedEssence} " +
                $"fragmentItemId={fragmentItem.ItemId} fragmentStack={fragmentItem.StackSize} fragmentBound={fragmentItem.Bound}.")
            .ConfigureAwait(false);
    }

    private static PlayerItemSnapshot RemoveLegacyRuneItem(
        PlayerItemSnapshot items,
        PlayerItemSnapshotItem rune)
    {
        return items with
        {
            RuneItems = items.RuneItems.Where(item => item.Uid != rune.Uid).ToArray(),
        };
    }

    private static PlayerItemSnapshotItem CreateLegacyRuneDecomposeEssenceItem(
        PlayerItemSnapshot items,
        uint itemId,
        ushort stackSize)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = GetNextSessionItemUid(items),
            ItemId = itemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = LegacyRuneBagPosition,
            StackSize = stackSize,
        };
    }

    private static bool TryAddLegacyRuneDecomposeEssence(
        PlayerItemSnapshot items,
        PlayerItemSnapshotItem essenceItem,
        out PlayerItemSnapshot updatedItems)
    {
        updatedItems = items;
        if (items.RuneItems.Any(item => item.Uid == essenceItem.Uid) ||
            items.RuneItems.Count(item => item.Position == LegacyRuneBagPosition) >= 100)
        {
            return false;
        }

        updatedItems = items with
        {
            RuneItems = items.RuneItems
                .Concat([essenceItem])
                .OrderBy(item => item.Position)
                .ThenBy(item => IsLegacyRuneBagPosition(item.Position) ? 0u : item.Uid)
                .ToArray(),
        };
        return true;
    }

    private static PlayerItemSnapshot AddOrStackLegacyBoundInventoryItem(
        PlayerItemSnapshot items,
        ItemType itemType,
        ushort stackCount,
        out PlayerItemSnapshotItem rewardItem,
        out ushort itemMode)
    {
        var itemId = CheckedUInt32(itemType.Id, nameof(itemType.Id));
        var inventoryItems = items.InventoryItems.ToArray();
        for (var index = 0; index < inventoryItems.Length; index++)
        {
            var candidate = inventoryItems[index];
            if (candidate.ItemId != itemId ||
                candidate.Position != 0 ||
                candidate.Bound == 0 ||
                candidate.WarehouseId != 0)
            {
                continue;
            }

            var currentStack = candidate.StackSize == 0 ? (ushort)1 : candidate.StackSize;
            var updatedStack = checked((ushort)Math.Min(ushort.MaxValue, currentStack + stackCount));
            rewardItem = candidate.WithStackSize(updatedStack);
            inventoryItems[index] = rewardItem;
            itemMode = LegacyGameItemPacket.UpdateItemMode;
            return items with { InventoryItems = inventoryItems };
        }

        rewardItem = CreatePurchasedItems(items, itemType, count: 1, isBound: true)[0]
            .WithStackSize(stackCount);
        itemMode = LegacyGameItemPacket.AddItemMode;
        return items with
        {
            InventoryItems = items.InventoryItems.Concat([rewardItem]).ToArray(),
        };
    }

    private static uint GetLegacyRuneDecomposeFamily(uint itemId)
    {
        var sourceGroup = itemId / 1_000;
        var sourceGroupText = sourceGroup.ToString(CultureInfo.InvariantCulture);
        return sourceGroupText.Length < 3
            ? 0
            : uint.Parse(sourceGroupText[..3], CultureInfo.InvariantCulture);
    }

    private static uint GetLegacyRuneDecomposeEssenceItemId(uint runeFamily)
    {
        return runeFamily switch
        {
            401 => LegacyRedRuneEssenceItemId,
            402 => LegacyBlueRuneEssenceItemId,
            _ => LegacyYellowRuneEssenceItemId,
        };
    }

    private static uint GetLegacyRuneDecomposeFragmentItemId(uint itemId, bool sourceBound)
    {
        return ItemType.GetLegacyEquipmentPosition(itemId) switch
        {
            101 => sourceBound ? LegacyBoundRedRuneFragmentItemId : LegacyUnboundRedRuneFragmentItemId,
            102 => sourceBound ? LegacyBoundBlueRuneFragmentItemId : LegacyUnboundBlueRuneFragmentItemId,
            _ => sourceBound ? LegacyBoundYellowRuneFragmentItemId : LegacyUnboundYellowRuneFragmentItemId,
        };
    }
}
