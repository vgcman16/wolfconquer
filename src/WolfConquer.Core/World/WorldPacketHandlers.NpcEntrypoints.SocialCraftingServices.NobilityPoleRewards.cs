using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long TunedLegacyNobilityPoleConquerPointsReward = 300_000;
    private const uint LegacyNobilityPoleClassExperienceReward = 500;
    private const ushort LegacyNobilityPoleRewardAvatarId = 63;

    private static readonly LegacyNobilityPoleRewardSchedule[] LegacyNobilityPoleRewardSchedules =
    [
        new(
            "MsgNobilityPole",
            "King",
            5_000_000,
            [(3_324_226, 2), (3_001_044, 2), (3_009_002, 2)],
            [(3_001_044, 1)],
            6),
        new(
            "MsgNobilityPole1",
            "Prince",
            4_000_000,
            [(3_324_226, 2), (3_001_044, 2), (3_009_002, 2)],
            [(3_001_044, 2)],
            4),
        new(
            "MsgNobilityPole2",
            "Duke",
            3_000_000,
            [(3_324_226, 2), (3_001_044, 2), (3_009_002, 2)],
            [(3_001_044, 2)],
            4),
        new(
            "MsgNobilityPole3",
            "Earl",
            2_000_000,
            [(3_001_044, 2), (3_009_002, 2)],
            [(3_001_044, 1)],
            3),
    ];

    private static async ValueTask TryClaimLegacyNobilityPoleRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var playerUid = session.Character.Uid <= 0
            ? 0
            : checked((ulong)session.Character.Uid);
        var eligibleRewards = new List<LegacyNobilityPoleRewardSchedule>();
        var sawActiveProcess = false;
        var sawAlreadyClaimed = false;
        var sawWrongOwner = false;
        var sawMissingOwner = false;
        foreach (var reward in LegacyNobilityPoleRewardSchedules)
        {
            if (ReadAttributeBool(session.Character.Attributes, GetLegacyNobilityPoleRewardClaimedAttributeNames(reward)))
            {
                sawAlreadyClaimed = true;
                continue;
            }

            var processFound = TryReadLegacyNobilityPoleRewardProcess(
                session.Character.Attributes,
                reward.ScheduleName,
                out var process);
            if (processFound && IsLegacyNobilityPoleRewardAliveProcess(process))
            {
                sawActiveProcess = true;
                continue;
            }

            if (!TryReadLegacyNobilityPoleRewardWinnerPlayerUid(session.Character, reward, out var winnerPlayerUid))
            {
                sawMissingOwner = true;
                continue;
            }

            if (processFound && !IsLegacyNobilityPoleRewardDeadProcess(process))
            {
                sawActiveProcess = true;
                continue;
            }

            if (winnerPlayerUid != playerUid)
            {
                sawWrongOwner = true;
                continue;
            }

            eligibleRewards.Add(reward);
        }

        if (eligibleRewards.Count == 0)
        {
            await SendLegacyNobilityPoleRewardNoClaimDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    sawActiveProcess,
                    sawAlreadyClaimed,
                    sawWrongOwner,
                    sawMissingOwner,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!TryPlanLegacyNobilityPoleRewardItems(
                session.PlayerItems.InventoryItems.Count,
                eligibleRewards,
                out var rewardPlans,
                out var missingSpaces))
        {
            await SendLegacyNobilityPoleRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    $"Please make {missingSpaces.ToString(CultureInfo.InvariantCulture)} more space in your inventory.",
                    $"skipped full inventory eligibleSchedules={FormatLegacyNobilityPoleRewardSchedules(eligibleRewards)} missingSpaces={missingSpaces.ToString(CultureInfo.InvariantCulture)}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var rewardItems = rewardPlans.SelectMany(plan => plan.Items).ToArray();
        var grantedItems = CreateLegacyNobilityPoleRewardItems(context, session.PlayerItems, rewardItems);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat(grantedItems).OrderBy(item => item.Uid).ToArray(),
        };
        var currentClassExperience = ReadAttributeUInt64(session.Character.Attributes, "ClassExperience");
        var totalClassExperience = checked((ulong)eligibleRewards.Count * LegacyNobilityPoleClassExperienceReward);
        var updatedClassExperience = checked(currentClassExperience + totalClassExperience);
        var totalTunedConquerPoints = checked(eligibleRewards.Count * TunedLegacyNobilityPoleConquerPointsReward);
        var updatedCharacter = session.Character with
        {
            ConquerPoints = checked(session.Character.ConquerPoints + totalTunedConquerPoints),
        };
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "ConquerPoints",
            CheckedUInt64(updatedCharacter.ConquerPoints, nameof(updatedCharacter.ConquerPoints)));
        updatedCharacter = WithCharacterAttribute(updatedCharacter, "ClassExperience", updatedClassExperience);
        updatedCharacter = WithLegacyNobilityPoleRewardClaimCleared(updatedCharacter, eligibleRewards);

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
        await SendScalarUpdateAsync(
                updatedContext,
                updatedCharacter,
                LegacyMsgUpdateDataType.ClassExperience,
                updatedClassExperience,
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

        await PersistLegacyNobilityPoleRewardAsync(updatedContext, updatedSession, sourceMethod, eligibleRewards, rewardPlans)
            .ConfigureAwait(false);
        foreach (var item in grantedItems)
        {
            await PersistLegacyNpcItemsAsync(updatedContext, item.ItemId, sourceMethod).ConfigureAwait(false);
        }

        await SendLegacyNpcDialogAsync(
                updatedContext,
                sourceMethod,
                [$"You claimed {totalTunedConquerPoints.ToString(CultureInfo.InvariantCulture)} CPs, {totalClassExperience.ToString(CultureInfo.InvariantCulture)} class experience, and Nobility Pole War items."],
                LegacySocialCraftingServicesCloseOptions,
                LegacyNobilityPoleRewardAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNobilityPoleRewardBroadcastsAsync(updatedContext, updatedCharacter, eligibleRewards, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc NobilityPoleWar reward " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                $"schedules={FormatLegacyNobilityPoleRewardSchedules(eligibleRewards)} " +
                $"sourceConquerPoints={FormatLegacyNobilityPoleRewardSourceConquerPoints(eligibleRewards)} " +
                $"tunedConquerPoints={totalTunedConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"classExperience={totalClassExperience.ToString(CultureInfo.InvariantCulture)} " +
                $"items={FormatLegacyNobilityPoleRewardPlans(rewardPlans)} " +
                "source=references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgNpc\\NpcHandler.cs NobilityWar option 2.")
            .ConfigureAwait(false);
    }

    private static bool TryPlanLegacyNobilityPoleRewardItems(
        int currentInventoryRows,
        IReadOnlyList<LegacyNobilityPoleRewardSchedule> eligibleRewards,
        out IReadOnlyList<LegacyNobilityPoleRewardPlan> rewardPlans,
        out int missingSpaces)
    {
        var plans = new List<LegacyNobilityPoleRewardPlan>(eligibleRewards.Count);
        var plannedRows = currentInventoryRows;
        foreach (var reward in eligibleRewards)
        {
            if (plannedRows + reward.FullItems.Count <= LegacyInventorySlots)
            {
                plans.Add(new LegacyNobilityPoleRewardPlan(reward, reward.FullItems, UsesFallback: false));
                plannedRows += reward.FullItems.Count;
                continue;
            }

            if (plannedRows + reward.FallbackItems.Count <= LegacyInventorySlots)
            {
                plans.Add(new LegacyNobilityPoleRewardPlan(reward, reward.FallbackItems, UsesFallback: true));
                plannedRows += reward.FallbackItems.Count;
                continue;
            }

            rewardPlans = [];
            missingSpaces = Math.Max(1, checked(plannedRows + reward.FallbackItems.Count - LegacyInventorySlots));
            return false;
        }

        rewardPlans = plans;
        missingSpaces = 0;
        return true;
    }

    private static IReadOnlyList<PlayerItemSnapshotItem> CreateLegacyNobilityPoleRewardItems(
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

    private static bool TryReadLegacyNobilityPoleRewardProcess(
        IReadOnlyDictionary<string, string> attributes,
        string scheduleName,
        out string process)
    {
        process = ReadAttributeString(
                attributes,
                "Legacy" + scheduleName + "Process",
                scheduleName + "Process",
                "Runtime" + scheduleName + "Process",
                "Legacy" + scheduleName + "Proces",
                scheduleName + "Proces",
                "Runtime" + scheduleName + "Proces",
                "LegacyNobilityPoleProcess",
                "NobilityPoleProcess",
                "RuntimeNobilityPoleProcess",
                "LegacyNobilityPoleProces",
                "NobilityPoleProces",
                "RuntimeNobilityPoleProces")
            .Trim();
        return process.Length != 0;
    }

    private static bool IsLegacyNobilityPoleRewardDeadProcess(string process)
    {
        return process.Equals("Dead", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("ProcesType.Dead", StringComparison.OrdinalIgnoreCase) ||
            process == "0";
    }

    private static bool IsLegacyNobilityPoleRewardAliveProcess(string process)
    {
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) ||
            process == "1";
    }

    private static bool TryReadLegacyNobilityPoleRewardWinnerPlayerUid(
        CharacterSnapshot character,
        LegacyNobilityPoleRewardSchedule reward,
        out ulong winnerPlayerUid)
    {
        if (TryReadUInt64(
                character.Attributes,
                out winnerPlayerUid,
                GetLegacyNobilityPoleRoundOwnerPlayerUidAttributeNames(reward)) &&
            winnerPlayerUid != 0)
        {
            return true;
        }

        if (!IsLegacyNobilityPoleLastFinishedSchedule(character, reward))
        {
            return false;
        }

        return TryReadUInt64(
                character.Attributes,
                out winnerPlayerUid,
                "LegacyNobilityPoleRoundOwnerPlayerUID",
                "LegacyNobilityPoleRoundOwnerPlayerUid",
                "NobilityPoleRoundOwnerPlayerUID",
                "NobilityPoleRoundOwnerPlayerUid",
                "RuntimeNobilityPoleRoundOwnerPlayerUID",
                "RuntimeNobilityPoleRoundOwnerPlayerUid") &&
            winnerPlayerUid != 0;
    }

    private static bool IsLegacyNobilityPoleLastFinishedSchedule(
        CharacterSnapshot character,
        LegacyNobilityPoleRewardSchedule reward)
    {
        var schedule = ReadAttributeString(
                character.Attributes,
                "LegacyNobilityPoleLastFinishedSchedule",
                "NobilityPoleLastFinishedSchedule",
                "RuntimeNobilityPoleLastFinishedSchedule")
            .Trim();
        if (schedule.Length != 0)
        {
            return schedule.Equals(reward.ScheduleName, StringComparison.OrdinalIgnoreCase);
        }

        var rank = ReadAttributeString(
                character.Attributes,
                "LegacyNobilityPoleLastFinishedRank",
                "NobilityPoleLastFinishedRank",
                "RuntimeNobilityPoleLastFinishedRank")
            .Trim();
        return rank.Equals(reward.DisplayRank, StringComparison.OrdinalIgnoreCase);
    }

    private static CharacterSnapshot WithLegacyNobilityPoleRewardClaimCleared(
        CharacterSnapshot character,
        IReadOnlyList<LegacyNobilityPoleRewardSchedule> rewards)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var reward in rewards)
        {
            foreach (var key in GetLegacyNobilityPoleRewardClaimedAttributeNames(reward))
            {
                attributes[key] = "true";
            }

            foreach (var key in GetLegacyNobilityPoleWinnerClearedAttributeNames(reward))
            {
                attributes[key] = "true";
            }

            foreach (var key in GetLegacyNobilityPoleRoundOwnerAttributeNames(reward))
            {
                attributes.Remove(key);
            }
        }

        if (rewards.Any(reward => IsLegacyNobilityPoleLastFinishedSchedule(character, reward)))
        {
            foreach (var key in GetLegacyNobilityPoleGenericRoundOwnerAttributeNames())
            {
                attributes.Remove(key);
            }
        }

        return character with { Attributes = attributes };
    }

    private static async ValueTask PersistLegacyNobilityPoleRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        IReadOnlyList<LegacyNobilityPoleRewardSchedule> rewards,
        IReadOnlyList<LegacyNobilityPoleRewardPlan> rewardPlans)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["ClassExperience"] = ReadAttributeString(session.Character.Attributes, "ClassExperience"),
            ["LegacyNobilityPoleRewardLastClaimedSchedules"] = FormatLegacyNobilityPoleRewardSchedules(rewards),
            ["NobilityPoleRewardLastClaimedSchedules"] = FormatLegacyNobilityPoleRewardSchedules(rewards),
            ["LegacyNobilityPoleRewardLastItems"] = FormatLegacyNobilityPoleRewardPlans(rewardPlans),
            ["NobilityPoleRewardLastItems"] = FormatLegacyNobilityPoleRewardPlans(rewardPlans),
        };
        foreach (var reward in rewards)
        {
            foreach (var key in GetLegacyNobilityPoleRewardClaimedAttributeNames(reward))
            {
                values[key] = "true";
            }

            foreach (var key in GetLegacyNobilityPoleWinnerClearedAttributeNames(reward))
            {
                values[key] = "true";
            }

            foreach (var key in GetLegacyNobilityPoleRoundOwnerAttributeNames(reward))
            {
                values[key] = "0";
            }
        }

        if (rewards.Any(reward => IsLegacyNobilityPoleLastFinishedSchedule(session.Character, reward)))
        {
            foreach (var key in GetLegacyNobilityPoleGenericRoundOwnerAttributeNames())
            {
                values[key] = "0";
            }
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
                    "PARITY-GAP LegacyNpc NobilityPoleWar reward persistence " +
                    $"{FormatSession(context)} source={sourceMethod} schedules={FormatLegacyNobilityPoleRewardSchedules(rewards)} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNpc NobilityPoleWar reward " +
                $"{FormatSession(context)} source={sourceMethod} schedules={FormatLegacyNobilityPoleRewardSchedules(rewards)} path={Path.GetFileName(path)} keys={string.Join(',', values.Keys)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyNobilityPoleRewardNoClaimDialogAndLogAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        bool sawActiveProcess,
        bool sawAlreadyClaimed,
        bool sawWrongOwner,
        bool sawMissingOwner,
        CancellationToken cancellationToken)
    {
        if (sawActiveProcess)
        {
            await SendLegacyNobilityPoleRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Please come back when Nobility Pole War is finished.",
                    "skipped active MsgNobilityPole process",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (sawAlreadyClaimed && !sawWrongOwner && !sawMissingOwner)
        {
            await SendLegacyNobilityPoleRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "This Nobility Pole War reward was already claimed.",
                    "already claimed",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (sawWrongOwner)
        {
            await SendLegacyNobilityPoleRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "You do not have any reward to claim.",
                    "skipped wrong player",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await SendLegacyNobilityPoleRewardDialogAndLogAsync(
                context,
                sourceMethod,
                optionId,
                "There is no Nobility Pole War reward available now.",
                "skipped missing MsgNobilityPole RoundOwner",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyNobilityPoleRewardDialogAndLogAsync(
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
                LegacySocialCraftingServicesCloseOptions,
                LegacyNobilityPoleRewardAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc NobilityPoleWar reward " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyNobilityPoleRewardBroadcastsAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot winner,
        IReadOnlyList<LegacyNobilityPoleRewardSchedule> rewards,
        CancellationToken cancellationToken)
    {
        var activeSessions = context.Sessions?.Snapshot();
        foreach (var reward in rewards)
        {
            var frame = LegacyMsgMessagePacket
                .SystemRedCenter(
                    $"{winner.Name} has received {TunedLegacyNobilityPoleConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs for winning NobilityPoleWar[{reward.DisplayRank}].")
                .ToFrameBytes();
            if (activeSessions is null || activeSessions.Count == 0)
            {
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                continue;
            }

            foreach (var active in activeSessions)
            {
                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static string[] GetLegacyNobilityPoleRewardClaimedAttributeNames(
        LegacyNobilityPoleRewardSchedule reward)
    {
        return
        [
            "Legacy" + reward.ScheduleName + "RewardClaimed",
            reward.ScheduleName + "RewardClaimed",
            "Runtime" + reward.ScheduleName + "RewardClaimed",
            "LegacyNobilityPole" + reward.DisplayRank + "RewardClaimed",
            "NobilityPole" + reward.DisplayRank + "RewardClaimed",
            "RuntimeNobilityPole" + reward.DisplayRank + "RewardClaimed",
        ];
    }

    private static string[] GetLegacyNobilityPoleWinnerClearedAttributeNames(
        LegacyNobilityPoleRewardSchedule reward)
    {
        return
        [
            "Legacy" + reward.ScheduleName + "WinnerCleared",
            reward.ScheduleName + "WinnerCleared",
            "Runtime" + reward.ScheduleName + "WinnerCleared",
            "LegacyNobilityPole" + reward.DisplayRank + "WinnerCleared",
            "NobilityPole" + reward.DisplayRank + "WinnerCleared",
            "RuntimeNobilityPole" + reward.DisplayRank + "WinnerCleared",
        ];
    }

    private static string[] GetLegacyNobilityPoleRoundOwnerPlayerUidAttributeNames(
        LegacyNobilityPoleRewardSchedule reward)
    {
        return
        [
            "Legacy" + reward.ScheduleName + "RoundOwnerPlayerUID",
            "Legacy" + reward.ScheduleName + "RoundOwnerPlayerUid",
            "Legacy" + reward.ScheduleName + "RoundOwnerUID",
            "Legacy" + reward.ScheduleName + "RoundOwnerUid",
            reward.ScheduleName + "RoundOwnerPlayerUID",
            reward.ScheduleName + "RoundOwnerPlayerUid",
            reward.ScheduleName + "RoundOwnerUID",
            reward.ScheduleName + "RoundOwnerUid",
            "Runtime" + reward.ScheduleName + "RoundOwnerPlayerUID",
            "Runtime" + reward.ScheduleName + "RoundOwnerPlayerUid",
            "Runtime" + reward.ScheduleName + "RoundOwnerUID",
            "Runtime" + reward.ScheduleName + "RoundOwnerUid",
        ];
    }

    private static string[] GetLegacyNobilityPoleRoundOwnerAttributeNames(
        LegacyNobilityPoleRewardSchedule reward)
    {
        return
        [
            .. GetLegacyNobilityPoleRoundOwnerPlayerUidAttributeNames(reward),
            "Legacy" + reward.ScheduleName + "RoundOwnerName",
            reward.ScheduleName + "RoundOwnerName",
            "Runtime" + reward.ScheduleName + "RoundOwnerName",
            "Legacy" + reward.ScheduleName + "RoundOwnerScore",
            reward.ScheduleName + "RoundOwnerScore",
            "Runtime" + reward.ScheduleName + "RoundOwnerScore",
            "Legacy" + reward.ScheduleName + "PoleOwnerName",
            reward.ScheduleName + "PoleOwnerName",
            "Runtime" + reward.ScheduleName + "PoleOwnerName",
            "Legacy" + reward.ScheduleName + "PoleReset",
            reward.ScheduleName + "PoleReset",
            "Runtime" + reward.ScheduleName + "PoleReset",
        ];
    }

    private static string[] GetLegacyNobilityPoleGenericRoundOwnerAttributeNames()
    {
        return
        [
            "LegacyNobilityPoleRoundOwnerPlayerUID",
            "LegacyNobilityPoleRoundOwnerPlayerUid",
            "NobilityPoleRoundOwnerPlayerUID",
            "NobilityPoleRoundOwnerPlayerUid",
            "RuntimeNobilityPoleRoundOwnerPlayerUID",
            "RuntimeNobilityPoleRoundOwnerPlayerUid",
            "LegacyNobilityPoleRoundOwnerName",
            "NobilityPoleRoundOwnerName",
            "RuntimeNobilityPoleRoundOwnerName",
            "LegacyNobilityPoleRoundOwnerScore",
            "NobilityPoleRoundOwnerScore",
            "RuntimeNobilityPoleRoundOwnerScore",
            "LegacyNobilityPolePoleOwnerName",
            "NobilityPolePoleOwnerName",
            "RuntimeNobilityPolePoleOwnerName",
            "LegacyNobilityPolePoleReset",
            "NobilityPolePoleReset",
            "RuntimeNobilityPolePoleReset",
        ];
    }

    private static string FormatLegacyNobilityPoleRewardSchedules(
        IEnumerable<LegacyNobilityPoleRewardSchedule> rewards)
    {
        return string.Join(',', rewards.Select(reward => reward.ScheduleName));
    }

    private static string FormatLegacyNobilityPoleRewardSourceConquerPoints(
        IEnumerable<LegacyNobilityPoleRewardSchedule> rewards)
    {
        return string.Join(
            ',',
            rewards.Select(reward =>
                reward.DisplayRank +
                ":" +
                reward.SourceConquerPoints.ToString(CultureInfo.InvariantCulture)));
    }

    private static string FormatLegacyNobilityPoleRewardPlans(
        IEnumerable<LegacyNobilityPoleRewardPlan> plans)
    {
        return string.Join(
            ';',
            plans.Select(plan =>
                plan.Schedule.DisplayRank +
                ":" +
                FormatLegacyNobilityPoleRewardItems(plan.Items) +
                (plan.UsesFallback ? ":fallback" : ":full")));
    }

    private static string FormatLegacyNobilityPoleRewardItems(
        IEnumerable<(uint ItemId, ushort StackSize)> items)
    {
        return string.Join(
            ',',
            items.Select(item =>
                item.ItemId.ToString(CultureInfo.InvariantCulture) +
                "x" +
                item.StackSize.ToString(CultureInfo.InvariantCulture)));
    }

    private readonly record struct LegacyNobilityPoleRewardSchedule(
        string ScheduleName,
        string DisplayRank,
        long SourceConquerPoints,
        IReadOnlyList<(uint ItemId, ushort StackSize)> FullItems,
        IReadOnlyList<(uint ItemId, ushort StackSize)> FallbackItems,
        int SourceRequiredSpace);

    private readonly record struct LegacyNobilityPoleRewardPlan(
        LegacyNobilityPoleRewardSchedule Schedule,
        IReadOnlyList<(uint ItemId, ushort StackSize)> Items,
        bool UsesFallback);
}
