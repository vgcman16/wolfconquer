using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyAnimaFirstItemNpcId = 4_200_001;
    private const uint LegacyAnimaLastItemNpcId = 4_200_019;
    private const uint LegacyRedRuneSelectionPackItemId = 3_306_560;
    private const uint LegacyRedRuneSelectionBoundPackItemId = 3_306_562;
    private const uint LegacyYellowRuneSelectionPackItemId = 3_306_363;
    private const uint LegacyBlueRuneSelectionPackItemId = 3_306_561;
    private const uint LegacyBlueRuneSelectionBoundPackItemId = 3_306_563;
    private const uint LegacyAnimaEmbedWindowId = 930;
    private const ushort LegacyAnimaAvatarId = 1976;

    private static bool IsLegacyItemPackNpc(uint npcId)
    {
        return IsLegacyItemPackServicesActivationItem(npcId) ||
            IsLegacyItemPackServicesSyntheticNpc(npcId) ||
            (npcId >= LegacyAnimaFirstItemNpcId && npcId <= LegacyAnimaLastItemNpcId) ||
            npcId is LegacyRedRuneSelectionPackItemId or
                LegacyRedRuneSelectionBoundPackItemId or
                LegacyYellowRuneSelectionPackItemId or
                LegacyBlueRuneSelectionPackItemId or
                LegacyBlueRuneSelectionBoundPackItemId;
    }

    private static async ValueTask HandleLegacyItemPackUseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerItemSnapshotItem item,
        CancellationToken cancellationToken)
    {
        session = LegacyAccountSecurityResolveAnimaSession(context, session);
        context = context with { Session = session };

        if (TryGetLegacyItemPackServicesActivation(item.ItemId, out var activation))
        {
            if (context.Sessions is not { } sessions)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP skipped MsgItemUsage ItemPackServices activation " +
                        $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId} " +
                        $"activeNpcId={activation.NpcId} initialOption={activation.InitialOption}; " +
                        "reason=session-store-missing; no dialog was sent.")
                    .ConfigureAwait(false);
                return;
            }

            if (!sessions.TryGet(context.Connection, out var registeredSession) ||
                registeredSession is null ||
                !ReferenceEquals(registeredSession, session))
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP skipped MsgItemUsage ItemPackServices activation " +
                        $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId} " +
                        $"activeNpcId={activation.NpcId} initialOption={activation.InitialOption}; " +
                        "reason=session-registration-missing-or-stale; no dialog was sent.")
                    .ConfigureAwait(false);
                return;
            }

            var activatedSession = session with { ActiveNpcId = activation.NpcId };
            sessions.Set(context.Connection, activatedSession);
            var activatedContext = context with { Session = activatedSession };
            var handled = await TryHandleLegacyItemPackServicesNpcAsync(
                    activatedContext,
                    activatedSession,
                    activation.NpcId,
                    activation.InitialOption,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer routed MsgItemUsage ItemPackServices activation " +
                    $"{FormatSession(activatedContext)} itemUid={item.Uid} itemId={item.ItemId} " +
                    $"activeNpcId={activation.NpcId} initialOption={activation.InitialOption} handled={handled}.")
                .ConfigureAwait(false);
            return;
        }

        if (item.ItemId >= LegacyAnimaFirstItemNpcId &&
            item.ItemId <= LegacyAnimaLastItemNpcId &&
            await TryBlockLegacySecurityAnimaItemOperationAsync(
                    context,
                    session,
                    LegacySecurityAnimaItemNpcSource,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        var updatedSession = session with { ActiveNpcId = item.ItemId };
        context.Sessions?.Set(context.Connection, updatedSession);
        await TryHandleLegacyItemPackNpcAsync(
                context with { Session = updatedSession },
                updatedSession,
                item.ItemId,
                optionId: 0,
                input: item.ItemId.ToString(CultureInfo.InvariantCulture),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyItemPackNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string input,
        CancellationToken cancellationToken)
    {
        if (IsLegacyItemPackServicesSyntheticNpc(npcId))
        {
            return await TryHandleLegacyItemPackServicesNpcAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (npcId >= LegacyAnimaFirstItemNpcId && npcId <= LegacyAnimaLastItemNpcId)
        {
            return await TryHandleLegacyAnimaItemNpcAsync(context, session, npcId, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        return npcId switch
        {
            LegacyRedRuneSelectionPackItemId or LegacyRedRuneSelectionBoundPackItemId =>
                await TryHandleLegacyRuneSelectionPackNpcAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        input,
                        "NpcHandler.RedRuneSelectionPack",
                        LegacyRedRuneSelectionBoundPackItemId,
                        LegacyRedRuneSelectionPackPages,
                        LegacyRedRuneSelectionRewards,
                        cancellationToken)
                    .ConfigureAwait(false),
            LegacyYellowRuneSelectionPackItemId =>
                await TryHandleLegacyRuneSelectionPackNpcAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        input,
                        "NpcHandler.YellowRuneSelectionPack",
                        LegacyYellowRuneSelectionPackItemId,
                        LegacyYellowRuneSelectionPackPages,
                        LegacyYellowRuneSelectionRewards,
                        cancellationToken)
                    .ConfigureAwait(false),
            LegacyBlueRuneSelectionPackItemId or LegacyBlueRuneSelectionBoundPackItemId =>
                await TryHandleLegacyRuneSelectionPackNpcAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        input,
                        "NpcHandler.BlueRuneSelectionPack",
                        LegacyBlueRuneSelectionBoundPackItemId,
                        LegacyBlueRuneSelectionPackPages,
                        LegacyBlueRuneSelectionRewards,
                        cancellationToken)
                    .ConfigureAwait(false),
            _ => false,
        };
    }

    private static bool IsLegacyItemPackServicesActivationItem(uint itemId)
    {
        return itemId is LegacyItemPackServicesSteed1ItemId or
            LegacyItemPackServicesSteed3ItemId or
            LegacyItemPackServicesSteed6ItemId or
            LegacyItemPackServicesNobleSteedPackItemId or
            LegacyItemPackServicesRareSteedPack6ItemId or
            LegacyItemPackServicesP7WeaponSoulPackItemId or
            LegacyItemPackServicesSacredRefineryPackItemId or
            LegacyItemPackServicesP7EquipmentSoulPackItemId or
            LegacyItemPackServicesDazzlingDiamondBoxItemId or
            LegacyItemPackServicesBlackFridayGarmentPackItemId or
            LegacyItemPackServicesBlackFridayMountPackItemId or
            LegacyItemPackServicesBlackFridayAccesoryPackItemId or
            LegacyItemPackServicesSteed1PackItemId or
            LegacyItemPackServicesSteed3PackItemId;
    }

    private static bool IsLegacyItemPackServicesSyntheticNpc(uint npcId)
    {
        return npcId is LegacyItemPackServicesSteed1NpcId or
            LegacyItemPackServicesSteed3NpcId or
            LegacyItemPackServicesSteed6NpcId or
            LegacyItemPackServicesNobleSteedPackNpcId or
            LegacyItemPackServicesRareSteedPack6NpcId or
            LegacyItemPackServicesSelectP7WeaponSoulPackNpcId or
            LegacyItemPackServicesSelectSacredRefineryPackNpcId or
            LegacyItemPackServicesSelectP7EquipmentSoulPackNpcId or
            LegacyItemPackServicesDazzlingDiamondBoxNpcId or
            LegacyItemPackServicesBlackFridayGarmentPackNpcId or
            LegacyItemPackServicesBlackFridayMountPackNpcId or
            LegacyItemPackServicesBlackFridayAccesoryNpcId or
            LegacyItemPackServicesSteed1PackNpcId or
            LegacyItemPackServicesSteed3PackNpcId;
    }

    private static bool TryGetLegacyItemPackServicesActivation(
        uint itemId,
        out LegacyItemPackServicesActivation activation)
    {
        activation = itemId switch
        {
            LegacyItemPackServicesSteed1ItemId => new(LegacyItemPackServicesSteed1NpcId, 0),
            LegacyItemPackServicesSteed3ItemId => new(LegacyItemPackServicesSteed3NpcId, 0),
            LegacyItemPackServicesSteed6ItemId => new(LegacyItemPackServicesSteed6NpcId, 0),
            LegacyItemPackServicesNobleSteedPackItemId => new(LegacyItemPackServicesNobleSteedPackNpcId, 0),
            LegacyItemPackServicesRareSteedPack6ItemId => new(LegacyItemPackServicesRareSteedPack6NpcId, 0),
            LegacyItemPackServicesP7WeaponSoulPackItemId => new(LegacyItemPackServicesSelectP7WeaponSoulPackNpcId, 0),
            LegacyItemPackServicesSacredRefineryPackItemId => new(LegacyItemPackServicesSelectSacredRefineryPackNpcId, 0),
            LegacyItemPackServicesP7EquipmentSoulPackItemId => new(LegacyItemPackServicesSelectP7EquipmentSoulPackNpcId, 40),
            LegacyItemPackServicesDazzlingDiamondBoxItemId => new(LegacyItemPackServicesDazzlingDiamondBoxNpcId, 0),
            LegacyItemPackServicesBlackFridayGarmentPackItemId => new(LegacyItemPackServicesBlackFridayGarmentPackNpcId, 0),
            LegacyItemPackServicesBlackFridayMountPackItemId => new(LegacyItemPackServicesBlackFridayMountPackNpcId, 0),
            LegacyItemPackServicesBlackFridayAccesoryPackItemId => new(LegacyItemPackServicesBlackFridayAccesoryNpcId, 0),
            LegacyItemPackServicesSteed1PackItemId => new(LegacyItemPackServicesSteed1PackNpcId, 0),
            LegacyItemPackServicesSteed3PackItemId => new(LegacyItemPackServicesSteed3PackNpcId, 0),
            _ => default,
        };
        return activation.NpcId != 0;
    }

    private static async ValueTask<bool> TryHandleLegacyAnimaItemNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        session = LegacyAccountSecurityResolveAnimaSession(context, session);
        context = context with { Session = session };

        if (await TryBlockLegacySecurityAnimaItemOperationAsync(
                context,
                session,
                LegacySecurityAnimaItemNpcSource,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.Animas",
                        LegacyAnimaIntroTexts,
                        LegacyAnimaIntroOptions,
                        LegacyAnimaAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.Animas",
                        LegacyAnimaUseTexts,
                        CreateLegacyAnimaUseOptions(npcId),
                        LegacyAnimaAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyAnimaConquerPointPreviewAsync(context, npcId, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
            case 4:
                await context.Connection.SendAsync(
                        LegacyMsgMessagePacket.SystemTalk(WolfConquer.Core.Runtime.ConquerRuntimeDefaults.WebsiteUrl).ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer handled LegacyNpc Animas website branch " +
                        $"{FormatSession(context)} source=NpcHandler.Animas npcId={npcId} option={optionId}.")
                    .ConfigureAwait(false);
                return true;

            case 5:
                await SendLegacyNpcOpenClientWindowActionAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        LegacyAnimaEmbedWindowId,
                        "NpcHandler.Animas",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 7:
                await TryExchangeLegacyAnimaForConquerPointsAsync(context, session, npcId, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
            case 20:
            case 30:
                await LogLegacyAnimaDragonFurnaceBranchAsync(context, session, npcId, optionId)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask SendLegacyAnimaConquerPointPreviewAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        CancellationToken cancellationToken)
    {
        var conquerPointsWorth = TryReadItemType(context.WorldData, npcId, out var itemType)
            ? itemType.ConquerPointsWorth
            : 0;
        var price = conquerPointsWorth > 0 ? conquerPointsWorth - 1 : 0;
        await SendLegacyNpcDialogAsync(
                context,
                "NpcHandler.Animas",
                [
                    "Anima contains everlasting vital power that can greatly improve equipment when embedded in",
                    "You may upgrade Animae with the Dragon Furnace. For P12 Anima and above",
                    "you can exchange it for a physical reward. The price of your Anima is:\n",
                    "          +---------------------------------------------------------------------+\n",
                    $"              Current Anima Price: {price.ToString(CultureInfo.InvariantCulture)} CPs\n",
                    "          +---------------------------------------------------------------------+\n",
                    "Are you sure you want to exchange this Anima for CPs now?\n",
                ],
                LegacyAnimaConquerPointOptions,
                LegacyAnimaAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask TryExchangeLegacyAnimaForConquerPointsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        CancellationToken cancellationToken)
    {
        if (!TryReadItemType(context.WorldData, npcId, out var itemType))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP skipped LegacyNpc Animas CP exchange " +
                    $"{FormatSession(context)} source=NpcHandler.Animas npcId={npcId}; itemtype data missing.")
                .ConfigureAwait(false);
            return;
        }

        var conquerPoints = itemType.ConquerPointsWorth > 0 ? checked((long)itemType.ConquerPointsWorth - 1) : 0;
        if (!HasLegacyInventoryStackCount(session.PlayerItems, npcId, 1))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc Animas CP exchange " +
                    $"{FormatSession(context)} source=NpcHandler.Animas npcId={npcId}; source item missing.")
                .ConfigureAwait(false);
            return;
        }

        var frames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(session.PlayerItems, npcId, 1, frames);
        var updatedCharacter = session.Character with
        {
            ConquerPoints = checked(session.Character.ConquerPoints + conquerPoints),
        };
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await PersistLegacyItemPackStateAsync(updatedContext, updatedSession, persistConquerPoints: true, cancellationToken)
            .ConfigureAwait(false);

        foreach (var frame in frames)
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                new LegacyMsgUpdatePacket(
                    CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.ConquerPoints, checked((ulong)updatedCharacter.ConquerPoints))])
                .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket
                    .ForMessageBox(
                        $"You successfully exchanged a {itemType.Name} for {conquerPoints.ToString(CultureInfo.InvariantCulture)} CPs!")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc Animas CP exchange " +
                $"{FormatSession(updatedContext)} source=NpcHandler.Animas npcId={npcId} " +
                $"cp={conquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"sourceConquerPointsWorth={itemType.ConquerPointsWorth.ToString(CultureInfo.InvariantCulture)} " +
                $"tunedConquerPoints={conquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"updatedConquerPoints={updatedCharacter.ConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                "walletUpdate=LegacyMsgUpdate.ConquerPoints.")
            .ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyAnimaDragonFurnaceBranchAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId)
    {
        var requiredCount = optionId == 20 ? 2U : 1U;
        if (!HasLegacyInventoryStackCount(session.PlayerItems, npcId, requiredCount))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox($"Please make sure you have {requiredCount.ToString(CultureInfo.InvariantCulture)} Item{npcId}, first.")
                        .ToFrameBytes(),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc Animas DragonFurnace branch " +
                $"{FormatSession(context)} source=NpcHandler.Animas npcId={npcId} option={optionId}; " +
                "dynamic Dragon Furnace transport/spawn branch is outside this item-pack parity slice.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyRuneSelectionPackNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string input,
        string sourceMethod,
        uint boundPackItemId,
        IReadOnlyDictionary<byte, LegacyRuneSelectionPage> pages,
        IReadOnlyDictionary<byte, uint> rewards,
        CancellationToken cancellationToken)
    {
        var inputItemId = ResolveLegacyItemPackInputItemId(npcId, input);
        if (pages.TryGetValue(optionId, out var page))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [page.Text],
                    page.Options,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!rewards.TryGetValue(optionId, out var rewardItemId))
        {
            return false;
        }

        await GrantLegacyRuneSelectionRewardAsync(
                context,
                session,
                inputItemId,
                rewardItemId,
                isBound: inputItemId == boundPackItemId,
                optionId,
                sourceMethod,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static uint ResolveLegacyItemPackInputItemId(uint npcId, string input)
    {
        return uint.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var inputItemId)
            ? inputItemId
            : npcId;
    }

    private static async ValueTask GrantLegacyRuneSelectionRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint packItemId,
        uint rewardItemId,
        bool isBound,
        byte optionId,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        if (!HasLegacyInventoryStackCount(session.PlayerItems, packItemId, 1))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc rune selection pack " +
                    $"{FormatSession(context)} source={sourceMethod} packItemId={packItemId} option={optionId}; pack missing.")
                .ConfigureAwait(false);
            return;
        }

        var frames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(session.PlayerItems, packItemId, 1, frames);
        if (!HasLegacyInventorySpace(updatedItems, 1))
        {
            var consumedSession = session with { PlayerItems = updatedItems };
            context.Sessions?.Set(context.Connection, consumedSession);
            await PersistLegacyItemPackStateAsync(
                    context with { Session = consumedSession },
                    consumedSession,
                    persistConquerPoints: false,
                    cancellationToken)
                .ConfigureAwait(false);
            foreach (var frame in frames)
            {
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }

            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("Please make 1 more space in your inventory.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer consumed LegacyNpc rune selection pack without reward " +
                    $"{FormatSession(context)} source={sourceMethod} packItemId={packItemId} option={optionId}; inventory is full.")
                .ConfigureAwait(false);
            return;
        }

        var rewardItem = CreateLegacyItemPackRewardItem(context, updatedItems, rewardItemId, isBound);
        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems.Concat([rewardItem]).ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await PersistLegacyItemPackStateAsync(updatedContext, updatedSession, persistConquerPoints: false, cancellationToken)
            .ConfigureAwait(false);

        foreach (var frame in frames)
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
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc rune selection pack " +
                $"{FormatSession(updatedContext)} source={sourceMethod} packItemId={packItemId} " +
                $"option={optionId} rewardItemId={rewardItemId} bound={isBound}.")
            .ConfigureAwait(false);
    }

    private static PlayerItemSnapshotItem CreateLegacyItemPackRewardItem(
        WorldPacketHandlerContext context,
        PlayerItemSnapshot items,
        uint itemId,
        bool isBound)
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
            Bound = isBound ? (byte)1 : (byte)0,
        };
    }

    private static async ValueTask PersistLegacyItemPackStateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
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
                    out var path,
                    out var reason))
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc item-pack character persistence " +
                        $"{FormatSession(context)} reason={reason}.")
                    .ConfigureAwait(false);
            }
            else
            {
                await context.Log.WriteLineAsync(
                        "WorldServer persisted LegacyNpc item-pack character " +
                        $"{FormatSession(context)} path={Path.GetFileName(path)} keys=ConquerPoints.")
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
                        "WorldServer persisted LegacyNpc item-pack items " +
                        $"{FormatSession(context)} path={session.Character.Uid.ToString(CultureInfo.InvariantCulture)}.bin.")
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc item-pack item persistence " +
                        $"{FormatSession(context)} reason=itemSnapshotSaveFailed exception={ex.GetType().Name}.")
                    .ConfigureAwait(false);
            }
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc item-pack item persistence " +
                    $"{FormatSession(context)} reason=playerItemsRootMissing.")
                .ConfigureAwait(false);
        }
    }

    private readonly record struct LegacyItemPackServicesActivation(
        uint NpcId,
        byte InitialOption);

    private sealed record LegacyRuneSelectionPage(
        string Text,
        IReadOnlyList<LegacyNpcDialogOption> Options);

    private static readonly string[] LegacyAnimaIntroTexts =
    [
        "Anima contains everlasting vital power that can greatly improve equipment when embedded in.",
        "You may upgrade Animae with the Dragon Furnace. It`s not always successful.",
        "Anima can also be exchanged for CPs. Do you want to use this Anima now?\n",
    ];

    private static readonly LegacyNpcDialogOption[] LegacyAnimaIntroOptions =
    [
        new("Yes.", 1),
        new("Get~details~on~the~official~site.", 3),
        new("Watch~video~introducing~Anima.", 4),
        new("No,~not~now.", LegacyNpcClosedOption),
    ];

    private static readonly string[] LegacyAnimaUseTexts =
    [
        "    With everlasting vital power, this Anima can be embedded into weapon or equipment",
        "for huge attribute enhancement. You also have a chance to upgrade its phase by forging,",
        "fusing, or smelting in the alien world of the Dragon Furnace.\n",
    ];

    private static readonly LegacyNpcDialogOption[] LegacyAnimaConquerPointOptions =
    [
        new("Yes.", 7),
        new("No,~not~now.", LegacyNpcClosedOption),
    ];

    private static IReadOnlyList<LegacyNpcDialogOption> CreateLegacyAnimaUseOptions(uint itemId)
    {
        var options = new List<LegacyNpcDialogOption>
        {
            new("Embed~Anima.", 5),
        };
        if (itemId % 100 < 13)
        {
            options.Add(new($"Fuse~Anima.~(2x~P{(itemId % 100).ToString(CultureInfo.InvariantCulture)}Anima)", 20));
        }

        if (itemId % 100 >= 3 && itemId < 13)
        {
            options.Add(new($"Smelt~Anima.~(1~P{(itemId % 100).ToString(CultureInfo.InvariantCulture)}Anima)", 30));
        }

        return options;
    }

    private static readonly IReadOnlyDictionary<byte, LegacyRuneSelectionPage> LegacyRedRuneSelectionPackPages =
        new Dictionary<byte, LegacyRuneSelectionPage>
        {
            [0] = new(
                "Please choose one from the following Red Rune .",
                [
                    new("Trojan-Comprehension.", 1),
                    new("Warrior-Pitching.", 2),
                    new("Archer-Frost~Arrows.", 3),
                    new("Ninja-Assassin.", 4),
                    new("Monk-Acalanatha.", 5),
                    new("Pirate-Tide~Trap.", 6),
                    new("DragonWarrior-Swinging~Tail.", 7),
                    new("Next.", 8),
                ]),
            [8] = new(
                "Please choose one from the following Red Rune .",
                [
                    new("WaterTaoist-Sacrifice.", 9),
                    new("FireTaoist-Burning~Sky.", 10),
                    new("Windwalker-Revenge~Gale.", 11),
                    new("Thunderstriker`s~Chained~Storm.", 12),
                    new("Thunderstriker`s~FightingWill.", 13),
                    new("Previous.", 0),
                ]),
        };

    private static readonly IReadOnlyDictionary<byte, uint> LegacyRedRuneSelectionRewards =
        new Dictionary<byte, uint>
        {
            [1] = 4_010_001,
            [2] = 4_010_101,
            [3] = 4_010_201,
            [4] = 4_010_301,
            [5] = 4_010_401,
            [6] = 4_010_501,
            [7] = 4_010_601,
            [9] = 4_010_701,
            [10] = 4_010_801,
            [11] = 4_010_901,
            [12] = 4_011_501,
            [13] = 4_011_401,
        };

    private static readonly IReadOnlyDictionary<byte, LegacyRuneSelectionPage> LegacyYellowRuneSelectionPackPages =
        new Dictionary<byte, LegacyRuneSelectionPage>
        {
            [0] = new(
                "Please choose one from the following Yellow Rune ~.",
                [
                    new("TortoiseBreaker~.", 1),
                    new("DrainingTouchBooster~.", 2),
                    new("BloodSpawnBooster~.", 3),
                    new("Healer~.", 4),
                    new("Sturdiness~.", 5),
                    new("XPKiller~.", 6),
                    new("Grabber~.", 7),
                    new("MPMaster~.", 8),
                    new("BossKiller~.", 9),
                    new("Next.", 70),
                ]),
            [70] = new(
                "Please choose one from the following Yellow Rune ~.",
                [
                    new("NoMercy~.", 10),
                    new("XPBooster~.", 11),
                    new("FreeSoulBooster~.", 12),
                    new("Witchery~.", 13),
                    new("QuickMount~.", 14),
                    new("Breakdown~.", 15),
                    new("Whetted Blade~.", 16),
                    new("Sanctity~.", 17),
                    new("Might~.", 18),
                    new("Next.", 80),
                ]),
            [80] = new(
                "Please choose one from the following Yellow Rune ~.",
                [
                    new("Serenity~.", 19),
                    new("Solidness~.", 20),
                    new("SoulChant~.", 21),
                    new("WideSwipe~.", 22),
                    new("Circle of Life~.", 23),
                    new("Sharpness~.", 24),
                    new("Evocation~.", 25),
                    new("Overwhelm~.", 26),
                    new("Judgment~.", 27),
                    new("NatureShield~.", 40),
                    new("Nature`sChan~.", 41),
                    new("JusticeGuard~.", 42),
                    new("Nexy.", 90),
                ]),
            [90] = new(
                "Pاختر ما تريد ~.",
                [
                    new("TidalWave~.", 28),
                    new("DeadlySight~.", 29),
                    new("TacitStrike~.", 30),
                    new("Quench~.", 31),
                    new("Fearless~.", 32),
                    new("LifeDrain~.", 33),
                    new("ReverseMagic~.", 34),
                    new("Conqueror`sBlade~.", 35),
                    new("Deterrence~.", 36),
                    new("Shackle~.", 37),
                    new("TortoiseSmasher~.", 38),
                    new("Tempered Glaive~.", 39),
                    new("Next~.", 91),
                    new("Previous.", 0),
                ]),
            [91] = new(
                "Please choose one from the following Yellow Rune ~.",
                [
                    new("VioletShield~.", 43),
                    new("CurseBlock~.", 44),
                    new("TortoiseSmasher~.", 45),
                    new("EnormousCrusher~.", 46),
                    new("Consolidation~.", 47),
                    new("Slow Pace~.", 48),
                    new("Sky Veill~.", 49),
                    new("Adamant~.", 50),
                    new("Front Break~.", 51),
                    new("Barrier~.", 52),
                    new("SurgingForce~.", 53),
                    new("DivineShield ~.", 54),
                    new("Previous.", 0),
                ]),
        };

    private static readonly IReadOnlyDictionary<byte, uint> LegacyYellowRuneSelectionRewards =
        new Dictionary<byte, uint>
        {
            [1] = 4_030_101,
            [2] = 4_030_201,
            [3] = 4_030_301,
            [4] = 4_030_401,
            [5] = 4_030_501,
            [6] = 4_030_601,
            [7] = 4_030_701,
            [8] = 4_030_801,
            [9] = 4_030_901,
            [10] = 4_031_001,
            [11] = 4_031_101,
            [12] = 4_031_201,
            [13] = 4_031_301,
            [14] = 4_031_401,
            [15] = 4_031_501,
            [16] = 4_035_701,
            [17] = 4_032_001,
            [18] = 4_035_601,
            [19] = 4_032_201,
            [20] = 4_035_401,
            [21] = 4_032_401,
            [22] = 4_032_501,
            [23] = 4_036_001,
            [24] = 4_032_701,
            [25] = 4_032_801,
            [26] = 4_032_901,
            [27] = 4_033_001,
            [28] = 4_033_101,
            [29] = 4_033_301,
            [30] = 4_033_401,
            [31] = 4_033_601,
            [32] = 4_035_901,
            [33] = 4_033_801,
            [34] = 4_033_901,
            [35] = 4_035_501,
            [36] = 4_034_101,
            [37] = 4_036_101,
            [38] = 4_034_801,
            [39] = 4_035_801,
            [40] = 4_035_101,
            [41] = 4_034_201,
            [42] = 4_034_301,
            [43] = 4_035_201,
            [44] = 4_034_601,
            [45] = 4_034_801,
            [46] = 4_035_301,
            [47] = 4_036_201,
            [48] = 4_036_301,
            [49] = 4_036_401,
            [50] = 4_036_501,
            [51] = 4_036_701,
            [52] = 4_031_601,
            [53] = 4_034_401,
            [54] = 4_034_001,
        };

    private static readonly IReadOnlyDictionary<byte, LegacyRuneSelectionPage> LegacyBlueRuneSelectionPackPages =
        new Dictionary<byte, LegacyRuneSelectionPage>
        {
            [0] = new(
                "Please choose one from the following Blue Rune .",
                [
                    new("Trojan-Rampage.", 1),
                    new("Warrior-All-round.", 2),
                    new("Archer-Infinity.", 3),
                    new("Ninja-Absolution.", 4),
                    new("Monk-Iron~Shield.", 5),
                    new("Pirate-Slayer.", 6),
                    new("DragonWarrior-Counter Punch.", 7),
                    new("Next.", 8),
                ]),
            [8] = new(
                "Please choose one from the following Blue Rune .",
                [
                    new("WaterTaoist-Fine~Rain.", 9),
                    new("FireTaoist-Fire~Curse.", 10),
                    new("Windwalker-Wild~Wind.", 11),
                    new("Thunderstriker`s~Flash~Shield.", 12),
                    new("WindWalker~Double~Thunder.", 13),
                    new("Next.", 80),
                ]),
            [80] = new(
                "Please choose one from the following Blue Rune .",
                [
                    new("WindWalker~Crack~Star.", 14),
                    new("DragonWarrior~Shadow~Fist.", 15),
                    new("Warrior~Retaliation.", 16),
                    new("Archer~Star~Raid.", 17),
                    new("Ninja~Doul.", 18),
                    new("Next.", 70),
                ]),
            [70] = new(
                "Please choose one from the following Blue Rune .",
                [
                    new("Pirate~Neptune`sCurs.", 19),
                    new("WaterTaoist~Space~Leap.", 20),
                    new("FireTaoist~RiseofTaoism.", 21),
                    new("Monk~Fury~Strike.", 22),
                    new("ThunderStrike~Flash~Shield.", 23),
                    new("Trojan~Blood~Tide.", 24),
                    new("Archer~Caprice.", 25),
                    new("Monk~ShieldofTruth.", 26),
                    new("Water~SoulReap.", 27),
                    new("Thx.", 0),
                ]),
        };

    private static readonly IReadOnlyDictionary<byte, uint> LegacyBlueRuneSelectionRewards =
        new Dictionary<byte, uint>
        {
            [1] = 4_020_101,
            [2] = 4_020_201,
            [3] = 4_020_301,
            [4] = 4_020_401,
            [5] = 4_020_501,
            [6] = 4_020_601,
            [7] = 4_020_701,
            [9] = 4_020_801,
            [10] = 4_020_901,
            [11] = 4_021_001,
            [12] = 4_022_201,
            [13] = 4_021_101,
            [14] = 4_021_201,
            [15] = 4_021_301,
            [16] = 4_021_401,
            [17] = 4_021_501,
            [18] = 4_021_601,
            [19] = 4_021_701,
            [20] = 4_021_801,
            [21] = 4_021_901,
            [22] = 4_022_001,
            [23] = 4_022_201,
            [24] = 4_023_001,
            [25] = 4_028_001,
            [26] = 4_027_001,
            [27] = 4_025_001,
        };
}
