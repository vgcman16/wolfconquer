using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyWarFightersSourceConquerPointsReward = 5_000_000;
    private const long TunedLegacyWarFightersConquerPointsReward = 300_000;
    private const ushort LegacyWarFightersStudyPointsReward = 10_000;
    private const int LegacyWarFightersInnerPowerPotencyReward = 10_000;
    private const string LegacyWarFightersRewardClaimedAttribute = "LegacyWarFightersRewardClaimed";

    private static readonly (uint ItemId, ushort StackSize)[] LegacyWarFightersRewardItems =
    [
        (3_009_003, 3),
        (4_050_001, 500),
        (4_040_001, 500),
    ];

    private static async ValueTask TryClaimLegacyWarFightersRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (IsLegacyWarFightersProcessAlive(session.Character))
        {
            await SendLegacyWarFightersRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Please come back when War Fighters is finished.",
                    "skipped active MsgWarFighters.Proces",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (ReadAttributeBool(session.Character.Attributes, GetLegacyWarFightersRewardClaimedAttributeNames()))
        {
            await SendLegacyWarFightersRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "This War Fighters reward was already claimed.",
                    "already claimed",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyWarFightersWinnerPlayerUid(session.Character, out var winnerPlayerUid))
        {
            await SendLegacyWarFightersRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "There is no War Fighters reward available now.",
                    "skipped missing MsgWarFighters.RoundOwner",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var playerUid = session.Character.Uid <= 0
            ? 0
            : checked((ulong)session.Character.Uid);
        if (playerUid != winnerPlayerUid)
        {
            await SendLegacyWarFightersRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "You do not have any War Fighters reward to claim.",
                    $"skipped wrong player winnerPlayerUid={winnerPlayerUid.ToString(CultureInfo.InvariantCulture)} playerUid={playerUid.ToString(CultureInfo.InvariantCulture)}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, LegacyWarFightersRewardItems.Length))
        {
            await SendLegacyWarFightersRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Please make 3 more spaces in your inventory.",
                    "skipped full inventory sourceRequiredSpace=3",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var grantedItems = CreateLegacyWarFightersRewardItems(context, session.PlayerItems);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat(grantedItems).OrderBy(item => item.Uid).ToArray(),
        };
        var updatedCharacter = session.Character with
        {
            ConquerPoints = checked(session.Character.ConquerPoints + TunedLegacyWarFightersConquerPointsReward),
        };
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "ConquerPoints",
            CheckedUInt64(updatedCharacter.ConquerPoints, nameof(updatedCharacter.ConquerPoints)));
        updatedCharacter = AddLegacyStudyPoints(updatedCharacter, LegacyWarFightersStudyPointsReward);
        updatedCharacter = WithLegacyWarFightersRewardClaimCleared(updatedCharacter);
        LegacyWarFightersStates.Clear();

        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendScalarUpdateAsync(
                updatedContext,
                updatedCharacter,
                LegacyMsgUpdateDataType.ConquerPoints,
                checked((ulong)updatedCharacter.ConquerPoints),
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var item in grantedItems)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyItemFollowupsAsync(updatedContext, item, cancellationToken)
                .ConfigureAwait(false);
        }

        var innerPowerApplied = await TryApplyLegacyWarFightersInnerPowerPotencyAwardAsync(
                updatedContext,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);

        await PersistLegacyWarFightersRewardAsync(updatedContext, updatedSession, sourceMethod, innerPowerApplied)
            .ConfigureAwait(false);
        foreach (var item in grantedItems)
        {
            await PersistLegacyNpcItemsAsync(updatedContext, item.ItemId, sourceMethod).ConfigureAwait(false);
        }

        await SendLegacyNpcDialogAsync(
                updatedContext,
                sourceMethod,
                [$"You claimed {TunedLegacyWarFightersConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs, study points, inner power potency, and War Fighters items."],
                LegacyMissedWarEventUnavailableOptions,
                LegacyMissedWarEventAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyWarFightersRewardBroadcastAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc WarFighters reward " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                $"winnerPlayerUid={winnerPlayerUid.ToString(CultureInfo.InvariantCulture)} " +
                $"sourceConquerPoints={LegacyWarFightersSourceConquerPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"tunedConquerPoints={TunedLegacyWarFightersConquerPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"items={FormatLegacyWarFightersRewardItems()} " +
                $"studyPoints={LegacyWarFightersStudyPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"innerPowerPotency={LegacyWarFightersInnerPowerPotencyReward.ToString(CultureInfo.InvariantCulture)} " +
                $"innerPowerApplied={innerPowerApplied.ToString(CultureInfo.InvariantCulture)} " +
                "source=references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgNpc\\NpcHandler.cs WarFighters option 2.")
            .ConfigureAwait(false);
    }

    private static IReadOnlyList<PlayerItemSnapshotItem> CreateLegacyWarFightersRewardItems(
        WorldPacketHandlerContext context,
        PlayerItemSnapshot items)
    {
        var nextUid = GetNextSessionItemUid(items);
        var grants = new PlayerItemSnapshotItem[LegacyWarFightersRewardItems.Length];
        for (var index = 0; index < grants.Length; index++)
        {
            var (itemId, stackSize) = LegacyWarFightersRewardItems[index];
            var durability = TryReadItemType(context.WorldData, itemId, out var itemType)
                ? checked((ushort)itemType.LegacyDurability)
                : (ushort)0;
            grants[index] = new PlayerItemSnapshotItem
            {
                Uid = checked(nextUid + (uint)index),
                ItemId = itemId,
                Durability = durability,
                MaximumDurability = durability,
                Position = 0,
                StackSize = stackSize,
            };
        }

        return grants;
    }

    private static bool TryReadLegacyWarFightersWinnerPlayerUid(
        CharacterSnapshot character,
        out ulong winnerPlayerUid)
    {
        if (TryReadUInt64(
                character.Attributes,
                out winnerPlayerUid,
                GetLegacyWarFightersWinnerPlayerUidAttributeNames()) &&
            winnerPlayerUid != 0)
        {
            return true;
        }

        return TryReadLegacyWarFightersWinnerPlayerUidFromScores(character, out winnerPlayerUid);
    }

    private static bool TryReadLegacyWarFightersWinnerPlayerUidFromScores(
        CharacterSnapshot character,
        out ulong winnerPlayerUid)
    {
        winnerPlayerUid = 0;
        var raw = ReadAttributeString(
            character.Attributes,
            GetLegacyWarFightersScoresAttributeNames());
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        ulong bestScore = 0;
        foreach (var entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = entry.Split(',', StringSplitOptions.TrimEntries);
            if (fields.Length < 2 ||
                !ulong.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var playerUid) ||
                !ulong.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score) ||
                playerUid == 0 ||
                (winnerPlayerUid != 0 && score <= bestScore))
            {
                continue;
            }

            winnerPlayerUid = playerUid;
            bestScore = score;
        }

        return winnerPlayerUid != 0;
    }

    private static CharacterSnapshot WithLegacyWarFightersRewardClaimCleared(CharacterSnapshot character)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var key in GetLegacyWarFightersRewardClaimedAttributeNames())
        {
            attributes[key] = "true";
        }

        foreach (var key in GetLegacyWarFightersWinnerClearedAttributeNames())
        {
            attributes[key] = "true";
        }

        foreach (var key in GetLegacyWarFightersScoresAttributeNames())
        {
            attributes.Remove(key);
        }

        foreach (var key in GetLegacyWarFightersRoundOwnerAttributeNames())
        {
            attributes.Remove(key);
        }

        return character with { Attributes = attributes };
    }

    private static async ValueTask<bool> TryApplyLegacyWarFightersInnerPowerPotencyAwardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is null)
        {
            await LogLegacyWarFightersInnerPowerDeferredAsync(context, session, "InnerPower.txt rows are not loaded")
                .ConfigureAwait(false);
            return false;
        }

        if (session.Character.Uid is < 0 or > int.MaxValue)
        {
            await LogLegacyWarFightersInnerPowerDeferredAsync(
                    context,
                    session,
                    $"InnerPower.txt row key {session.Character.Uid} exceeds source row key range")
                .ConfigureAwait(false);
            return false;
        }

        var characterId = checked((int)session.Character.Uid);
        if (!context.WorldData.InnerPowerRanksByCharacterId.TryGetValue(characterId, out var rows) ||
            rows.Count == 0)
        {
            await LogLegacyWarFightersInnerPowerDeferredAsync(
                    context,
                    session,
                    $"InnerPower.txt row for character {characterId} is not loaded")
                .ConfigureAwait(false);
            return false;
        }

        var updatedPotency = 0;
        var applied = false;
        var deferReason = string.Empty;
        lock (rows)
        {
            var row = rows[0];
            if (TryReadLegacyInnerPowerState(row, out var state, out deferReason))
            {
                updatedPotency = checked(state.Potency + LegacyWarFightersInnerPowerPotencyReward);
                var updatedRow = CreateLegacyInnerPowerRankEntry(row, state with { Potency = updatedPotency });
                applied = TryReplaceLegacyInnerPowerRow(rows, updatedRow, out deferReason);
            }
        }

        if (!applied)
        {
            await LogLegacyWarFightersInnerPowerDeferredAsync(context, session, deferReason).ConfigureAwait(false);
            return false;
        }

        await SendScalarUpdateAsync(
                context,
                session.Character,
                LegacyMsgUpdateDataType.InnerPowerPotency,
                checked((ulong)updatedPotency),
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask LogLegacyWarFightersInnerPowerDeferredAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "WorldServer deferred LegacyNpc WarFighters InnerPower potency reward " +
                $"{FormatSession(context with { Session = session })}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask PersistLegacyWarFightersRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        bool innerPowerApplied)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["SubProfInfo"] = ReadAttributeString(session.Character.Attributes, "SubProfInfo"),
            [LegacyWarFightersRewardClaimedAttribute] = "true",
            ["WarFightersRewardClaimed"] = "true",
            ["RuntimeWarFightersRewardClaimed"] = "true",
            ["LegacyMsgWarFightersRewardClaimed"] = "true",
            ["MsgWarFightersRewardClaimed"] = "true",
            ["RuntimeMsgWarFightersRewardClaimed"] = "true",
            ["LegacyWarFightersWinnerCleared"] = "true",
            ["WarFightersWinnerCleared"] = "true",
            ["RuntimeWarFightersWinnerCleared"] = "true",
            ["LegacyMsgWarFightersWinnerCleared"] = "true",
            ["MsgWarFightersWinnerCleared"] = "true",
            ["RuntimeMsgWarFightersWinnerCleared"] = "true",
            ["LegacyWarFightersInnerPowerApplied"] = innerPowerApplied ? "true" : "false",
        };

        foreach (var key in GetLegacyWarFightersRoundOwnerAttributeNames())
        {
            values[key] = "0";
        }

        foreach (var key in GetLegacyWarFightersScoresAttributeNames())
        {
            values[key] = "0";
        }

        if (!TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                session.Character.Uid,
                values,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc WarFighters reward persistence " +
                    $"{FormatSession(context)} source={sourceMethod} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNpc WarFighters reward " +
                $"{FormatSession(context)} source={sourceMethod} path={Path.GetFileName(path)} keys={string.Join(',', values.Keys)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWarFightersRewardDialogAndLogAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string text,
        string reason,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [text],
                LegacyMissedWarEventUnavailableOptions,
                LegacyMissedWarEventAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc WarFighters reward " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWarFightersRewardBroadcastAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot winner,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket
            .SystemRedCenter(
                $"{winner.Name} has received {TunedLegacyWarFightersConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs for winning WarFighters.")
            .ToFrameBytes();
        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is null || activeSessions.Count == 0)
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (var active in activeSessions)
        {
            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string[] GetLegacyWarFightersRewardClaimedAttributeNames()
    {
        return
        [
            LegacyWarFightersRewardClaimedAttribute,
            "WarFightersRewardClaimed",
            "RuntimeWarFightersRewardClaimed",
            "LegacyMsgWarFightersRewardClaimed",
            "MsgWarFightersRewardClaimed",
            "RuntimeMsgWarFightersRewardClaimed",
        ];
    }

    private static string[] GetLegacyWarFightersWinnerClearedAttributeNames()
    {
        return
        [
            "LegacyWarFightersWinnerCleared",
            "WarFightersWinnerCleared",
            "RuntimeWarFightersWinnerCleared",
            "LegacyMsgWarFightersWinnerCleared",
            "MsgWarFightersWinnerCleared",
            "RuntimeMsgWarFightersWinnerCleared",
        ];
    }

    private static string[] GetLegacyWarFightersWinnerPlayerUidAttributeNames()
    {
        var names = new List<string>();
        foreach (var prefix in GetLegacyWarFightersAttributePrefixes())
        {
            names.Add(prefix + "RoundOwnerPlayerUID");
            names.Add(prefix + "RoundOwnerPlayerUid");
            names.Add(prefix + "RoundOwnerUID");
            names.Add(prefix + "RoundOwnerUid");
            names.Add(prefix + "WinnerPlayerUID");
            names.Add(prefix + "WinnerPlayerUid");
            names.Add(prefix + "WinnerUID");
            names.Add(prefix + "WinnerUid");
        }

        return names.ToArray();
    }

    private static string[] GetLegacyWarFightersRoundOwnerAttributeNames()
    {
        var names = new List<string>();
        foreach (var prefix in GetLegacyWarFightersAttributePrefixes())
        {
            names.Add(prefix + "RoundOwnerPlayerUID");
            names.Add(prefix + "RoundOwnerPlayerUid");
            names.Add(prefix + "RoundOwnerUID");
            names.Add(prefix + "RoundOwnerUid");
            names.Add(prefix + "RoundOwnerName");
            names.Add(prefix + "RoundOwnerScore");
            names.Add(prefix + "WinnerPlayerUID");
            names.Add(prefix + "WinnerPlayerUid");
            names.Add(prefix + "WinnerUID");
            names.Add(prefix + "WinnerUid");
            names.Add(prefix + "WinnerName");
            names.Add(prefix + "WinnerScore");
            names.Add(prefix + "PoleOwnerName");
            names.Add(prefix + "PoleReset");
        }

        return names.ToArray();
    }

    private static string[] GetLegacyWarFightersScoresAttributeNames()
    {
        return
        [
            LegacyWarFightersScoresAttribute,
            "LegacyWarFightersScores",
            "MsgWarFightersScores",
            "RuntimeWarFightersScores",
            "LegacyMsgWarFightersScores",
            "RuntimeMsgWarFightersScores",
        ];
    }

    private static string[] GetLegacyWarFightersAttributePrefixes()
    {
        return
        [
            "LegacyMsgWarFighters",
            "MsgWarFighters",
            "RuntimeMsgWarFighters",
            "LegacyWarFighters",
            "WarFighters",
            "RuntimeWarFighters",
        ];
    }

    private static string FormatLegacyWarFightersRewardItems()
    {
        return string.Join(
            ',',
            LegacyWarFightersRewardItems.Select(item =>
                item.ItemId.ToString(CultureInfo.InvariantCulture) +
                "x" +
                item.StackSize.ToString(CultureInfo.InvariantCulture)));
    }
}
