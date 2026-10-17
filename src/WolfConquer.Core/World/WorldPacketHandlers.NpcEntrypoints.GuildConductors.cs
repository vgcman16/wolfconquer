using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyGuildWarMapId = 1_038;
    private const string LegacyGuildConductorsSourceMethod = "NpcHandler.GuildConductorsProces";
    private const string LegacyGuildCondTeleBackSourceMethod = "NpcHandler.GuildCondTeleBack";
    private const string LegacySpaceMarksSourceMethod = "NpcHandler.SpaceMarks";
    private const long LegacyGuildConductorTeleportCost = 1_000;
    private const long LegacyGuildConductorBackCost = 500;
    private const long LegacyGuildTeleporterItemCost = 100_000;
    private const long LegacySpaceMarkScrollCost = 1_000;

    private static readonly HashSet<uint> LegacyGuildConductorForwardNpcIds =
    [
        101_621,
        101_619,
        101_617,
        101_615,
    ];

    private static readonly HashSet<uint> LegacyGuildConductorBackNpcIds =
    [
        101_620,
        101_618,
        101_616,
        101_614,
    ];

    private static readonly IReadOnlyDictionary<uint, LegacyNpcTeleportDestination> LegacyGuildConductorFallbackDestinations =
        new Dictionary<uint, LegacyNpcTeleportDestination>
        {
            [101_614] = new(1_005, 33, 42, LegacyGuildConductorsSourceMethod),
            [101_616] = new(1_005, 33, 48, LegacyGuildConductorsSourceMethod),
            [101_618] = new(1_005, 32, 53, LegacyGuildConductorsSourceMethod),
            [101_620] = new(1_000, 307, 279, LegacyGuildConductorsSourceMethod),
        };

    private static readonly IReadOnlyDictionary<byte, uint> LegacyGuildTeleporterItems =
        new Dictionary<byte, uint>
        {
            [3] = 720_021,
            [4] = 720_022,
            [5] = 720_023,
            [6] = 720_024,
        };

    private static readonly IReadOnlyDictionary<uint, uint> LegacySpaceMarkScrollItems =
        new Dictionary<uint, uint>
        {
            [10_031] = 1_060_025,
            [10_032] = 1_060_026,
            [10_033] = 1_060_027,
            [10_034] = 1_060_028,
            [10_035] = 1_060_029,
            [10_036] = 1_060_038,
            [10_037] = 1_060_031,
            [10_038] = 1_060_032,
            [10_039] = 1_060_033,
            [10_040] = 1_060_034,
            [10_041] = 1_060_035,
            [10_043] = 1_060_037,
        };

    private static readonly LegacyNpcDialogOption[] LegacyGuildConductorOptions =
    [
        new("Please~teleport~me~there.", 1),
        new("Buy~Guild~Teleporter.", 2),
        new("Just~passing~by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyGuildTeleporterShopOptions =
    [
        new("Buy~Teleporter~1.", 3),
        new("Buy~Teleporter~2.", 4),
        new("Buy~Teleporter~3.", 5),
        new("Buy~Teleporter~4.", 6),
        new("I~changed~my~mind.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyGuildConductorBackOptions =
    [
        new("Teleport me.", 1),
        new("I'm too poor.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySpaceMarksWaterOptions =
    [
        new("Ok give me.", 1),
        new("No thanks.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> TryHandleLegacyGuildConductorsAndSpaceMarksAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (LegacyGuildConductorForwardNpcIds.Contains(npcId))
        {
            if (session.Character.MapId != LegacyGuildWarMapId)
            {
                return false;
            }

            return await TryHandleLegacyGuildConductorForwardAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (LegacySpaceMarkScrollItems.ContainsKey(npcId))
        {
            if (LegacyNpcScriptDefinition.TryGetByNpcId(npcId, out _))
            {
                return false;
            }

            if (ReadAttributeUInt64(session.Character.Attributes, "DynamicID", "DynamicId") != 0)
            {
                return false;
            }

            return await TryHandleLegacySpaceMarksAsync(context, session, npcId, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        return LegacyGuildConductorBackNpcIds.Contains(npcId) &&
            await TryHandleLegacyGuildConductorBackAsync(context, session, npcId, optionId, cancellationToken)
                .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyGuildConductorForwardAsync(
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
                        LegacyGuildConductorsSourceMethod,
                        ["Are you heading for the next teleporter? It is free for our members, and 1,000 silver for others."],
                        LegacyGuildConductorOptions,
                        3,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                return await TryTeleportLegacyGuildConductorForwardAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyGuildConductorsSourceMethod,
                        ["A Teleporter costs 100,000 silver. After you buy one and place it at a good location, you can be teleported there directly."],
                        LegacyGuildTeleporterShopOptions,
                        3,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 3:
            case 4:
            case 5:
            case 6:
                return await TryBuyLegacyGuildTeleporterAsync(context, session, optionId, cancellationToken)
                    .ConfigureAwait(false);
            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryTeleportLegacyGuildConductorForwardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Money < LegacyGuildConductorTeleportCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyGuildConductorsSourceMethod,
                    ["Please take 1000 silvers to use guild teleporter."],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    3,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!TryGetLegacyGuildConductorDestination(npcId - 1, context.WorldData?.GuildWarInfo, out var destination))
        {
            return false;
        }

        var chargedSession = await ChargeLegacyNpcMoneyAsync(
                context,
                session,
                LegacyGuildConductorTeleportCost,
                LegacyGuildConductorsSourceMethod,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcTeleportAsync(context, chargedSession, npcId, optionId, destination, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryBuyLegacyGuildTeleporterAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!AllowsLegacyGuildTeleporterPurchase(session.Character))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyGuildConductorsSourceMethod,
                    ["Sorry, only the guild leader who won the guild war can buy a Guild Teleporter."],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    3,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyGuildConductorsSourceMethod,
                    ["Please make one more space in your inventory."],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    3,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.Money < LegacyGuildTeleporterItemCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyGuildConductorsSourceMethod,
                    ["You do not have 100.000 silvers with you."],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    3,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var itemId = LegacyGuildTeleporterItems[optionId];
        await GrantLegacyNpcPurchasedItemAsync(
                context,
                session,
                itemId,
                LegacyGuildTeleporterItemCost,
                LegacyGuildConductorsSourceMethod,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyGuildConductorBackAsync(
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
                    LegacyGuildCondTeleBackSourceMethod,
                    ["In exchange of a fee of 500 gold, I will teleport you back to the guild war map. Don't bother me if you are poor."],
                    LegacyGuildConductorBackOptions,
                    3,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId != 1)
        {
            return false;
        }

        if (session.Character.Money < LegacyGuildConductorBackCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyGuildCondTeleBackSourceMethod,
                    ["You do not have 500 silvers with you."],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    3,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var chargedSession = await ChargeLegacyNpcMoneyAsync(
                context,
                session,
                LegacyGuildConductorBackCost,
                LegacyGuildCondTeleBackSourceMethod,
                cancellationToken)
            .ConfigureAwait(false);
        if (HasLegacyStatusFlag(chargedSession.Character, LegacyRideFlag))
        {
            var unmountedSession = chargedSession with
            {
                Character = WithoutLegacyStatusFlag(chargedSession.Character, LegacyRideFlag),
            };
            context.Sessions?.Set(context.Connection, unmountedSession);
            chargedSession = unmountedSession;
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context with { Session = unmountedSession },
                    unmountedSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await SendLegacyNpcTeleportAsync(
                context,
                chargedSession,
                npcId,
                optionId,
                new LegacyNpcTeleportDestination(LegacyGuildWarMapId, 348, 339, LegacyGuildCondTeleBackSourceMethod),
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacySpaceMarksAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            if (IsLegacyWaterClass(session.Character.Class))
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySpaceMarksSourceMethod,
                        ["I sell WindSpell for 1,000 silver. Use it to teleport back to me."],
                        LegacySpaceMarksWaterOptions,
                        3,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await SendLegacyNpcDialogAsync(
                    context,
                    LegacySpaceMarksSourceMethod,
                    ["Only Water class can buy this teleport scroll."],
                    [new LegacyNpcDialogOption("I see.", LegacyNpcClosedOption)],
                    3,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId != 1)
        {
            return false;
        }

        if (!IsLegacyWaterClass(session.Character.Class))
        {
            return true;
        }

        if (session.Character.Money < LegacySpaceMarkScrollCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacySpaceMarksSourceMethod,
                    ["You do not have 1000 Silvers with you."],
                    [new LegacyNpcDialogOption("I~see", LegacyNpcClosedOption)],
                    3,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacySpaceMarksSourceMethod,
                    ["Please make space in you inventory"],
                    [new LegacyNpcDialogOption("Ah~thank~you!.", LegacyNpcClosedOption)],
                    3,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        await GrantLegacyNpcPurchasedItemAsync(
                context,
                session,
                LegacySpaceMarkScrollItems[npcId],
                LegacySpaceMarkScrollCost,
                LegacySpaceMarksSourceMethod,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession> ChargeLegacyNpcMoneyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        long cost,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        var updatedCharacter = session.Character with { Money = session.Character.Money - cost };
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        await SendWalletMoneyUpdateAsync(context with { Session = updatedSession }, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        PersistLegacyNpcMoney(context, updatedSession, sourceMethod);
        return updatedSession;
    }

    private static async ValueTask GrantLegacyNpcPurchasedItemAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint itemId,
        long cost,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        var chargedCharacter = session.Character with { Money = session.Character.Money - cost };
        var grantedItem = CreateLegacyNpcItemGrant(context, session.PlayerItems, itemId);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat([grantedItem]).OrderBy(item => item.Uid).ToArray(),
        };
        var updatedSession = session with
        {
            Character = chargedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendWalletMoneyUpdateAsync(updatedContext, chargedCharacter, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(grantedItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyItemFollowupsAsync(updatedContext, grantedItem, cancellationToken).ConfigureAwait(false);

        PersistLegacyNpcMoney(context, updatedSession, sourceMethod);
        await PersistLegacyNpcItemsAsync(updatedContext, itemId, sourceMethod).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc purchased item " +
                $"{FormatSession(updatedContext)} source={sourceMethod} itemId={itemId} cost={cost}.")
            .ConfigureAwait(false);
    }

    private static PlayerItemSnapshotItem CreateLegacyNpcItemGrant(
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

    private static bool TryGetLegacyGuildConductorDestination(
        uint conductorNpcId,
        GuildWarData? guildWarInfo,
        out LegacyNpcTeleportDestination destination)
    {
        if (guildWarInfo is not null)
        {
            foreach (var conductor in guildWarInfo.Conductors)
            {
                if (conductor.Fields.Count < 4 ||
                    !uint.TryParse(conductor.Fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var npcId) ||
                    npcId != conductorNpcId ||
                    !ushort.TryParse(conductor.Fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                    !ushort.TryParse(conductor.Fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
                    !uint.TryParse(conductor.Fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mapId))
                {
                    continue;
                }

                destination = new LegacyNpcTeleportDestination(
                    checked((int)mapId),
                    x,
                    y,
                    LegacyGuildConductorsSourceMethod);
                return true;
            }
        }

        return LegacyGuildConductorFallbackDestinations.TryGetValue(conductorNpcId, out destination);
    }

    private static bool AllowsLegacyGuildTeleporterPurchase(CharacterSnapshot character)
    {
        var guildId = ReadAttributeUInt64(character.Attributes, "GuildID", "GuildId");
        var winnerGuildId = ReadAttributeUInt64(
            character.Attributes,
            "LegacyNewGuildWarWinnerGuildID",
            "NewGuildWarWinnerGuildID",
            "GuildWarWinnerGuildID");
        var process = ReadAttributeString(
            character.Attributes,
            "LegacyNewGuildWarProcess",
            "NewGuildWarProcess",
            "GuildWarProcess");
        return guildId != 0 &&
            guildId == winnerGuildId &&
            process.Equals("Dead", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegacyWaterClass(long classCode)
    {
        var family = classCode / 10;
        return family == 13;
    }

    private static void PersistLegacyNpcMoney(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Money"] = session.Character.Money.ToString(CultureInfo.InvariantCulture),
        };
        TryPersistLegacyCharacterAttributes(session.UsersRoot, session.AccountId, values, out _, out _);
    }

    private static async ValueTask PersistLegacyNpcItemsAsync(
        WorldPacketHandlerContext context,
        uint itemId,
        string sourceMethod)
    {
        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot) || context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc item purchase missing PlayerItemsRoot " +
                    $"{FormatSession(context)} source={sourceMethod} itemId={itemId}; granted item is session-only.")
                .ConfigureAwait(false);
            return;
        }

        try
        {
            new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(session.Character.Uid, session.PlayerItems);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException ||
            ex is NotSupportedException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc item purchase persistence " +
                    $"{FormatSession(context)} source={sourceMethod} itemId={itemId} reason={ex.Message}.")
                .ConfigureAwait(false);
        }
    }
}
