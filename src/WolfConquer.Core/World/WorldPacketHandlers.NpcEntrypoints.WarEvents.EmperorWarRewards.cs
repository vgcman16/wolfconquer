using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyEmperorWarSourceConquerPointsReward = 5_000_000;
    private const long TunedLegacyEmperorWarConquerPointsReward = 300_000;
    private const ushort LegacyEmperorWarStudyPointsReward = 10_000;
    private const int LegacyEmperorWarInnerPowerPotencyReward = 10_000;
    private const string LegacyEmperorWarRewardClaimedAttribute = "LegacyEmperorWarRewardClaimed";

    private static readonly (uint ItemId, ushort StackSize)[] LegacyEmperorWarRewardItems =
    [
        (3_009_003, 3),
        (4_050_001, 500),
        (4_040_001, 500),
        (3_324_226, 5),
    ];

    private static async ValueTask TryClaimLegacyEmperorWarRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (IsLegacyEmperorWarProcessAlive(session.Character))
        {
            await SendLegacyEmperorWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Please come back when Emperor War is finished.",
                    "skipped active MsgEmperorWar.Proces",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (ReadAttributeBool(session.Character.Attributes, GetLegacyEmperorWarRewardClaimedAttributeNames()))
        {
            await SendLegacyEmperorWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "This Emperor War reward was already claimed.",
                    "already claimed",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyEmperorWarWinnerPlayerUid(session.Character, out var winnerPlayerUid))
        {
            await SendLegacyEmperorWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "There is no Emperor War reward available now.",
                    "skipped missing MsgEmperorWar.RoundOwner",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var playerUid = session.Character.Uid <= 0
            ? 0
            : checked((ulong)session.Character.Uid);
        if (playerUid != winnerPlayerUid)
        {
            await SendLegacyEmperorWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "You do not have any Emperor War reward to claim.",
                    $"skipped wrong player winnerPlayerUid={winnerPlayerUid.ToString(CultureInfo.InvariantCulture)} playerUid={playerUid.ToString(CultureInfo.InvariantCulture)}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, LegacyEmperorWarRewardItems.Length))
        {
            await SendLegacyEmperorWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Please make 4 more spaces in your inventory.",
                    "skipped full inventory sourceRequiredSpace=4",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var grantedItems = CreateLegacyEmperorWarRewardItems(context, session.PlayerItems);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat(grantedItems).OrderBy(item => item.Uid).ToArray(),
        };
        var updatedCharacter = session.Character with
        {
            ConquerPoints = checked(session.Character.ConquerPoints + TunedLegacyEmperorWarConquerPointsReward),
        };
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "ConquerPoints",
            CheckedUInt64(updatedCharacter.ConquerPoints, nameof(updatedCharacter.ConquerPoints)));
        updatedCharacter = AddLegacyStudyPoints(updatedCharacter, LegacyEmperorWarStudyPointsReward);
        updatedCharacter = WithLegacyEmperorWarRewardClaimCleared(updatedCharacter);

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

        var innerPowerApplied = await TryApplyLegacyEmperorWarInnerPowerPotencyAwardAsync(
                updatedContext,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);

        await PersistLegacyEmperorWarRewardAsync(updatedContext, updatedSession, sourceMethod, innerPowerApplied)
            .ConfigureAwait(false);
        foreach (var item in grantedItems)
        {
            await PersistLegacyNpcItemsAsync(updatedContext, item.ItemId, sourceMethod).ConfigureAwait(false);
        }

        await SendLegacyNpcDialogAsync(
                updatedContext,
                sourceMethod,
                [$"You claimed {TunedLegacyEmperorWarConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs, study points, inner power potency, and Emperor War items."],
                LegacyMissedWarEventUnavailableOptions,
                LegacyMissedWarEventAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyEmperorWarRewardBroadcastAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc EmperorWar reward " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                $"winnerPlayerUid={winnerPlayerUid.ToString(CultureInfo.InvariantCulture)} " +
                $"sourceConquerPoints={LegacyEmperorWarSourceConquerPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"tunedConquerPoints={TunedLegacyEmperorWarConquerPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"items={FormatLegacyEmperorWarRewardItems()} " +
                $"studyPoints={LegacyEmperorWarStudyPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"innerPowerPotency={LegacyEmperorWarInnerPowerPotencyReward.ToString(CultureInfo.InvariantCulture)} " +
                $"innerPowerApplied={innerPowerApplied.ToString(CultureInfo.InvariantCulture)} " +
                "source=references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgNpc\\NpcHandler.cs EmperorWar option 2.")
            .ConfigureAwait(false);
    }

    private static IReadOnlyList<PlayerItemSnapshotItem> CreateLegacyEmperorWarRewardItems(
        WorldPacketHandlerContext context,
        PlayerItemSnapshot items)
    {
        var nextUid = GetNextSessionItemUid(items);
        var grants = new PlayerItemSnapshotItem[LegacyEmperorWarRewardItems.Length];
        for (var index = 0; index < grants.Length; index++)
        {
            var (itemId, stackSize) = LegacyEmperorWarRewardItems[index];
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

    private static bool TryReadLegacyEmperorWarWinnerPlayerUid(
        CharacterSnapshot character,
        out ulong winnerPlayerUid)
    {
        if (TryReadUInt64(
                character.Attributes,
                out winnerPlayerUid,
                GetLegacyEmperorWarWinnerPlayerUidAttributeNames()) &&
            winnerPlayerUid != 0)
        {
            return true;
        }

        return TryReadLegacyEmperorWarWinnerPlayerUidFromScores(character, out winnerPlayerUid);
    }

    private static bool TryReadLegacyEmperorWarWinnerPlayerUidFromScores(
        CharacterSnapshot character,
        out ulong winnerPlayerUid)
    {
        winnerPlayerUid = 0;
        var raw = ReadAttributeString(
            character.Attributes,
            LegacyEmperorWarScoresAttribute,
            "LegacyEmperorWarScores",
            "MsgEmperorWarScores",
            "LegacyMsgEmperorWarScores");
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

    private static CharacterSnapshot WithLegacyEmperorWarRewardClaimCleared(CharacterSnapshot character)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var key in GetLegacyEmperorWarRewardClaimedAttributeNames())
        {
            attributes[key] = "true";
        }

        foreach (var key in GetLegacyEmperorWarWinnerClearedAttributeNames())
        {
            attributes[key] = "true";
        }

        attributes.Remove(LegacyEmperorWarScoresAttribute);
        attributes.Remove("LegacyEmperorWarScores");
        attributes.Remove("MsgEmperorWarScores");
        attributes.Remove("LegacyMsgEmperorWarScores");
        foreach (var key in GetLegacyEmperorWarRoundOwnerAttributeNames())
        {
            attributes.Remove(key);
        }

        return character with { Attributes = attributes };
    }

    private static async ValueTask<bool> TryApplyLegacyEmperorWarInnerPowerPotencyAwardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is null)
        {
            await LogLegacyEmperorWarInnerPowerDeferredAsync(context, session, "InnerPower.txt rows are not loaded")
                .ConfigureAwait(false);
            return false;
        }

        if (session.Character.Uid is < 0 or > int.MaxValue)
        {
            await LogLegacyEmperorWarInnerPowerDeferredAsync(
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
            await LogLegacyEmperorWarInnerPowerDeferredAsync(
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
                updatedPotency = checked(state.Potency + LegacyEmperorWarInnerPowerPotencyReward);
                var updatedRow = CreateLegacyInnerPowerRankEntry(row, state with { Potency = updatedPotency });
                applied = TryReplaceLegacyInnerPowerRow(rows, updatedRow, out deferReason);
            }
        }

        if (!applied)
        {
            await LogLegacyEmperorWarInnerPowerDeferredAsync(context, session, deferReason).ConfigureAwait(false);
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

    private static async ValueTask LogLegacyEmperorWarInnerPowerDeferredAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "WorldServer deferred LegacyNpc EmperorWar InnerPower potency reward " +
                $"{FormatSession(context with { Session = session })}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask PersistLegacyEmperorWarRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        bool innerPowerApplied)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["SubProfInfo"] = ReadAttributeString(session.Character.Attributes, "SubProfInfo"),
            [LegacyEmperorWarRewardClaimedAttribute] = "true",
            ["EmperorWarRewardClaimed"] = "true",
            ["RuntimeEmperorWarRewardClaimed"] = "true",
            ["LegacyMsgEmperorWarRewardClaimed"] = "true",
            ["MsgEmperorWarRewardClaimed"] = "true",
            ["RuntimeMsgEmperorWarRewardClaimed"] = "true",
            ["LegacyEmperorWarWinnerCleared"] = "true",
            ["EmperorWarWinnerCleared"] = "true",
            ["RuntimeEmperorWarWinnerCleared"] = "true",
            ["LegacyMsgEmperorWarWinnerCleared"] = "true",
            ["MsgEmperorWarWinnerCleared"] = "true",
            ["RuntimeMsgEmperorWarWinnerCleared"] = "true",
            ["LegacyEmperorWarInnerPowerApplied"] = innerPowerApplied ? "true" : "false",
        };

        foreach (var key in GetLegacyEmperorWarRoundOwnerAttributeNames())
        {
            values[key] = "0";
        }

        values[LegacyEmperorWarScoresAttribute] = "0";
        values["LegacyEmperorWarScores"] = "0";
        values["MsgEmperorWarScores"] = "0";
        values["LegacyMsgEmperorWarScores"] = "0";

        if (!TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                session.Character.Uid,
                values,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc EmperorWar reward persistence " +
                    $"{FormatSession(context)} source={sourceMethod} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNpc EmperorWar reward " +
                $"{FormatSession(context)} source={sourceMethod} path={Path.GetFileName(path)} keys={string.Join(',', values.Keys)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyEmperorWarRewardDialogAndLogAsync(
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
                "WorldServer skipped LegacyNpc EmperorWar reward " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyEmperorWarRewardBroadcastAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot winner,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket
            .SystemRedCenter(
                $"{winner.Name} has received {TunedLegacyEmperorWarConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs for winning EmperorWar.")
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

    private static string[] GetLegacyEmperorWarRewardClaimedAttributeNames()
    {
        return
        [
            LegacyEmperorWarRewardClaimedAttribute,
            "EmperorWarRewardClaimed",
            "RuntimeEmperorWarRewardClaimed",
            "LegacyMsgEmperorWarRewardClaimed",
            "MsgEmperorWarRewardClaimed",
            "RuntimeMsgEmperorWarRewardClaimed",
        ];
    }

    private static string[] GetLegacyEmperorWarWinnerClearedAttributeNames()
    {
        return
        [
            "LegacyEmperorWarWinnerCleared",
            "EmperorWarWinnerCleared",
            "RuntimeEmperorWarWinnerCleared",
            "LegacyMsgEmperorWarWinnerCleared",
            "MsgEmperorWarWinnerCleared",
            "RuntimeMsgEmperorWarWinnerCleared",
        ];
    }

    private static string[] GetLegacyEmperorWarWinnerPlayerUidAttributeNames()
    {
        var names = new List<string>();
        foreach (var prefix in GetLegacyEmperorWarAttributePrefixes())
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

    private static string[] GetLegacyEmperorWarRoundOwnerAttributeNames()
    {
        var names = new List<string>();
        foreach (var prefix in GetLegacyEmperorWarAttributePrefixes())
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

    private static string[] GetLegacyEmperorWarAttributePrefixes()
    {
        return
        [
            "LegacyMsgEmperorWar",
            "MsgEmperorWar",
            "RuntimeMsgEmperorWar",
            "LegacyEmperorWar",
            "EmperorWar",
            "RuntimeEmperorWar",
        ];
    }

    private static string FormatLegacyEmperorWarRewardItems()
    {
        return string.Join(
            ',',
            LegacyEmperorWarRewardItems.Select(item =>
                item.ItemId.ToString(CultureInfo.InvariantCulture) +
                "x" +
                item.StackSize.ToString(CultureInfo.InvariantCulture)));
    }
}
