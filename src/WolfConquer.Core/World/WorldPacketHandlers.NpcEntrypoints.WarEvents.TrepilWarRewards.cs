using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyTrepilWarSourceConquerPointsReward = 5_000_000;
    private const long TunedLegacyTrepilWarConquerPointsReward = 300_000;
    private const uint LegacyTrepilWarClassExperienceReward = 500;
    private const ushort LegacyTrepilWarStudyPointsReward = 10_000;
    private const int LegacyTrepilWarInnerPowerPotencyReward = 10_000;
    private const string LegacyTrepilWarRewardClaimedAttribute = "LegacyTrepilWarRewardClaimed";

    private static readonly (uint ItemId, ushort StackSize)[] LegacyTrepilWarRewardItems =
    [
        (3_009_003, 5),
        (3_324_226, 2),
        (4_050_001, 500),
        (4_040_001, 500),
    ];

    private static async ValueTask TryClaimLegacyTrepilWarRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (IsLegacyTrepilWarProcessAlive(session.Character))
        {
            await SendLegacyTrepilWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Please come back when Trepil War is finished.",
                    "skipped active MsgSchedules.MsgTrepilWar.Proces",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (ReadAttributeBool(
                session.Character.Attributes,
                LegacyTrepilWarRewardClaimedAttribute,
                "TrepilWarRewardClaimed",
                "LegacyMsgTrepilWarRewardClaimed",
                "MsgTrepilWarRewardClaimed"))
        {
            await SendLegacyTrepilWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "This Trepil War reward was already claimed.",
                    "already claimed",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyTrepilWarWinnerGuildId(session.Character, out var winnerGuildId))
        {
            await SendLegacyTrepilWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "There is no Trepil War reward available now.",
                    "skipped missing MsgSchedules.MsgTrepilWar.RoundOwner",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var playerGuildId = ReadAttributeUInt64(
            session.Character.Attributes,
            "GuildID",
            "GuildId",
            "MyGuildID",
            "MyGuildId",
            "GuildUID",
            "GuildUid");
        if (playerGuildId != winnerGuildId)
        {
            await SendLegacyTrepilWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "You do not have any reward to claim.",
                    $"skipped wrong guild winnerGuildId={winnerGuildId.ToString(CultureInfo.InvariantCulture)} playerGuildId={playerGuildId.ToString(CultureInfo.InvariantCulture)}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!IsLegacyGuildLeader(session.Character))
        {
            await SendLegacyTrepilWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Only the guild leader can claim the reward.",
                    "skipped non-guild-leader rank",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, LegacyTrepilWarRewardItems.Length))
        {
            await SendLegacyTrepilWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Please make 4 more spaces in your inventory.",
                    "skipped full inventory sourceRequiredSpace=4",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var grantedItems = CreateLegacyTrepilWarRewardItems(context, session.PlayerItems);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat(grantedItems).OrderBy(item => item.Uid).ToArray(),
        };
        var currentClassExperience = ReadAttributeUInt64(session.Character.Attributes, "ClassExperience");
        var updatedClassExperience = checked(currentClassExperience + LegacyTrepilWarClassExperienceReward);
        var updatedCharacter = session.Character with
        {
            ConquerPoints = checked(session.Character.ConquerPoints + TunedLegacyTrepilWarConquerPointsReward),
        };
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "ConquerPoints",
            CheckedUInt64(updatedCharacter.ConquerPoints, nameof(updatedCharacter.ConquerPoints)));
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "ClassExperience",
            updatedClassExperience);
        updatedCharacter = AddLegacyStudyPoints(updatedCharacter, LegacyTrepilWarStudyPointsReward);
        updatedCharacter = WithLegacyTrepilWarRewardClaimCleared(updatedCharacter);

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

        var innerPowerApplied = await TryApplyLegacyTrepilWarInnerPowerPotencyAwardAsync(
                updatedContext,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);

        await PersistLegacyTrepilWarRewardAsync(updatedContext, updatedSession, sourceMethod, innerPowerApplied)
            .ConfigureAwait(false);
        foreach (var item in grantedItems)
        {
            await PersistLegacyNpcItemsAsync(updatedContext, item.ItemId, sourceMethod).ConfigureAwait(false);
        }

        await SendLegacyNpcDialogAsync(
                updatedContext,
                sourceMethod,
                [$"You claimed {TunedLegacyTrepilWarConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs, {LegacyTrepilWarClassExperienceReward.ToString(CultureInfo.InvariantCulture)} class experience, study points, inner power potency, and Trepil War items."],
                LegacyUnionEliteWarEventClosedOptions,
                LegacyUnionEliteWarEventAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyTrepilWarRewardBroadcastAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc TrepilWar reward " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                $"winnerGuildId={winnerGuildId.ToString(CultureInfo.InvariantCulture)} " +
                $"sourceConquerPoints={LegacyTrepilWarSourceConquerPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"tunedConquerPoints={TunedLegacyTrepilWarConquerPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"classExperience={LegacyTrepilWarClassExperienceReward.ToString(CultureInfo.InvariantCulture)} " +
                $"items={FormatLegacyTrepilWarRewardItems()} " +
                $"studyPoints={LegacyTrepilWarStudyPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"innerPowerPotency={LegacyTrepilWarInnerPowerPotencyReward.ToString(CultureInfo.InvariantCulture)} " +
                $"innerPowerApplied={innerPowerApplied.ToString(CultureInfo.InvariantCulture)} " +
                "source=references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgNpc\\NpcHandler.cs MsgTrepilWar case 2.")
            .ConfigureAwait(false);
    }

    private static IReadOnlyList<PlayerItemSnapshotItem> CreateLegacyTrepilWarRewardItems(
        WorldPacketHandlerContext context,
        PlayerItemSnapshot items)
    {
        var nextUid = GetNextSessionItemUid(items);
        var grants = new PlayerItemSnapshotItem[LegacyTrepilWarRewardItems.Length];
        for (var index = 0; index < grants.Length; index++)
        {
            var (itemId, stackSize) = LegacyTrepilWarRewardItems[index];
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

    private static bool TryReadLegacyTrepilWarWinnerGuildId(
        CharacterSnapshot character,
        out ulong winnerGuildId)
    {
        return TryReadUInt64(
                character.Attributes,
                out winnerGuildId,
                "LegacyMsgTrepilWarRoundOwnerGuildID",
                "LegacyMsgTrepilWarRoundOwnerGuildId",
                "MsgTrepilWarRoundOwnerGuildID",
                "MsgTrepilWarRoundOwnerGuildId",
                "LegacyTrepilWarRoundOwnerGuildID",
                "LegacyTrepilWarRoundOwnerGuildId",
                "TrepilWarRoundOwnerGuildID",
                "TrepilWarRoundOwnerGuildId") &&
            winnerGuildId != 0;
    }

    private static CharacterSnapshot WithLegacyTrepilWarRewardClaimCleared(CharacterSnapshot character)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [LegacyTrepilWarRewardClaimedAttribute] = "true",
            ["TrepilWarRewardClaimed"] = "true",
            ["LegacyMsgTrepilWarRewardClaimed"] = "true",
            ["MsgTrepilWarRewardClaimed"] = "true",
            ["LegacyMsgTrepilWarWinnerCleared"] = "true",
            ["MsgTrepilWarWinnerCleared"] = "true",
            ["LegacyTrepilWarWinnerCleared"] = "true",
            ["TrepilWarWinnerCleared"] = "true",
        };
        attributes.Remove("LegacyMsgTrepilWarRoundOwnerGuildID");
        attributes.Remove("LegacyMsgTrepilWarRoundOwnerGuildId");
        attributes.Remove("MsgTrepilWarRoundOwnerGuildID");
        attributes.Remove("MsgTrepilWarRoundOwnerGuildId");
        attributes.Remove("LegacyTrepilWarRoundOwnerGuildID");
        attributes.Remove("LegacyTrepilWarRoundOwnerGuildId");
        attributes.Remove("TrepilWarRoundOwnerGuildID");
        attributes.Remove("TrepilWarRoundOwnerGuildId");
        attributes.Remove("LegacyMsgTrepilWarRoundOwnerGuildName");
        attributes.Remove("MsgTrepilWarRoundOwnerGuildName");
        attributes.Remove("LegacyTrepilWarRoundOwnerGuildName");
        attributes.Remove("TrepilWarRoundOwnerGuildName");
        attributes.Remove("LegacyMsgTrepilWarRoundOwnerScore");
        attributes.Remove("MsgTrepilWarRoundOwnerScore");
        attributes.Remove("LegacyTrepilWarRoundOwnerScore");
        attributes.Remove("TrepilWarRoundOwnerScore");
        return character with { Attributes = attributes };
    }

    private static async ValueTask<bool> TryApplyLegacyTrepilWarInnerPowerPotencyAwardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is null)
        {
            await LogLegacyTrepilWarInnerPowerDeferredAsync(context, session, "InnerPower.txt rows are not loaded")
                .ConfigureAwait(false);
            return false;
        }

        if (session.Character.Uid is < 0 or > int.MaxValue)
        {
            await LogLegacyTrepilWarInnerPowerDeferredAsync(
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
            await LogLegacyTrepilWarInnerPowerDeferredAsync(
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
                updatedPotency = checked(state.Potency + LegacyTrepilWarInnerPowerPotencyReward);
                var updatedRow = CreateLegacyInnerPowerRankEntry(row, state with { Potency = updatedPotency });
                applied = TryReplaceLegacyInnerPowerRow(rows, updatedRow, out deferReason);
            }
        }

        if (!applied)
        {
            await LogLegacyTrepilWarInnerPowerDeferredAsync(context, session, deferReason).ConfigureAwait(false);
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

    private static async ValueTask LogLegacyTrepilWarInnerPowerDeferredAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "WorldServer deferred LegacyNpc TrepilWar InnerPower potency reward " +
                $"{FormatSession(context with { Session = session })}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask PersistLegacyTrepilWarRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        bool innerPowerApplied)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["ClassExperience"] = ReadAttributeString(session.Character.Attributes, "ClassExperience"),
            ["SubProfInfo"] = ReadAttributeString(session.Character.Attributes, "SubProfInfo"),
            [LegacyTrepilWarRewardClaimedAttribute] = "true",
            ["TrepilWarRewardClaimed"] = "true",
            ["LegacyMsgTrepilWarRewardClaimed"] = "true",
            ["MsgTrepilWarRewardClaimed"] = "true",
            ["LegacyMsgTrepilWarWinnerCleared"] = "true",
            ["MsgTrepilWarWinnerCleared"] = "true",
            ["LegacyTrepilWarWinnerCleared"] = "true",
            ["TrepilWarWinnerCleared"] = "true",
            ["LegacyMsgTrepilWarRoundOwnerGuildID"] = "0",
            ["LegacyMsgTrepilWarRoundOwnerGuildId"] = "0",
            ["MsgTrepilWarRoundOwnerGuildID"] = "0",
            ["MsgTrepilWarRoundOwnerGuildId"] = "0",
            ["LegacyTrepilWarRoundOwnerGuildID"] = "0",
            ["LegacyTrepilWarRoundOwnerGuildId"] = "0",
            ["TrepilWarRoundOwnerGuildID"] = "0",
            ["TrepilWarRoundOwnerGuildId"] = "0",
            ["LegacyMsgTrepilWarRoundOwnerGuildName"] = "0",
            ["MsgTrepilWarRoundOwnerGuildName"] = "0",
            ["LegacyTrepilWarRoundOwnerGuildName"] = "0",
            ["TrepilWarRoundOwnerGuildName"] = "0",
            ["LegacyMsgTrepilWarRoundOwnerScore"] = "0",
            ["MsgTrepilWarRoundOwnerScore"] = "0",
            ["LegacyTrepilWarRoundOwnerScore"] = "0",
            ["TrepilWarRoundOwnerScore"] = "0",
            ["LegacyTrepilWarInnerPowerApplied"] = innerPowerApplied ? "true" : "false",
        };

        if (!TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                session.Character.Uid,
                values,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc TrepilWar reward persistence " +
                    $"{FormatSession(context)} source={sourceMethod} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNpc TrepilWar reward " +
                $"{FormatSession(context)} source={sourceMethod} path={Path.GetFileName(path)} keys={string.Join(',', values.Keys)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyTrepilWarRewardDialogAndLogAsync(
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
                LegacyUnionEliteWarEventClosedOptions,
                LegacyUnionEliteWarEventAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc TrepilWar reward " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyTrepilWarRewardBroadcastAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot winner,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket
            .SystemRedCenter(
                $"{winner.Name} has received {TunedLegacyTrepilWarConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs for winning TrepilWar.")
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

    private static string FormatLegacyTrepilWarRewardItems()
    {
        return string.Join(
            ',',
            LegacyTrepilWarRewardItems.Select(item =>
                item.ItemId.ToString(CultureInfo.InvariantCulture) +
                "x" +
                item.StackSize.ToString(CultureInfo.InvariantCulture)));
    }
}
