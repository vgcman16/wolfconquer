using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyClassPkWarPowerExpBallItemId = 722_057;

    private static async ValueTask TryClaimLegacyClassPkWarRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var state = ReadLegacyClassPkWarRuntimeState(session.Character);
        if (!state.Known || !state.BracketIsValid)
        {
            await SendLegacyClassPkWarRewardFencedAsync(
                    context,
                    optionId,
                    "ClassPkWar process, finish timer, class, or level bracket is not locally represented for this character",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!LegacyClassPkWarState.IsAliveProcess(state.Process))
        {
            await SendLegacyClassPkWarRewardFencedAsync(
                    context,
                    optionId,
                    $"source IsFinish requires Alive process but local process={state.Process}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (ReadAttributeBool(session.Character.Attributes, GetLegacyClassPkWarRewardClaimedAttributeNames(state)))
        {
            await SendLegacyClassPkWarRewardFencedAsync(
                    context,
                    optionId,
                    "ClassPkWar reward was already claimed",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!IsLegacyClassPkWarSoleAlivePlayer(context, session, state, out var aliveCount))
        {
            await SendLegacyClassPkWarRewardFencedAsync(
                    context,
                    optionId,
                    $"source IsFinish saw aliveCount={aliveCount.ToString(CultureInfo.InvariantCulture)} for dynamicId={state.DynamicId.ToString(CultureInfo.InvariantCulture)}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var rewardItem = CreateLegacyClassPkWarPowerExpBallItem(context, session.PlayerItems);
        var previousWinnerUid = ReadLegacyClassPkWarPreviousWinnerUid(session.Character, state);
        var previousStatusFlag = ReadLegacyClassPkWarPreviousStatusFlag(session.Character, state);
        var updatedItems = session.PlayerItems;
        var rewardItemInReturnedWarehouse = false;
        if (HasLegacyInventorySpace(
                updatedItems,
                LegacyClassPkWarState.RewardPowerExpBallStack))
        {
            updatedItems = updatedItems with
            {
                InventoryItems = updatedItems.InventoryItems
                    .Concat([rewardItem])
                    .OrderBy(item => item.Uid)
                    .ToArray(),
            };
        }
        else
        {
            rewardItemInReturnedWarehouse = true;
            rewardItem = rewardItem.WithWarehouseId(LegacyReturnedItemsWarehouseId, position: 0);
            updatedItems = updatedItems with
            {
                WarehouseItems = updatedItems.WarehouseItems
                    .Concat([rewardItem])
                    .OrderBy(item => item.WarehouseId)
                    .ThenBy(item => item.Uid)
                    .ToArray(),
            };
        }

        var updatedCharacter = session.Character with
        {
            ConquerPoints = checked(session.Character.ConquerPoints + LegacyClassPkWarState.RewardConquerPoints),
        };
        if (previousWinnerUid == updatedCharacter.Uid && previousStatusFlag >= 0)
        {
            updatedCharacter = WithoutLegacyStatusFlag(updatedCharacter, previousStatusFlag);
        }

        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "ConquerPoints",
            CheckedUInt64(updatedCharacter.ConquerPoints, nameof(updatedCharacter.ConquerPoints)));
        updatedCharacter = WithLegacyClassPkWarRewardState(updatedCharacter, state);

        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await ClearLegacyClassPkWarPreviousWinnerFlagAsync(
                updatedContext,
                previousWinnerUid,
                previousStatusFlag,
                cancellationToken)
            .ConfigureAwait(false);
        await SendScalarUpdateAsync(
                updatedContext,
                updatedCharacter,
                LegacyMsgUpdateDataType.ConquerPoints,
                checked((ulong)updatedCharacter.ConquerPoints),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket
                    .CreateLegacyRuntimeStatusFlagVectorUpdate(
                        CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                        CreateLegacyRuntimeStatusFlagVector(updatedCharacter))
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(
                        rewardItem,
                        mode: rewardItemInReturnedWarehouse
                            ? LegacyGameItemPacket.AddItemReturnedMode
                            : LegacyGameItemPacket.AddItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyItemFollowupsAsync(updatedContext, rewardItem, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket
                    .SystemTalk(
                        "You received " +
                        LegacyClassPkWarState.RewardConquerPoints.ToString(CultureInfo.InvariantCulture) +
                        " ConquerPoints. ")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await PersistLegacyClassPkWarRewardAsync(
                updatedContext,
                updatedSession,
                state,
                rewardItem,
                rewardItemInReturnedWarehouse)
            .ConfigureAwait(false);
        await PersistLegacyNpcItemsAsync(
                updatedContext,
                rewardItem.ItemId,
                LegacyClassPkWarSourceMethod)
            .ConfigureAwait(false);
        await SendLegacyClassPkWarRewardBroadcastAsync(updatedContext, updatedCharacter, state, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcTeleportAsync(
                updatedContext,
                updatedSession,
                LegacyClassPkWarNpcId,
                optionId,
                new LegacyNpcTeleportDestination(1_002, 410, 354, LegacyClassPkWarSourceMethod),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc ClassPkWar reward " +
                $"{FormatSession(updatedContext)} source={LegacyClassPkWarSourceMethod} option={optionId} " +
                $"type={state.TournamentType} level={state.TournamentLevel} dynamicId={state.DynamicId.ToString(CultureInfo.InvariantCulture)} " +
                $"conquerPoints={LegacyClassPkWarState.RewardConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"itemId={LegacyClassPkWarPowerExpBallItemId.ToString(CultureInfo.InvariantCulture)} " +
                $"stack={LegacyClassPkWarState.RewardPowerExpBallStack.ToString(CultureInfo.InvariantCulture)} " +
                $"returnedWarehouse={rewardItemInReturnedWarehouse.ToString(CultureInfo.InvariantCulture)}.")
            .ConfigureAwait(false);
    }

    private static uint ReadLegacyClassPkWarPreviousWinnerUid(
        CharacterSnapshot character,
        LegacyClassPkWarRuntimeState state)
    {
        return TryReadUInt64(
                character.Attributes,
                out var raw,
                GetLegacyClassPkWarWinnerAttributeNames(state)) &&
            raw <= uint.MaxValue
            ? checked((uint)raw)
            : 0;
    }

    private static int ReadLegacyClassPkWarPreviousStatusFlag(
        CharacterSnapshot character,
        LegacyClassPkWarRuntimeState state)
    {
        var raw = ReadAttributeString(character.Attributes, GetLegacyClassPkWarLastFlagAttributeNames(state));
        return LegacyClassPkWarTitleFlags.TryReadStatusFlag(raw, out var statusFlag)
            ? statusFlag
            : -1;
    }

    private static async ValueTask ClearLegacyClassPkWarPreviousWinnerFlagAsync(
        WorldPacketHandlerContext context,
        uint previousWinnerUid,
        int previousStatusFlag,
        CancellationToken cancellationToken)
    {
        if (previousWinnerUid == 0 ||
            previousStatusFlag < 0 ||
            context.Sessions is not { } sessions)
        {
            return;
        }

        var cleared = false;
        foreach (var active in sessions.Snapshot())
        {
            if (active.Session.Character.Uid != previousWinnerUid ||
                ReferenceEquals(active.Connection, context.Connection) ||
                !HasLegacyStatusFlag(active.Session.Character, previousStatusFlag))
            {
                continue;
            }

            var updatedCharacter = WithoutLegacyStatusFlag(active.Session.Character, previousStatusFlag);
            var updatedSession = active.Session with { Character = updatedCharacter };
            sessions.Set(active.Connection, updatedSession);
            await active.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeStatusFlagVectorUpdate(
                            CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                            CreateLegacyRuntimeStatusFlagVector(updatedCharacter))
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            cleared = true;
        }

        if (cleared)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyNpc ClassPkWar previous winner title cleanup " +
                    $"{FormatSession(context)} source={LegacyClassPkWarSourceMethod} " +
                    $"previousWinnerUid={previousWinnerUid.ToString(CultureInfo.InvariantCulture)} " +
                    $"removedFlag={previousStatusFlag.ToString(CultureInfo.InvariantCulture)}.")
                .ConfigureAwait(false);
        }
    }

    private static bool IsLegacyClassPkWarSoleAlivePlayer(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyClassPkWarRuntimeState state,
        out int aliveCount)
    {
        aliveCount = 0;
        var dynamicId = ResolveLegacyClassPkWarDynamicId(session, state);
        if (dynamicId == 0)
        {
            return false;
        }

        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is null || activeSessions.Count == 0)
        {
            aliveCount = IsLegacyClassPkWarAliveParticipant(session, dynamicId) ? 1 : 0;
            return aliveCount == 1;
        }

        WorldActiveSession? current = null;
        foreach (var active in activeSessions)
        {
            if (ReferenceEquals(active.Connection, context.Connection))
            {
                current = active;
            }

            if (IsLegacyClassPkWarAliveParticipant(active.Session, dynamicId))
            {
                aliveCount++;
            }
        }

        return aliveCount == 1 &&
            current is not null &&
            IsLegacyClassPkWarAliveParticipant(current.Session, dynamicId);
    }

    private static bool IsLegacyClassPkWarAliveParticipant(WorldSession session, uint dynamicId)
    {
        return session.Character.MapId == LegacyClassPkWarState.MapId &&
            session.Character.MinHitPoints > 0 &&
            ResolveLegacyClassPkWarDynamicId(session, ReadLegacyClassPkWarRuntimeState(session.Character)) == dynamicId;
    }

    private static uint ResolveLegacyClassPkWarDynamicId(
        WorldSession session,
        LegacyClassPkWarRuntimeState state)
    {
        if (TryReadUInt32Attribute(session.Character.Attributes, out var genericDynamicId, "DynamicID", "DynamicId") &&
            genericDynamicId != 0)
        {
            return genericDynamicId;
        }

        return state.DynamicId;
    }

    private static PlayerItemSnapshotItem CreateLegacyClassPkWarPowerExpBallItem(
        WorldPacketHandlerContext context,
        PlayerItemSnapshot items)
    {
        var durability = TryReadItemType(context.WorldData, LegacyClassPkWarPowerExpBallItemId, out var itemType)
            ? checked((ushort)itemType.LegacyDurability)
            : (ushort)0;
        return new PlayerItemSnapshotItem
        {
            Uid = GetNextSessionItemUid(items),
            ItemId = LegacyClassPkWarPowerExpBallItemId,
            Durability = durability,
            MaximumDurability = durability,
            Position = 0,
            StackSize = LegacyClassPkWarState.RewardPowerExpBallStack,
        };
    }

    private static CharacterSnapshot WithLegacyClassPkWarRewardState(
        CharacterSnapshot character,
        LegacyClassPkWarRuntimeState state)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var key in GetLegacyClassPkWarRewardClaimedAttributeNames(state))
        {
            attributes[key] = "true";
        }

        foreach (var key in GetLegacyClassPkWarProcessAttributeNames(state))
        {
            attributes[key] = "Dead";
        }

        foreach (var key in GetLegacyClassPkWarWinnerAttributeNames(state))
        {
            attributes[key] = character.Uid.ToString(CultureInfo.InvariantCulture);
        }

        var auraName = LegacyClassPkWarTitleFlags.GetAuraName(state.TournamentType);
        foreach (var key in GetLegacyClassPkWarLastFlagAttributeNames(state))
        {
            attributes[key] = auraName;
        }

        attributes["LegacyClassPkWarLastWinnerName"] = character.Name;
        attributes["LegacyClassPkWarLastRewardUtc"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        attributes["LegacyClassPkWarLastRewardItemId"] = LegacyClassPkWarPowerExpBallItemId.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyClassPkWarLastRewardStack"] = LegacyClassPkWarState.RewardPowerExpBallStack.ToString(CultureInfo.InvariantCulture);

        var updated = character with { Attributes = attributes };
        var statusFlag = LegacyClassPkWarTitleFlags.GetStatusFlag(state.TournamentType);
        return statusFlag < 0
            ? updated
            : WithLegacyStatusFlag(updated, statusFlag);
    }

    private static async ValueTask PersistLegacyClassPkWarRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyClassPkWarRuntimeState state,
        PlayerItemSnapshotItem rewardItem,
        bool returnedWarehouse)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["LegacyClassPkWarLastWinnerName"] = session.Character.Name,
            ["LegacyClassPkWarLastRewardUtc"] = ReadAttributeString(session.Character.Attributes, "LegacyClassPkWarLastRewardUtc"),
            ["LegacyClassPkWarLastRewardItemId"] = rewardItem.ItemId.ToString(CultureInfo.InvariantCulture),
            ["LegacyClassPkWarLastRewardStack"] = rewardItem.StackSize.ToString(CultureInfo.InvariantCulture),
            ["LegacyClassPkWarRewardReturnedWarehouse"] = returnedWarehouse ? "true" : "false",
        };

        foreach (var key in GetLegacyClassPkWarRewardClaimedAttributeNames(state))
        {
            values[key] = "true";
        }

        foreach (var key in GetLegacyClassPkWarProcessAttributeNames(state))
        {
            values[key] = "Dead";
        }

        foreach (var key in GetLegacyClassPkWarWinnerAttributeNames(state))
        {
            values[key] = session.Character.Uid.ToString(CultureInfo.InvariantCulture);
        }

        foreach (var key in GetLegacyClassPkWarLastFlagAttributeNames(state))
        {
            values[key] = LegacyClassPkWarTitleFlags.GetAuraName(state.TournamentType);
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
                    "PARITY-GAP LegacyNpc ClassPkWar reward persistence " +
                    $"{FormatSession(context)} source={LegacyClassPkWarSourceMethod} reason={reason}; " +
                    $"source saves winner and LastFlag to {LegacyClassPkWarState.FileName}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNpc ClassPkWar reward " +
                $"{FormatSession(context)} source={LegacyClassPkWarSourceMethod} path={Path.GetFileName(path)} keys={string.Join(',', values.Keys)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyClassPkWarRewardBroadcastAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot winner,
        LegacyClassPkWarRuntimeState state,
        CancellationToken cancellationToken)
    {
        var message =
            winner.Name +
            " Won " +
            state.TournamentType +
            " PK War (" +
            state.TournamentLevel +
            ") , he received Top " +
            state.TournamentType +
            ", " +
            LegacyClassPkWarState.RewardConquerPoints.ToString(CultureInfo.InvariantCulture) +
            " ConquerPoints and 4 PowerExpBalls !";
        var frame = LegacyMsgMessagePacket.SystemRedCenter(message).ToFrameBytes();
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

    private static string[] GetLegacyClassPkWarRewardClaimedAttributeNames(LegacyClassPkWarRuntimeState state)
    {
        return GetLegacyClassPkWarAttributeNames(
            new LegacyClassPkWarBracket(state.TournamentType, state.TournamentLevel),
            "RewardClaimed",
            "Claimed",
            "ReceiveReward");
    }

    private static string[] GetLegacyClassPkWarProcessAttributeNames(LegacyClassPkWarRuntimeState state)
    {
        return GetLegacyClassPkWarAttributeNames(
            new LegacyClassPkWarBracket(state.TournamentType, state.TournamentLevel),
            "Process",
            "Proces");
    }

    private static string[] GetLegacyClassPkWarWinnerAttributeNames(LegacyClassPkWarRuntimeState state)
    {
        return GetLegacyClassPkWarAttributeNames(
            new LegacyClassPkWarBracket(state.TournamentType, state.TournamentLevel),
            "Winner",
            "WinnerUID",
            "WinnerUid",
            "WinnerPlayerUID",
            "WinnerPlayerUid");
    }

    private static string[] GetLegacyClassPkWarLastFlagAttributeNames(LegacyClassPkWarRuntimeState state)
    {
        return GetLegacyClassPkWarAttributeNames(
            new LegacyClassPkWarBracket(state.TournamentType, state.TournamentLevel),
            "LastFlag",
            "Aura",
            "TopFlag");
    }

}
