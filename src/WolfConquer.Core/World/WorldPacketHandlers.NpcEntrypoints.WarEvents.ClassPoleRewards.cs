using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyClassPoleWarSourceConquerPointsReward = 3_000_000;
    private const long TunedLegacyClassPoleWarConquerPointsReward = 300_000;
    private const ushort LegacyClassPoleWarStudyPointsReward = 10_000;
    private const int LegacyClassPoleWarInnerPowerPotencyReward = 10_000;

    private static readonly (uint ItemId, ushort StackSize)[] LegacyClassPoleWarBaseRewardItems =
    [
        (3_009_003, 1),
        (4_050_001, 100),
        (4_040_001, 100),
    ];

    private static readonly (uint ItemId, ushort StackSize)[] LegacyClassPoleWarThunderRewardItems =
    [
        (3_009_003, 1),
        (4_050_001, 100),
        (4_040_001, 100),
        (3_324_226, 5),
    ];

    private static async ValueTask TryClaimLegacyClassPoleWarRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyClassPoleWarRewardSchedule(optionId, out var reward))
        {
            return;
        }

        if (ReadAttributeBool(session.Character.Attributes, GetLegacyClassPoleWarRewardClaimedAttributeNames(reward.ScheduleName)))
        {
            await SendLegacyClassPoleWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "This ClassPoleWar reward was already claimed.",
                    $"already claimed schedule={reward.ScheduleName}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var processFound = TryReadLegacyClassPoleWarRewardProcess(
            session.Character.Attributes,
            reward.ScheduleName,
            out var process);
        var ownerFound = TryReadLegacyClassPoleWarRewardWinnerPlayerUid(
            session.Character.Attributes,
            reward.ScheduleName,
            out var winnerPlayerUid);
        if (!processFound && !ownerFound)
        {
            await SendLegacyClassPoleWarRewardEmptyStateFallbackAsync(
                    context,
                    sourceMethod,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (processFound && IsLegacyClassPoleWarRewardAliveProcess(process))
        {
            await SendLegacyClassPoleWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Please come back when ClassPoleWar is finished.",
                    $"skipped schedule={reward.ScheduleName} process still Alive process={process}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!ownerFound)
        {
            await SendLegacyClassPoleWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "There is no ClassPoleWar reward available now.",
                    $"skipped missing {reward.ScheduleName}.RoundOwner",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (processFound && !IsLegacyClassPoleWarRewardDeadProcess(process))
        {
            await SendLegacyClassPoleWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Please come back when ClassPoleWar is finished.",
                    $"skipped schedule={reward.ScheduleName} process not Dead process={process}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var playerUid = session.Character.Uid <= 0
            ? 0
            : checked((ulong)session.Character.Uid);
        if (playerUid != winnerPlayerUid)
        {
            await SendLegacyClassPoleWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "You do not have any ClassPoleWar reward to claim.",
                    $"skipped wrong player schedule={reward.ScheduleName} winnerPlayerUid={winnerPlayerUid.ToString(CultureInfo.InvariantCulture)} playerUid={playerUid.ToString(CultureInfo.InvariantCulture)}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var rewardItems = GetLegacyClassPoleWarRewardItems(reward.GrantsThunderBonusItem);
        if (!HasLegacyInventorySpace(session.PlayerItems, rewardItems.Length))
        {
            await SendLegacyClassPoleWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    $"Please make {rewardItems.Length.ToString(CultureInfo.InvariantCulture)} more spaces in your inventory.",
                    $"skipped full inventory sourceRequiredSpace={rewardItems.Length.ToString(CultureInfo.InvariantCulture)}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var grantedItems = CreateLegacyClassPoleWarRewardItems(context, session.PlayerItems, rewardItems);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat(grantedItems).OrderBy(item => item.Uid).ToArray(),
        };
        var updatedCharacter = session.Character with
        {
            ConquerPoints = checked(session.Character.ConquerPoints + TunedLegacyClassPoleWarConquerPointsReward),
        };
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "ConquerPoints",
            CheckedUInt64(updatedCharacter.ConquerPoints, nameof(updatedCharacter.ConquerPoints)));
        updatedCharacter = AddLegacyStudyPoints(updatedCharacter, LegacyClassPoleWarStudyPointsReward);
        updatedCharacter = WithLegacyClassPoleWarRewardClaimCleared(updatedCharacter, reward.ScheduleName);

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

        var innerPowerApplied = await TryApplyLegacyClassPoleWarInnerPowerPotencyAwardAsync(
                updatedContext,
                updatedSession,
                reward.ScheduleName,
                cancellationToken)
            .ConfigureAwait(false);

        await PersistLegacyClassPoleWarRewardAsync(updatedContext, updatedSession, sourceMethod, reward, innerPowerApplied)
            .ConfigureAwait(false);
        foreach (var item in grantedItems)
        {
            await PersistLegacyNpcItemsAsync(updatedContext, item.ItemId, sourceMethod).ConfigureAwait(false);
        }

        await SendLegacyNpcDialogAsync(
                updatedContext,
                sourceMethod,
                [$"You claimed {TunedLegacyClassPoleWarConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs, study points, inner power potency, and ClassPoleWar items."],
                LegacyMissedWarEventUnavailableOptions,
                LegacyClassPoleWarClassAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyClassPoleWarRewardBroadcastAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc ClassPoleWar reward " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                $"schedule={reward.ScheduleName} winnerPlayerUid={winnerPlayerUid.ToString(CultureInfo.InvariantCulture)} " +
                $"sourceConquerPoints={LegacyClassPoleWarSourceConquerPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"tunedConquerPoints={TunedLegacyClassPoleWarConquerPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"items={FormatLegacyClassPoleWarRewardItems(rewardItems)} " +
                $"studyPoints={LegacyClassPoleWarStudyPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"innerPowerPotency={LegacyClassPoleWarInnerPowerPotencyReward.ToString(CultureInfo.InvariantCulture)} " +
                $"innerPowerApplied={innerPowerApplied.ToString(CultureInfo.InvariantCulture)} " +
                "source=references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgNpc\\NpcHandler.cs ClassPoleWar cases 2-13.")
            .ConfigureAwait(false);
    }

    private static bool TryGetLegacyClassPoleWarRewardSchedule(
        byte optionId,
        out LegacyClassPoleWarRewardSchedule reward)
    {
        reward = optionId switch
        {
            2 => new LegacyClassPoleWarRewardSchedule("MsgArcherMaster", false),
            3 => new LegacyClassPoleWarRewardSchedule("MsgWarriorGod", false),
            4 => new LegacyClassPoleWarRewardSchedule("MsgNinjaMaster", false),
            5 => new LegacyClassPoleWarRewardSchedule("MsgMonkMaster", false),
            6 => new LegacyClassPoleWarRewardSchedule("MsgTrojanMaster", false),
            7 => new LegacyClassPoleWarRewardSchedule("MsgTaoistMaster", false),
            8 => new LegacyClassPoleWarRewardSchedule("MsgFireMaster", false),
            9 => new LegacyClassPoleWarRewardSchedule("MsgPirateMaster", false),
            10 => new LegacyClassPoleWarRewardSchedule("MsgKungfuKing", false),
            11 => new LegacyClassPoleWarRewardSchedule("MsgWindWalker", false),
            12 => new LegacyClassPoleWarRewardSchedule("MsgClassPolaWar", false),
            13 => new LegacyClassPoleWarRewardSchedule("MsgThnderStrike", true),
            _ => default,
        };
        return reward.ScheduleName.Length != 0;
    }

    private static (uint ItemId, ushort StackSize)[] GetLegacyClassPoleWarRewardItems(bool grantsThunderBonusItem)
    {
        return grantsThunderBonusItem
            ? LegacyClassPoleWarThunderRewardItems
            : LegacyClassPoleWarBaseRewardItems;
    }

    private static IReadOnlyList<PlayerItemSnapshotItem> CreateLegacyClassPoleWarRewardItems(
        WorldPacketHandlerContext context,
        PlayerItemSnapshot items,
        IReadOnlyList<(uint ItemId, ushort StackSize)> rewardItems)
    {
        var nextUid = GetNextSessionItemUid(items);
        var grants = new PlayerItemSnapshotItem[rewardItems.Count];
        for (var index = 0; index < grants.Length; index++)
        {
            var (itemId, stackSize) = rewardItems[index];
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

    private static bool TryReadLegacyClassPoleWarRewardProcess(
        IReadOnlyDictionary<string, string> attributes,
        string scheduleName,
        out string process)
    {
        process = ReadAttributeString(
                attributes,
                GetLegacyClassPoleWarRewardAttributeNames(scheduleName, "Process", "Proces"))
            .Trim();
        return process.Length != 0;
    }

    private static bool IsLegacyClassPoleWarRewardDeadProcess(string process)
    {
        return process.Equals("Dead", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("ProcesType.Dead", StringComparison.OrdinalIgnoreCase) ||
            process == "0";
    }

    private static bool IsLegacyClassPoleWarRewardAliveProcess(string process)
    {
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) ||
            process == "1";
    }

    private static bool TryReadLegacyClassPoleWarRewardWinnerPlayerUid(
        IReadOnlyDictionary<string, string> attributes,
        string scheduleName,
        out ulong winnerPlayerUid)
    {
        return TryReadUInt64(
                attributes,
                out winnerPlayerUid,
                GetLegacyClassPoleWarRoundOwnerPlayerUidAttributeNames(scheduleName)) &&
            winnerPlayerUid != 0;
    }

    private static CharacterSnapshot WithLegacyClassPoleWarRewardClaimCleared(
        CharacterSnapshot character,
        string scheduleName)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var key in GetLegacyClassPoleWarRewardClaimedAttributeNames(scheduleName))
        {
            attributes[key] = "true";
        }

        foreach (var key in GetLegacyClassPoleWarWinnerClearedAttributeNames(scheduleName))
        {
            attributes[key] = "true";
        }

        foreach (var key in GetLegacyClassPoleWarRoundOwnerAttributeNames(scheduleName))
        {
            attributes.Remove(key);
        }

        foreach (var key in GetLegacyClassPoleWarScoresAttributeNames(scheduleName))
        {
            attributes.Remove(key);
        }

        return character with { Attributes = attributes };
    }

    private static async ValueTask<bool> TryApplyLegacyClassPoleWarInnerPowerPotencyAwardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string scheduleName,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is null)
        {
            await LogLegacyClassPoleWarInnerPowerDeferredAsync(context, session, scheduleName, "InnerPower.txt rows are not loaded")
                .ConfigureAwait(false);
            return false;
        }

        if (session.Character.Uid is < 0 or > int.MaxValue)
        {
            await LogLegacyClassPoleWarInnerPowerDeferredAsync(
                    context,
                    session,
                    scheduleName,
                    $"InnerPower.txt row key {session.Character.Uid} exceeds source row key range")
                .ConfigureAwait(false);
            return false;
        }

        var characterId = checked((int)session.Character.Uid);
        if (!context.WorldData.InnerPowerRanksByCharacterId.TryGetValue(characterId, out var rows) ||
            rows.Count == 0)
        {
            await LogLegacyClassPoleWarInnerPowerDeferredAsync(
                    context,
                    session,
                    scheduleName,
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
                updatedPotency = checked(state.Potency + LegacyClassPoleWarInnerPowerPotencyReward);
                var updatedRow = CreateLegacyInnerPowerRankEntry(row, state with { Potency = updatedPotency });
                applied = TryReplaceLegacyInnerPowerRow(rows, updatedRow, out deferReason);
            }
        }

        if (!applied)
        {
            await LogLegacyClassPoleWarInnerPowerDeferredAsync(context, session, scheduleName, deferReason)
                .ConfigureAwait(false);
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

    private static async ValueTask LogLegacyClassPoleWarInnerPowerDeferredAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string scheduleName,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "WorldServer deferred LegacyNpc ClassPoleWar InnerPower potency reward " +
                $"{FormatSession(context with { Session = session })} schedule={scheduleName}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask PersistLegacyClassPoleWarRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        LegacyClassPoleWarRewardSchedule reward,
        bool innerPowerApplied)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["SubProfInfo"] = ReadAttributeString(session.Character.Attributes, "SubProfInfo"),
            ["LegacyClassPoleWarInnerPowerApplied"] = innerPowerApplied ? "true" : "false",
        };

        foreach (var key in GetLegacyClassPoleWarRewardClaimedAttributeNames(reward.ScheduleName))
        {
            values[key] = "true";
        }

        foreach (var key in GetLegacyClassPoleWarWinnerClearedAttributeNames(reward.ScheduleName))
        {
            values[key] = "true";
        }

        foreach (var key in GetLegacyClassPoleWarRoundOwnerAttributeNames(reward.ScheduleName))
        {
            values[key] = "0";
        }

        foreach (var key in GetLegacyClassPoleWarScoresAttributeNames(reward.ScheduleName))
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
                    "PARITY-GAP LegacyNpc ClassPoleWar reward persistence " +
                    $"{FormatSession(context)} source={sourceMethod} schedule={reward.ScheduleName} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNpc ClassPoleWar reward " +
                $"{FormatSession(context)} source={sourceMethod} schedule={reward.ScheduleName} path={Path.GetFileName(path)} keys={string.Join(',', values.Keys)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyClassPoleWarRewardDialogAndLogAsync(
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
                LegacyClassPoleWarClassAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc ClassPoleWar reward " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}.")
            .ConfigureAwait(false);
    }

    private static ValueTask SendLegacyClassPoleWarRewardEmptyStateFallbackAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return SendLegacyPoleWarRewardUnavailableAsync(
            context,
            sourceMethod,
            LegacyClassPoleWarClassAvatarId,
            optionId,
            "ClassPoleWar reward option did not expose process or round-owner state attributes; expected legacy schedule process to be Dead and RoundOwner UID to match the player before granting 3,000,000 source CP, items, study points, and inner power rewards.",
            cancellationToken);
    }

    private static async ValueTask SendLegacyClassPoleWarRewardBroadcastAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot winner,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket
            .SystemRedCenter(
                $"{winner.Name} has received {TunedLegacyClassPoleWarConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs for winning ClassPoleWar.")
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

    private static string[] GetLegacyClassPoleWarRewardClaimedAttributeNames(string scheduleName)
    {
        return GetLegacyClassPoleWarRewardAttributeNames(scheduleName, "RewardClaimed");
    }

    private static string[] GetLegacyClassPoleWarWinnerClearedAttributeNames(string scheduleName)
    {
        return GetLegacyClassPoleWarRewardAttributeNames(scheduleName, "WinnerCleared");
    }

    private static string[] GetLegacyClassPoleWarRoundOwnerPlayerUidAttributeNames(string scheduleName)
    {
        return GetLegacyClassPoleWarRewardAttributeNames(
            scheduleName,
            "RoundOwnerPlayerUID",
            "RoundOwnerPlayerUid",
            "RoundOwnerPlayerId",
            "RoundOwnerUID",
            "RoundOwnerUid",
            "RoundOwnerId",
            "RoundOwner",
            "WinnerPlayerUID",
            "WinnerPlayerUid",
            "WinnerPlayerId",
            "WinnerUID",
            "WinnerUid",
            "WinnerId",
            "Winner");
    }

    private static string[] GetLegacyClassPoleWarRoundOwnerAttributeNames(string scheduleName)
    {
        return GetLegacyClassPoleWarRewardAttributeNames(
            scheduleName,
            "RoundOwnerPlayerUID",
            "RoundOwnerPlayerUid",
            "RoundOwnerPlayerId",
            "RoundOwnerUID",
            "RoundOwnerUid",
            "RoundOwnerId",
            "RoundOwner",
            "RoundOwnerName",
            "RoundOwnerScore",
            "WinnerPlayerUID",
            "WinnerPlayerUid",
            "WinnerPlayerId",
            "WinnerUID",
            "WinnerUid",
            "WinnerId",
            "Winner",
            "WinnerName",
            "WinnerScore",
            "PoleOwnerName",
            "PoleReset");
    }

    private static string[] GetLegacyClassPoleWarScoresAttributeNames(string scheduleName)
    {
        return GetLegacyClassPoleWarRewardAttributeNames(scheduleName, "Scores", "PoleScores");
    }

    private static string[] GetLegacyClassPoleWarRewardAttributeNames(
        string scheduleName,
        params string[] suffixes)
    {
        var names = new List<string>(GetLegacyClassPoleWarAttributeNames(scheduleName, suffixes));
        foreach (var alias in GetLegacyClassPoleWarRewardExtraAliases(scheduleName))
        {
            foreach (var suffix in suffixes)
            {
                names.Add("Legacy" + alias + suffix);
                names.Add(alias + suffix);
                names.Add("Runtime" + alias + suffix);
            }
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> GetLegacyClassPoleWarRewardExtraAliases(string scheduleName)
    {
        return scheduleName switch
        {
            "MsgArcherMaster" => ["ArcherPole"],
            _ => [],
        };
    }

    private static string FormatLegacyClassPoleWarRewardItems(
        IReadOnlyList<(uint ItemId, ushort StackSize)> rewardItems)
    {
        return string.Join(
            ',',
            rewardItems.Select(item =>
                item.ItemId.ToString(CultureInfo.InvariantCulture) +
                "x" +
                item.StackSize.ToString(CultureInfo.InvariantCulture)));
    }

    private readonly record struct LegacyClassPoleWarRewardSchedule(
        string ScheduleName,
        bool GrantsThunderBonusItem);
}
