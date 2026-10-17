using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyTopPoleSourceConquerPointsReward = 10_000_000;
    private const long TunedLegacyTopPoleConquerPointsReward = 300_000;
    private const ushort LegacyTopPoleStudyPointsReward = 10_000;
    private const int LegacyTopPoleInnerPowerPotencyReward = 10_000;
    private const string LegacyTopPoleRewardClaimedAttribute = "LegacyTopPoleRewardClaimed";

    private static readonly (uint ItemId, ushort StackSize)[] LegacyTopPoleRewardItems =
    [
        (3_009_003, 3),
        (4_050_001, 500),
        (4_040_001, 500),
    ];

    private static async ValueTask TryClaimLegacyTopPoleRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (IsLegacyTopPoleProcessAlive(session.Character))
        {
            await SendLegacyTopPoleRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Please come back when TopPole is finished.",
                    "skipped active MsgTopPole process",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (ReadAttributeBool(session.Character.Attributes, GetLegacyTopPoleRewardClaimedAttributeNames()))
        {
            await SendLegacyTopPoleRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "This TopPole reward was already claimed.",
                    "already claimed",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyTopPoleWinnerPlayerUid(session.Character, out var winnerPlayerUid))
        {
            await SendLegacyTopPoleRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "There is no reward available now.",
                    "skipped missing MsgTopPole RoundOwner",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var playerUid = session.Character.Uid <= 0
            ? 0
            : checked((ulong)session.Character.Uid);
        if (playerUid != winnerPlayerUid)
        {
            await SendLegacyTopPoleRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "You do not have any reward to claim.",
                    $"skipped wrong player winnerPlayerUid={winnerPlayerUid.ToString(CultureInfo.InvariantCulture)} playerUid={playerUid.ToString(CultureInfo.InvariantCulture)}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, LegacyTopPoleRewardItems.Length))
        {
            await SendLegacyTopPoleRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Please make 3 more spaces in your inventory.",
                    "skipped full inventory sourceRequiredSpace=3",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var grantedItems = CreateLegacyTopPoleRewardItems(context, session.PlayerItems);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat(grantedItems).OrderBy(item => item.Uid).ToArray(),
        };
        var updatedConquerPoints = checked(session.Character.ConquerPoints + TunedLegacyTopPoleConquerPointsReward);
        var updatedCharacter = session.Character with
        {
            ConquerPoints = updatedConquerPoints,
        };
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "ConquerPoints",
            CheckedUInt64(updatedConquerPoints, nameof(updatedConquerPoints)));
        updatedCharacter = AddLegacyStudyPoints(updatedCharacter, LegacyTopPoleStudyPointsReward);
        updatedCharacter = WithLegacyTopPoleRewardClaimCleared(updatedCharacter);

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

        var innerPowerApplied = await TryApplyLegacyTopPoleInnerPowerPotencyAwardAsync(
                updatedContext,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);

        await PersistLegacyTopPoleRewardAsync(updatedContext, updatedSession, sourceMethod, innerPowerApplied)
            .ConfigureAwait(false);
        foreach (var item in grantedItems)
        {
            await PersistLegacyNpcItemsAsync(updatedContext, item.ItemId, sourceMethod).ConfigureAwait(false);
        }

        await SendLegacyNpcDialogAsync(
                updatedContext,
                sourceMethod,
                [$"You claimed {TunedLegacyTopPoleConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs, study points, inner power potency, and TopPole items."],
                LegacyPoleWarEventOkayOptions,
                LegacyPoleWarEventAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyTopPoleRewardBroadcastAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc TopPole reward " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                $"winnerPlayerUid={winnerPlayerUid.ToString(CultureInfo.InvariantCulture)} " +
                $"sourceConquerPoints={LegacyTopPoleSourceConquerPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"tunedConquerPoints={TunedLegacyTopPoleConquerPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"items={FormatLegacyTopPoleRewardItems()} " +
                $"studyPoints={LegacyTopPoleStudyPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"innerPowerPotency={LegacyTopPoleInnerPowerPotencyReward.ToString(CultureInfo.InvariantCulture)} " +
                $"innerPowerApplied={innerPowerApplied.ToString(CultureInfo.InvariantCulture)} " +
                "source=references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgNpc\\NpcHandler.cs TopPole option 2.")
            .ConfigureAwait(false);
    }

    private static IReadOnlyList<PlayerItemSnapshotItem> CreateLegacyTopPoleRewardItems(
        WorldPacketHandlerContext context,
        PlayerItemSnapshot items)
    {
        var nextUid = GetNextSessionItemUid(items);
        var grants = new PlayerItemSnapshotItem[LegacyTopPoleRewardItems.Length];
        for (var index = 0; index < grants.Length; index++)
        {
            var (itemId, stackSize) = LegacyTopPoleRewardItems[index];
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

    private static bool TryReadLegacyTopPoleWinnerPlayerUid(
        CharacterSnapshot character,
        out ulong winnerPlayerUid)
    {
        if (TryReadUInt64(
                character.Attributes,
                out winnerPlayerUid,
                GetLegacyTopPoleWinnerPlayerUidAttributeNames()) &&
            winnerPlayerUid != 0)
        {
            return true;
        }

        return TryReadLegacyTopPoleWinnerPlayerUidFromScores(character, out winnerPlayerUid);
    }

    private static bool TryReadLegacyTopPoleWinnerPlayerUidFromScores(
        CharacterSnapshot character,
        out ulong winnerPlayerUid)
    {
        winnerPlayerUid = 0;
        var raw = ReadAttributeString(
            character.Attributes,
            LegacyTopPoleScoresAttribute,
            "LegacyTopPoleScores",
            "MsgTopPoleScores",
            "LegacyMsgTopPoleScores");
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

    private static CharacterSnapshot WithLegacyTopPoleRewardClaimCleared(CharacterSnapshot character)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var key in GetLegacyTopPoleRewardClaimedAttributeNames())
        {
            attributes[key] = "true";
        }

        foreach (var key in GetLegacyTopPoleWinnerClearedAttributeNames())
        {
            attributes[key] = "true";
        }

        attributes.Remove(LegacyTopPoleScoresAttribute);
        attributes.Remove("LegacyTopPoleScores");
        attributes.Remove("MsgTopPoleScores");
        attributes.Remove("LegacyMsgTopPoleScores");
        foreach (var key in GetLegacyTopPoleRoundOwnerAttributeNames())
        {
            attributes.Remove(key);
        }

        return character with { Attributes = attributes };
    }

    private static async ValueTask<bool> TryApplyLegacyTopPoleInnerPowerPotencyAwardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is null)
        {
            await LogLegacyTopPoleInnerPowerDeferredAsync(context, session, "InnerPower.txt rows are not loaded")
                .ConfigureAwait(false);
            return false;
        }

        if (session.Character.Uid is < 0 or > int.MaxValue)
        {
            await LogLegacyTopPoleInnerPowerDeferredAsync(
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
            await LogLegacyTopPoleInnerPowerDeferredAsync(
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
                updatedPotency = checked(state.Potency + LegacyTopPoleInnerPowerPotencyReward);
                var updatedRow = CreateLegacyInnerPowerRankEntry(row, state with { Potency = updatedPotency });
                applied = TryReplaceLegacyInnerPowerRow(rows, updatedRow, out deferReason);
            }
        }

        if (!applied)
        {
            await LogLegacyTopPoleInnerPowerDeferredAsync(context, session, deferReason).ConfigureAwait(false);
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

    private static async ValueTask LogLegacyTopPoleInnerPowerDeferredAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "WorldServer deferred LegacyNpc TopPole InnerPower potency reward " +
                $"{FormatSession(context with { Session = session })}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask PersistLegacyTopPoleRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        bool innerPowerApplied)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["SubProfInfo"] = ReadAttributeString(session.Character.Attributes, "SubProfInfo"),
            ["LegacyTopPoleInnerPowerApplied"] = innerPowerApplied ? "true" : "false",
        };

        foreach (var key in GetLegacyTopPoleRewardClaimedAttributeNames())
        {
            values[key] = "true";
        }

        foreach (var key in GetLegacyTopPoleWinnerClearedAttributeNames())
        {
            values[key] = "true";
        }

        foreach (var key in GetLegacyTopPoleRoundOwnerAttributeNames())
        {
            values[key] = "0";
        }

        values[LegacyTopPoleScoresAttribute] = "0";
        values["LegacyTopPoleScores"] = "0";
        values["MsgTopPoleScores"] = "0";
        values["LegacyMsgTopPoleScores"] = "0";

        if (!TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                session.Character.Uid,
                values,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc TopPole reward persistence " +
                    $"{FormatSession(context)} source={sourceMethod} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNpc TopPole reward " +
                $"{FormatSession(context)} source={sourceMethod} path={Path.GetFileName(path)} keys={string.Join(',', values.Keys)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyTopPoleRewardDialogAndLogAsync(
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
                LegacyPoleWarEventOkayOptions,
                LegacyPoleWarEventAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc TopPole reward " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyTopPoleRewardBroadcastAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot winner,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket
            .SystemRedCenter(
                $"{winner.Name} has received {TunedLegacyTopPoleConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs for winning TopPole.")
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

    private static string FormatLegacyTopPoleRewardItems()
    {
        return string.Join(
            ',',
            LegacyTopPoleRewardItems.Select(item =>
                $"{item.ItemId.ToString(CultureInfo.InvariantCulture)}x{item.StackSize.ToString(CultureInfo.InvariantCulture)}"));
    }

    private static string[] GetLegacyTopPoleRewardClaimedAttributeNames()
    {
        return
        [
            LegacyTopPoleRewardClaimedAttribute,
            "TopPoleRewardClaimed",
            "RuntimeTopPoleRewardClaimed",
            "LegacyMsgTopPoleRewardClaimed",
            "MsgTopPoleRewardClaimed",
            "RuntimeMsgTopPoleRewardClaimed",
        ];
    }

    private static string[] GetLegacyTopPoleWinnerClearedAttributeNames()
    {
        return
        [
            "LegacyTopPoleWinnerCleared",
            "TopPoleWinnerCleared",
            "RuntimeTopPoleWinnerCleared",
            "LegacyMsgTopPoleWinnerCleared",
            "MsgTopPoleWinnerCleared",
            "RuntimeMsgTopPoleWinnerCleared",
        ];
    }

    private static string[] GetLegacyTopPoleWinnerPlayerUidAttributeNames()
    {
        var names = new List<string>();
        foreach (var prefix in GetLegacyTopPoleAttributePrefixes())
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

    private static string[] GetLegacyTopPoleRoundOwnerAttributeNames()
    {
        var names = new List<string>();
        foreach (var prefix in GetLegacyTopPoleAttributePrefixes())
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

    private static string[] GetLegacyTopPoleAttributePrefixes()
    {
        return
        [
            "LegacyMsgTopPole",
            "MsgTopPole",
            "RuntimeMsgTopPole",
            "LegacyTopPole",
            "TopPole",
            "RuntimeTopPole",
        ];
    }
}
