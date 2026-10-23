using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyDeitylandNavigationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (IsLegacyNezhaNpc(npcId))
        {
            return await TryHandleLegacyNezhaAsync(context, session, npcId, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsLegacySaintTaiyiNpc(npcId))
        {
            return await TryHandleLegacySaintTaiyiAsync(context, session, npcId, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsLegacyRareDeitylandChestNpc(npcId))
        {
            return await TryHandleLegacyRareDeitylandChestAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsLegacyEpicDeitylandChestNpc(npcId))
        {
            return await TryHandleLegacyEpicDeitylandChestAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsLegacyVictoryChestNpc(npcId))
        {
            return await TryHandleLegacyVictoryChestAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsLegacyDeityAltarNpc(npcId))
        {
            return await TryHandleLegacyDeityAltarAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsLegacyDeitylandPortalNpc(npcId))
        {
            return await TryHandleLegacyDeitylandPortalAsync(context, session, npcId, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsLegacyYuKoonNpc(npcId))
        {
            return await TryHandleLegacyYuKoonAsync(context, session, npcId, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (npcId == LegacyDragonYuKoonNpcId)
        {
            return await TryHandleLegacyDragonYuKoonAsync(context, session, npcId, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsLegacyDeityCobbWindNpc(npcId))
        {
            return await TryHandleLegacyDeityCobbWindAsync(context, session, npcId, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyDeityAltarAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.DeityAltar",
                        CreateLegacyDeityAltarRankingDialogTexts(session),
                        LegacyDeityAltarMainOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.DeityAltar",
                        LegacyDeityAltarRulesDialogTexts,
                        LegacyDeityAltarRulesOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.DeityAltar",
                        LegacyDeityAltarTransformDialogTexts,
                        LegacyDeityAltarTransformOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 3:
                await TryTransformLegacyDeityAltarRuneAsync(
                        context,
                        session,
                        LegacyDeityAltarWitcheryRuneItemId,
                        LegacyDeityAltarNoMercyRuneItemId,
                        LegacyDeityAltarMissingWitcheryText,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 4:
                await TryTransformLegacyDeityAltarRuneAsync(
                        context,
                        session,
                        LegacyDeityAltarNoMercyRuneItemId,
                        LegacyDeityAltarWitcheryRuneItemId,
                        LegacyDeityAltarMissingNoMercyText,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 5:
            case 6:
            case 7:
                await TrySacrificeLegacyDeityAltarJadesAsync(context, session, optionId, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyRareDeitylandChestAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyDeitylandChestDialogAsync(
                    context,
                    "NpcHandler.RareDeitylandChests",
                    LegacyRareDeitylandChestDialogTexts,
                    session.PlayerItems,
                    LegacyRareDeitylandChestKeyItemId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            var rewardItemId = LegacyRareDeitylandChestRewards[
                Random.Shared.Next(0, LegacyRareDeitylandChestRewards.Length)];
            await TryUnlockLegacyDeitylandChestAsync(
                    context,
                    session,
                    "NpcHandler.RareDeitylandChests",
                    LegacyRareDeitylandChestKeyItemId,
                    rewardItemId,
                    conquerPointsReward: 0,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyEpicDeitylandChestAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyDeitylandChestDialogAsync(
                    context,
                    "NpcHandler.EpicDeitylandChests",
                    LegacyEpicDeitylandChestDialogTexts,
                    session.PlayerItems,
                    LegacyEpicDeitylandChestDialogKeyItemId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            var roll = Random.Shared.Next(1, 5);
            var rewardItemId = roll switch
            {
                1 => LegacyEpicDeitylandChestRewards[0],
                2 => LegacyEpicDeitylandChestRewards[1],
                3 => LegacyEpicDeitylandChestRewards[2],
                _ => 0U,
            };
            await TryUnlockLegacyDeitylandChestAsync(
                    context,
                    session,
                    "NpcHandler.EpicDeitylandChests",
                    LegacyEpicDeitylandChestUnlockKeyItemId,
                    rewardItemId,
                    conquerPointsReward: roll == 4 ? LegacyEpicDeitylandChestConquerPointReward : 0,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyVictoryChestAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.VictoryChest",
                    LegacyVictoryChestDialogTexts,
                    IsLegacyVictoryChestReady(session.Character.PlayerQuests)
                        ? LegacyVictoryChestUnlockedOptions
                        : LegacyVictoryChestLockedOptions,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            await TryUnlockLegacyVictoryChestAsync(context, session, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyDeitylandPortalAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.DeitylandPortals",
                    [LegacyDeitylandPortalDialogText],
                    LegacyDeitylandPortalOptions,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    new LegacyNpcTeleportDestination(10_250, 1_009, 1_283, "NpcHandler.DeitylandPortals"),
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static IReadOnlyList<string> CreateLegacyDeityAltarRankingDialogTexts(WorldSession session)
    {
        var sacrificedJades = ReadAttributeUInt64(
            session.Character.Attributes,
            RuntimeLegacyDeityAltarSacrificedJadesAttribute);
        return
        [
            "\t\t\t\t\t\t\t\t\t*Deityland Sacrifice Ranking*\n",
            "----------------------------------------------------------\n",
            "[Rank]    [Jades]  [Character]                [Prize]\n",
            "----------------------------------------------------------\n",
            "The final ranking will be determined at 23:59 every day. The prizes will be sent via mail.\n",
            $"You`ve sacrificed {sacrificedJades.ToString(CultureInfo.InvariantCulture)} Jades of Faith. Sacrifice 10 jades to get a reward, and 30 jades to qualify for the ranking.\n",
        ];
    }

    private static async ValueTask TryTransformLegacyDeityAltarRuneAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint sourceItemId,
        uint rewardItemId,
        string missingText,
        CancellationToken cancellationToken)
    {
        if (!HasLegacyInventoryStackCount(session.PlayerItems, sourceItemId, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox(missingText).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc DeityAltar rune transform " +
                    $"{FormatSession(context)} source=NpcHandler.DeityAltar itemId={sourceItemId}; item missing.")
                .ConfigureAwait(false);
            return;
        }

        var consumeFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(session.PlayerItems, sourceItemId, 1, consumeFrames);
        var rewardItem = CreateLegacyDeityAltarGrantedItem(context, updatedItems, rewardItemId);
        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems.Concat([rewardItem]).ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await PersistLegacyDeityAltarItemsAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        foreach (var consumeFrame in consumeFrames)
        {
            await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyItemFollowupsAsync(updatedContext, rewardItem, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc DeityAltar rune transform " +
                $"{FormatSession(updatedContext)} source=NpcHandler.DeityAltar sourceItemId={sourceItemId} rewardItemId={rewardItemId}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask TrySacrificeLegacyDeityAltarJadesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var count = optionId switch
        {
            5 => 1U,
            6 => 10U,
            7 => CountLegacyInventoryStack(session.PlayerItems, LegacyDeityAltarJadeOfFaithItemId),
            _ => 0U,
        };
        if (count == 0 ||
            !HasLegacyInventoryStackCount(session.PlayerItems, LegacyDeityAltarJadeOfFaithItemId, count))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.DeityAltar",
                    LegacyDeityAltarMissingJadesTexts,
                    LegacyDeityAltarSingleCloseOptions,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var consumeFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(
            session.PlayerItems,
            LegacyDeityAltarJadeOfFaithItemId,
            count,
            consumeFrames);
        var current = ReadAttributeUInt64(
            session.Character.Attributes,
            RuntimeLegacyDeityAltarSacrificedJadesAttribute);
        var updatedJades = checked(current + count);
        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            RuntimeLegacyDeityAltarSacrificedJadesAttribute,
            updatedJades);
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await PersistLegacyDeityAltarStateAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        foreach (var consumeFrame in consumeFrames)
        {
            await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);
        }

        await SendLegacyNpcDialogAsync(
                updatedContext,
                "NpcHandler.DeityAltar",
                CreateLegacyDeityAltarRankingDialogTexts(updatedSession),
                LegacyDeityAltarMainOptions,
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc DeityAltar Jade sacrifice " +
                $"{FormatSession(updatedContext)} source=NpcHandler.DeityAltar count={count} total={updatedJades}.")
            .ConfigureAwait(false);
    }

    private static uint CountLegacyInventoryStack(PlayerItemSnapshot items, uint itemId)
    {
        ulong total = 0;
        foreach (var item in items.InventoryItems.Where(item => item.ItemId == itemId))
        {
            total += item.StackSize == 0 ? 1UL : item.StackSize;
        }

        return checked((uint)Math.Min(total, uint.MaxValue));
    }

    private static PlayerItemSnapshotItem CreateLegacyDeityAltarGrantedItem(
        WorldPacketHandlerContext context,
        PlayerItemSnapshot items,
        uint itemId)
    {
        var durability = TryReadItemType(context.WorldData, itemId, out var itemType)
            ? checked((ushort)itemType.LegacyDurability)
            : (ushort)0;
        return new PlayerItemSnapshotItem
        {
            Uid = GetNextSessionItemUid(items),
            ItemId = itemId,
            Durability = durability,
            MaximumDurability = durability,
            Position = 0,
            StackSize = 1,
        };
    }

    private static async ValueTask PersistLegacyDeityAltarStateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [RuntimeLegacyDeityAltarSacrificedJadesAttribute] =
                ReadAttributeUInt64(
                        session.Character.Attributes,
                        RuntimeLegacyDeityAltarSacrificedJadesAttribute)
                    .ToString(CultureInfo.InvariantCulture),
        };
        if (!TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                values,
                out _,
                out var characterReason))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc DeityAltar character persistence " +
                    $"{FormatSession(context)} source=NpcHandler.DeityAltar reason={characterReason}.")
                .ConfigureAwait(false);
        }

        await PersistLegacyDeityAltarItemsAsync(context, session, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask PersistLegacyDeityAltarItemsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            try
            {
                new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                    session.Character.Uid,
                    session.PlayerItems);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc DeityAltar item persistence " +
                        $"{FormatSession(context)} source=NpcHandler.DeityAltar error={ex.Message}.")
                    .ConfigureAwait(false);
            }
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc DeityAltar missing PlayerItemsRoot " +
                    $"{FormatSession(context)} source=NpcHandler.DeityAltar; item mutation is session-only.")
                .ConfigureAwait(false);
        }
    }

    // Victory claim/reward transaction is defined in the VictoryChest partials.

    private static bool IsLegacyVictoryChestReady(PlayerQuestSnapshot playerQuests)
    {
        return playerQuests.TryGetQuest(LegacyDeityLandQuestId, out var quest) &&
            (quest.Status == PlayerQuestStatus.Finished || IsLegacyVictoryChestObjectiveReady(quest));
    }

    private static bool IsLegacyVictoryChestObjectiveReady(PlayerQuestSnapshotEntry? quest)
    {
        return quest is { Status: PlayerQuestStatus.Accepted, Intentions.Count: > 0 } &&
            quest.Intentions[0] >= 100;
    }

    private static async ValueTask SendLegacyDeitylandChestDialogAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        IReadOnlyList<string> texts,
        PlayerItemSnapshot items,
        uint keyItemId,
        CancellationToken cancellationToken)
    {
        var options = FindLegacyPositiveDeitylandChestKey(items, keyItemId) >= 0
            ? LegacyDeitylandChestKeyOptions
            : LegacyDeitylandChestNoKeyOptions;
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                texts,
                options,
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static PlayerItemSnapshotItem CreateLegacyDeitylandChestRewardItem(
        WorldPacketHandlerContext context,
        PlayerItemSnapshot items,
        uint itemId)
    {
        var durability = TryReadItemType(context.WorldData, itemId, out var itemType)
            ? checked((ushort)itemType.LegacyDurability)
            : (ushort)0;
        return new PlayerItemSnapshotItem
        {
            Uid = GetNextSessionItemUid(items),
            ItemId = itemId,
            Durability = durability,
            MaximumDurability = durability,
            Position = 0,
            StackSize = 1,
        };
    }

    private static async ValueTask PersistLegacyDeitylandChestUnlockAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        bool persistConquerPoints,
        CancellationToken cancellationToken)
    {
        if (persistConquerPoints)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ConquerPoints"] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            };
            if (!TryPersistLegacyCharacterAttributes(
                    session.UsersRoot,
                    session.AccountId,
                    session.Character.Uid,
                    values,
                    out _,
                    out var characterReason))
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc Deityland chest character persistence " +
                        $"{FormatSession(context)} source={sourceMethod} reason={characterReason}.")
                    .ConfigureAwait(false);
            }
        }

        if (!string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            try
            {
                new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                    session.Character.Uid,
                    session.PlayerItems);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc Deityland chest item persistence " +
                        $"{FormatSession(context)} source={sourceMethod} error={ex.Message}.")
                    .ConfigureAwait(false);
            }
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc Deityland chest missing PlayerItemsRoot " +
                    $"{FormatSession(context)} source={sourceMethod}; item mutation is session-only.")
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask<bool> TryHandleLegacyNezhaAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.Nezha",
                    LegacyNezhaDialogTexts,
                    LegacyNezhaOptions,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            await SendLegacyNpcOpenClientWindowActionAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    LegacyNezhaRelicDecomposeWindowId,
                    "NpcHandler.Nezha",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacySaintTaiyiAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.SaintTaiyi",
                        LegacySaintTaiyiDialogTexts,
                        LegacySaintTaiyiOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await TryCraftLegacySaintTaiyiRelicSpiritWithMoneyAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
                await TryCraftLegacySaintTaiyiRelicSpiritWithCrystalsAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.SaintTaiyi",
                        LegacySaintTaiyiRelicsInfoTexts,
                        LegacySaintTaiyiRelicsInfoOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 4:
                await SendLegacySaintTaiyiRelicAttributesLinkAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 5:
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.SaintTaiyi",
                        LegacySaintTaiyiRelicDurabilityTexts,
                        LegacySaintTaiyiSingleCloseOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyYuKoonAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.YuKoon",
                    LegacyYuKoonDialogTexts,
                    LegacyYuKoonOptions,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    new LegacyNpcTeleportDestination(10_137, 453, 483, "NpcHandler.YuKoon"),
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 2)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.YuKoon",
                    LegacyDeitylandChaosInfoTexts,
                    LegacyDeitylandChaosInfoOptions,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyDragonYuKoonAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.DragonYuKoon",
                    LegacyDragonYuKoonDialogTexts,
                    LegacyDragonYuKoonOptions,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            await SendLegacyDragonYuKoonEnterPromptAsync(context, session, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 2)
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    new LegacyNpcTeleportDestination(10_137, 102, 408, "NpcHandler.DragonYuKoon"),
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 3)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.DragonYuKoon",
                    LegacyDeitylandChaosInfoTexts,
                    LegacyDeitylandChaosInfoOptions,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyDeityCobbWindAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.DeityCobbWind",
                    CreateLegacyDeityCobbWindDialogTexts(),
                    LegacyDeityCobbWindOptions,
                    LegacyCobbWindAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.DeityCobbWind",
                    LegacyDeityCobbWindDetailsDialogTexts,
                    LegacyDeityCobbWindDetailsOptions,
                    LegacyCobbWindAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (TryCreateLegacyDeityCobbWindBossPrompt(optionId, out var promptTexts, out var promptOptions))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.DeityCobbWind",
                    promptTexts,
                    promptOptions,
                    LegacyCobbWindAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (TryGetLegacyDeityCobbWindFreeDestination(optionId, out var freeDestination))
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    freeDestination,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (TryGetLegacyDeityCobbWindPaidDestination(optionId, out var paidDestination))
        {
            await SendLegacyDeityCobbWindPaidTeleportAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    paidDestination,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask TryCraftLegacySaintTaiyiRelicSpiritWithMoneyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (session.Character.Money < LegacySaintTaiyiRelicSpiritMoneyCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.SaintTaiyi",
                    [LegacySaintTaiyiNoMoneyText],
                    LegacySaintTaiyiAwwOptions,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await SendLegacySaintTaiyiInventoryFullAsync(context, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var grantedItem = CreateLegacySaintTaiyiRelicSpiritGrant(context, session.PlayerItems);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat([grantedItem]).ToArray(),
        };
        var updatedCharacter = session.Character with
        {
            Money = checked(session.Character.Money - LegacySaintTaiyiRelicSpiritMoneyCost),
        };
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await PersistLegacySaintTaiyiCraftAsync(
                updatedContext,
                updatedSession,
                reason: "Money",
                persistCharacterMoney: true,
                cancellationToken)
            .ConfigureAwait(false);
        await SendWalletMoneyUpdateAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacySaintTaiyiRelicSpiritGrantAsync(updatedContext, grantedItem, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc SaintTaiyi Relic Spirit money craft " +
                $"{FormatSession(updatedContext)} source=NpcHandler.SaintTaiyi cost={LegacySaintTaiyiRelicSpiritMoneyCost}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask TryCraftLegacySaintTaiyiRelicSpiritWithCrystalsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (!HasLegacyInventoryStackCount(
                session.PlayerItems,
                LegacySaintTaiyiRelicCrystalItemId,
                LegacySaintTaiyiRelicCrystalCost))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.SaintTaiyi",
                    [LegacySaintTaiyiNoRelicCrystalsText],
                    LegacySaintTaiyiSingleCloseOptions,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await SendLegacySaintTaiyiInventoryFullAsync(context, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var consumeFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(
            session.PlayerItems,
            LegacySaintTaiyiRelicCrystalItemId,
            LegacySaintTaiyiRelicCrystalCost,
            consumeFrames);
        var grantedItem = CreateLegacySaintTaiyiRelicSpiritGrant(context, updatedItems);
        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems.Concat([grantedItem]).ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await PersistLegacySaintTaiyiCraftAsync(
                updatedContext,
                updatedSession,
                reason: "RelicCrystals",
                persistCharacterMoney: false,
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var consumeFrame in consumeFrames)
        {
            await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);
        }

        await SendLegacySaintTaiyiRelicSpiritGrantAsync(updatedContext, grantedItem, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc SaintTaiyi Relic Spirit crystal craft " +
                $"{FormatSession(updatedContext)} source=NpcHandler.SaintTaiyi " +
                $"crystals={LegacySaintTaiyiRelicCrystalCost}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacySaintTaiyiRelicAttributesLinkAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(WolfConquer.Core.Runtime.ConquerRuntimeDefaults.WebsiteUrl).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc SaintTaiyi relic attributes link " +
                $"{FormatSession(context with { Session = session })} source=NpcHandler.SaintTaiyi.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacySaintTaiyiInventoryFullAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox("Please make 1 more space in your inventory.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc SaintTaiyi Relic Spirit craft " +
                $"{FormatSession(context)} source=NpcHandler.SaintTaiyi; inventory is full.")
            .ConfigureAwait(false);
    }

    private static PlayerItemSnapshotItem CreateLegacySaintTaiyiRelicSpiritGrant(
        WorldPacketHandlerContext context,
        PlayerItemSnapshot items)
    {
        var durability = TryReadItemType(context.WorldData, LegacySaintTaiyiRelicSpiritItemId, out var itemType)
            ? checked((ushort)itemType.LegacyDurability)
            : (ushort)0;
        return new PlayerItemSnapshotItem
        {
            Uid = GetNextSessionItemUid(items),
            ItemId = LegacySaintTaiyiRelicSpiritItemId,
            Durability = durability,
            MaximumDurability = durability,
            Position = 0,
            StackSize = 1,
        };
    }

    private static async ValueTask SendLegacySaintTaiyiRelicSpiritGrantAsync(
        WorldPacketHandlerContext context,
        PlayerItemSnapshotItem grantedItem,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(grantedItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyItemFollowupsAsync(context, grantedItem, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyStringPacket.PlayerEffect(
                    CheckedUInt32(context.Session!.Character.Uid, nameof(context.Session.Character.Uid)),
                    "zf2-e128").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask PersistLegacySaintTaiyiCraftAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string reason,
        bool persistCharacterMoney,
        CancellationToken cancellationToken)
    {
        if (persistCharacterMoney)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Money"] = session.Character.Money.ToString(CultureInfo.InvariantCulture),
            };
            if (TryPersistLegacyCharacterAttributes(
                    session.UsersRoot,
                    session.AccountId,
                    values,
                    out var characterPath,
                    out var characterReason))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer persisted LegacyNpc SaintTaiyi craft character " +
                        $"{FormatSession(context)} source=NpcHandler.SaintTaiyi reason={reason} " +
                        $"path={Path.GetFileName(characterPath)} keys=Money.")
                    .ConfigureAwait(false);
            }
            else
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc SaintTaiyi craft character persistence " +
                        $"{FormatSession(context)} source=NpcHandler.SaintTaiyi reason={characterReason}.")
                    .ConfigureAwait(false);
            }
        }

        if (!string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            try
            {
                new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                    session.Character.Uid,
                    session.PlayerItems);
                await context.Log.WriteLineAsync(
                        "WorldServer persisted LegacyNpc SaintTaiyi craft items " +
                        $"{FormatSession(context)} source=NpcHandler.SaintTaiyi reason={reason} " +
                        $"path={session.Character.Uid.ToString(CultureInfo.InvariantCulture)}.bin.")
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc SaintTaiyi craft item persistence " +
                        $"{FormatSession(context)} source=NpcHandler.SaintTaiyi reason={reason} error={ex.Message}.")
                    .ConfigureAwait(false);
            }
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc SaintTaiyi craft missing PlayerItemsRoot " +
                    $"{FormatSession(context)} source=NpcHandler.SaintTaiyi reason={reason}; item mutation is session-only.")
                .ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<string> CreateLegacyDeityCobbWindDialogTexts()
    {
        var texts = new List<string>(LegacyDeityCobbWindDialogBaseTexts);
        var now = DateTime.Now;
        if (now.Minute == 5)
        {
            texts.Add("\t[Dragon Wraith] has appeared around (163,415) on the Deity Land!\n");
            texts.Add("Many heroes are heading to fight against the beasts. Hurry and go support them!");
        }
        else if ((now.Hour == 19 || now.Hour == 21) && now.Minute == 10)
        {
            texts.Add("\t[Queen of Evil] has appeared at random, central part of Deityland!\n");
            texts.Add("Many heroes are heading to fight against the beasts. Hurry and go support them!");
        }
        else if (now.Minute == 30)
        {
            texts.Add("\t[Chilling Spook] has appeared around (1020,698) on the Deity Land!\n");
            texts.Add("Many heroes are heading to fight against the beasts. Hurry and go support them!");
        }
        else if (now.Minute == 45)
        {
            texts.Add("\t[Nether Tyrant] has appeared at random, central part of Deityland!\n");
            texts.Add("Many heroes are heading to fight against the beasts. Hurry and go support them!");
        }
        else if (now.Minute == 57)
        {
            texts.Add("\t[Bloody Banshee] has appeared around (484,176) in the upper-right part of the Deityland!\n");
            texts.Add("Many heroes are heading to fight against the beasts. Hurry and go support them!");
        }
        else if (now.Minute < 5 || now.Minute > 57)
        {
            texts.Add($"*Dragon Wraith will appear at {now.Hour + (now.Minute > 57 ? 1 : 0)}:05 today around (163,415) in the upper-left part of the Deityland.*\n");
        }
        else if ((now.Hour == 19 || now.Hour == 21) && now.Minute < 10)
        {
            texts.Add($"*Queen of Evil will appear at {now.Hour}:10 today at random, central part of Deityland.*\n");
        }
        else if (now.Minute < 30)
        {
            texts.Add($"*Chilling Spook will appear at {now.Hour}:30 today around (1020,698) in the lower-right part of the Deityland.*\n");
        }
        else if (now.Minute < 45)
        {
            texts.Add($"*Nether Tyrant will appear at {now.Hour}:45 today at random, central part of Deityland.*\n");
        }
        else if (now.Minute < 57)
        {
            texts.Add($"*Bloody Banshee will appear at {now.Hour}:57 today around (484,176) in the upper-right part of the Deityland.*\n");
        }

        return texts;
    }

    private static bool TryCreateLegacyDeityCobbWindBossPrompt(
        byte optionId,
        out IReadOnlyList<string> texts,
        out IReadOnlyList<LegacyNpcDialogOption> options)
    {
        texts = [];
        options = [];
        var now = DateTime.Now;
        switch (optionId)
        {
            case 2:
                if (now.Minute != 5)
                {
                    texts = ["Dragon Wraith has not appeared yet. Do you want to go check the possible area, first?"];
                    options = [new LegacyNpcDialogOption("Go~to~the~upper-left~part.", 12)];
                }
                else
                {
                    texts = ["Dragon Wraith must be killed without any delay. Set off now?"];
                    options =
                    [
                        new LegacyNpcDialogOption("Go~right~up~to~Dragon Wraith.~(1~CP)", 22),
                        new LegacyNpcDialogOption("Go~to~the~upper-left~part.", 12),
                    ];
                }
                return true;
            case 3:
                if (now.Minute != 30)
                {
                    texts = ["Chilling Spook has not appeared yet. Do you want to go check the possible area, first?"];
                    options = [new LegacyNpcDialogOption("Go~to~the~lower-right~part.", 13)];
                }
                else
                {
                    texts = ["Chilling Spook must be killed without any delay. Set off now?"];
                    options =
                    [
                        new LegacyNpcDialogOption("Go~right~up~to~Chilling Spook.~(1~CP)", 23),
                        new LegacyNpcDialogOption("Go~to~the~lower-right~part.", 13),
                    ];
                }
                return true;
            case 4:
                if (now.Minute != 57)
                {
                    texts = ["Bloody Banshee has not appeared yet. Do you want to go check the possible area, first?"];
                    options = [new LegacyNpcDialogOption("Go~to~the~upper-right~part.", 14)];
                }
                else
                {
                    texts = ["Bloody Banshee must be killed without any delay. Set off now?"];
                    options =
                    [
                        new LegacyNpcDialogOption("Go~right~up~to~Bloody Banshee.~(1~CP)", 24),
                        new LegacyNpcDialogOption("Go~to~the~upper-right~part.", 14),
                    ];
                }
                return true;
            case 5:
                if (now.Hour == 19 || now.Hour == 21)
                {
                    texts = ["QueenOfEvil has not appeared yet. Do you want to go check the possible area, first?"];
                    options = [new LegacyNpcDialogOption("Go~to~the~upper-right~part.", 15)];
                }
                else
                {
                    texts = ["QueenOfEvil must be killed without any delay. Set off now?"];
                    options =
                    [
                        new LegacyNpcDialogOption("Go~right~up~to~QueenOfEvil.~(1~CP)", 25),
                        new LegacyNpcDialogOption("Go~to~the~upper-right~part.", 15),
                    ];
                }
                return true;
            case 6:
                if (now.Minute != 20)
                {
                    texts = ["RockMonsterKing has not appeared yet. Do you want to go check the possible area, first?"];
                    options = [new LegacyNpcDialogOption("Go~to~the~lower-right~part.", 16)];
                }
                else
                {
                    texts = ["RockMonsterKing must be killed without any delay. Set off now?"];
                    options =
                    [
                        new LegacyNpcDialogOption("Go~right~up~to~RockMonsterKing.~(1~CP)", 26),
                        new LegacyNpcDialogOption("Go~to~the~lower-right~part.", 16),
                    ];
                }
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask SendLegacyDragonYuKoonEnterPromptAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var attributes = new Dictionary<string, string>(
            session.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            [RuntimePendingMessageBoxAttribute] = RuntimeLegacyDragonYuKoonDeitylandMessageBox,
            [RuntimePendingMessageBoxMapIdAttribute] = 10_250.ToString(CultureInfo.InvariantCulture),
            [RuntimePendingMessageBoxXAttribute] = 1_014.ToString(CultureInfo.InvariantCulture),
            [RuntimePendingMessageBoxYAttribute] = 1_288.ToString(CultureInfo.InvariantCulture),
            [RuntimePendingMessageBoxDynamicIdAttribute] = 0.ToString(CultureInfo.InvariantCulture),
            [RuntimePendingMessageBoxSecondsAttribute] = 0.ToString(CultureInfo.InvariantCulture),
        };
        var updatedSession = session with { Character = session.Character with { Attributes = attributes } };
        context.Sessions?.Set(context.Connection, updatedSession);

        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(LegacyDragonYuKoonEnterDeitylandPromptText).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc message box " +
                $"{FormatSession(context)} source=NpcHandler.DragonYuKoon pending={RuntimeLegacyDragonYuKoonDeitylandMessageBox}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyDragonYuKoonMessageBoxReplyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyNpcDialogRequestPacket packet,
        CancellationToken cancellationToken)
    {
        var clearedSession = session with
        {
            Character = WithoutRuntimeMessageBoxAttributes(session.Character),
        };
        context.Sessions?.Set(context.Connection, clearedSession);

        if (!packet.IsAcceptedMessageBox)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer canceled DragonYuKoon Deityland message box " +
                    $"{FormatSession(context)} option={packet.OptionId}.")
                .ConfigureAwait(false);
            return;
        }

        await SendLegacyNpcTeleportAsync(
                context with { Session = clearedSession },
                clearedSession,
                LegacyDragonYuKoonNpcId,
                optionId: 1,
                new LegacyNpcTeleportDestination(10_250, 1_014, 1_288, "NpcHandler.DragonYuKoon"),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(LegacyDragonYuKoonEnterDeitylandBoxText).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer accepted DragonYuKoon Deityland message box " +
                $"{FormatSession(context)} map=10250 x=1014 y=1288.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyDeityCobbWindPaidTeleportAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        LegacyNpcTeleportDestination destination,
        CancellationToken cancellationToken)
    {
        if (session.Character.ConquerPoints < LegacyDeityCobbWindBossTeleportCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.DeityCobbWind",
                    [LegacyDeityCobbWindNoConquerPointsText],
                    LegacyDeityCobbWindNoConquerPointsOptions,
                    LegacyCobbWindAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var updatedCharacter = session.Character with
        {
            ConquerPoints = checked(session.Character.ConquerPoints - LegacyDeityCobbWindBossTeleportCost),
        };
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await PersistLegacyDeityCobbWindConquerPointsAsync(updatedContext, updatedSession)
            .ConfigureAwait(false);
        await SendScalarUpdateAsync(
                updatedContext,
                updatedCharacter,
                LegacyMsgUpdateDataType.ConquerPoints,
                checked((ulong)updatedCharacter.ConquerPoints),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcTeleportAsync(
                updatedContext,
                updatedSession,
                npcId,
                optionId,
                destination,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled DeityCobbWind paid teleport " +
                $"{FormatSession(updatedContext)} source={destination.SourceMethod} option={optionId} " +
                $"cost={LegacyDeityCobbWindBossTeleportCost.ToString(CultureInfo.InvariantCulture)} " +
                $"sourceConquerPointCost={LegacyDeityCobbWindBossTeleportCost.ToString(CultureInfo.InvariantCulture)} " +
                $"tunedConquerPointCost={LegacyDeityCobbWindBossTeleportCost.ToString(CultureInfo.InvariantCulture)} " +
                $"updatedConquerPoints={updatedCharacter.ConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                "walletUpdate=LegacyMsgUpdate.ConquerPoints.")
            .ConfigureAwait(false);
    }

    private static async ValueTask PersistLegacyDeityCobbWindConquerPointsAsync(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
        };

        if (TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                session.Character.Uid,
                values,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyNpc DeityCobbWind character " +
                    $"{FormatSession(context)} path={Path.GetFileName(path)} keys=ConquerPoints.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc DeityCobbWind character persistence " +
                $"{FormatSession(context)} reason={reason}.")
            .ConfigureAwait(false);
    }

    private static bool TryGetLegacyDeityCobbWindFreeDestination(
        byte optionId,
        out LegacyNpcTeleportDestination destination)
    {
        destination = optionId switch
        {
            12 => new LegacyNpcTeleportDestination(10_250, 366, 653, "NpcHandler.DeityCobbWind"),
            13 => new LegacyNpcTeleportDestination(10_250, 924, 819, "NpcHandler.DeityCobbWind"),
            14 => new LegacyNpcTeleportDestination(10_250, 471, 295, "NpcHandler.DeityCobbWind"),
            15 => new LegacyNpcTeleportDestination(10_250, 640, 621, "NpcHandler.DeityCobbWind"),
            16 => new LegacyNpcTeleportDestination(10_250, 422, 408, "NpcHandler.DeityCobbWind"),
            _ => default,
        };
        return !string.IsNullOrEmpty(destination.SourceMethod);
    }

    private static bool TryGetLegacyDeityCobbWindPaidDestination(
        byte optionId,
        out LegacyNpcTeleportDestination destination)
    {
        destination = optionId switch
        {
            22 => new LegacyNpcTeleportDestination(10_250, 163, 415, "NpcHandler.DeityCobbWind"),
            23 => new LegacyNpcTeleportDestination(10_250, 1_020, 698, "NpcHandler.DeityCobbWind"),
            24 => new LegacyNpcTeleportDestination(10_250, 484, 176, "NpcHandler.DeityCobbWind"),
            25 => new LegacyNpcTeleportDestination(10_250, 644, 605, "NpcHandler.DeityCobbWind"),
            26 => new LegacyNpcTeleportDestination(10_250, 422, 408, "NpcHandler.DeityCobbWind"),
            _ => default,
        };
        return !string.IsNullOrEmpty(destination.SourceMethod);
    }

    private static bool IsLegacyYuKoonNpc(uint npcId)
    {
        return npcId is LegacyYuKoonPrimaryNpcId or LegacyYuKoonSecondaryNpcId;
    }

    private static bool IsLegacyNezhaNpc(uint npcId)
    {
        return npcId is LegacyNezhaPrimaryNpcId or LegacyNezhaSecondaryNpcId;
    }

    private static bool IsLegacySaintTaiyiNpc(uint npcId)
    {
        return npcId is LegacySaintTaiyiPrimaryNpcId or LegacySaintTaiyiSecondaryNpcId;
    }

    private static bool IsLegacyRareDeitylandChestNpc(uint npcId)
    {
        return npcId is >= 22_598 and <= 22_605 or >= 21_973 and <= 21_976;
    }

    private static bool IsLegacyEpicDeitylandChestNpc(uint npcId)
    {
        return npcId is >= 22_607 and <= 22_609 or 21_974;
    }

    private static bool IsLegacyVictoryChestNpc(uint npcId)
    {
        return npcId is LegacyVictoryChestPrimaryNpcId or LegacyVictoryChestSecondaryNpcId;
    }

    private static bool IsLegacyDeityAltarNpc(uint npcId)
    {
        return npcId is LegacyDeityAltarPrimaryNpcId or LegacyDeityAltarSecondaryNpcId;
    }

    private static bool IsLegacyDeitylandPortalNpc(uint npcId)
    {
        return npcId is >= LegacyDeitylandPortalFirstNpcId and <= LegacyDeitylandPortalLastNpcId;
    }

    private static bool IsLegacyDeityCobbWindNpc(uint npcId)
    {
        return npcId is LegacyDeityCobbWindPrimaryNpcId or LegacyDeityCobbWindSecondaryNpcId;
    }
}
