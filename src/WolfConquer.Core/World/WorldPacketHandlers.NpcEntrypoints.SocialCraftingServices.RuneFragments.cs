using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySocialCraftingServicesRuneFragmentMinItemId = 3_306_369;
    private const uint LegacySocialCraftingServicesRuneFragmentMaxItemId = 3_306_371;
    private const ushort LegacySocialCraftingServicesRuneFragmentCombineCount = 20;
    private const string RuntimeLegacyRuneFragmentDoubleFailRateAttribute = "LegacyRuneFragmentDoubleFailRate";
    private const string RuntimeLegacyRuneFragmentCompleteRuneIndexAttribute = "LegacyRuneFragmentCompleteRuneIndex";

    private const string LegacySocialCraftingServicesRuneFragmentsSource = "NpcHandler.RuneFragments";

    private const string LegacySocialCraftingServicesRuneFragmentsLines = "55286-55357";

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesRuneFragmentsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint itemId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesRuneFragmentsSource,
                        [
                            "You can combine 20 rune fragments into a complete rune.",
                            "If you use one fragment alone, you have a chance of getting two fragments.",
                        ],
                        [new("Combine 20 fragments.", 1), new("Use 1 fragment for double.", 2)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await TryApplyLegacyRuneFragmentCompleteRuneAsync(
                        context,
                        session,
                        itemId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await TryApplyLegacyRuneFragmentDoubleAsync(
                        context,
                        session,
                        itemId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask TryApplyLegacyRuneFragmentCompleteRuneAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint itemId,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyRuneFragment(session.PlayerItems, itemId, out var fragment))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .BoxDialog("Please make sure you have 20 fragments to make a Rune.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc RuneFragments complete rune " +
                    $"{FormatSession(context)} source={LegacySocialCraftingServicesRuneFragmentsSource} " +
                    $"itemId={itemId}; source fragment missing.")
                .ConfigureAwait(false);
            return;
        }

        var workingItems = session.PlayerItems;
        var workingFragment = fragment!;
        if (LegacySocialCraftingServicesIsRuneFragmentItemId(itemId) &&
            workingFragment.Bound < 1)
        {
            var boundFragment = workingFragment.WithBound(1);
            workingItems = ReplaceInventoryItem(workingItems, workingFragment, boundFragment);
            workingFragment = boundFragment;
        }

        if (CountLegacyRuneFragmentsForCombine(workingItems, itemId, workingFragment.Bound) <
            LegacySocialCraftingServicesRuneFragmentCombineCount)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .BoxDialog("Please make sure you have 20 fragments to make a Rune.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc RuneFragments complete rune " +
                    $"{FormatSession(context)} source={LegacySocialCraftingServicesRuneFragmentsSource} " +
                    $"itemId={itemId} bound={workingFragment.Bound}; fragments missing.")
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(workingItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .BoxDialog("Please make sure you have 1 free slot in your inventory.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc RuneFragments complete rune " +
                    $"{FormatSession(context)} source={LegacySocialCraftingServicesRuneFragmentsSource} " +
                    $"itemId={itemId}; inventory is full.")
                .ConfigureAwait(false);
            return;
        }

        if (!TrySelectLegacyRuneFragmentCompleteRune(
                context,
                session,
                itemId,
                out var rewardItemType,
                out var poolIndex,
                out var pendingReason))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RuneFragments complete rune " +
                    $"{FormatSession(context)} source={LegacySocialCraftingServicesRuneFragmentsSource} " +
                    $"sourceLines={LegacySocialCraftingServicesRuneFragmentsLines} itemId={itemId}; {pendingReason}")
                .ConfigureAwait(false);
            return;
        }

        if (!TryConsumeLegacyInventoryItemCount(
                workingItems,
                itemId,
                workingFragment.Bound,
                LegacySocialCraftingServicesRuneFragmentCombineCount,
                out var updatedItems,
                out var consumeFrames))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RuneFragments complete rune " +
                    $"{FormatSession(context)} source={LegacySocialCraftingServicesRuneFragmentsSource} " +
                    $"sourceLines={LegacySocialCraftingServicesRuneFragmentsLines} itemId={itemId}; " +
                    "source inventory count passed but local consume could not remove 20 fragments.")
                .ConfigureAwait(false);
            return;
        }

        var rewardItem = CreatePurchasedItems(
            updatedItems,
            rewardItemType!,
            count: 1,
            isBound: workingFragment.Bound > 0)[0];
        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems.Concat([rewardItem]).ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await PersistLegacyRuneFragmentItemsAsync(updatedContext, updatedSession).ConfigureAwait(false);

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
                        "You have got 1 " + rewardItemType!.Name + (workingFragment.Bound > 0 ? "(B)." : "."))
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc RuneFragments complete rune " +
                $"{FormatSession(updatedContext)} source={LegacySocialCraftingServicesRuneFragmentsSource} " +
                $"sourceLines={LegacySocialCraftingServicesRuneFragmentsLines} itemId={itemId} " +
                $"rewardItemId={rewardItem.ItemId} poolIndex={poolIndex} bound={rewardItem.Bound}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask TryApplyLegacyRuneFragmentDoubleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint itemId,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyRuneFragment(session.PlayerItems, itemId, out var fragment))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc RuneFragments double attempt " +
                    $"{FormatSession(context)} source={LegacySocialCraftingServicesRuneFragmentsSource} " +
                    $"itemId={itemId}; source fragment missing.")
                .ConfigureAwait(false);
            return;
        }

        var workingItems = session.PlayerItems;
        var workingFragment = fragment!;
        if (LegacySocialCraftingServicesIsRuneFragmentItemId(itemId) &&
            workingFragment.Bound < 1)
        {
            var boundFragment = workingFragment.WithBound(1);
            workingItems = ReplaceInventoryItem(workingItems, workingFragment, boundFragment);
            workingFragment = boundFragment;
        }

        var failed = CheckLegacyRuneFragmentDoubleFailureRate(session.Character);
        PlayerItemSnapshot updatedItems;
        PlayerItemSnapshotItem? updatedFragment = null;
        byte[] itemFrame;
        string dialog;
        if (failed)
        {
            updatedItems = ConsumeSingleLegacyInventoryItem(workingItems, workingFragment, out itemFrame);
            dialog = "Failed to combine. The magic of the fragment has vanished, and it disappeared.";
        }
        else
        {
            var currentStack = Math.Max(1, (int)workingFragment.StackSize);
            updatedFragment = workingFragment.WithStackSize(checked((ushort)Math.Min(ushort.MaxValue, currentStack + 1)));
            updatedItems = ReplaceInventoryItem(workingItems, workingFragment, updatedFragment);
            itemFrame = LegacyGameItemPacket
                .FromSnapshotItem(updatedFragment, mode: LegacyGameItemPacket.UpdateItemMode)
                .ToFrameBytes();
            dialog = $"Congratulations! You successfully made 2 {GetLegacyRuneFragmentName(context, itemId)}s!";
        }

        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await PersistLegacyRuneFragmentItemsAsync(updatedContext, updatedSession).ConfigureAwait(false);

        await context.Connection.SendAsync(itemFrame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.BoxDialog(dialog).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc RuneFragments double attempt " +
                $"{FormatSession(updatedContext)} source={LegacySocialCraftingServicesRuneFragmentsSource} " +
                $"sourceLines={LegacySocialCraftingServicesRuneFragmentsLines} itemId={itemId} " +
                $"Core.Rate(50)={failed.ToString().ToLowerInvariant()} " +
                $"stack={Math.Max(0, CountLegacyInventoryItem(updatedItems, itemId))} " +
                $"updatedUid={(updatedFragment?.Uid ?? workingFragment.Uid)}.")
            .ConfigureAwait(false);
    }

    private static bool TryGetLegacyRuneFragment(
        PlayerItemSnapshot items,
        uint itemId,
        out PlayerItemSnapshotItem? fragment)
    {
        fragment = items.InventoryItems
            .Where(item => item.ItemId == itemId && Math.Max(1, (int)item.StackSize) >= 1)
            .OrderBy(item => item.Uid)
            .FirstOrDefault();
        return fragment is not null;
    }

    private static int CountLegacyRuneFragmentsForCombine(PlayerItemSnapshot items, uint itemId, byte bound)
    {
        var count = 0;
        foreach (var item in items.InventoryItems)
        {
            if (item.ItemId != itemId || item.Bound != bound)
            {
                continue;
            }

            count = checked(count + Math.Max(1, (int)item.StackSize));
        }

        return count;
    }

    private static bool CheckLegacyRuneFragmentDoubleFailureRate(CharacterSnapshot character)
    {
        return TryReadAttributeBool(
                character.Attributes,
                out var forced,
                RuntimeLegacyRuneFragmentDoubleFailRateAttribute)
            ? forced
            : Random.Shared.Next(0, 100) < 50;
    }

    private static bool TrySelectLegacyRuneFragmentCompleteRune(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint fragmentItemId,
        out ItemType? itemType,
        out int poolIndex,
        out string pendingReason)
    {
        itemType = null;
        poolIndex = -1;
        if (!TryGetLegacyRuneFragmentEquipmentPosition(context, fragmentItemId, out var equipmentPosition, out pendingReason))
        {
            return false;
        }

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

    private static bool TryGetLegacyRuneFragmentEquipmentPosition(
        WorldPacketHandlerContext context,
        uint fragmentItemId,
        out ushort equipmentPosition,
        out string pendingReason)
    {
        equipmentPosition = fragmentItemId switch
        {
            3_306_369 => 101,
            3_306_370 => 103,
            3_306_371 => 102,
            _ => 0,
        };
        if (equipmentPosition != 0)
        {
            pendingReason = "";
            return true;
        }

        var name = GetLegacyRuneFragmentName(context, fragmentItemId);
        if (name.Contains("Red", StringComparison.OrdinalIgnoreCase))
        {
            equipmentPosition = 101;
        }
        else if (name.Contains("Blue", StringComparison.OrdinalIgnoreCase))
        {
            equipmentPosition = 102;
        }
        else if (name.Contains("Yellow", StringComparison.OrdinalIgnoreCase))
        {
            equipmentPosition = 103;
        }

        pendingReason = equipmentPosition == 0
            ? $"fragment item {fragmentItemId} name does not identify a red, blue, or yellow rune pool"
            : "";
        return equipmentPosition != 0;
    }

    private static int SelectLegacyRuneFragmentCompleteRunePoolIndex(CharacterSnapshot character, int poolCount)
    {
        if (TryReadInt64Attribute(
                character.Attributes,
                RuntimeLegacyRuneFragmentCompleteRuneIndexAttribute,
                out var forcedIndex))
        {
            return checked((int)PositiveModulo(forcedIndex, poolCount));
        }

        return Random.Shared.Next(0, poolCount);
    }

    private static string GetLegacyRuneFragmentName(WorldPacketHandlerContext context, uint itemId)
    {
        return TryReadItemType(context.WorldData, itemId, out var itemType) && itemType.Name.Length > 0
            ? itemType.Name
            : $"Item{itemId}";
    }

    private static async ValueTask PersistLegacyRuneFragmentItemsAsync(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RuneFragments item persistence " +
                    $"{FormatSession(context)} reason=playerItemsRootMissing.")
                .ConfigureAwait(false);
            return;
        }

        try
        {
            new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                session.Character.Uid,
                session.PlayerItems);
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyNpc RuneFragments items " +
                    $"{FormatSession(context)} path={session.Character.Uid}.bin.")
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException ||
            ex is NotSupportedException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RuneFragments item persistence " +
                    $"{FormatSession(context)} reason=itemSnapshotSaveFailed exception={ex.GetType().Name}.")
                .ConfigureAwait(false);
        }
    }

    private static bool LegacySocialCraftingServicesIsRuneFragmentItemId(uint npcId)
    {
        return npcId is >= LegacySocialCraftingServicesRuneFragmentMinItemId and <= LegacySocialCraftingServicesRuneFragmentMaxItemId;
    }
}
