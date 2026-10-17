using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyItemPackServicesSteed3PackNpcId = 4_294_967_264;
    private const uint LegacyItemPackServicesSteed1PackNpcId = 4_294_967_265;
    private const uint LegacyItemPackServicesRareSteedPack6NpcId = 4_294_967_280;
    private const uint LegacyItemPackServicesNobleSteedPackNpcId = 4_294_967_281;
    private const uint LegacyItemPackServicesSteed6NpcId = 4_294_967_289;
    private const uint LegacyItemPackServicesSteed3NpcId = 4_294_967_290;
    private const uint LegacyItemPackServicesSteed1NpcId = 4_294_967_291;
    private const uint LegacyItemPackServicesSteed3PackItemId = 3_007_283;
    private const uint LegacyItemPackServicesSteed1PackItemId = 3_007_287;
    private const uint LegacyItemPackServicesSteed1ItemId = 728_525;
    private const uint LegacyItemPackServicesSteed3ItemId = 728_526;
    private const uint LegacyItemPackServicesSteed6ItemId = 728_527;
    private const uint LegacyItemPackServicesRareSteedPack6ItemId = 3_001_406;
    private const uint LegacyItemPackServicesNobleSteedPackItemId = 729_990;
    private const uint LegacyItemPackServicesGrantedSteedItemId = 300_000;

    private static async ValueTask<bool> LegacyItemPackServicesTryHandleBoundSteedPackAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        uint packItemId,
        string plusText,
        string source,
        string sourceLines,
        CancellationToken cancellationToken)
    {
        if (!LegacyItemPackServicesHasBindingClassStack(session, packItemId, 1, requiredBound: 1))
        {
            await LegacyItemPackServicesSendMessageBoxAsync(
                    context,
                    source,
                    "Sorry, you don`t have a Steed+1Pack.",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [$"You can claim one of three {plusText} bound steeds from this pack. Which one do you want?"],
                        [
                            new($"{plusText}~White~Steed(B)", 1),
                            new($"{plusText}~Brown~Steed(B)", 5),
                            new($"{plusText}~Black~Steed(B)", 10),
                            new("Nevermind", LegacyNpcClosedOption),
                        ],
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await LegacyItemPackServicesSendConfirmAsync(
                        context,
                        source,
                        $"Are you sure you want to claim a {plusText} White Steed (B) from this pack?",
                        yesOptionId: 2,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 5:
                await LegacyItemPackServicesSendConfirmAsync(
                        context,
                        source,
                        $"Are you sure you want to claim a {plusText} Brown Steed (B) from this pack?",
                        yesOptionId: 6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 10:
                await LegacyItemPackServicesSendConfirmAsync(
                        context,
                        source,
                        $"Are you sure you want to claim a {plusText} Black Steed (B) from this pack?",
                        yesOptionId: 11,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
            case 6:
            case 11:
                var selection = optionId switch
                {
                    2 => new LegacyItemPackServicesSteedSelection(
                        Plus: plusText == "+3" ? (byte)3 : (byte)1,
                        Bound: true,
                        ProgressGreen: 150,
                        ProgressBlue: byte.MaxValue,
                        ProgressRed: 0,
                        SuccessTalk: $"You received {plusText} White Steed (B)!"),
                    6 => new LegacyItemPackServicesSteedSelection(
                        Plus: plusText == "+3" ? (byte)3 : (byte)1,
                        Bound: true,
                        ProgressGreen: 0,
                        ProgressBlue: 150,
                        ProgressRed: byte.MaxValue,
                        SuccessTalk: $"You received {plusText} Brown Steed (B)!"),
                    _ => new LegacyItemPackServicesSteedSelection(
                        Plus: plusText == "+3" ? (byte)3 : (byte)1,
                        Bound: true,
                        ProgressGreen: byte.MaxValue,
                        ProgressBlue: 0,
                        ProgressRed: 150,
                        SuccessTalk: $"You received {plusText} Black Steed (B)!"),
                };
                await LegacyItemPackServicesExchangeSteedAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        packItemId,
                        selection,
                        source,
                        optionId switch
                        {
                            2 => source == "NpcHandler.Steed3Pack" ? "21351-21354" : "21404-21407",
                            6 => source == "NpcHandler.Steed3Pack" ? "21342-21345" : "21395-21398",
                            _ => source == "NpcHandler.Steed3Pack" ? "21333-21336" : "21386-21389",
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 3:
            case 4:
            case 7:
            case 8:
            case 9:
                await LegacyItemPackServicesLogNoOpAsync(context, npcId, optionId, source, sourceLines)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyItemPackServicesTryHandleSimpleSteedPackAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        uint packItemId,
        string plusText,
        string source,
        string sourceLines,
        CancellationToken cancellationToken)
    {
        if (!LegacyItemPackServicesHasBindingClassStack(session, packItemId, 1, requiredBound: 0))
        {
            await LegacyItemPackServicesSendMessageBoxAsync(
                    context,
                    source,
                    "Sorry, you don`t have a Steed+1.",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    ["Hello. You can choose a black, brown, or white horse."],
                    [
                        new("Black~horse", 1),
                        new("Brown~horse", 2),
                        new("White~horse", 3),
                        new("Nevermind", LegacyNpcClosedOption),
                    ],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId is >= 1 and <= 3)
        {
            var plus = plusText switch
            {
                "+1" => (byte)1,
                "+3" => (byte)3,
                _ => (byte)6,
            };
            var selection = optionId switch
            {
                1 => new LegacyItemPackServicesSteedSelection(
                    plus,
                    Bound: false,
                    ProgressGreen: byte.MaxValue,
                    ProgressBlue: 0,
                    ProgressRed: 150,
                    SuccessTalk: null),
                2 => new LegacyItemPackServicesSteedSelection(
                    plus,
                    Bound: false,
                    ProgressGreen: 0,
                    ProgressBlue: 150,
                    ProgressRed: byte.MaxValue,
                    SuccessTalk: null),
                _ => new LegacyItemPackServicesSteedSelection(
                    plus,
                    Bound: false,
                    ProgressGreen: 150,
                    ProgressBlue: byte.MaxValue,
                    ProgressRed: 0,
                    SuccessTalk: null),
            };
            await LegacyItemPackServicesExchangeSteedAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    packItemId,
                    selection,
                    source,
                    sourceLines,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> LegacyItemPackServicesTryHandleRareSteedPackAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        uint packItemId,
        string source,
        string sourceLines,
        CancellationToken cancellationToken)
    {
        if (!LegacyItemPackServicesHasBindingClassStack(session, packItemId, 1, requiredBound: 0))
        {
            await LegacyItemPackServicesSendMessageBoxAsync(
                    context,
                    source,
                    "Sorry, you don`t have a NobleSteedPack",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    ["Hello. You can choose a rare +6 horse."],
                    [
                        new("+6~Spitfire~Steed", 1),
                        new("+6~Frostbite~Steed", 2),
                        new("+6~Blazehoof~Steed", 3),
                        new("+6~Spotted~Steed", 4),
                        new("+6~Zebra", 5),
                        new("Nevermind", LegacyNpcClosedOption),
                    ],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId is >= 1 and <= 5)
        {
            var selection = optionId switch
            {
                1 => new LegacyItemPackServicesSteedSelection(6, false, 201, 135, 187, null),
                2 => new LegacyItemPackServicesSteedSelection(6, false, 184, 63, 62, null),
                3 => new LegacyItemPackServicesSteedSelection(6, false, 137, 156, 148, null),
                4 => new LegacyItemPackServicesSteedSelection(6, false, 122, 134, 147, null),
                _ => new LegacyItemPackServicesSteedSelection(6, false, 46, 39, 142, null),
            };
            await LegacyItemPackServicesExchangeSteedAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    packItemId,
                    selection,
                    source,
                    sourceLines,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask LegacyItemPackServicesExchangeSteedAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        uint packItemId,
        LegacyItemPackServicesSteedSelection selection,
        string source,
        string sourceLines,
        CancellationToken cancellationToken)
    {
        // Inventory.Remove gates by one binding class, then removes the first matching item without that filter.
        var packItem = session.PlayerItems.InventoryItems.FirstOrDefault(item => item.ItemId == packItemId);
        if (packItem is null)
        {
            await LegacyItemPackServicesLogSteedFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    source,
                    sourceLines,
                    "pack-count-drift; no session mutation was committed")
                .ConfigureAwait(false);
            return;
        }

        var consumedItems = ConsumeSingleLegacyInventoryItem(
            session.PlayerItems,
            packItem,
            out var consumptionFrame);
        if (!HasLegacyInventorySpace(consumedItems, 1))
        {
            await LegacyItemPackServicesSendMessageBoxAsync(
                    context,
                    source,
                    LegacyItemPackServicesInventorySpaceText,
                    cancellationToken)
                .ConfigureAwait(false);
            await LegacyItemPackServicesLogSteedFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    source,
                    sourceLines,
                    "inventory-full-after-planned-pack-removal; no pack was consumed and no steed was granted")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LegacyItemPackServicesLogSteedFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    source,
                    sourceLines,
                    "session-store-missing; no item persistence or live mutation occurred")
                .ConfigureAwait(false);
            return;
        }

        var playerItemsRoot = context.PlayerItemsRoot;
        if (string.IsNullOrWhiteSpace(playerItemsRoot))
        {
            await LegacyItemPackServicesLogSteedFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    source,
                    sourceLines,
                    "player-items-root-missing; no item persistence or live mutation occurred")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyItemType(context.WorldData, LegacyItemPackServicesGrantedSteedItemId, out var steedItemType))
        {
            await LegacyItemPackServicesLogSteedFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    source,
                    sourceLines,
                    $"reward-itemtype-missing:itemId={LegacyItemPackServicesGrantedSteedItemId}; no pack was consumed")
                .ConfigureAwait(false);
            return;
        }

        if (steedItemType.Id != checked((int)LegacyItemPackServicesGrantedSteedItemId) ||
            steedItemType.LegacyDurability > ushort.MaxValue)
        {
            await LegacyItemPackServicesLogSteedFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    source,
                    sourceLines,
                    $"reward-itemtype-invalid:itemId={LegacyItemPackServicesGrantedSteedItemId}; no pack was consumed")
                .ConfigureAwait(false);
            return;
        }

        PlayerItemSnapshotItem grantedSteed;
        try
        {
            var durability = checked((ushort)steedItemType.LegacyDurability);
            grantedSteed = new PlayerItemSnapshotItem
            {
                Uid = GetNextSessionItemUid(session.PlayerItems),
                ItemId = LegacyItemPackServicesGrantedSteedItemId,
                Durability = durability,
                MaximumDurability = durability,
                Position = 0,
                SocketProgress = selection.SocketProgress,
                Plus = selection.Plus,
                Bless = selection.ProgressRed,
                Bound = selection.Bound ? (byte)1 : (byte)0,
                Enchant = selection.ProgressBlue,
                StackSize = 1,
                Color = checked((uint)Random.Shared.Next(3, 9)),
            };
        }
        catch (OverflowException)
        {
            await LegacyItemPackServicesLogSteedFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    source,
                    sourceLines,
                    "item-uid-exhausted; no pack was consumed and no steed was granted")
                .ConfigureAwait(false);
            return;
        }

        var updatedItems = consumedItems with
        {
            InventoryItems = consumedItems.InventoryItems
                .Concat([grantedSteed])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(
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
            await LegacyItemPackServicesLogSteedFenceAsync(
                    context,
                    npcId,
                    optionId,
                    packItemId,
                    source,
                    sourceLines,
                    $"persistence-{ex.GetType().Name}; no live session mutation or item packet was committed")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        if (selection.SuccessTalk is not null)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(selection.SuccessTalk).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(consumptionFrame, cancellationToken).ConfigureAwait(false);
        var grantedSteedPacket = LegacyGameItemPacket.FromSnapshotItem(grantedSteed) with
        {
            ProgressGreen = selection.ProgressGreen,
        };
        await context.Connection.SendAsync(grantedSteedPacket.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc ItemPackServices steed exchange " +
                $"{FormatSession(updatedContext)} source={source} sourceLines={sourceLines} " +
                $"npcId={npcId} optionId={optionId} packItemId={packItemId} " +
                $"rewardItemId={LegacyItemPackServicesGrantedSteedItemId} plus={selection.Plus} " +
                $"bound={selection.Bound.ToString().ToLowerInvariant()} socketProgress={selection.SocketProgress} " +
                $"progressGreen={selection.ProgressGreen} enchant={selection.ProgressBlue} " +
                $"bless={selection.ProgressRed} color={grantedSteed.Color} " +
                $"sourceOrder={(selection.SuccessTalk is null ? "remove+add-steed" : "talk+remove+add-steed")}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask LegacyItemPackServicesLogSteedFenceAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        byte optionId,
        uint packItemId,
        string source,
        string sourceLines,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc ItemPackServices steed exchange " +
                $"{FormatSession(context)} source={source} sourceLines={sourceLines} " +
                $"npcId={npcId} optionId={optionId} packItemId={packItemId} reason={reason}.")
            .ConfigureAwait(false);
    }

    private static bool LegacyItemPackServicesHasBindingClassStack(
        WorldSession session,
        uint itemId,
        ushort count,
        byte requiredBound)
    {
        var total = 0u;
        foreach (var item in session.PlayerItems.InventoryItems)
        {
            if (item.ItemId != itemId ||
                (requiredBound == 0 ? item.Bound != 0 : item.Bound == 0))
            {
                continue;
            }

            total += item.StackSize;
            if (total >= count)
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct LegacyItemPackServicesSteedSelection(
        byte Plus,
        bool Bound,
        byte ProgressGreen,
        byte ProgressBlue,
        byte ProgressRed,
        string? SuccessTalk)
    {
        public uint SocketProgress => checked((uint)(
            ProgressGreen |
            (ProgressBlue << 8) |
            (ProgressRed << 16)));
    }
}
