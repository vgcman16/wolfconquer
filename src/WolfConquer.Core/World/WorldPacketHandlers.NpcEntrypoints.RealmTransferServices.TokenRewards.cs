using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyRealmTransferServicesGoldPrizeTokenNpcId = 4_294_967_269;
    private const uint LegacyRealmTransferServicesSashFragmentRealmNpcId = 4_294_967_277;

    private const uint LegacyRealmTransferServicesGoldPrizeTokenItemId = 723_467;
    private const uint LegacyRealmTransferServicesGoldPrizeRewardItemId = 2_100_075;
    private const uint LegacyRealmTransferServicesSashFragmentItemId = 3_600_024;
    private const uint LegacyRealmTransferServicesSmallSashItemId = 1_100_003;
    private const ushort LegacyRealmTransferServicesGoldPrizeTokenCount = 3;
    private const uint LegacyRealmTransferServicesSashFragmentRatePercent = 20;
    private const ushort LegacyRealmTransferServicesSashFragmentCombineCount = 5;
    private const string RuntimeLegacyRealmTransferServicesSashFragmentRateRollAttribute =
        "LegacyRealmTransferServicesSashFragmentRateRoll";

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesGoldPrizeTokenOptions =
    [
        new("Yes,~I~am~ready.", 1),
        new("Nevermind.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesSashFragmentOptions =
    [
        new("1~fragment.~(20%~chance)", 1),
        new("Combine~5~fragments.", 10),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesSashFragmentCombineSuccessOptions =
    [
        new("Great!", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesSashFragmentChanceResultOptions =
    [
        new("Great!", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesSashFragmentCombineMissingOptions =
    [
        new("Alright.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> TryHandleLegacyRealmTransferServicesGoldPrizeTokenAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.GoldPrizeToken";
        const string sourceLines = "24120-24189";

        if (!HasLegacyRealmTransferServicesUnboundGoldPrizeTokens(
                session,
                LegacyRealmTransferServicesGoldPrizeTokenCount))
        {
            await SendLegacyRealmTransferServicesNoticeAsync(
                    context,
                    source,
                    "Sorry, you do not have 3 Lord Tokens.",
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return optionId is 0 or 1;
        }

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [$"Hey {session.Character.Name}, are you sure your account is ready to equip the legendary Gold Prize for 3 tokens?"],
                        LegacyRealmTransferServicesGoldPrizeTokenOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                if (!HasLegacyInventorySpace(session.PlayerItems, 1))
                {
                    await SendLegacyRealmTransferServicesNoticeAsync(
                            context,
                            source,
                            "Please make one more space in your inventory.",
                            avatarId: null,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await ExchangeLegacyRealmTransferServicesGoldPrizeTokensAsync(
                        context,
                        session,
                        optionId,
                        source,
                        sourceLines,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask ExchangeLegacyRealmTransferServicesGoldPrizeTokensAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string source,
        string sourceLines,
        ushort? avatarId,
        CancellationToken cancellationToken)
    {
        var playerItemsRoot = context.PlayerItemsRoot;
        if (string.IsNullOrWhiteSpace(playerItemsRoot))
        {
            await SendLegacyRealmTransferServicesGapAsync(
                    context,
                    LegacyRealmTransferServicesGoldPrizeTokenNpcId,
                    optionId,
                    source,
                    sourceLines,
                    "remove 3 Lord Tokens, broadcast the claim, write token log, and grant GoldPrize item 2100075",
                    "Gold Prize Token exchange is unavailable here until token removal, reward grants, broadcast, and audit logging are modeled safely.",
                    avatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Gold Prize Token exchange " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    "reason=session-store-missing; no item persistence, broadcast, audit, or session mutation occurred.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyItemType(context.WorldData, LegacyRealmTransferServicesGoldPrizeRewardItemId, out var rewardItemType))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Gold Prize Token exchange " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"rewardItemId={LegacyRealmTransferServicesGoldPrizeRewardItemId} " +
                    "reason=itemtype-missing; no tokens were consumed and no broadcast or audit was emitted.")
                .ConfigureAwait(false);
            return;
        }

        if (rewardItemType.Id != LegacyRealmTransferServicesGoldPrizeRewardItemId ||
            rewardItemType.LegacyDurability > ushort.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Gold Prize Token exchange " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"rewardItemId={LegacyRealmTransferServicesGoldPrizeRewardItemId} " +
                    "reason=itemtype-invalid; no tokens were consumed and no broadcast or audit was emitted.")
                .ConfigureAwait(false);
            return;
        }

        PlayerItemSnapshotItem grantedItem;
        try
        {
            grantedItem = CreatePurchasedItems(
                session.PlayerItems,
                rewardItemType,
                count: 1,
                isBound: false)[0];
        }
        catch (OverflowException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Gold Prize Token exchange " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"rewardItemId={LegacyRealmTransferServicesGoldPrizeRewardItemId} " +
                    "reason=item-uid-exhausted; no tokens were consumed and no broadcast or audit was emitted.")
                .ConfigureAwait(false);
            return;
        }

        var consumptionFrames = new List<byte[]>(LegacyRealmTransferServicesGoldPrizeTokenCount);
        var updatedItems = session.PlayerItems;
        for (var index = 0; index < LegacyRealmTransferServicesGoldPrizeTokenCount; index++)
        {
            var token = updatedItems.InventoryItems.FirstOrDefault(
                item => item.ItemId == LegacyRealmTransferServicesGoldPrizeTokenItemId && item.Bound == 0);
            if (token is null)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc RealmTransferServices Gold Prize Token exchange " +
                        $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                        "reason=unbound-token-count-drift; no session mutation, broadcast, or audit was committed.")
                    .ConfigureAwait(false);
                return;
            }

            updatedItems = ConsumeSingleLegacyInventoryItem(updatedItems, token, out var consumptionFrame);
            consumptionFrames.Add(consumptionFrame);
        }

        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems
                .Concat([grantedItem])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };

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
            NotSupportedException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Gold Prize Token exchange persistence " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"rewardItemId={LegacyRealmTransferServicesGoldPrizeRewardItemId} reason={ex.GetType().Name}; " +
                    "no session mutation, broadcast, or audit was committed.")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        var globalMessage =
            $"Congratulation to {updatedSession.Character.Name} ! he/she managed to open the legendary LordToken and received 1 GoldPrize.";
        var globalFrame = LegacyMsgMessagePacket.SystemTalk(globalMessage).ToFrameBytes();
        var activeSessions = sessions.Snapshot();
        if (activeSessions.Count == 0)
        {
            await context.Connection.SendAsync(globalFrame, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            foreach (var active in activeSessions)
            {
                await active.Connection.SendAsync(globalFrame, cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (var consumptionFrame in consumptionFrames)
        {
            await context.Connection.SendAsync(consumptionFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "TokenGoldPrize audit " +
                $"playerName={updatedSession.Character.Name} characterUid={updatedSession.Character.Uid} " +
                $"accountId={updatedSession.AccountId} username={updatedSession.Username} " +
                $"timestampUtc={DateTimeOffset.UtcNow:O} " +
                $"source={source} sourceLines=24153-24181 optionId={optionId}.")
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(grantedItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyItemFollowupsAsync(updatedContext, grantedItem, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc RealmTransferServices Gold Prize Token exchange " +
                $"{FormatSession(updatedContext)} source={source} sourceLines=24150-24187 optionId={optionId} " +
                $"tokenItemId={LegacyRealmTransferServicesGoldPrizeTokenItemId} " +
                $"tokenCount={LegacyRealmTransferServicesGoldPrizeTokenCount} " +
                $"rewardItemId={LegacyRealmTransferServicesGoldPrizeRewardItemId} bound=false " +
                "sourceOrder=global-talk+remove-three+audit+add-item no-success-dialog=true.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyRealmTransferServicesSashFragmentAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.SashFragment_Realm";
        const string sourceLines = "24192-24255";

        if (!HasLegacyRealmTransferServicesUnboundSashFragments(session, requiredCount: 1))
        {
            await SendLegacyRealmTransferServicesNoticeAsync(
                    context,
                    source,
                    "Sorry, you don`t have a SashFragment(Realm).",
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return optionId is 0 or 1 or 10;
        }

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "Using one fragment gives a 20 percent chance to get a Sash (S), and the fragment is lost if it fails.",
                            "With 5 fragments, you can combine them into a Sash (S). How many fragments would you like to use?",
                        ],
                        LegacyRealmTransferServicesSashFragmentOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                if (!HasLegacyInventorySpace(session.PlayerItems, 1))
                {
                    await SendLegacyRealmTransferServicesNoticeAsync(
                            context,
                            source,
                            "Please make one more space in your inventory.",
                            avatarId: null,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await UseSingleLegacyRealmTransferServicesSashFragmentAsync(
                        context,
                        session,
                        optionId,
                        source,
                        sourceLines,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
                if (!HasLegacyInventorySpace(session.PlayerItems, 1))
                {
                    await SendLegacyRealmTransferServicesNoticeAsync(
                            context,
                            source,
                            "Please make 1 more space in your inventory.",
                            avatarId: null,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (!HasLegacyRealmTransferServicesUnboundSashFragments(
                        session,
                        LegacyRealmTransferServicesSashFragmentCombineCount))
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            source,
                            ["You should have 5 Sash Fragments to combine them into a Sash (S). Please retry when you have enough fragments."],
                            LegacyRealmTransferServicesSashFragmentCombineMissingOptions,
                            avatarId: null,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await CombineLegacyRealmTransferServicesSashFragmentsAsync(
                        context,
                        session,
                        optionId,
                        source,
                        sourceLines,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask UseSingleLegacyRealmTransferServicesSashFragmentAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string source,
        string sourceLines,
        ushort? avatarId,
        CancellationToken cancellationToken)
    {
        var playerItemsRoot = context.PlayerItemsRoot;
        if (string.IsNullOrWhiteSpace(playerItemsRoot))
        {
            await SendLegacyRealmTransferServicesGapAsync(
                    context,
                    LegacyRealmTransferServicesSashFragmentRealmNpcId,
                    optionId,
                    source,
                    sourceLines,
                    "remove 1 Sash Fragment (Realm), roll 20 percent, and maybe grant Sash (S) item 1100003",
                    "Single Sash Fragment exchange is unavailable here until random reward rolls and inventory mutation are modeled safely.",
                    avatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Sash Fragment chance " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    "reason=session-store-missing; no item persistence or fragment mutation occurred.")
                .ConfigureAwait(false);
            return;
        }

        var fragment = session.PlayerItems.InventoryItems.FirstOrDefault(
            item => item.ItemId == LegacyRealmTransferServicesSashFragmentItemId && item.Bound == 0);
        if (fragment is null)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Sash Fragment chance " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    "reason=fragment-source-drift; no session mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        var rateRoll = TryReadUInt32Attribute(
                session.Character.Attributes,
                out var forcedRateRoll,
                RuntimeLegacyRealmTransferServicesSashFragmentRateRollAttribute)
            ? forcedRateRoll % 100
            : checked((uint)(Random.Shared.Next() % 100));
        var rateHit = LegacyRealmTransferServicesSashFragmentRatePercent > rateRoll;

        PlayerItemSnapshotItem? grantedItem = null;
        if (rateHit)
        {
            if (!TryReadLegacyItemType(context.WorldData, LegacyRealmTransferServicesSmallSashItemId, out var sashItemType))
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc RealmTransferServices Sash Fragment chance " +
                        $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                        $"rewardItemId={LegacyRealmTransferServicesSmallSashItemId} rateRoll={rateRoll} " +
                        "reason=itemtype-missing; no fragment was consumed.")
                    .ConfigureAwait(false);
                return;
            }

            if (sashItemType.Id != LegacyRealmTransferServicesSmallSashItemId ||
                sashItemType.LegacyDurability > ushort.MaxValue)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc RealmTransferServices Sash Fragment chance " +
                        $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                        $"rewardItemId={LegacyRealmTransferServicesSmallSashItemId} rateRoll={rateRoll} " +
                        "reason=itemtype-invalid; no fragment was consumed.")
                    .ConfigureAwait(false);
                return;
            }

            try
            {
                grantedItem = CreatePurchasedItems(
                    session.PlayerItems,
                    sashItemType,
                    count: 1,
                    isBound: false)[0];
            }
            catch (OverflowException)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc RealmTransferServices Sash Fragment chance " +
                        $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                        $"rewardItemId={LegacyRealmTransferServicesSmallSashItemId} rateRoll={rateRoll} " +
                        "reason=item-uid-exhausted; no fragment was consumed.")
                    .ConfigureAwait(false);
                return;
            }
        }

        var updatedItems = ConsumeSingleLegacyInventoryItem(
            session.PlayerItems,
            fragment,
            out var consumptionFrame);
        if (grantedItem is not null)
        {
            updatedItems = updatedItems with
            {
                InventoryItems = updatedItems.InventoryItems
                    .Concat([grantedItem])
                    .OrderBy(item => item.Uid)
                    .ToArray(),
            };
        }

        var updatedSession = session with { PlayerItems = updatedItems };
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
            NotSupportedException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Sash Fragment chance persistence " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"rewardItemId={LegacyRealmTransferServicesSmallSashItemId} rateRoll={rateRoll} " +
                    $"reason={ex.GetType().Name}; no session mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(consumptionFrame, cancellationToken).ConfigureAwait(false);
        if (grantedItem is not null)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(grantedItem).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyItemFollowupsAsync(updatedContext, grantedItem, cancellationToken).ConfigureAwait(false);
        }

        var resultText = rateHit
            ? "Wow, it must be your lucky day! You successfully made a Sash (S)!"
            : "The fragment vanished since you failed to make a Sash (S) with it.";
        await SendLegacyNpcDialogAsync(
                updatedContext,
                source,
                [resultText],
                LegacyRealmTransferServicesSashFragmentChanceResultOptions,
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc RealmTransferServices Sash Fragment chance " +
                $"{FormatSession(updatedContext)} source={source} sourceLines=24230-24252 optionId={optionId} " +
                $"fragmentItemId={LegacyRealmTransferServicesSashFragmentItemId} fragmentCount=1 " +
                $"rewardItemId={LegacyRealmTransferServicesSmallSashItemId} " +
                $"Rate({LegacyRealmTransferServicesSashFragmentRatePercent})={rateHit.ToString().ToLowerInvariant()} " +
                $"rateRoll={rateRoll} bound=false.")
            .ConfigureAwait(false);
    }

    private static async ValueTask CombineLegacyRealmTransferServicesSashFragmentsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string source,
        string sourceLines,
        ushort? avatarId,
        CancellationToken cancellationToken)
    {
        var playerItemsRoot = context.PlayerItemsRoot;
        if (string.IsNullOrWhiteSpace(playerItemsRoot))
        {
            await SendLegacyRealmTransferServicesGapAsync(
                    context,
                    LegacyRealmTransferServicesSashFragmentRealmNpcId,
                    optionId,
                    source,
                    sourceLines,
                    "remove 5 Sash Fragments (Realm) and grant Sash (S) item 1100003",
                    "Sash Fragment combine is unavailable here until fragment removal and reward item grants are modeled safely.",
                    avatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Sash Fragment combine " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    "reason=session-store-missing; no item persistence or fragment mutation occurred.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyItemType(context.WorldData, LegacyRealmTransferServicesSmallSashItemId, out var sashItemType))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Sash Fragment combine " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"rewardItemId={LegacyRealmTransferServicesSmallSashItemId} reason=itemtype-missing; no fragments were consumed.")
                .ConfigureAwait(false);
            return;
        }

        if (sashItemType.Id != LegacyRealmTransferServicesSmallSashItemId ||
            sashItemType.LegacyDurability > ushort.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Sash Fragment combine " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"rewardItemId={LegacyRealmTransferServicesSmallSashItemId} reason=itemtype-invalid; no fragments were consumed.")
                .ConfigureAwait(false);
            return;
        }

        var consumptionFrames = new List<byte[]>(LegacyRealmTransferServicesSashFragmentCombineCount);
        var updatedItems = session.PlayerItems;
        for (var index = 0; index < LegacyRealmTransferServicesSashFragmentCombineCount; index++)
        {
            var fragment = updatedItems.InventoryItems.FirstOrDefault(
                item => item.ItemId == LegacyRealmTransferServicesSashFragmentItemId);
            if (fragment is null)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc RealmTransferServices Sash Fragment combine " +
                        $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                        "reason=fragment-count-drift; no session mutation was committed.")
                    .ConfigureAwait(false);
                return;
            }

            updatedItems = ConsumeSingleLegacyInventoryItem(updatedItems, fragment, out var consumptionFrame);
            consumptionFrames.Add(consumptionFrame);
        }

        PlayerItemSnapshotItem grantedItem;
        try
        {
            grantedItem = CreatePurchasedItems(
                session.PlayerItems,
                sashItemType,
                count: 1,
                isBound: false)[0];
        }
        catch (OverflowException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Sash Fragment combine " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"rewardItemId={LegacyRealmTransferServicesSmallSashItemId} reason=item-uid-exhausted; " +
                    "no fragments were consumed.")
                .ConfigureAwait(false);
            return;
        }

        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems
                .Concat([grantedItem])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };

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
            NotSupportedException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Sash Fragment combine persistence " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"rewardItemId={LegacyRealmTransferServicesSmallSashItemId} reason={ex.GetType().Name}; " +
                    "no session mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        foreach (var consumptionFrame in consumptionFrames)
        {
            await context.Connection.SendAsync(consumptionFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(grantedItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyItemFollowupsAsync(updatedContext, grantedItem, cancellationToken).ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                source,
                ["You successfully made a Sash (S)!"],
                LegacyRealmTransferServicesSashFragmentCombineSuccessOptions,
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc RealmTransferServices Sash Fragment combine " +
                $"{FormatSession(updatedContext)} source={source} sourceLines=24207-24228 optionId={optionId} " +
                $"fragmentItemId={LegacyRealmTransferServicesSashFragmentItemId} " +
                $"fragmentCount={LegacyRealmTransferServicesSashFragmentCombineCount} " +
                $"rewardItemId={LegacyRealmTransferServicesSmallSashItemId} bound=false.")
            .ConfigureAwait(false);
    }

    private static bool HasLegacyRealmTransferServicesUnboundSashFragments(
        WorldSession session,
        ushort requiredCount)
    {
        var count = 0u;
        foreach (var item in session.PlayerItems.InventoryItems)
        {
            if (item.ItemId != LegacyRealmTransferServicesSashFragmentItemId || item.Bound != 0)
            {
                continue;
            }

            count += item.StackSize;
            if (count >= requiredCount)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLegacyRealmTransferServicesUnboundGoldPrizeTokens(
        WorldSession session,
        ushort requiredCount)
    {
        var count = 0u;
        foreach (var item in session.PlayerItems.InventoryItems)
        {
            if (item.ItemId != LegacyRealmTransferServicesGoldPrizeTokenItemId || item.Bound != 0)
            {
                continue;
            }

            count += item.StackSize;
            if (count >= requiredCount)
            {
                return true;
            }
        }

        return false;
    }
}
