using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyClearInventoryNpcId = 1_245;
    private const uint LegacyReallotPointsNpcId = 2_542;
    private const uint LegacyReallotPointsDragonBallItemId = 1_088_000;
    private const string LegacyClearInventorySource = "NpcHandler.clearinventory";
    private const string LegacyReallotPointsSource = "NpcHandler.ReallotPoints";
    private const string LegacyReallotPointsResetSourceRange = "NpcHandler.cs:7509-7564";

    public static ValueTask<bool> TryHandleLegacyUtilityNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return TryHandleLegacyUtilityNpcAsync(context, session, npcId, optionId, "", cancellationToken);
    }

    public static async ValueTask<bool> TryHandleLegacyUtilityNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string input,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyClearInventoryNpcId => await TryHandleLegacyClearInventoryNpcAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyReallotPointsNpcId => await TryHandleLegacyReallotPointsNpcAsync(
                    context,
                    session,
                    optionId,
                    input,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyClearInventoryNpcAsync(
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
                        LegacyClearInventorySource,
                        ["I can help clean unwanted items from your inventory."],
                        [
                            new LegacyNpcDialogOption("Clear my inventory.", 5),
                            new LegacyNpcDialogOption("No thanks.", byte.MaxValue),
                        ],
                        avatarId: 0,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 5:
                await TryApplyLegacyClearInventoryAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask TryApplyLegacyClearInventoryAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            await SendLegacyClearInventoryUnavailableAsync(
                    context,
                    "sessionStoreMissing",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            await SendLegacyClearInventoryUnavailableAsync(
                    context,
                    "playerItemsRootMissing",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var removedItems = session.PlayerItems.InventoryItems
            .OrderBy(item => item.Uid)
            .ToArray();
        if (removedItems.Length == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer applied LegacyNpc clearinventory " +
                    $"{FormatSession(context)} source={LegacyClearInventorySource} " +
                    "sourceRanges=NpcHandler.cs:5229-5247,Inventory.cs:1304-1311 removedCount=0.")
                .ConfigureAwait(false);
            return;
        }

        var updatedItems = session.PlayerItems with { InventoryItems = [] };
        var updatedSession = session with { PlayerItems = updatedItems };
        try
        {
            new Data.PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
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
                    "PARITY-GAP LegacyNpc clearinventory persistence " +
                    $"{FormatSession(context)} source={LegacyClearInventorySource} " +
                    $"sourceRanges=NpcHandler.cs:5229-5247,Inventory.cs:1304-1311 reason={ex.GetType().Name}; " +
                    "no session mutation or remove-inventory frames were committed.")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        foreach (var item in removedItems)
        {
            await context.Connection.SendAsync(
                    new MsgItemUsagePacket(
                            item.Uid,
                            DwParam: 0,
                            MsgItemUsageAction.RemoveInventory,
                            Timestamp: CreateLegacyClearInventoryItemUsageTimestamp(),
                            DwParam2: 0,
                            DwParam3: 0,
                            DwParam4: 0)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc clearinventory " +
                $"{FormatSession(updatedContext)} source={LegacyClearInventorySource} " +
                "sourceRanges=NpcHandler.cs:5229-5247,Inventory.cs:1304-1311,Inventory.cs:1123-1135 " +
                $"removedCount={removedItems.Length.ToString(CultureInfo.InvariantCulture)} " +
                "removeFull=true frameOrder=itemUidAscending persistenceBeforeSessionCommit=true.")
            .ConfigureAwait(false);
    }

    private static uint CreateLegacyClearInventoryItemUsageTimestamp()
    {
        // Decompiled ItemUsageCreate replaces a zero timestamp with DateTime.Now.Value().
        var now = DateTime.Now;
        return unchecked((uint)(Environment.TickCount +
            (int)((now.Ticks - DateTime.Now.Ticks) / TimeSpan.TicksPerMillisecond)));
    }

    private static async ValueTask SendLegacyClearInventoryUnavailableAsync(
        WorldPacketHandlerContext context,
        string reason,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyClearInventorySource,
                ["Inventory clearing is unavailable here because the legacy action deletes items."],
                [new LegacyNpcDialogOption("I understand.", byte.MaxValue)],
                avatarId: 0,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc clearinventory source=NpcHandler.cs:5229-5248; " +
                "source clears inventory destructively with client.Inventory.Clear, so WolfConquer leaves inventory unchanged. " +
                $"reason={reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyReallotPointsNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string input,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyReallotPointsSource,
                        ["Hello, friend. I can help you assign your attribute points."],
                        [
                            new LegacyNpcDialogOption("Vitality points.", 5),
                            new LegacyNpcDialogOption("Agility points.", 6),
                            new LegacyNpcDialogOption("Strength points.", 7),
                            new LegacyNpcDialogOption("Spirit points.", 8),
                            new LegacyNpcDialogOption("Reset all my points.", 9),
                            new LegacyNpcDialogOption("Just passing by.", byte.MaxValue),
                        ],
                        avatarId: 6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 5:
                await SendLegacyNpcInputDialogAsync(
                        context,
                        LegacyReallotPointsSource,
                        "Enter vitality points.",
                        "Vitality points.",
                        inputOptionId: 1,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 6:
                await SendLegacyNpcInputDialogAsync(
                        context,
                        LegacyReallotPointsSource,
                        "Enter agility points.",
                        "Agility points.",
                        inputOptionId: 2,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 7:
                await SendLegacyNpcInputDialogAsync(
                        context,
                        LegacyReallotPointsSource,
                        "Enter strength points.",
                        "Strength points.",
                        inputOptionId: 3,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 8:
                await SendLegacyNpcInputDialogAsync(
                        context,
                        LegacyReallotPointsSource,
                        "Enter spirit points.",
                        "Spirit points.",
                        inputOptionId: 4,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 9:
                await TryApplyLegacyReallotPointsResetAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case >= 1 and <= 4:
                await TryApplyLegacyReallotPointsAllocationAsync(
                        context,
                        session,
                        optionId,
                        input,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask TryApplyLegacyReallotPointsResetAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < 70)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyReallotPointsSource,
                    ["You cannot reallot your ability points unless you are reborn and above level 70."],
                    [new LegacyNpcDialogOption("I see.", byte.MaxValue)],
                    avatarId: 67,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc ReallotPoints reset " +
                    $"{FormatSession(context)} source={LegacyReallotPointsSource} " +
                    $"sourceRange={LegacyReallotPointsResetSourceRange} reason=levelBelow70.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is null ||
            context.WorldData is null ||
            string.IsNullOrWhiteSpace(session.UsersRoot) ||
            string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            await SendLegacyReallotPointsResetUnavailableAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await LogReallotParityGapAsync(
                    context,
                    optionId: 9,
                    input: "",
                    "reset requires Sessions, WorldData, UsersRoot, and PlayerItemsRoot for a persistence-first commit")
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyReallotPointsUnboundDragonBall(session.PlayerItems))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyReallotPointsSource,
                    ["Sorry, you do not have one DragonBall."],
                    [new LegacyNpcDialogOption("I see.", byte.MaxValue)],
                    avatarId: 67,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc ReallotPoints reset " +
                    $"{FormatSession(context)} source={LegacyReallotPointsSource} " +
                    $"sourceRange={LegacyReallotPointsResetSourceRange} " +
                    $"itemId={LegacyReallotPointsDragonBallItemId} bound=0 reason=itemMissing.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryCreateLegacyReallotPointsResetCharacter(
                session.Character,
                context.WorldData,
                out var updatedCharacter,
                out var secondRebornLevelDefaulted,
                out var formulaReason))
        {
            await SendLegacyReallotPointsResetUnavailableAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await LogReallotParityGapAsync(
                    context,
                    optionId: 9,
                    input: "",
                    $"{LegacyReallotPointsResetSourceRange} reset formula unavailable reason={formulaReason}")
                .ConfigureAwait(false);
            return;
        }

        if (!TryConsumeLegacyReallotPointsUnboundDragonBall(
                session.PlayerItems,
                out var updatedItems,
                out var itemMutationFrame))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc ReallotPoints reset " +
                    $"{FormatSession(context)} source={LegacyReallotPointsSource} " +
                    $"sourceRange={LegacyReallotPointsResetSourceRange} reason=itemMutationUnavailable.")
                .ConfigureAwait(false);
            return;
        }

        var persistenceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Agility"] = updatedCharacter.Agility.ToString(CultureInfo.InvariantCulture),
            ["Strength"] = updatedCharacter.Strength.ToString(CultureInfo.InvariantCulture),
            ["Vitaliti"] = updatedCharacter.Vitality.ToString(CultureInfo.InvariantCulture),
            ["Spirit"] = updatedCharacter.Spirit.ToString(CultureInfo.InvariantCulture),
            ["Atributes"] = updatedCharacter.AttributePoints.ToString(CultureInfo.InvariantCulture),
        };
        if (secondRebornLevelDefaulted)
        {
            persistenceValues["SRL"] = "130";
        }

        if (!TryPersistLegacyReallotPointsReset(
                context.PlayerItemsRoot,
                session,
                updatedCharacter,
                updatedItems,
                persistenceValues,
                out var characterPath,
                out var playerItemsPath,
                out var persistenceReason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc ReallotPoints reset " +
                    $"{FormatSession(context)} source={LegacyReallotPointsSource} " +
                    $"sourceRange={LegacyReallotPointsResetSourceRange} reason={persistenceReason}.")
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(itemMutationFrame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox("You have successfully reloaded your attribute points.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));
        foreach (var statUpdate in LegacyMsgUpdatePacket.CreateLegacyRuntimeSourceStatUpdates(uid, updatedCharacter))
        {
            await context.Connection.SendAsync(statUpdate.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeAttributePointsUpdate(
                        uid,
                        checked((ulong)updatedCharacter.AttributePoints))
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyEquipmentRuntimeRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc ReallotPoints reset " +
                $"{FormatSession(updatedContext)} source={LegacyReallotPointsSource} " +
                $"sourceRange={LegacyReallotPointsResetSourceRange} " +
                $"itemId={LegacyReallotPointsDragonBallItemId} itemBound=0 consumedCount=1 " +
                $"reborn={updatedCharacter.Reborn.ToString(CultureInfo.InvariantCulture)} " +
                $"attributePoints={updatedCharacter.AttributePoints.ToString(CultureInfo.InvariantCulture)} " +
                $"secondRebornLevelDefaulted={secondRebornLevelDefaulted.ToString(CultureInfo.InvariantCulture)} " +
                "updateOrder=Strength,Agility,Spirit,Vitality,Atributes " +
                $"equipmentRuntimeRefresh=true callItems=false characterPath={Path.GetFileName(characterPath)} " +
                $"playerItemsPath={Path.GetFileName(playerItemsPath)}.")
            .ConfigureAwait(false);
    }

    private static ValueTask SendLegacyReallotPointsResetUnavailableAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        return SendLegacyNpcDialogAsync(
            context,
            LegacyReallotPointsSource,
            ["Attribute reset is unavailable here because it must consume one Dragon Ball and rewrite your stats safely."],
            [new LegacyNpcDialogOption("I understand.", byte.MaxValue)],
            avatarId: 67,
            cancellationToken);
    }

    private static bool HasLegacyReallotPointsUnboundDragonBall(Data.PlayerItemSnapshot items)
    {
        var available = 0UL;
        foreach (var item in items.InventoryItems)
        {
            if (item.ItemId != LegacyReallotPointsDragonBallItemId || item.Bound != 0)
            {
                continue;
            }

            available += item.StackSize;
            if (available >= 1)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryConsumeLegacyReallotPointsUnboundDragonBall(
        Data.PlayerItemSnapshot items,
        out Data.PlayerItemSnapshot updatedItems,
        out byte[] mutationFrame)
    {
        var inventory = items.InventoryItems.ToList();
        for (var index = 0; index < inventory.Count; index++)
        {
            var item = inventory[index];
            if (item.ItemId != LegacyReallotPointsDragonBallItemId || item.Bound != 0 || item.StackSize == 0)
            {
                continue;
            }

            if (item.StackSize > 1 && item.Position < 40)
            {
                var decremented = item.WithStackSize(checked((ushort)(item.StackSize - 1)));
                inventory[index] = decremented;
                mutationFrame = LegacyGameItemPacket.FromSnapshotItem(
                        decremented,
                        mode: LegacyGameItemPacket.UpdateItemMode)
                    .ToFrameBytes();
            }
            else
            {
                inventory.RemoveAt(index);
                mutationFrame = new MsgItemUsagePacket(
                        item.Uid,
                        DwParam: 0,
                        MsgItemUsageAction.RemoveInventory,
                        Timestamp: 0,
                        DwParam2: 0,
                        DwParam3: 0,
                        DwParam4: 0)
                    .ToFrameBytes();
            }

            updatedItems = items with { InventoryItems = inventory };
            return true;
        }

        updatedItems = items;
        mutationFrame = [];
        return false;
    }

    private static bool TryCreateLegacyReallotPointsResetCharacter(
        Data.CharacterSnapshot character,
        Data.WorldDataCatalog worldData,
        out Data.CharacterSnapshot updatedCharacter,
        out bool secondRebornLevelDefaulted,
        out string reason)
    {
        updatedCharacter = character;
        secondRebornLevelDefaulted = false;
        reason = string.Empty;

        if (character.Uid is < uint.MinValue or > uint.MaxValue ||
            character.Level is < ushort.MinValue or > ushort.MaxValue ||
            character.Class is < uint.MinValue or > uint.MaxValue ||
            character.Reborn is < byte.MinValue or > byte.MaxValue)
        {
            reason = "sourceScalarRangeInvalid";
            return false;
        }

        if (!TryReadLegacyReallotPointsAttribute(
                character.Attributes,
                "ExtraAtributes",
                ushort.MaxValue,
                out var rawExtraAttributes))
        {
            reason = "extraAtributesInvalid";
            return false;
        }

        var level = checked((ushort)character.Level);
        var classCode = checked((uint)character.Class);
        var reborn = checked((byte)character.Reborn);
        var extraAttributes = rawExtraAttributes > 901 ? (ushort)900 : checked((ushort)rawExtraAttributes);
        ushort strength = 0;
        ushort agility = 0;
        ushort vitality = 1;
        ushort spirit = 0;
        ushort attributePoints;

        if (reborn == 0)
        {
            if (!TryResolveLegacyReallotPointsSourceStats(
                    worldData,
                    classCode,
                    Math.Min(120, (int)level),
                    out var sourceStats,
                    out reason))
            {
                return false;
            }

            strength = checked((ushort)sourceStats.Strength);
            agility = checked((ushort)sourceStats.Agility);
            vitality = checked((ushort)sourceStats.Vitality);
            spirit = checked((ushort)sourceStats.Spirit);
            attributePoints = 0;
            if (IsLegacyReallotPointsWaterClass(classCode))
            {
                if (level > 110)
                {
                    attributePoints = unchecked((ushort)((level - 110) * 3 + extraAttributes));
                }
            }
            else if (level > 120)
            {
                attributePoints = unchecked((ushort)((level - 120) * 3 + extraAttributes));
            }
        }
        else if (reborn == 1)
        {
            if (character.FirstClass is < uint.MinValue or > uint.MaxValue ||
                !TryReadLegacyReallotPointsAttribute(character.Attributes, "FRL", byte.MaxValue, out var rawFirstLevel))
            {
                reason = "firstRebornDataInvalid";
                return false;
            }

            var firstBonus = LegacyReallotPointsExtraAttributePoints(
                checked((byte)rawFirstLevel),
                checked((uint)character.FirstClass));
            attributePoints = unchecked((ushort)(
                firstBonus + 52 + 3 * (level - 15) + extraAttributes));
        }
        else
        {
            if (character.FirstClass is < uint.MinValue or > uint.MaxValue ||
                character.SecondClass is < uint.MinValue or > uint.MaxValue ||
                !TryReadLegacyReallotPointsAttribute(character.Attributes, "FRL", byte.MaxValue, out var rawFirstLevel) ||
                !TryReadLegacyReallotPointsAttribute(character.Attributes, "SRL", byte.MaxValue, out var rawSecondLevel))
            {
                reason = "secondRebornDataInvalid";
                return false;
            }

            var secondLevel = checked((byte)rawSecondLevel);
            if (secondLevel == 0)
            {
                secondLevel = 130;
                secondRebornLevelDefaulted = true;
            }

            var firstBonus = LegacyReallotPointsExtraAttributePoints(
                checked((byte)rawFirstLevel),
                checked((uint)character.FirstClass));
            var secondBonus = LegacyReallotPointsExtraAttributePoints(
                secondLevel,
                checked((uint)character.SecondClass));
            attributePoints = unchecked((ushort)(
                firstBonus + secondBonus + 52 + 3 * (level - 15) + extraAttributes));
        }

        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["Agility"] = agility.ToString(CultureInfo.InvariantCulture),
            ["Strength"] = strength.ToString(CultureInfo.InvariantCulture),
            ["Vitaliti"] = vitality.ToString(CultureInfo.InvariantCulture),
            ["Spirit"] = spirit.ToString(CultureInfo.InvariantCulture),
            ["Atributes"] = attributePoints.ToString(CultureInfo.InvariantCulture),
        };
        if (secondRebornLevelDefaulted)
        {
            attributes["SRL"] = "130";
        }

        updatedCharacter = character with
        {
            Strength = strength,
            Agility = agility,
            Vitality = vitality,
            Spirit = spirit,
            AttributePoints = attributePoints,
            Attributes = attributes,
        };
        return true;
    }

    private static bool TryResolveLegacyReallotPointsSourceStats(
        Data.WorldDataCatalog worldData,
        uint classCode,
        int level,
        out Data.ClassLevelStat sourceStats,
        out string reason)
    {
        sourceStats = default!;
        reason = string.Empty;
        var className = classCode switch
        {
            >= 1_000 and <= 1_051 => "Trojan",
            >= 2_000 and <= 2_051 => "Warrior",
            >= 4_000 and <= 4_051 => "Archer",
            >= 5_000 and <= 5_051 => "Ninja",
            >= 6_000 and <= 6_051 => "Monk",
            >= 7_000 and <= 7_051 => "Pirate",
            >= 8_000 and <= 8_051 => "LongLee",
            >= 9_000 and <= 9_051 => "Thunderstriker",
            >= 10_000 and <= 14_051 => "Taoist",
            >= 16_000 and <= 16_051 => "WindWalker",
            _ => string.Empty,
        };
        if (className.Length == 0)
        {
            reason = "sourceClassStatsUnsupported";
            return false;
        }

        if (!worldData.ClassLevelStatsByClassAndLevel.TryGetValue((className, level), out var found))
        {
            reason = $"sourceStatsMissing:{className}:{level.ToString(CultureInfo.InvariantCulture)}";
            return false;
        }

        sourceStats = found;
        if (sourceStats.Strength is < ushort.MinValue or > ushort.MaxValue ||
            sourceStats.Agility is < ushort.MinValue or > ushort.MaxValue ||
            sourceStats.Vitality is < ushort.MinValue or > ushort.MaxValue ||
            sourceStats.Spirit is < ushort.MinValue or > ushort.MaxValue)
        {
            reason = "sourceStatsRangeInvalid";
            return false;
        }

        return true;
    }

    private static bool TryReadLegacyReallotPointsAttribute(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        ulong maxValue,
        out ulong value)
    {
        if (!attributes.TryGetValue(key, out var raw))
        {
            value = 0;
            return true;
        }

        return ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
            value <= maxValue;
    }

    private static bool IsLegacyReallotPointsWaterClass(uint classCode)
    {
        return classCode is >= 13_002 and <= 13_051;
    }

    private static byte LegacyReallotPointsExtraAttributePoints(byte level, uint classCode)
    {
        if (classCode == 13_005)
        {
            if (level <= 110)
            {
                return 0;
            }

            return level switch
            {
                112 => 1,
                114 => 3,
                116 => 6,
                118 => 10,
                120 or 121 => 15,
                122 or 123 => 21,
                124 or 125 => 28,
                126 or 127 => 36,
                128 or 129 => 45,
                _ => 55,
            };
        }

        if (level <= 120)
        {
            return 0;
        }

        return level switch
        {
            121 => 1,
            122 => 3,
            123 => 6,
            124 => 10,
            125 => 15,
            126 => 21,
            127 => 28,
            128 => 36,
            129 => 45,
            _ => 55,
        };
    }

    private static bool TryPersistLegacyReallotPointsReset(
        string playerItemsRoot,
        WorldSession session,
        Data.CharacterSnapshot updatedCharacter,
        Data.PlayerItemSnapshot updatedItems,
        IReadOnlyDictionary<string, string> characterValues,
        out string characterPath,
        out string playerItemsPath,
        out string reason)
    {
        characterPath = string.Empty;
        playerItemsPath = Path.Combine(
            playerItemsRoot,
            updatedCharacter.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        reason = string.Empty;
        var playerItemsExisted = File.Exists(playerItemsPath);
        byte[]? originalPlayerItemsBytes = null;

        try
        {
            if (playerItemsExisted)
            {
                originalPlayerItemsBytes = File.ReadAllBytes(playerItemsPath);
            }

            new Data.PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(
                updatedCharacter.Uid,
                updatedItems);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException ||
            ex is NotSupportedException)
        {
            reason = "playerItemsPersistenceFailed:" + ex.GetType().Name;
            return false;
        }

        if (TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                updatedCharacter.Uid,
                characterValues,
                out characterPath,
                out var characterReason))
        {
            return true;
        }

        try
        {
            if (playerItemsExisted)
            {
                File.WriteAllBytes(playerItemsPath, originalPlayerItemsBytes!);
            }
            else
            {
                File.Delete(playerItemsPath);
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            reason = $"{characterReason};playerItemsRollbackFailed:{ex.GetType().Name}";
            return false;
        }

        reason = characterReason;
        return false;
    }

    private static async ValueTask SendLegacyNpcInputDialogAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        string text,
        string inputLabel,
        byte inputOptionId,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForDialog(text).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForInput(inputLabel, inputOptionId, maxLength: 16).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForClosedOption("Just passing by.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForAvatar(6).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForFinish().ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer sent LegacyNpc input dialog {FormatSession(context)} source={sourceMethod} option={inputOptionId}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask TryApplyLegacyReallotPointsAllocationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string input,
        CancellationToken cancellationToken)
    {
        if (!ushort.TryParse(input, out var requested))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc ReallotPoints allocation " +
                    $"{FormatSession(context)} source={LegacyReallotPointsSource} option={optionId} " +
                    $"inputLength={(input ?? "").Length.ToString(CultureInfo.InvariantCulture)} reason=invalidAmount.")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.AttributePoints is < ushort.MinValue or > ushort.MaxValue ||
            !TryResolveLegacyReallotPointsStat(
                session.Character,
                optionId,
                out var statKey,
                out var statType,
                out var currentStat))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc ReallotPoints allocation " +
                    $"{FormatSession(context)} source={LegacyReallotPointsSource} option={optionId} " +
                    "reason=sourceStatRangeInvalid.")
                .ConfigureAwait(false);
            return;
        }

        var available = (ushort)session.Character.AttributePoints;
        if (available < requested)
        {
            await SendLegacyReallotPointsInsufficientAsync(context, input, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc ReallotPoints allocation " +
                    $"{FormatSession(context)} source={LegacyReallotPointsSource} option={optionId} " +
                    $"requested={requested.ToString(CultureInfo.InvariantCulture)} " +
                    $"available={available.ToString(CultureInfo.InvariantCulture)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc ReallotPoints allocation " +
                    $"{FormatSession(context)} source={LegacyReallotPointsSource} option={optionId} " +
                    "reason=sessionStoreMissing.")
                .ConfigureAwait(false);
            return;
        }

        var updatedStat = unchecked((ushort)(currentStat + requested));
        var remaining = (ushort)(available - requested);
        var updatedCharacter = WithLegacyReallotPointsAllocation(
            session.Character,
            optionId,
            statKey,
            updatedStat,
            remaining);
        var persistenceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [statKey] = updatedStat.ToString(CultureInfo.InvariantCulture),
            ["Atributes"] = remaining.ToString(CultureInfo.InvariantCulture),
        };
        if (!TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                updatedCharacter.Uid,
                persistenceValues,
                out var persistedPath,
                out var persistenceReason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc ReallotPoints allocation " +
                    $"{FormatSession(context)} source={LegacyReallotPointsSource} option={optionId} " +
                    $"reason={persistenceReason}.")
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));
        var statUpdate = LegacyMsgUpdatePacket.CreateLegacyRuntimeSourceStatUpdates(uid, updatedCharacter)
            .Single(packet => packet.Updates.Count == 1 && packet.Updates[0].Type == statType);
        await context.Connection.SendAsync(statUpdate.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeAttributePointsUpdate(uid, remaining).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyEquipmentRuntimeRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        // Source option 4 creates this box for every parsed request, including a successful allocation.
        if (optionId == 4)
        {
            await SendLegacyReallotPointsInsufficientAsync(context, input, cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc ReallotPoints allocation " +
                $"{FormatSession(updatedContext)} source={LegacyReallotPointsSource} " +
                $"option={optionId} stat={statKey} requested={requested.ToString(CultureInfo.InvariantCulture)} " +
                $"attributePoints={available.ToString(CultureInfo.InvariantCulture)}->{remaining.ToString(CultureInfo.InvariantCulture)} " +
                $"equipmentRuntimeRefresh=true callItems=false path={Path.GetFileName(persistedPath)}.")
            .ConfigureAwait(false);
    }

    private static bool TryResolveLegacyReallotPointsStat(
        Data.CharacterSnapshot character,
        byte optionId,
        out string statKey,
        out LegacyMsgUpdateDataType statType,
        out ushort currentStat)
    {
        var (key, type, value) = optionId switch
        {
            1 => ("Vitaliti", LegacyMsgUpdateDataType.Vitality, character.Vitality),
            2 => ("Agility", LegacyMsgUpdateDataType.Agility, character.Agility),
            3 => ("Strength", LegacyMsgUpdateDataType.Strength, character.Strength),
            4 => ("Spirit", LegacyMsgUpdateDataType.Spirit, character.Spirit),
            _ => ("", default, -1L),
        };

        statKey = key;
        statType = type;
        if (value is < ushort.MinValue or > ushort.MaxValue)
        {
            currentStat = 0;
            return false;
        }

        currentStat = (ushort)value;
        return statKey.Length != 0;
    }

    private static Data.CharacterSnapshot WithLegacyReallotPointsAllocation(
        Data.CharacterSnapshot character,
        byte optionId,
        string statKey,
        ushort updatedStat,
        ushort remaining)
    {
        var updatedCharacter = optionId switch
        {
            1 => character with { Vitality = updatedStat, AttributePoints = remaining },
            2 => character with { Agility = updatedStat, AttributePoints = remaining },
            3 => character with { Strength = updatedStat, AttributePoints = remaining },
            4 => character with { Spirit = updatedStat, AttributePoints = remaining },
            _ => character,
        };
        updatedCharacter = WithCharacterAttribute(updatedCharacter, statKey, updatedStat);
        return WithCharacterAttribute(updatedCharacter, "Atributes", remaining);
    }

    private static ValueTask SendLegacyReallotPointsInsufficientAsync(
        WorldPacketHandlerContext context,
        string input,
        CancellationToken cancellationToken)
    {
        return context.Connection.SendAsync(
            LegacyNpcReplyPacket.ForMessageBox("You do not have: " + input + " attribute points.").ToFrameBytes(),
            cancellationToken);
    }

    private static async ValueTask LogReallotParityGapAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string input,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc ReallotPoints source=NpcHandler.cs:7494-7661 " +
                $"option={optionId} inputLength={(input ?? "").Length.ToString(CultureInfo.InvariantCulture)}; " +
                reason + ", so WolfConquer leaves character stats and inventory unchanged.")
            .ConfigureAwait(false);
    }
}
