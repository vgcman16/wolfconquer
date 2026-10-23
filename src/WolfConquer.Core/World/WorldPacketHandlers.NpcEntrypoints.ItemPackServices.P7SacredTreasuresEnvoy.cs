using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyItemPackServicesP7SacredTreasuresEnvoyNpcId = 16_849;
    private const uint LegacyItemPackServicesSavageBoneItemId = 3_004_181;
    private const uint LegacyItemPackServicesP7WeaponSoulPackRewardItemId = 3_004_247;
    private const uint LegacyItemPackServicesP7EquipmentSoulPackRewardItemId = 3_004_248;
    private const uint LegacyItemPackServicesSacredRefineryPackRewardItemId = 3_004_249;
    private const uint LegacyItemPackServicesSmallMoneyBagRewardItemId = 3_001_318;
    private const uint LegacyItemPackServicesLargeMoneyBagRewardItemId = 3_001_322;
    private const string RuntimeLegacyP7SacredTreasureRollAttribute = "LegacyP7SacredTreasureRoll";
    private const string RuntimeLegacyP7SacredRateRollAttribute = "LegacyP7SacredRateRoll";

    private static async ValueTask<bool> LegacyItemPackServicesTryHandleP7SacredTreasuresEnvoyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.P7SacredTreasuresEnvoy";
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "There`s a mysterious treasure sealed since the ancient times. With a Savage Bone, you can explore and have a chance to get Sacred refinery materials, rare",
                            "~weapon accessories, and even the legendary P7 Dragon Soul.",
                        ],
                        [
                            new("I~wanna~explore.", 1),
                            new("Special~exploration.", 2),
                            new("I~see.", LegacyNpcClosedOption),
                        ],
                        LegacyItemPackServicesSacredAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["The Nemesis Tyrant in the Bloodshed Sea has the Savage Bones. You can use one to explore each time."],
                        [
                            new("Use~a~Savage~Bone.", 10),
                            new("I~see.", LegacyNpcClosedOption),
                        ],
                        LegacyItemPackServicesSacredAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "If you don`t have a Savage Bone, you can also pay 3700 or 17.000 CPs to explore. Of course, the more you pay, the better you get.",
                            "~How much would you like to pay?",
                        ],
                        [
                            new("3700~CPs.", 3),
                            new("17.000~CPs.", 5),
                            new("I~see.", LegacyNpcClosedOption),
                        ],
                        LegacyItemPackServicesSacredAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 3:
                await LegacyItemPackServicesSendConfirmAsync(
                        context,
                        source,
                        "Are you sure you want to use 3700 CPs to explored the P7 Sacred Treasures?",
                        yesOptionId: 4,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 5:
                await LegacyItemPackServicesSendConfirmAsync(
                        context,
                        source,
                        "Are you sure you want to use 17.000 CPs to explored the P7 Sacred Treasures?",
                        yesOptionId: 6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 10:
                if (!LegacyItemPackServicesHasUnboundP7SacredItem(
                        session.PlayerItems,
                        LegacyItemPackServicesSavageBoneItemId))
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            source,
                            ["Sorry, you don`t have a Savage Bone."],
                            [new LegacyNpcDialogOption("My~bad.", LegacyNpcClosedOption)],
                            LegacyItemPackServicesSacredAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await LegacyItemPackServicesApplySavageBoneExplorationAsync(
                        context,
                        session,
                        optionId,
                        source,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 4:
            case 6:
                var cost = optionId == 4 ? 3_700 : 17_000;
                if (session.Character.ConquerPoints < cost)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            source,
                            ["Sorry, but you don`t have enough CPs to buy that."],
                            [new LegacyNpcDialogOption("Alright.", LegacyNpcClosedOption)],
                            LegacyItemPackServicesSacredAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (session.LegacyTradeActive)
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer ignored LegacyNpc ItemPackServices P7 Sacred Treasures paid exploration " +
                            $"{FormatSession(context)} source={source} optionId={optionId} reason=in-trade; source branch is silent.")
                        .ConfigureAwait(false);
                    return true;
                }

                if (!HasLegacyInventorySpace(session.PlayerItems, 1))
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            source,
                            ["Please make 1 space in your inventory."],
                            [new LegacyNpcDialogOption("Ah~yes.", LegacyNpcClosedOption)],
                            optionId == 4 ? LegacyItemPackServicesSacredAvatarId : null,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await LegacyItemPackServicesApplyPaidSacredExplorationAsync(
                        context,
                        session,
                        optionId,
                        source,
                        cost,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 11:
                await LegacyItemPackServicesSendMessageBoxAsync(
                        context,
                        source,
                        "You~arrived~at~the~Gale~Shallow.",
                        cancellationToken)
                    .ConfigureAwait(false);
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyItemPackServicesP7SacredTreasuresEnvoyNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(10_137, 100, 403, source),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 7:
            case 8:
            case 9:
                await LegacyItemPackServicesLogNoOpAsync(
                        context,
                        LegacyItemPackServicesP7SacredTreasuresEnvoyNpcId,
                        optionId,
                        source,
                        "33660-33663")
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask LegacyItemPackServicesApplySavageBoneExplorationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string source,
        CancellationToken cancellationToken)
    {
        if (!LegacyItemPackServicesTryGetP7SacredInfrastructure(
                context,
                session,
                requireUsersRoot: false,
                out var sessions,
                out var playerItemsRoot,
                out var infrastructureReason))
        {
            await LegacyItemPackServicesLogP7SacredFenceAsync(
                    context,
                    optionId,
                    source,
                    "33489-33518",
                    infrastructureReason + "; no Savage Bone was consumed")
                .ConfigureAwait(false);
            return;
        }

        // Pay the same positive unbound Savage Bone admitted by the gate.
        var removalTarget = session.PlayerItems.InventoryItems.FirstOrDefault(
            item => item.ItemId == LegacyItemPackServicesSavageBoneItemId &&
                item.Bound == 0 && item.StackSize > 0);
        if (removalTarget is null)
        {
            await LegacyItemPackServicesLogP7SacredFenceAsync(
                    context,
                    optionId,
                    source,
                    "33489-33518",
                    "Savage Bone count drifted after the unbound gate; no mutation was committed")
                .ConfigureAwait(false);
            return;
        }

        var consumedItems = ConsumeSingleLegacyInventoryItem(
            session.PlayerItems,
            removalTarget,
            out var removalFrame);
        if (!HasLegacyInventorySpace(consumedItems, 1))
        {
            await LegacyItemPackServicesLogP7SacredFenceAsync(
                    context,
                    optionId,
                    source,
                    "33489-33518",
                    "inventory-full-after-planned-Savage-Bone-removal; no mutation was committed")
                .ConfigureAwait(false);
            return;
        }

        var roll = LegacyItemPackServicesReadP7SacredTreasureRoll(session.Character);
        var rewardItemId = roll switch
        {
            < 200 => LegacyItemPackServicesP7WeaponSoulPackRewardItemId,
            > 600 and < 700 => LegacyItemPackServicesP7EquipmentSoulPackRewardItemId,
            _ => LegacyItemPackServicesSacredRefineryPackRewardItemId,
        };
        var successText = rewardItemId switch
        {
            LegacyItemPackServicesP7WeaponSoulPackRewardItemId => "You received a P7 Weapon Soul Pack.",
            LegacyItemPackServicesP7EquipmentSoulPackRewardItemId => "You received a P7 Equipment Soul Pack.",
            _ => "You received a SacredRefineryPack.",
        };

        if (!LegacyItemPackServicesTryCreateP7SacredReward(
                context.WorldData,
                session.PlayerItems,
                rewardItemId,
                out var rewardItem,
                out var rewardReason))
        {
            await LegacyItemPackServicesLogP7SacredFenceAsync(
                    context,
                    optionId,
                    source,
                    "33489-33518",
                    rewardReason + "; no Savage Bone was consumed")
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
            await LegacyItemPackServicesLogP7SacredFenceAsync(
                    context,
                    optionId,
                    source,
                    "33489-33518",
                    $"item-persistence-{ex.GetType().Name}; no session mutation or packets were committed")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(removalFrame, cancellationToken).ConfigureAwait(false);
        await LegacyItemPackServicesSendMessageBoxAsync(
                updatedContext,
                source,
                successText,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc ItemPackServices P7 Sacred Treasures Savage Bone exploration " +
                $"{FormatSession(updatedContext)} source={source} sourceLines=33489-33518 optionId={optionId} " +
                $"roll={roll} removedUid={removalTarget.Uid} removedBound={removalTarget.Bound} " +
                $"rewardItemId={rewardItemId} rewardUid={rewardItem.Uid} color={rewardItem.Color} " +
                "sourceOrder=remove-item+message-box+add-item.")
            .ConfigureAwait(false);
    }

    private static async ValueTask LegacyItemPackServicesApplyPaidSacredExplorationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string source,
        int cost,
        CancellationToken cancellationToken)
    {
        var sourceLines = optionId == 4 ? "33617-33636" : "33575-33594";
        if (!LegacyItemPackServicesTryGetP7SacredInfrastructure(
                context,
                session,
                requireUsersRoot: true,
                out var sessions,
                out _,
                out var infrastructureReason))
        {
            await LegacyItemPackServicesLogP7SacredFenceAsync(
                    context,
                    optionId,
                    source,
                    sourceLines,
                    infrastructureReason + "; no CPs or items were mutated")
                .ConfigureAwait(false);
            return;
        }

        var rate = optionId == 4 ? 4U : 20U;
        var rateRoll = LegacyItemPackServicesReadP7SacredRateRoll(session.Character);
        var rateHit = rate > rateRoll;
        var rewardItemId = rateHit
            ? LegacyItemPackServicesSavageBoneItemId
            : optionId == 4
                ? LegacyItemPackServicesSmallMoneyBagRewardItemId
                : LegacyItemPackServicesLargeMoneyBagRewardItemId;
        if (!LegacyItemPackServicesTryCreateP7SacredReward(
                context.WorldData,
                session.PlayerItems,
                rewardItemId,
                out var rewardItem,
                out var rewardReason))
        {
            await LegacyItemPackServicesLogP7SacredFenceAsync(
                    context,
                    optionId,
                    source,
                    sourceLines,
                    rewardReason + "; no CPs or items were mutated")
                .ConfigureAwait(false);
            return;
        }

        var updatedConquerPoints = checked(session.Character.ConquerPoints - cost);
        var updatedCharacter = WithCharacterAttribute(
            session.Character with { ConquerPoints = updatedConquerPoints },
            "ConquerPoints",
            checked((ulong)updatedConquerPoints));
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems
                .Concat([rewardItem])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        var characterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = updatedConquerPoints.ToString(CultureInfo.InvariantCulture),
        };

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistLegacyRewardPointsMutation(
                context,
                updatedSession,
                characterValues,
                persistItems: true,
                out var persistenceReason))
        {
            await LegacyItemPackServicesLogP7SacredFenceAsync(
                    context,
                    optionId,
                    source,
                    sourceLines,
                    persistenceReason + "; no CP, item, session, or packet mutation was committed")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendScalarUpdateAsync(
                updatedContext,
                updatedCharacter,
                LegacyMsgUpdateDataType.ConquerPoints,
                checked((ulong)updatedConquerPoints),
                cancellationToken)
            .ConfigureAwait(false);
        if (!rateHit)
        {
            await LegacyItemPackServicesSendMessageBoxAsync(
                    updatedContext,
                    source,
                    "You~received~a~Money~Bag.",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc ItemPackServices P7 Sacred Treasures paid exploration " +
                $"{FormatSession(updatedContext)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                $"cost={cost} conquerPoints={session.Character.ConquerPoints}->{updatedConquerPoints} " +
                $"Rate({rate})={rateHit.ToString().ToLowerInvariant()} rateRoll={rateRoll} " +
                $"rewardItemId={rewardItemId} rewardUid={rewardItem.Uid} color={rewardItem.Color} " +
                $"sourceOrder={(rateHit ? "conquer-points+add-item" : "conquer-points+message-box+add-item")}.")
            .ConfigureAwait(false);
    }

    private static bool LegacyItemPackServicesTryGetP7SacredInfrastructure(
        WorldPacketHandlerContext context,
        WorldSession session,
        bool requireUsersRoot,
        out WorldSessionStore sessions,
        out string playerItemsRoot,
        out string reason)
    {
        sessions = null!;
        playerItemsRoot = string.Empty;
        reason = string.Empty;
        if (context.Sessions is not { } availableSessions)
        {
            reason = "session-store-missing";
            return false;
        }

        if (!availableSessions.TryGet(context.Connection, out var registered) || registered is null)
        {
            reason = "session-registration-missing";
            return false;
        }

        if (session.Character.Uid <= 0)
        {
            reason = "character-uid-unavailable";
            return false;
        }

        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            reason = "player-items-root-missing";
            return false;
        }

        if (requireUsersRoot && string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            reason = "users-root-missing";
            return false;
        }

        sessions = availableSessions;
        playerItemsRoot = context.PlayerItemsRoot;
        return true;
    }

    private static bool LegacyItemPackServicesTryCreateP7SacredReward(
        WorldDataCatalog? worldData,
        PlayerItemSnapshot uidAllocationItems,
        uint itemId,
        out PlayerItemSnapshotItem rewardItem,
        out string reason)
    {
        rewardItem = null!;
        reason = string.Empty;
        if (!TryReadLegacyItemType(worldData, itemId, out var itemType))
        {
            reason = $"reward-itemtype-missing:itemId={itemId}";
            return false;
        }

        if (itemType.Id != checked((int)itemId) || itemType.LegacyDurability > ushort.MaxValue)
        {
            reason = $"reward-itemtype-invalid:itemId={itemId}";
            return false;
        }

        try
        {
            rewardItem = CreatePurchasedItems(
                    uidAllocationItems,
                    itemType,
                    count: 1,
                    isBound: false)[0]
                .WithColor(checked((uint)Random.Shared.Next(3, 9)));
            return true;
        }
        catch (OverflowException)
        {
            reason = $"item-uid-exhausted:itemId={itemId}";
            return false;
        }
    }

    private static bool LegacyItemPackServicesHasUnboundP7SacredItem(
        PlayerItemSnapshot items,
        uint itemId)
    {
        return items.InventoryItems.Any(item =>
            item.ItemId == itemId && item.Bound == 0 && item.StackSize > 0);
    }

    private static uint LegacyItemPackServicesReadP7SacredTreasureRoll(CharacterSnapshot character)
    {
        if (TryReadUInt32Attribute(
                character.Attributes,
                out var forcedRoll,
                RuntimeLegacyP7SacredTreasureRollAttribute))
        {
            return forcedRoll is >= 1 and <= 999 ? forcedRoll : forcedRoll % 999 + 1;
        }

        return checked((uint)Random.Shared.Next(1, 1_000));
    }

    private static uint LegacyItemPackServicesReadP7SacredRateRoll(CharacterSnapshot character)
    {
        return TryReadUInt32Attribute(
                character.Attributes,
                out var forcedRoll,
                RuntimeLegacyP7SacredRateRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
    }

    private static async ValueTask LegacyItemPackServicesLogP7SacredFenceAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string source,
        string sourceLines,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc ItemPackServices P7 Sacred Treasures " +
                $"{FormatSession(context)} source={source} sourceLines={sourceLines} " +
                $"npcId={LegacyItemPackServicesP7SacredTreasuresEnvoyNpcId} optionId={optionId} reason={reason}.")
            .ConfigureAwait(false);
    }
}
