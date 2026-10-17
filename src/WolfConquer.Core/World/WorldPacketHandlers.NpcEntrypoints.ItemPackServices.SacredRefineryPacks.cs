using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyItemPackServicesSelectSacredRefineryPackNpcId = 4_294_967_292;
    private const uint LegacyItemPackServicesSacredRefineryPackItemId = 3_004_249;
    private const ushort LegacyItemPackServicesSacredAvatarId = 200;
    private const string LegacyItemPackServicesSacredSource = "NpcHandler.SelectSacredMaterial";
    private const string LegacyItemPackServicesSacredSourceLines = "22513-22736";
    private const string LegacyItemPackServicesSacredSuccessText =
        "You received 1 Sacred Refinery Material.";

    private static async ValueTask<bool> LegacyItemPackServicesTryHandleSacredRefineryPackAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!LegacyItemPackServicesHasUnboundSacredRefineryPack(session))
        {
            await LegacyItemPackServicesSendMessageBoxAsync(
                    context,
                    LegacyItemPackServicesSacredSource,
                    "Sorry, you don`t have a SacredRefineryPack.",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (LegacyItemPackServicesSacredRefineryPages.TryGetValue(optionId, out var page))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyItemPackServicesSacredSource,
                    ["Choose what Refineries with level 6 you need ."],
                    page,
                    LegacyItemPackServicesSacredAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (LegacyItemPackServicesSacredRefineryRewards.TryGetValue(optionId, out var rewardItemId))
        {
            await LegacyItemPackServicesExchangeSacredRefineryPackAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    rewardItemId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (LegacyItemPackServicesSacredRefineryNoOpOptions.Contains(optionId))
        {
            await LegacyItemPackServicesLogNoOpAsync(
                    context,
                    npcId,
                    optionId,
                    LegacyItemPackServicesSacredSource,
                    "22738-22791")
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask LegacyItemPackServicesExchangeSacredRefineryPackAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        uint rewardItemId,
        CancellationToken cancellationToken)
    {
        // Contain gates on unbound items, while Remove consumes the first matching item regardless of binding.
        var packItem = session.PlayerItems.InventoryItems.FirstOrDefault(item =>
            item.ItemId == LegacyItemPackServicesSacredRefineryPackItemId);
        if (packItem is null || !LegacyItemPackServicesHasUnboundSacredRefineryPack(session))
        {
            await LegacyItemPackServicesLogSacredRefineryFenceAsync(
                    context,
                    npcId,
                    optionId,
                    rewardItemId,
                    "pack-count-drift")
                .ConfigureAwait(false);
            return;
        }

        if (optionId == 19 && !HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await LegacyItemPackServicesLogSacredRefineryFenceAsync(
                    context,
                    npcId,
                    optionId,
                    rewardItemId,
                    "inventory-full-before-source-add")
                .ConfigureAwait(false);
            return;
        }

        var consumedItems = ConsumeSingleLegacyInventoryItem(
            session.PlayerItems,
            packItem,
            out var consumptionFrame);
        if (optionId != 19 && !HasLegacyInventorySpace(consumedItems, 1))
        {
            await LegacyItemPackServicesLogSacredRefineryFenceAsync(
                    context,
                    npcId,
                    optionId,
                    rewardItemId,
                    "inventory-full-after-planned-pack-removal")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LegacyItemPackServicesLogSacredRefineryFenceAsync(
                    context,
                    npcId,
                    optionId,
                    rewardItemId,
                    "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        if (!sessions.TryGet(context.Connection, out var registeredSession) || registeredSession is null)
        {
            await LegacyItemPackServicesLogSacredRefineryFenceAsync(
                    context,
                    npcId,
                    optionId,
                    rewardItemId,
                    "session-registration-missing")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.Uid <= 0)
        {
            await LegacyItemPackServicesLogSacredRefineryFenceAsync(
                    context,
                    npcId,
                    optionId,
                    rewardItemId,
                    "character-uid-unavailable")
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            await LegacyItemPackServicesLogSacredRefineryFenceAsync(
                    context,
                    npcId,
                    optionId,
                    rewardItemId,
                    "player-items-root-missing")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyItemType(context.WorldData, rewardItemId, out var rewardItemType))
        {
            await LegacyItemPackServicesLogSacredRefineryFenceAsync(
                    context,
                    npcId,
                    optionId,
                    rewardItemId,
                    "reward-itemtype-missing")
                .ConfigureAwait(false);
            return;
        }

        if (rewardItemType.Id != checked((int)rewardItemId) ||
            rewardItemType.LegacyDurability > ushort.MaxValue)
        {
            await LegacyItemPackServicesLogSacredRefineryFenceAsync(
                    context,
                    npcId,
                    optionId,
                    rewardItemId,
                    "reward-itemtype-invalid")
                .ConfigureAwait(false);
            return;
        }

        PlayerItemSnapshotItem rewardItem;
        try
        {
            rewardItem = CreatePurchasedItems(
                    session.PlayerItems,
                    rewardItemType,
                    count: 1,
                    isBound: false)[0]
                .WithColor(checked((uint)Random.Shared.Next(3, 9)));
        }
        catch (OverflowException)
        {
            await LegacyItemPackServicesLogSacredRefineryFenceAsync(
                    context,
                    npcId,
                    optionId,
                    rewardItemId,
                    "item-uid-exhausted")
                .ConfigureAwait(false);
            return;
        }

        var updatedItems = consumedItems with
        {
            InventoryItems = consumedItems.InventoryItems
                .Concat([rewardItem])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                updatedSession.Character.Uid,
                updatedItems);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            System.Security.SecurityException)
        {
            await LegacyItemPackServicesLogSacredRefineryFenceAsync(
                    context,
                    npcId,
                    optionId,
                    rewardItemId,
                    "persistence-" + ex.GetType().Name)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        var rewardFrame = LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes();

        if (optionId == 19)
        {
            await context.Connection.SendAsync(rewardFrame, cancellationToken).ConfigureAwait(false);
            await SendLegacyItemFollowupsAsync(updatedContext, rewardItem, cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(consumptionFrame, cancellationToken).ConfigureAwait(false);
            await LegacyItemPackServicesSendMessageBoxAsync(
                    updatedContext,
                    LegacyItemPackServicesSacredSource,
                    LegacyItemPackServicesSacredSuccessText,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await context.Connection.SendAsync(consumptionFrame, cancellationToken).ConfigureAwait(false);
            await LegacyItemPackServicesSendMessageBoxAsync(
                    updatedContext,
                    LegacyItemPackServicesSacredSource,
                    LegacyItemPackServicesSacredSuccessText,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(rewardFrame, cancellationToken).ConfigureAwait(false);
            await SendLegacyItemFollowupsAsync(updatedContext, rewardItem, cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc ItemPackServices Sacred Refinery Pack exchange " +
                $"{FormatSession(updatedContext)} source={LegacyItemPackServicesSacredSource} " +
                $"sourceLines={LegacyItemPackServicesSacredSourceLines} npcId={npcId} optionId={optionId} " +
                $"packItemId={LegacyItemPackServicesSacredRefineryPackItemId} " +
                $"rewardItemId={rewardItemId} rewardItemUid={rewardItem.Uid} bound=false color={rewardItem.Color} " +
                $"sourceOrder={(optionId == 19 ? "Add,Remove,MessageBox" : "Remove,MessageBox,Add")}.")
            .ConfigureAwait(false);
    }

    private static bool LegacyItemPackServicesHasUnboundSacredRefineryPack(WorldSession session)
    {
        return session.PlayerItems.InventoryItems.Any(item =>
            item.ItemId == LegacyItemPackServicesSacredRefineryPackItemId &&
            item.Bound == 0 &&
            item.StackSize > 0);
    }

    private static async ValueTask LegacyItemPackServicesLogSacredRefineryFenceAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        byte optionId,
        uint rewardItemId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc ItemPackServices Sacred Refinery Pack exchange " +
                $"{FormatSession(context)} source={LegacyItemPackServicesSacredSource} " +
                $"sourceLines={LegacyItemPackServicesSacredSourceLines} npcId={npcId} optionId={optionId} " +
                $"packItemId={LegacyItemPackServicesSacredRefineryPackItemId} " +
                $"rewardItemId={rewardItemId} reason={reason}; " +
                "no item persistence, live session mutation, or exchange packet was committed.")
            .ConfigureAwait(false);
    }

    private static readonly IReadOnlyDictionary<byte, LegacyNpcDialogOption[]> LegacyItemPackServicesSacredRefineryPages =
        new Dictionary<byte, LegacyNpcDialogOption[]>
        {
            [0] =
            [
                new("Detoxication[Bag]. ", 32),
                new("Detoxication[Necklace]. ", 33),
                new("Breakthrough[Bracelet]. ", 34),
                new("Breakthrough[Ring]. ", 36),
                new("Penetration[Bracelet]. ", 37),
                new("Detoxication[Armor]. ", 38),
                new("Next Page.", 39),
            ],
            [31] =
            [
                new("Detoxication[Bag]. ", 32),
                new("Detoxication[Necklace]. ", 33),
                new("Breakthrough[Bracelet]. ", 34),
                new("Breakthrough[Ring]. ", 36),
                new("Penetration[Bracelet]. ", 37),
                new("Detoxication[Armor]. ", 38),
                new("Next Page.", 39),
            ],
            [39] =
            [
                new("Counteraction[Necklace]. ", 15),
                new("Breakthrough[2-Handed]. ", 16),
                new("CriticalStrike[Ring]. ", 17),
                new("M-Defense[Bracelet]. ", 18),
                new("CriticalStrike[2-Handed]. ", 19),
                new("M-Defense[Ring]. ", 20),
                new("Next Page.", 21),
            ],
            [21] =
            [
                new("Penetration[Bag]. ", 22),
                new("Block[Shield]. ", 23),
                new("Detoxication[Boots]. ", 24),
                new("Counteraction[Bag]. ", 25),
                new("Breakthrough[Bow]. ", 26),
                new("Immunity[Boots]. ", 27),
                new("Next Page.", 28),
            ],
            [28] =
            [
                new("SkillC.Strike[Bracelet]. ", 29),
                new("CriticalStrike[Bow]. ", 30),
                new("M-Defense[Bag]. ", 41),
                new("Penetration[HeadGear]. ", 42),
                new("Block[HeadGear]. ", 43),
                new("Detoxication[Headgear]. ", 44),
                new("Next Page.", 45),
            ],
            [45] =
            [
                new("SkillC.Strike[Bracelet]. ", 29),
                new("CriticalStrike[Bow]. ", 30),
                new("M-Defense[Bag]. ", 41),
                new("Penetration[HeadGear]. ", 42),
                new("Block[HeadGear]. ", 43),
                new("Detoxication[Headgear]. ", 44),
                new("Next Page.", 46),
            ],
            [46] =
            [
                new("Counteraction[Armor]. ", 47),
                new("Breakthrough[1-Handed]. ", 48),
                new("Intensification[Headgear]. ", 49),
                new("Immunity[Armor]. ", 83),
                new("SkillC.Strike[Backsword]. ", 84),
                new("CriticalStrike[1-Handed]. ", 85),
                new("Next Page.", 86),
            ],
            [86] =
            [
                new("M-Defense[Necklace]. ", 87),
                new("SkillC.Strike[Backsword]. ", 88),
                new("Penetration[Bracelet]. ", 89),
                new("Detoxication[Bag]. ", 90),
                new("Penetration[Headgear]. ", 91),
                new("Detoxication[Necklace]. ", 92),
                new("Next Page.", 93),
            ],
            [93] =
            [
                new("Detoxication[Boots]. ", 94),
                new("Detoxication[Armor]. ", 95),
                new("Detoxication[Headgear]. ", 96),
                new("Block[Shield]. ", 97),
                new("Breakthrough[Bow]. ", 101),
                new("Breakthrough[2-Handed]. ", 102),
                new("Next Page.", 103),
            ],
            [103] =
            [
                new("Breakthrough[1-Handed]. ", 104),
                new("CriticalStrike[2-Handed]. ", 105),
                new("CriticalStrike[1-Handed]. ", 106),
                new("CriticalStrike[Bow]. ", 107),
                new("First page.", 31),
            ],
        };

    private static readonly IReadOnlyDictionary<byte, uint> LegacyItemPackServicesSacredRefineryRewards =
        new Dictionary<byte, uint>
        {
            [15] = 3_004_160,
            [16] = 3_004_159,
            [17] = 3_004_158,
            [18] = 3_004_157,
            [19] = 3_004_156,
            [20] = 3_004_155,
            [22] = 3_004_154,
            [23] = 3_004_153,
            [24] = 3_004_152,
            [25] = 3_004_151,
            [26] = 3_004_150,
            [27] = 3_004_149,
            [29] = 3_004_148,
            [30] = 3_004_147,
            [32] = 3_004_166,
            [33] = 3_004_165,
            [34] = 3_004_164,
            [36] = 3_004_163,
            [37] = 3_004_162,
            [41] = 3_004_146,
            [42] = 3_004_145,
            [43] = 3_004_144,
            [44] = 3_004_143,
            [47] = 3_004_142,
            [48] = 3_004_141,
            [49] = 3_004_140,
            [83] = 3_004_139,
            [84] = 3_004_138,
            [85] = 3_004_137,
            [87] = 3_004_136,
            [88] = 3_004_138,
            [89] = 3_004_162,
            [90] = 3_004_154,
            [91] = 3_004_145,
            [92] = 3_004_165,
            [94] = 3_004_152,
            [95] = 3_004_161,
            [96] = 3_004_143,
            [97] = 3_004_153,
            [101] = 3_004_150,
            [102] = 3_004_159,
            [104] = 3_004_141,
            [105] = 3_004_156,
            [106] = 3_004_137,
            [107] = 3_004_147,
        };

    private static readonly HashSet<byte> LegacyItemPackServicesSacredRefineryNoOpOptions =
    [
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 35, 38, 40, 50, 51, 52, 53, 54,
        55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73,
        74, 75, 76, 77, 78, 79, 80, 81, 82, 98, 99, 100,
    ];
}
