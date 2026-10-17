using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyGuildWarScoreConquerPointsReward = 300_000;
    private const uint LegacyGuildWarScoreRewardItemId = 4_300_004;
    private const uint LegacyGuildWarScoreFlutteringPhoenixTitleId = 8_045;
    private const uint LegacyGuildWarScoreStarlightWingsTitleId = 6_003;
    private const uint LegacyGuildWarScoreHandsomeTitleId = 2_024;
    private const string LegacyGuildWarScoreRewardClaimedAttribute = "LegacyGuildWarScoreRewardClaimed";
    private const long LegacyClanWarCurrentSourceConquerPointsReward = 15_000_000;
    private const long TunedLegacyClanWarCurrentConquerPointsReward = 300_000;
    private const ushort LegacyClanWarCurrentStudyPointsReward = 10_000;
    private const int LegacyClanWarCurrentInnerPowerPotencyReward = 10_000;
    private const string LegacyClanWarCurrentRewardSourceMethod = "MsgClanWar.CityWar.GetRewards";

    private static readonly uint[] LegacyGuildWarScoreSpecialTitleRewards =
    [
        LegacyGuildWarScoreFlutteringPhoenixTitleId,
        LegacyGuildWarScoreStarlightWingsTitleId,
        LegacyGuildWarScoreHandsomeTitleId,
    ];

    private static readonly (uint ItemId, ushort StackSize)[] LegacyClanWarCurrentBaseRewardItems =
    [
        (3_009_003, 3),
        (3_324_226, 10),
        (4_050_001, 500),
        (4_040_001, 500),
    ];

    private static async ValueTask<(WorldSession Session, int SentFrameCount, bool Granted)> TryGrantLegacyClanWarCurrentCompletionRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string city,
        LegacyClanWarCurrentWinner winner,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeLegacyClanWarCityName(city, out var normalizedCity))
        {
            return (session, 0, false);
        }

        if (winner.ClanId == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy ClanWar current-war reward " +
                    $"{FormatSession(context)} city={normalizedCity} reason=missing winner.")
                .ConfigureAwait(false);
            return (session, 0, false);
        }

        if (ReadAttributeBool(session.Character.Attributes, GetLegacyClanWarCurrentRewardClaimedAttributeNames(normalizedCity)))
        {
            return (session, 0, false);
        }

        if (!IsLegacyClanWarCurrentRewardScopeEligible(session.Character, normalizedCity))
        {
            return (session, 0, false);
        }

        var playerClanId = ReadAttributeUInt64(
            session.Character.Attributes,
            "ClanID",
            "ClanId",
            "ClanUID",
            "ClanUid",
            "MyClanID",
            "MyClanId",
            "MyClanUID",
            "MyClanUid",
            "ClainID",
            "ClainId");
        if (playerClanId != winner.ClanId ||
            !IsLegacyClanLeader(session.Character))
        {
            return (session, 0, false);
        }

        var rewardDefinitions = GetLegacyClanWarCurrentRewardItems(winner);
        if (!HasLegacyInventorySpace(session.PlayerItems, rewardDefinitions.Count))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy ClanWar current-war reward " +
                    $"{FormatSession(context)} city={normalizedCity} winnerClanId={winner.ClanId.ToString(CultureInfo.InvariantCulture)} " +
                    $"sourceRequiredSpace={rewardDefinitions.Count.ToString(CultureInfo.InvariantCulture)} reason=full inventory.")
                .ConfigureAwait(false);
            return (session, 0, false);
        }

        var grantedItems = CreateLegacyClanWarCurrentRewardItems(context, session.PlayerItems, rewardDefinitions);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat(grantedItems).OrderBy(item => item.Uid).ToArray(),
        };
        var updatedCharacter = session.Character with
        {
            ConquerPoints = checked(session.Character.ConquerPoints + TunedLegacyClanWarCurrentConquerPointsReward),
        };
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "ConquerPoints",
            CheckedUInt64(updatedCharacter.ConquerPoints, nameof(updatedCharacter.ConquerPoints)));
        updatedCharacter = AddLegacyStudyPoints(updatedCharacter, LegacyClanWarCurrentStudyPointsReward);
        updatedCharacter = WithLegacyClanWarCurrentRewardClaimedAttributes(updatedCharacter, normalizedCity, winner);

        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        var sentFrameCount = 0;
        await SendScalarUpdateAsync(
                updatedContext,
                updatedCharacter,
                LegacyMsgUpdateDataType.ConquerPoints,
                checked((ulong)updatedCharacter.ConquerPoints),
                cancellationToken)
            .ConfigureAwait(false);
        sentFrameCount++;

        foreach (var item in grantedItems)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            sentFrameCount++;
            await SendLegacyItemFollowupsAsync(updatedContext, item, cancellationToken)
                .ConfigureAwait(false);
        }

        var innerPowerApplied = await TryApplyLegacyClanWarCurrentInnerPowerPotencyAwardAsync(
                updatedContext,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);
        if (innerPowerApplied)
        {
            sentFrameCount++;
        }

        await PersistLegacyClanWarCurrentRewardAsync(updatedContext, updatedSession, normalizedCity, winner, innerPowerApplied)
            .ConfigureAwait(false);
        foreach (var item in grantedItems)
        {
            await PersistLegacyNpcItemsAsync(updatedContext, item.ItemId, LegacyClanWarCurrentRewardSourceMethod)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy ClanWar current-war completion reward " +
                $"{FormatSession(updatedContext)} city={normalizedCity} " +
                $"winnerClanId={winner.ClanId.ToString(CultureInfo.InvariantCulture)} " +
                $"sourceConquerPoints={LegacyClanWarCurrentSourceConquerPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"tunedConquerPoints={TunedLegacyClanWarCurrentConquerPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"items={FormatLegacyClanWarCurrentRewardItems(rewardDefinitions)} " +
                $"studyPoints={LegacyClanWarCurrentStudyPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"innerPowerPotency={LegacyClanWarCurrentInnerPowerPotencyReward.ToString(CultureInfo.InvariantCulture)} " +
                $"innerPowerApplied={innerPowerApplied.ToString(CultureInfo.InvariantCulture)} " +
                "source=MsgClanWar.CityWar.GetRewards.")
            .ConfigureAwait(false);
        return (updatedSession, sentFrameCount, true);
    }

    private static async ValueTask TryClaimLegacyClanWarScoreRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        if (IsLegacyGuildWarScoreProcessAlive(session.Character))
        {
            await SendLegacyClanWarScoreRewardDialogAndLogAsync(
                    context,
                    optionId,
                    sourceMethod,
                    "Please come back when Guild War Score is finished.",
                    "skipped active MsgSchedules.TopWarScore.Proces",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (ReadAttributeBool(
                session.Character.Attributes,
                LegacyGuildWarScoreRewardClaimedAttribute,
                "GuildWarScoreRewardClaimed",
                "LegacyTopWarScoreRewardClaimed",
                "TopWarScoreRewardClaimed"))
        {
            await SendLegacyClanWarScoreRewardDialogAndLogAsync(
                    context,
                    optionId,
                    sourceMethod,
                    "This Guild War Score reward was already claimed.",
                    "already claimed",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyGuildWarScoreWinnerGuildId(session.Character, out var winnerGuildId))
        {
            await SendLegacyClanWarScoreRewardDialogAndLogAsync(
                    context,
                    optionId,
                    sourceMethod,
                    "There is no Guild War Score reward available now.",
                    "skipped missing TopWarScore winner",
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
            await SendLegacyClanWarScoreRewardDialogAndLogAsync(
                    context,
                    optionId,
                    sourceMethod,
                    "You do not have any reward to claim.",
                    $"skipped wrong guild winnerGuildId={winnerGuildId.ToString(CultureInfo.InvariantCulture)} playerGuildId={playerGuildId.ToString(CultureInfo.InvariantCulture)}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!IsLegacyGuildLeader(session.Character))
        {
            await SendLegacyClanWarScoreRewardDialogAndLogAsync(
                    context,
                    optionId,
                    sourceMethod,
                    "Only the guild leader can claim the reward.",
                    "skipped non-guild-leader rank",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await SendLegacyClanWarScoreRewardDialogAndLogAsync(
                    context,
                    optionId,
                    sourceMethod,
                    "Please make one more space in your inventory.",
                    "skipped full inventory",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var grantedItem = CreateLegacyNpcItemGrant(
            context,
            session.PlayerItems,
            LegacyGuildWarScoreRewardItemId);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat([grantedItem]).OrderBy(item => item.Uid).ToArray(),
        };
        var updatedCharacter = session.Character with
        {
            ConquerPoints = checked(session.Character.ConquerPoints + LegacyGuildWarScoreConquerPointsReward),
        };
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "ConquerPoints",
            CheckedUInt64(updatedCharacter.ConquerPoints, nameof(updatedCharacter.ConquerPoints)));
        updatedCharacter = WithLegacyGuildWarScoreRewardTitles(updatedCharacter);
        updatedCharacter = WithLegacyGuildWarScoreRewardClaimCleared(updatedCharacter);

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
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(grantedItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyItemFollowupsAsync(updatedContext, grantedItem, cancellationToken)
            .ConfigureAwait(false);
        foreach (var titleId in LegacyGuildWarScoreSpecialTitleRewards)
        {
            await context.Connection.SendAsync(
                    LegacySpecialTitlePacket.FromTitleId(titleId).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await PersistLegacyClanWarScoreRewardAsync(updatedContext, updatedSession)
            .ConfigureAwait(false);
        await PersistLegacyNpcItemsAsync(
                updatedContext,
                LegacyGuildWarScoreRewardItemId,
                sourceMethod)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                sourceMethod,
                [$"You claimed {LegacyGuildWarScoreConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs and item {LegacyGuildWarScoreRewardItemId.ToString(CultureInfo.InvariantCulture)} for winning Guild War Score."],
                LegacyClanWarEventClosedOptions,
                LegacyClanWarScoreAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyClanWarScoreRewardBroadcastAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc ClanWarScore reward " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                $"winnerGuildId={winnerGuildId.ToString(CultureInfo.InvariantCulture)} " +
                $"conquerPoints={LegacyGuildWarScoreConquerPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"itemId={LegacyGuildWarScoreRewardItemId.ToString(CultureInfo.InvariantCulture)} " +
                "specialTitles=8045,6003,2024 " +
                "sourceReward=NewSystem.PrizeAllTops.GuildWarScore.")
            .ConfigureAwait(false);
    }

    private static bool TryReadLegacyGuildWarScoreWinnerGuildId(
        CharacterSnapshot character,
        out ulong winnerGuildId)
    {
        if (TryReadUInt64(
                character.Attributes,
                out winnerGuildId,
                "LegacyTopWarScoreRoundOwnerGuildID",
                "TopWarScoreRoundOwnerGuildID",
                "LegacyTopWarScoreRoundOwnerGuildId",
                "TopWarScoreRoundOwnerGuildId",
                "LegacyGuildWarScoreRoundOwnerGuildID",
                "GuildWarScoreRoundOwnerGuildID",
                "LegacyGuildWarScoreRoundOwnerGuildId",
                "GuildWarScoreRoundOwnerGuildId") &&
            winnerGuildId != 0)
        {
            return true;
        }

        return TryReadLegacyGuildWarScoreWinnerGuildIdFromScores(character, out winnerGuildId);
    }

    private static bool TryReadLegacyGuildWarScoreWinnerGuildIdFromScores(
        CharacterSnapshot character,
        out ulong winnerGuildId)
    {
        winnerGuildId = 0;
        var raw = ReadAttributeString(
            character.Attributes,
            LegacyGuildWarScoreScoresAttribute,
            "LegacyGuildWarScoreScores",
            "TopWarScoreScores",
            "LegacyTopWarScoreScores");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        ulong bestScore = 0;
        foreach (var entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = entry.Split(',', StringSplitOptions.TrimEntries);
            if (fields.Length < 2 ||
                !ulong.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var guildId) ||
                !ulong.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score) ||
                guildId == 0 ||
                (winnerGuildId != 0 && score <= bestScore))
            {
                continue;
            }

            winnerGuildId = guildId;
            bestScore = score;
        }

        return winnerGuildId != 0;
    }

    private static bool IsLegacyGuildLeader(CharacterSnapshot character)
    {
        var raw = ReadAttributeString(
                character.Attributes,
                "GuildRank",
                "GuildMemberRank",
                "MyGuildRank",
                "MyGuildMemberRank",
                "LegacyGuildRank")
            .Trim();
        return raw.Equals("GuildLeader", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("Leader", StringComparison.OrdinalIgnoreCase) ||
            (ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank) &&
                rank is 1 or 100);
    }

    private static IReadOnlyList<(uint ItemId, ushort StackSize)> GetLegacyClanWarCurrentRewardItems(
        LegacyClanWarCurrentWinner winner)
    {
        var items = new List<(uint ItemId, ushort StackSize)>(LegacyClanWarCurrentBaseRewardItems);
        if (winner.RewardItemId != 0)
        {
            items.Add((winner.RewardItemId, 1));
        }

        return items;
    }

    private static IReadOnlyList<PlayerItemSnapshotItem> CreateLegacyClanWarCurrentRewardItems(
        WorldPacketHandlerContext context,
        PlayerItemSnapshot items,
        IReadOnlyList<(uint ItemId, ushort StackSize)> rewardDefinitions)
    {
        var nextUid = GetNextSessionItemUid(items);
        var grants = new PlayerItemSnapshotItem[rewardDefinitions.Count];
        for (var index = 0; index < grants.Length; index++)
        {
            var (itemId, stackSize) = rewardDefinitions[index];
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

    private static bool IsLegacyClanLeader(CharacterSnapshot character)
    {
        var raw = ReadAttributeString(
                character.Attributes,
                "ClanRank",
                "ClanMemberRank",
                "MyClanRank",
                "MyClanMemberRank",
                "LegacyClanRank")
            .Trim();
        return raw.Equals("ClanLeader", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("Leader", StringComparison.OrdinalIgnoreCase) ||
            (ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank) &&
                rank is 1 or 100);
    }

    private static bool IsLegacyClanWarCurrentRewardScopeEligible(CharacterSnapshot character, string city)
    {
        if (character.MinHitPoints <= 0 ||
            !TryResolveLegacyClanWarCurrentScorePanelScope(character, out var scope) ||
            !IsLegacyClanWarCityMatch(scope.City, city) ||
            character.MapId != scope.MapId)
        {
            return false;
        }

        return !scope.DynamicId.HasValue ||
            (TryReadLegacyClanWarCharacterDynamicId(character, out var dynamicId) &&
                dynamicId == scope.DynamicId.Value);
    }

    private static CharacterSnapshot WithLegacyClanWarCurrentRewardClaimedAttributes(
        CharacterSnapshot character,
        string city,
        LegacyClanWarCurrentWinner winner)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyClanWarCurrentLastRewardCity"] = city,
            ["ClanWarCurrentLastRewardCity"] = city,
            ["LegacyClanWarCurrentLastRewardWinnerClanID"] = winner.ClanId.ToString(CultureInfo.InvariantCulture),
            ["ClanWarCurrentLastRewardWinnerClanID"] = winner.ClanId.ToString(CultureInfo.InvariantCulture),
            ["LegacyClanWarCurrentLastRewardWinnerClanName"] = winner.ClanName,
            ["ClanWarCurrentLastRewardWinnerClanName"] = winner.ClanName,
            ["LegacyClanWarCurrentRewardItemID"] = winner.RewardItemId.ToString(CultureInfo.InvariantCulture),
            ["ClanWarCurrentRewardItemID"] = winner.RewardItemId.ToString(CultureInfo.InvariantCulture),
            ["LegacyClanWarCurrentSourceConquerPoints"] = LegacyClanWarCurrentSourceConquerPointsReward.ToString(CultureInfo.InvariantCulture),
            ["ClanWarCurrentSourceConquerPoints"] = LegacyClanWarCurrentSourceConquerPointsReward.ToString(CultureInfo.InvariantCulture),
            ["LegacyClanWarCurrentTunedConquerPoints"] = TunedLegacyClanWarCurrentConquerPointsReward.ToString(CultureInfo.InvariantCulture),
            ["ClanWarCurrentTunedConquerPoints"] = TunedLegacyClanWarCurrentConquerPointsReward.ToString(CultureInfo.InvariantCulture),
            ["LegacyClanWarCurrentStudyPoints"] = LegacyClanWarCurrentStudyPointsReward.ToString(CultureInfo.InvariantCulture),
            ["ClanWarCurrentStudyPoints"] = LegacyClanWarCurrentStudyPointsReward.ToString(CultureInfo.InvariantCulture),
            ["LegacyClanWarCurrentInnerPowerPotency"] = LegacyClanWarCurrentInnerPowerPotencyReward.ToString(CultureInfo.InvariantCulture),
            ["ClanWarCurrentInnerPowerPotency"] = LegacyClanWarCurrentInnerPowerPotencyReward.ToString(CultureInfo.InvariantCulture),
        };
        foreach (var key in GetLegacyClanWarCurrentRewardClaimedAttributeNames(city))
        {
            attributes[key] = "true";
        }

        return character with { Attributes = attributes };
    }

    private static async ValueTask<bool> TryApplyLegacyClanWarCurrentInnerPowerPotencyAwardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is null)
        {
            await LogLegacyClanWarCurrentInnerPowerDeferredAsync(context, session, "InnerPower.txt rows are not loaded")
                .ConfigureAwait(false);
            return false;
        }

        if (session.Character.Uid is < 0 or > int.MaxValue)
        {
            await LogLegacyClanWarCurrentInnerPowerDeferredAsync(
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
            await LogLegacyClanWarCurrentInnerPowerDeferredAsync(
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
                updatedPotency = checked(state.Potency + LegacyClanWarCurrentInnerPowerPotencyReward);
                var updatedRow = CreateLegacyInnerPowerRankEntry(row, state with { Potency = updatedPotency });
                applied = TryReplaceLegacyInnerPowerRow(rows, updatedRow, out deferReason);
            }
        }

        if (!applied)
        {
            await LogLegacyClanWarCurrentInnerPowerDeferredAsync(context, session, deferReason)
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

    private static async ValueTask LogLegacyClanWarCurrentInnerPowerDeferredAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "WorldServer deferred legacy ClanWar current-war InnerPower potency reward " +
                $"{FormatSession(context with { Session = session })}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask PersistLegacyClanWarCurrentRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string city,
        LegacyClanWarCurrentWinner winner,
        bool innerPowerApplied)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["SubProfInfo"] = ReadAttributeString(session.Character.Attributes, "SubProfInfo"),
            ["LegacyClanWarCurrentLastRewardCity"] = city,
            ["ClanWarCurrentLastRewardCity"] = city,
            ["LegacyClanWarCurrentLastRewardWinnerClanID"] = winner.ClanId.ToString(CultureInfo.InvariantCulture),
            ["ClanWarCurrentLastRewardWinnerClanID"] = winner.ClanId.ToString(CultureInfo.InvariantCulture),
            ["LegacyClanWarCurrentLastRewardWinnerClanName"] = winner.ClanName,
            ["ClanWarCurrentLastRewardWinnerClanName"] = winner.ClanName,
            ["LegacyClanWarCurrentRewardItemID"] = winner.RewardItemId.ToString(CultureInfo.InvariantCulture),
            ["ClanWarCurrentRewardItemID"] = winner.RewardItemId.ToString(CultureInfo.InvariantCulture),
            ["LegacyClanWarCurrentSourceConquerPoints"] = LegacyClanWarCurrentSourceConquerPointsReward.ToString(CultureInfo.InvariantCulture),
            ["ClanWarCurrentSourceConquerPoints"] = LegacyClanWarCurrentSourceConquerPointsReward.ToString(CultureInfo.InvariantCulture),
            ["LegacyClanWarCurrentTunedConquerPoints"] = TunedLegacyClanWarCurrentConquerPointsReward.ToString(CultureInfo.InvariantCulture),
            ["ClanWarCurrentTunedConquerPoints"] = TunedLegacyClanWarCurrentConquerPointsReward.ToString(CultureInfo.InvariantCulture),
            ["LegacyClanWarCurrentStudyPoints"] = LegacyClanWarCurrentStudyPointsReward.ToString(CultureInfo.InvariantCulture),
            ["ClanWarCurrentStudyPoints"] = LegacyClanWarCurrentStudyPointsReward.ToString(CultureInfo.InvariantCulture),
            ["LegacyClanWarCurrentInnerPowerPotency"] = LegacyClanWarCurrentInnerPowerPotencyReward.ToString(CultureInfo.InvariantCulture),
            ["ClanWarCurrentInnerPowerPotency"] = LegacyClanWarCurrentInnerPowerPotencyReward.ToString(CultureInfo.InvariantCulture),
            ["LegacyClanWarCurrentInnerPowerApplied"] = innerPowerApplied ? "true" : "false",
            ["ClanWarCurrentInnerPowerApplied"] = innerPowerApplied ? "true" : "false",
        };
        foreach (var key in GetLegacyClanWarCurrentRewardClaimedAttributeNames(city))
        {
            values[key] = "true";
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
                    "PARITY-GAP legacy ClanWar current-war reward persistence " +
                    $"{FormatSession(context)} source={LegacyClanWarCurrentRewardSourceMethod} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted legacy ClanWar current-war reward " +
                $"{FormatSession(context)} source={LegacyClanWarCurrentRewardSourceMethod} path={Path.GetFileName(path)} keys={string.Join(',', values.Keys)}.")
            .ConfigureAwait(false);
    }

    private static string[] GetLegacyClanWarCurrentRewardClaimedAttributeNames(string city)
    {
        return
        [
            "LegacyClanWarCurrent" + city + "RewardClaimed",
            "ClanWarCurrent" + city + "RewardClaimed",
            "RuntimeClanWarCurrent" + city + "RewardClaimed",
            "LegacyClanWar" + city + "RewardClaimed",
            "ClanWar" + city + "RewardClaimed",
            "RuntimeClanWar" + city + "RewardClaimed",
        ];
    }

    private static string FormatLegacyClanWarCurrentRewardItems(
        IReadOnlyList<(uint ItemId, ushort StackSize)> rewardDefinitions)
    {
        return string.Join(
            ',',
            rewardDefinitions.Select(item =>
                item.ItemId.ToString(CultureInfo.InvariantCulture) +
                "x" +
                item.StackSize.ToString(CultureInfo.InvariantCulture)));
    }

    private static CharacterSnapshot WithLegacyGuildWarScoreRewardTitles(CharacterSnapshot character)
    {
        var titleStates = ReadLegacySpecialTitleStates(character);
        foreach (var titleId in LegacyGuildWarScoreSpecialTitleRewards)
        {
            titleStates[titleId] = true;
        }

        var parts = new List<string> { titleStates.Count.ToString(CultureInfo.InvariantCulture) };
        foreach (var (titleId, active) in titleStates.OrderBy(pair => pair.Key))
        {
            parts.Add(titleId.ToString(CultureInfo.InvariantCulture));
            parts.Add(active ? "1" : "0");
        }

        var updated = WithCharacterAttribute(character, "SpecialTitles", string.Join(",", parts) + ",");
        updated = WithCharacterAttribute(
            updated,
            "SpecialTitleID",
            LegacyGuildWarScoreHandsomeTitleId.ToString(CultureInfo.InvariantCulture));
        updated = WithCharacterAttribute(
            updated,
            "SpecialWingID",
            LegacyGuildWarScoreStarlightWingsTitleId.ToString(CultureInfo.InvariantCulture));
        return WithCharacterAttribute(updated, "SpecialTitleScore", "0");
    }

    private static Dictionary<uint, bool> ReadLegacySpecialTitleStates(CharacterSnapshot character)
    {
        var titleStates = new Dictionary<uint, bool>();
        if (!character.Attributes.TryGetValue("SpecialTitles", out var raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return titleStates;
        }

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!TryGetLegacySpecialTitlePairRange(parts, out var startIndex, out var endIndex))
        {
            return titleStates;
        }

        for (var index = startIndex; index + 1 < endIndex; index += 2)
        {
            if (uint.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var titleId))
            {
                titleStates[titleId] = parts[index + 1] == "1";
            }
        }

        return titleStates;
    }

    private static CharacterSnapshot WithLegacyGuildWarScoreRewardClaimCleared(CharacterSnapshot character)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [LegacyGuildWarScoreRewardClaimedAttribute] = "true",
            ["GuildWarScoreRewardClaimed"] = "true",
            ["LegacyTopWarScoreRewardClaimed"] = "true",
            ["TopWarScoreRewardClaimed"] = "true",
            ["LegacyGuildWarScoreWinnerCleared"] = "true",
            ["GuildWarScoreWinnerCleared"] = "true",
            ["LegacyTopWarScoreWinnerCleared"] = "true",
            ["TopWarScoreWinnerCleared"] = "true",
        };
        attributes.Remove(LegacyGuildWarScoreScoresAttribute);
        attributes.Remove("LegacyGuildWarScoreScores");
        attributes.Remove("TopWarScoreScores");
        attributes.Remove("LegacyTopWarScoreScores");
        return character with { Attributes = attributes };
    }

    private static async ValueTask PersistLegacyClanWarScoreRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["SpecialTitles"] = ReadAttributeString(session.Character.Attributes, "SpecialTitles"),
            ["SpecialTitleID"] = ReadAttributeString(session.Character.Attributes, "SpecialTitleID"),
            ["SpecialWingID"] = ReadAttributeString(session.Character.Attributes, "SpecialWingID"),
            ["SpecialTitleScore"] = ReadAttributeString(session.Character.Attributes, "SpecialTitleScore"),
            [LegacyGuildWarScoreRewardClaimedAttribute] = "true",
            ["GuildWarScoreRewardClaimed"] = "true",
            ["LegacyTopWarScoreRewardClaimed"] = "true",
            ["TopWarScoreRewardClaimed"] = "true",
            ["LegacyGuildWarScoreWinnerCleared"] = "true",
            ["GuildWarScoreWinnerCleared"] = "true",
            ["LegacyTopWarScoreWinnerCleared"] = "true",
            ["TopWarScoreWinnerCleared"] = "true",
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
                    "PARITY-GAP LegacyNpc ClanWarScore reward persistence " +
                    $"{FormatSession(context)} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNpc ClanWarScore reward " +
                $"{FormatSession(context)} path={Path.GetFileName(path)} keys={string.Join(',', values.Keys)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyClanWarScoreRewardDialogAndLogAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string sourceMethod,
        string text,
        string reason,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [text],
                LegacyClanWarEventClosedOptions,
                LegacyClanWarScoreAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc ClanWarScore reward " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyClanWarScoreRewardBroadcastAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot winner,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket
            .SystemRedCenter(
                $"{winner.Name} has received {LegacyGuildWarScoreConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs for winning GuildWarScore.")
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
}
