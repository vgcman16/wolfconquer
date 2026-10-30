using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const byte LegacyAdminStuffLevel140Option = 60;
    private const byte LegacyAdminStuff12MaxWeaponProficiencyOption = 149;
    private const byte LegacyAdminStuff12Level140Option = 162;
    private const byte LegacyAdminOpenDragonWarOption = 201;
    private const uint LegacyAdminStuff12NpcId = 22_224;
    private const uint LegacyAdminStuffNpcId = 77_839;
    private const uint LegacyInnerPowerBookNpcId = 18_786;
    private const ushort LegacyAdminUtilityAvatarId = 285;
    private const ushort LegacyInnerPowerBookAvatarId = 6;

    private static readonly LegacyNpcDialogOption[] LegacyAdminStuffRootOptions =
    [
        new("Helmets.", 1),
        new("Armors.", 2),
        new("Necklaces.", 3),
        new("Weapons.", 4),
        new("Rings.", 5),
        new("Boots.", 6),
        new("Horse.", 120),
        new("Open DragonWar.", LegacyAdminOpenDragonWarOption),
        new("Level 140.", LegacyAdminStuffLevel140Option),
        new("No thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyAdminStuff12RootOptions =
    [
        new("Helmet.", 1),
        new("Armor.", 2),
        new("Necklace.", 3),
        new("Weapon.", 4),
        new("Ring.", 5),
        new("Boots.", 6),
        new("Talisman.", 7),
        new("Open DragonWar.", LegacyAdminOpenDragonWarOption),
        new("Level 140.", LegacyAdminStuff12Level140Option),
        new("Max weapon proficiency.", LegacyAdminStuff12MaxWeaponProficiencyOption),
        new("No thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyAdminStuffCategoryOptions =
    [
        new("That admin grant is disabled.", 200),
        new("Back.", 0),
        new("No thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyInnerPowerBookIntroOptions =
    [
        new("How to acquire Inner Power?", 1),
        new("Not interested.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyInnerPowerBookClaimOptions =
    [
        new("Claim the next book.", 10),
        new("Just passing by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyInnerPowerBookInfoOptions =
    [
        new("How can I get the books?", 2),
        new("I see.", LegacyNpcClosedOption),
    ];

    public static async ValueTask<bool> TryHandleLegacyAdminUtilityNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyAdminStuffNpcId => await TryHandleLegacyPrivilegedAdminUtilityNpcAsync(
                    context,
                    npcId,
                    optionId,
                    "NpcHandler.Stuff",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyAdminStuff12NpcId => await TryHandleLegacyPrivilegedAdminUtilityNpcAsync(
                    context,
                    npcId,
                    optionId,
                    "NpcHandler.Stuff12",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyInnerPowerBookNpcId => await TryHandleLegacyInnerPowerBookNpcAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyPrivilegedAdminUtilityNpcAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        byte optionId,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Saved names/Role/booleans are not permission. The new explicit GM
        // policy does not grant transaction authority to these legacy actions.
        await context.Log.WriteLineAsync(
                "WorldServer disabled LegacyNpc admin utility " +
                $"npcId={npcId} source={sourceMethod} option={optionId}; reason=ownedGmTransactionRequired.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyStuffNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.Stuff";

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["Legacy admin equipment selector. Item grants are disabled on this runtime."],
                    LegacyAdminStuffRootOptions,
                    LegacyAdminUtilityAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId is >= 1 and <= 6 or 254)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["This category belonged to the old admin/debug item giver."],
                    LegacyAdminStuffCategoryOptions,
                    292,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == LegacyAdminOpenDragonWarOption)
        {
            await TryOpenLegacyAdminDragonWarAsync(context, sourceMethod, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == LegacyAdminStuffLevel140Option)
        {
            await TryHandleLegacyAdminLevel140Async(
                    context,
                    session,
                    optionId,
                    sourceMethod,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        await SendLegacyAdminUtilityGapAsync(
                context,
                sourceMethod,
                optionId,
                "Admin item grants are disabled.",
                "source grants free max-quality items, steeds, sockets, accessories, or level changes; local runtime leaves player state unchanged.",
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyStuff12NpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.Stuff12";

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["Legacy advanced admin equipment selector. Item grants are disabled on this runtime."],
                    LegacyAdminStuff12RootOptions,
                    LegacyAdminUtilityAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId is >= 1 and <= 7)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["This category belonged to the old advanced admin/debug item giver."],
                    LegacyAdminStuffCategoryOptions,
                    292,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == LegacyAdminOpenDragonWarOption)
        {
            await TryOpenLegacyAdminDragonWarAsync(context, sourceMethod, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == LegacyAdminStuff12Level140Option)
        {
            await TryHandleLegacyAdminLevel140Async(
                    context,
                    session,
                    optionId,
                    sourceMethod,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == LegacyAdminStuff12MaxWeaponProficiencyOption)
        {
            await TryHandleLegacyAdminMaxWeaponProficiencyAsync(
                    context,
                    session,
                    optionId,
                    sourceMethod,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        await SendLegacyAdminUtilityGapAsync(
                context,
                sourceMethod,
                optionId,
                "Advanced admin item grants are disabled.",
                "source grants free plus-12 souls/items, rewrites weapon proficiencies, or changes player level; local runtime leaves player state unchanged.",
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyInnerPowerBookNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.InnerPowerNpc";

        switch (optionId)
        {
            case 0:
                if (session.Character.Reborn != 2)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            sourceMethod,
                            ["You are not strong enough to study Inner Power books. Come back after your second rebirth."],
                            [new("Alright.", LegacyNpcClosedOption)],
                            LegacyInnerPowerBookAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                var options = HasLegacyInnerPowerFirstStageUnlocked(context, session)
                    ? LegacyInnerPowerBookClaimOptions
                    : LegacyInnerPowerBookIntroOptions;
                var text = HasLegacyInnerPowerFirstStageUnlocked(context, session)
                    ? "Good to see you again. If you have finished studying the Inner Power Book I gave you, you can claim the next book."
                    : "I isolated myself for 100 years to study humankind's potential and finally found the essence of Inner Power.";
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [text],
                        options,
                        LegacyInnerPowerBookAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "Inner Power concepts are embodied through books.",
                            "Training also requires Potency Points, which the source server awarded through Kingdom Missions.",
                        ],
                        LegacyInnerPowerBookInfoOptions,
                        7,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                if (!TryGetLegacyInnerPowerBookItem(context.WorldData, stageId: 1, slot: 1, out var firstBook, out var firstBookReason))
                {
                    await SendLegacyAdminUtilityGapAsync(
                            context,
                            sourceMethod,
                            optionId,
                            "The first Inner Power book is not available in this runtime.",
                            firstBookReason,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await GrantLegacyInnerPowerBookAsync(
                        context,
                        session,
                        firstBook,
                        systemMessage: null,
                        systemMessageBeforeItem: false,
                        sourceMethod,
                        cancellationToken)
                    .ConfigureAwait(false);
                await SendLegacyInnerPowerBookGrantDialogAsync(
                        context,
                        sourceMethod,
                        "Universal Concept (A)",
                        avatar: 7,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
                await TryHandleLegacyInnerPowerNextBookAsync(context, session, sourceMethod, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static bool HasLegacyInnerPowerFirstStageUnlocked(WorldPacketHandlerContext context, WorldSession session)
    {
        return TryReadLegacyInnerPowerFirstStageUnlocked(context, session, out var unlocked) && unlocked;
    }

    private static bool TryReadLegacyInnerPowerFirstStageUnlocked(
        WorldPacketHandlerContext context,
        WorldSession session,
        out bool unlocked)
    {
        unlocked = false;
        if (context.WorldData is not { } worldData ||
            session.Character.Uid > int.MaxValue ||
            !worldData.InnerPowerRanksByCharacterId.TryGetValue((int)session.Character.Uid, out var rows) ||
            rows.Count == 0 ||
            !TryReadLegacyInnerPowerState(rows[0], out var state, out _))
        {
            return false;
        }

        unlocked = state.Stages.Count > 0 && state.Stages[0].Unlocked;
        return true;
    }

    private static async ValueTask TryHandleLegacyInnerPowerNextBookAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        var stateReason = string.Empty;
        if (context.WorldData is not { } worldData ||
            session.Character.Uid > int.MaxValue ||
            !worldData.InnerPowerRanksByCharacterId.TryGetValue((int)session.Character.Uid, out var rows) ||
            rows.Count == 0 ||
            !TryReadLegacyInnerPowerState(rows[0], out var state, out stateReason))
        {
            await SendLegacyAdminUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId: 10,
                    "The next Inner Power book is not available in this runtime.",
                    string.IsNullOrEmpty(stateReason)
                        ? "parseable InnerPower.txt state for this character is not loaded"
                        : stateReason,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var lastStage = state.Stages.LastOrDefault(stage => stage.Unlocked);
        if (lastStage is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyNpc InnerPower book claim " +
                    $"{FormatSession(context)} source={sourceMethod} option=10; no unlocked InnerPower stage.")
                .ConfigureAwait(false);
            return;
        }

        if (lastStage.Gongs.All(gong => gong.Complete))
        {
            if (lastStage.Id >= worldData.NeiGongHeader.SecretCount)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["You've completed the inner power concept ."],
                        [new("Thanks~a~lot.", LegacyNpcClosedOption)],
                        LegacyInnerPowerBookAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            if (!TryGetLegacyInnerPowerBookItem(worldData, lastStage.Id + 1, slot: 1, out var nextStageBook, out var nextStageReason))
            {
                await SendLegacyAdminUtilityGapAsync(
                        context,
                        sourceMethod,
                        optionId: 10,
                        "The next Inner Power book is not available in this runtime.",
                        nextStageReason,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            await TryGrantLegacyInnerPowerNextBookAsync(
                    context,
                    session,
                    nextStageBook,
                    systemMessageBeforeItem: false,
                    sourceMethod,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var lastGong = lastStage.Gongs.LastOrDefault(gong => gong.Unlocked);
        if (lastGong is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyNpc InnerPower book claim " +
                    $"{FormatSession(context)} source={sourceMethod} option=10; no unlocked gong in stage={lastStage.Id}.")
                .ConfigureAwait(false);
            return;
        }

        if (lastGong.Complete)
        {
            if (!TryGetLegacyInnerPowerBookItemByGongId(
                    worldData,
                    lastStage.Id,
                    unchecked((byte)(lastGong.Id + 1)),
                    out var nextGongBook,
                    out var nextGongReason))
            {
                await SendLegacyAdminUtilityGapAsync(
                        context,
                        sourceMethod,
                        optionId: 10,
                        "The next Inner Power book is not available in this runtime.",
                        nextGongReason,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            await TryGrantLegacyInnerPowerNextBookAsync(
                    context,
                    session,
                    nextGongBook,
                    systemMessageBeforeItem: true,
                    sourceMethod,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!TryGetLegacyInnerPowerBookItemByGongId(
                worldData,
                lastStage.Id,
                lastGong.Id,
                out var currentBook,
                out var currentBookReason))
        {
            await SendLegacyAdminUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId: 10,
                    "The next Inner Power book is not available in this runtime.",
                    currentBookReason,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await SendLegacyInnerPowerBookTrainingDialogAsync(context, sourceMethod, currentBook.Name, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask TryGrantLegacyInnerPowerNextBookAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        ItemType book,
        bool systemMessageBeforeItem,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.BoxDialog("Please make 1 more space in your inventory.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc InnerPower book claim " +
                    $"{FormatSession(context)} source={sourceMethod} option=10 itemId={book.Id}; inventory is full.")
                .ConfigureAwait(false);
            return;
        }

        if (session.PlayerItems.InventoryItems.Any(item => item.ItemId == book.Id))
        {
            await SendLegacyInnerPowerBookTrainingDialogAsync(context, sourceMethod, book.Name, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await GrantLegacyInnerPowerBookAsync(
                context,
                session,
                book,
                $"You received an Inner Power Book, {book.Name}. Hurry and go training your Inner Power with it!",
                systemMessageBeforeItem,
                sourceMethod,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyInnerPowerBookGrantDialogAsync(
                context,
                sourceMethod,
                book.Name,
                LegacyInnerPowerBookAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask GrantLegacyInnerPowerBookAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        ItemType book,
        string? systemMessage,
        bool systemMessageBeforeItem,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        var grantedItem = CreatePurchasedItems(session.PlayerItems, book, count: 1)[0];
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat([grantedItem]).ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);

        if (!string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            try
            {
                new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                    updatedSession.Character.Uid,
                    updatedItems);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc InnerPower book item persistence " +
                        $"{FormatSession(context with { Session = updatedSession })} source={sourceMethod} itemId={book.Id} reason={ex.Message}.")
                    .ConfigureAwait(false);
            }
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc InnerPower book item grant missing PlayerItemsRoot " +
                    $"{FormatSession(context with { Session = updatedSession })} source={sourceMethod} itemId={book.Id}; granted item is session-only.")
                .ConfigureAwait(false);
        }

        if (systemMessageBeforeItem && systemMessage is not null)
        {
            await context.Connection.SendAsync(LegacyMsgMessagePacket.SystemTalk(systemMessage).ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(LegacyGameItemPacket.FromSnapshotItem(grantedItem).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        if (!systemMessageBeforeItem && systemMessage is not null)
        {
            await context.Connection.SendAsync(LegacyMsgMessagePacket.SystemTalk(systemMessage).ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc InnerPower book claim " +
                $"{FormatSession(context with { Session = updatedSession })} source={sourceMethod} itemId={book.Id}.")
            .ConfigureAwait(false);
    }

    private static bool TryGetLegacyInnerPowerBookItem(
        WorldDataCatalog? worldData,
        int stageId,
        int slot,
        out ItemType book,
        out string reason)
    {
        book = default!;
        if (worldData is null ||
            worldData.NeiGongHeader.SecretCount <= 0 ||
            stageId is < 1 or > int.MaxValue ||
            slot < 1 ||
            !worldData.NeiGongSecretsById.TryGetValue(stageId, out var stage) ||
            stage.NeiGongCount < slot ||
            !worldData.NeiGongTypeBySecretIdAndSlot.TryGetValue((stageId, slot), out var gong) ||
            gong.TypeId is < 1 or > byte.MaxValue ||
            gong.RequiredItemTypeId < 1 ||
            !TryReadItemType(worldData, checked((uint)gong.RequiredItemTypeId), out book))
        {
            reason = $"parseable NeiGongInfo.ini stage {stageId} slot {slot} book metadata and matching item data are not loaded";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryGetLegacyInnerPowerBookItemByGongId(
        WorldDataCatalog? worldData,
        int stageId,
        byte gongId,
        out ItemType book,
        out string reason)
    {
        book = default!;
        if (worldData is null ||
            !worldData.NeiGongSecretsById.TryGetValue(stageId, out var stage) ||
            !worldData.NeiGongTypesBySecretId.TryGetValue(stageId, out var gongs))
        {
            reason = $"parseable NeiGongInfo.ini stage {stageId} gong {gongId} metadata is not loaded";
            return false;
        }

        var matches = gongs.Where(gong => gong.TypeId == gongId).ToArray();
        if (stage.NeiGongCount <= 0 ||
            matches.Length != 1 ||
            matches[0].RequiredItemTypeId < 1 ||
            !TryReadItemType(worldData, checked((uint)matches[0].RequiredItemTypeId), out book))
        {
            reason = $"parseable NeiGongInfo.ini stage {stageId} gong {gongId} book metadata and matching item data are not loaded";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static async ValueTask SendLegacyInnerPowerBookTrainingDialogAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        string bookName,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [
                    $"Kid, you need to train the power of {bookName} to the perfect level before",
                    "you can claim the next book. Be patient, okay?",
                ],
                [new("Okay.", LegacyNpcClosedOption)],
                LegacyInnerPowerBookAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyInnerPowerBookGrantDialogAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        string bookName,
        ushort avatar,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [
                    $"Meeting you was fate. I`d like present you a {bookName}. When you acquire its essence and reach the perfect level, you can",
                    "find me to claim the next book.",
                ],
                [new("Thanks~a~lot!", LegacyNpcClosedOption)],
                avatar,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyAdminUtilityGapAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string dialogText,
        string reason,
        CancellationToken cancellationToken)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc AdminUtility unavailable " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}")
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [dialogText],
                [new("I understand.", LegacyNpcClosedOption)],
                LegacyAdminUtilityAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
