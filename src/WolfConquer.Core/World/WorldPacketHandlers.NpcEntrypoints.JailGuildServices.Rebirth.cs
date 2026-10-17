using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyJailGuildServicesRebirthMasterNpcId = 9_072;

    private const uint LegacyJailGuildServicesCelestialStoneItemId = 721_259;
    private const uint LegacyJailGuildServicesExemptionTokenItemId = 723_701;
    private const uint LegacyJailGuildServicesOblivionDewItemId = 711_083;

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesRebirthMasterSource =
        new("NpcHandler.RebirthMaster", "40340-41025");

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesRebirthMasterOblivionDewSource =
        new("NpcHandler.RebirthMaster", "40405-40419");


    private static readonly HashSet<byte> LegacyJailGuildServicesRebirthClassOptions =
    [
        11,
        21,
        41,
        51,
        61,
        71,
        81,
        91,
        132,
        142,
        161,
    ];

    private static readonly HashSet<byte> LegacyJailGuildServicesRebirthGemOptions =
    [
        203,
        213,
        223,
        233,
        243,
        253,
        254,
    ];

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesRebirthMasterAsync(
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
                        LegacyJailGuildServicesRebirthMasterSource.SourceMethod,
                        ["I devoted my life to the research of eternity, and now I know the secret of rebirth. What can I help you with?"],
                        [
                            new("1st Rebirth.", 1),
                            new("2nd Rebirth.", 4),
                            new("Level 140.", 211),
                            new("Reincarnation.", 5),
                            new("OblivionDew.", 67),
                            new("Reallot my Attribute Points.", 8),
                            new("Just passing by.", LegacyNpcClosedOption),
                        ],
                        avatarId: 67,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 8:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesRebirthMasterSource.SourceMethod,
                        ["Reborn players above level 70 can redistribute attribute points at the cost of a DragonBall."],
                        [
                            new("I will reallot my points.", 9),
                            new("OblivionDew.", 67),
                            new("Let me think it over.", LegacyNpcClosedOption),
                        ],
                        avatarId: 67,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 5:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesRebirthMasterSource.SourceMethod,
                        [
                            "If you have been reborn twice and are at least level 110, bring Oblivion Dew and I can help you reincarnate.",
                            "Your class skills will revert to level 0, then return when you reach level 110 again.",
                        ],
                        [
                            new("I want to get reincarnated.", 6),
                            new("Where can I get the Dew?", 7),
                        ],
                        avatarId: 67,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 7:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesRebirthMasterSource.SourceMethod,
                        ["Oblivion Dew is available in the Honor Store and Horse Race Store."],
                        [new LegacyNpcDialogOption("Got it. Thanks.", LegacyNpcClosedOption)],
                        avatarId: 67,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                return await TryHandleLegacyJailGuildServicesFirstRebirthRequestAsync(context, session, optionId, cancellationToken)
                    .ConfigureAwait(false);

            case 4:
                return await TryHandleLegacyJailGuildServicesSecondRebirthRequestAsync(context, session, optionId, cancellationToken)
                    .ConfigureAwait(false);

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesRebirthMasterSource.SourceMethod,
                        ["Select the super gem you desire."],
                        [
                            new("SuperPhoenixGem.", 203),
                            new("SuperDragonGem.", 213),
                            new("SuperFuryGem.", 223),
                            new("SuperRainbowGem.", 233),
                            new("SuperVioletGem.", 253),
                            new("SuperMoonGem.", 254),
                            new("SuperKylinGem.", 243),
                            new("I will think about it.", LegacyNpcClosedOption),
                        ],
                        avatarId: 67,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyJailGuildServicesRebirthClassDialogAsync(context, cancellationToken).ConfigureAwait(false);
                return true;

            case 67:
                return await TryHandleLegacyJailGuildServicesRebirthMasterOblivionDewAsync(
                        context,
                        session,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 6:
            case 9:
            case 211:
                return await TryHandleLegacyJailGuildServicesRebirthMutationAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);

            default:
                if (LegacyJailGuildServicesRebirthGemOptions.Contains(optionId))
                {
                    await SendLegacyJailGuildServicesRebirthClassDialogAsync(context, cancellationToken).ConfigureAwait(false);
                    return true;
                }

                if (LegacyJailGuildServicesRebirthClassOptions.Contains(optionId))
                {
                    await SendLegacyJailGuildServicesUnavailableAsync(
                            context,
                            LegacyJailGuildServicesRebirthMasterSource,
                            LegacyJailGuildServicesRebirthMasterNpcId,
                            optionId,
                            "rebirth class change, item consumption, equipment mutation, and skill grants are not modeled",
                            avatarId: 67,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesFirstRebirthRequestAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!LegacyJailGuildServicesCanFirstRebirth(session))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesRebirthMasterSource.SourceMethod,
                    ["You have not reached the highest rank yet. Water Saints need level 110; other classes need level 120."],
                    LegacyJailGuildServicesISeeOptions,
                    avatarId: 67,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventoryStackCount(session.PlayerItems, LegacyJailGuildServicesCelestialStoneItemId, 1))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesRebirthMasterSource.SourceMethod,
                    ["Sorry, you do not have a CelestialStone."],
                    LegacyJailGuildServicesISeeOptions,
                    avatarId: 67,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        await SendLegacyNpcDialogAsync(
                context,
                LegacyJailGuildServicesRebirthMasterSource.SourceMethod,
                [
                    "There are two kinds of rebirth: normal and blessed.",
                    "Normal rebirth lets you choose a Super Gem. Blessed rebirth sets -1 blessing on one equipped item.",
                ],
                [
                    new("Normal rebirth.", 2),
                    new("Blessed rebirth.", 3),
                    new("I will think about it.", LegacyNpcClosedOption),
                ],
                avatarId: 67,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesSecondRebirthRequestAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!LegacyJailGuildServicesCanSecondRebirth(session))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesRebirthMasterSource.SourceMethod,
                    ["Sorry, you are not eligible for second rebirth. Please wait until you are a level 120 reborn person."],
                    LegacyJailGuildServicesISeeOptions,
                    avatarId: 67,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventoryStackCount(session.PlayerItems, LegacyJailGuildServicesExemptionTokenItemId, 1))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesRebirthMasterSource.SourceMethod,
                    ["Sorry, you do not have an ExemptionToken."],
                    LegacyJailGuildServicesISeeOptions,
                    avatarId: 67,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        await SendLegacyNpcDialogAsync(
                context,
                LegacyJailGuildServicesRebirthMasterSource.SourceMethod,
                ["Do not worry. You just need an ExemptionToken and everything will be fine."],
                [
                    new("Here is the ExemptionToken.", 3),
                    new("I will think about it.", LegacyNpcClosedOption),
                ],
                avatarId: 67,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesRebirthMasterOblivionDewAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (session.Character.ConquerPoints < 1)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("sorry you not have enough ConquerPoints  .").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyNpc JailGuildServices RebirthMaster OblivionDew insufficient CP " +
                    $"{FormatSession(context)} source={LegacyJailGuildServicesRebirthMasterOblivionDewSource.SourceMethod} " +
                    $"sourceLines={LegacyJailGuildServicesRebirthMasterOblivionDewSource.SourceLines} optionId=67.")
                .ConfigureAwait(false);
            return true;
        }

        if (context.Sessions is null)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc JailGuildServices RebirthMaster OblivionDew persistence " +
                    $"{FormatSession(context)} source={LegacyJailGuildServicesRebirthMasterOblivionDewSource.SourceMethod} " +
                    $"sourceLines={LegacyJailGuildServicesRebirthMasterOblivionDewSource.SourceLines} optionId=67 " +
                    "reason=sessionStoreMissing; no CP, item, session, or packet mutation was committed.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.Uid is < uint.MinValue or > uint.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc JailGuildServices RebirthMaster OblivionDew mutation " +
                    $"{FormatSession(context)} source={LegacyJailGuildServicesRebirthMasterOblivionDewSource.SourceMethod} " +
                    $"sourceLines={LegacyJailGuildServicesRebirthMasterOblivionDewSource.SourceLines} optionId=67 " +
                    "reason=characterUidOutOfRange; no CP, item, session, or packet mutation was committed.")
                .ConfigureAwait(false);
            return true;
        }

        PlayerItemSnapshotItem? grantedItem = null;
        var updatedItems = session.PlayerItems;
        if (HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            if (!TryReadLegacyItemType(
                    context.WorldData,
                    LegacyJailGuildServicesOblivionDewItemId,
                    out var itemType) ||
                itemType.Id != checked((int)LegacyJailGuildServicesOblivionDewItemId) ||
                itemType.LegacyDurability > ushort.MaxValue)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc JailGuildServices RebirthMaster OblivionDew mutation " +
                        $"{FormatSession(context)} source={LegacyJailGuildServicesRebirthMasterOblivionDewSource.SourceMethod} " +
                        $"sourceLines={LegacyJailGuildServicesRebirthMasterOblivionDewSource.SourceLines} optionId=67 " +
                        $"itemId={LegacyJailGuildServicesOblivionDewItemId} reason=itemtypeMissingOrInvalid; " +
                        "no CP, item, session, or packet mutation was committed.")
                    .ConfigureAwait(false);
                return true;
            }

            try
            {
                grantedItem = CreatePurchasedItems(
                        session.PlayerItems,
                        itemType,
                        count: 1,
                        isBound: false)[0]
                    .WithColor(checked((uint)Random.Shared.Next(3, 9)));
            }
            catch (OverflowException)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc JailGuildServices RebirthMaster OblivionDew mutation " +
                        $"{FormatSession(context)} source={LegacyJailGuildServicesRebirthMasterOblivionDewSource.SourceMethod} " +
                        $"sourceLines={LegacyJailGuildServicesRebirthMasterOblivionDewSource.SourceLines} optionId=67 " +
                        $"itemId={LegacyJailGuildServicesOblivionDewItemId} reason=itemUidExhausted; " +
                        "no CP, item, session, or packet mutation was committed.")
                    .ConfigureAwait(false);
                return true;
            }

            updatedItems = session.PlayerItems with
            {
                InventoryItems = session.PlayerItems.InventoryItems
                    .Concat([grantedItem])
                    .OrderBy(item => item.Uid)
                    .ToArray(),
            };
        }

        var updatedConquerPoints = session.Character.ConquerPoints - 1;
        var updatedCharacter = WithCharacterAttribute(
            session.Character with { ConquerPoints = updatedConquerPoints },
            "ConquerPoints",
            checked((ulong)updatedConquerPoints));
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
                persistItems: grantedItem is not null,
                out var persistenceReason))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc JailGuildServices RebirthMaster OblivionDew persistence " +
                    $"{FormatSession(context)} source={LegacyJailGuildServicesRebirthMasterOblivionDewSource.SourceMethod} " +
                    $"sourceLines={LegacyJailGuildServicesRebirthMasterOblivionDewSource.SourceLines} optionId=67 " +
                    $"itemId={LegacyJailGuildServicesOblivionDewItemId} reason={persistenceReason}; " +
                    "no CP, item, session, or packet mutation was committed.")
                .ConfigureAwait(false);
            return true;
        }

        context.Sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendConquerPointsUpdateAsync(updatedContext, updatedCharacter, cancellationToken).ConfigureAwait(false);
        if (grantedItem is not null)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(grantedItem).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc JailGuildServices RebirthMaster OblivionDew purchase " +
                $"{FormatSession(updatedContext)} source={LegacyJailGuildServicesRebirthMasterOblivionDewSource.SourceMethod} " +
                $"sourceLines={LegacyJailGuildServicesRebirthMasterOblivionDewSource.SourceLines} optionId=67 " +
                $"conquerPoints={session.Character.ConquerPoints}->{updatedConquerPoints} " +
                $"itemGranted={(grantedItem is not null).ToString(CultureInfo.InvariantCulture)} " +
                $"itemUid={(grantedItem?.Uid ?? 0).ToString(CultureInfo.InvariantCulture)} " +
                "sourceOrder=conquer-points+add-item no-success-dialog=true.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesRebirthMutationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 211 && session.Character.ConquerPoints < 1)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesRebirthMasterSource.SourceMethod,
                    ["Sorry, you do not have enough CPs."],
                    LegacyJailGuildServicesISeeOptions,
                    avatarId: 67,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 9 && session.Character.Level < 70)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesRebirthMasterSource.SourceMethod,
                    ["You cannot reallot your ability points unless you are reborn and above level 70."],
                    LegacyJailGuildServicesISeeOptions,
                    avatarId: 67,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 9 && !HasLegacyInventoryStackCount(session.PlayerItems, LegacyJailGuildServicesDragonBallItemId, 1))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesRebirthMasterSource.SourceMethod,
                    ["Sorry, you do not have one DragonBall."],
                    LegacyJailGuildServicesISeeOptions,
                    avatarId: 67,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 6 &&
            (session.Character.Reborn < 2 ||
                !HasLegacyInventoryStackCount(session.PlayerItems, LegacyJailGuildServicesOblivionDewItemId, 1)))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesRebirthMasterSource.SourceMethod,
                    ["You have not been reborn twice or you do not have Oblivion Dew."],
                    [new LegacyNpcDialogOption("Got it. Thanks.", LegacyNpcClosedOption)],
                    avatarId: 67,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var reason = optionId switch
        {
            6 => "reincarnation client GUI and rebirth state are not modeled",
            9 => "attribute reallocation and DragonBall consumption are not modeled",
            211 => "level update and CP debit are not modeled",
            _ => "rebirth mutation is not modeled",
        };
        await SendLegacyJailGuildServicesUnavailableAsync(
                context,
                LegacyJailGuildServicesRebirthMasterSource,
                LegacyJailGuildServicesRebirthMasterNpcId,
                optionId,
                reason,
                avatarId: 67,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask SendLegacyJailGuildServicesRebirthClassDialogAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyJailGuildServicesRebirthMasterSource.SourceMethod,
                ["Select the class you want to rebirth into."],
                [
                    new("Trojan.", 11),
                    new("Warrior.", 21),
                    new("Archer.", 41),
                    new("WaterTaoist.", 132),
                    new("FireTaoist.", 142),
                    new("Ninja.", 51),
                    new("Monk.", 61),
                    new("Pirate.", 71),
                    new("Dragon-Warrior.", 81),
                    new("Windwalker.", 161),
                    new("Thunderstriker.", 91),
                    new("I will think about it.", LegacyNpcClosedOption),
                ],
                avatarId: 67,
                cancellationToken)
            .ConfigureAwait(false);
    }


    private static bool LegacyJailGuildServicesCanFirstRebirth(WorldSession session)
    {
        return session.Character.Reborn == 0 &&
            ((session.Character.Level >= 110 && IsLegacyWaterClass(session.Character.Class)) ||
                (session.Character.Level >= 120 && session.Character.Class % 10 >= 5));
    }

    private static bool LegacyJailGuildServicesCanSecondRebirth(WorldSession session)
    {
        return session.Character.Reborn == 1 &&
            ((session.Character.Level >= 110 && IsLegacyWaterClass(session.Character.Class)) ||
                (session.Character.Level >= 120 && session.Character.Class % 10 >= 5));
    }
}
