using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyChampionsOfWarrConquerPointsReward = 150_000;
    private const uint LegacyChampionsOfWarrRewardItemId = 4_200_013;
    private const uint LegacyChampionsOfWarrChampionHaloTitleId = 8_030;
    private const uint LegacyChampionsOfWarrSirenSongTitleId = 6_017;
    private const uint LegacyChampionsOfWarrPromiseOfSkyTitleId = 2_235;
    private const uint LegacyChampionsOfWarrSlideCrossServerChatType = 2_401;
    private const string LegacyChampionsOfWarrRewardClaimedAttribute = "LegacyChampionsOfWarrRewardClaimed";

    private static readonly uint[] LegacyChampionsOfWarrSpecialTitleRewards =
    [
        LegacyChampionsOfWarrChampionHaloTitleId,
        LegacyChampionsOfWarrSirenSongTitleId,
        LegacyChampionsOfWarrPromiseOfSkyTitleId,
    ];

    private static async ValueTask TryClaimLegacyChampionsOfWarrRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (IsLegacyChampionsOfWarrProcessAlive(session.Character))
        {
            await SendLegacyChampionsOfWarrRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Please come back when Champions of War is finished.",
                    "skipped active MsgSchedules.ChampionsOfWarr.Proces",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (ReadAttributeBool(
                session.Character.Attributes,
                LegacyChampionsOfWarrRewardClaimedAttribute,
                "ChampionsOfWarrRewardClaimed",
                "LegacyChampionWarrRewardClaimed",
                "ChampionWarrRewardClaimed"))
        {
            await SendLegacyChampionsOfWarrRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "This Champions of War reward was already claimed.",
                    "already claimed",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyChampionsOfWarrWinnerGuildId(session.Character, out var winnerGuildId))
        {
            await SendLegacyChampionsOfWarrRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "There is no Champions of War reward available now.",
                    "skipped missing ChampionsOfWarr RoundOwner",
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
            await SendLegacyChampionsOfWarrRewardDialogAndLogAsync(
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
            await SendLegacyChampionsOfWarrRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Only the guild leader can claim the reward.",
                    "skipped non-guild-leader rank",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await SendLegacyChampionsOfWarrRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Please make one more space in your inventory.",
                    "skipped full inventory",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var grantedItem = CreateLegacyNpcItemGrant(
            context,
            session.PlayerItems,
            LegacyChampionsOfWarrRewardItemId);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat([grantedItem]).OrderBy(item => item.Uid).ToArray(),
        };
        var updatedCharacter = session.Character with
        {
            ConquerPoints = checked(session.Character.ConquerPoints + LegacyChampionsOfWarrConquerPointsReward),
        };
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "ConquerPoints",
            CheckedUInt64(updatedCharacter.ConquerPoints, nameof(updatedCharacter.ConquerPoints)));
        updatedCharacter = WithLegacyChampionsOfWarrRewardTitles(updatedCharacter);
        updatedCharacter = WithLegacyChampionsOfWarrRewardClaimCleared(updatedCharacter);

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
        foreach (var titleId in LegacyChampionsOfWarrSpecialTitleRewards)
        {
            await context.Connection.SendAsync(
                    LegacySpecialTitlePacket.FromTitleId(titleId).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await PersistLegacyChampionsOfWarrRewardAsync(updatedContext, updatedSession, sourceMethod)
            .ConfigureAwait(false);
        await PersistLegacyNpcItemsAsync(
                updatedContext,
                LegacyChampionsOfWarrRewardItemId,
                sourceMethod)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                sourceMethod,
                [$"You claimed {LegacyChampionsOfWarrConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs and item {LegacyChampionsOfWarrRewardItemId.ToString(CultureInfo.InvariantCulture)} for winning Champions of War."],
                LegacyPlayerWarEventCloseOptions,
                LegacyPlayerWarEventAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyChampionsOfWarrRewardBroadcastAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc ChampionsOfWarr reward " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                $"winnerGuildId={winnerGuildId.ToString(CultureInfo.InvariantCulture)} " +
                $"conquerPoints={LegacyChampionsOfWarrConquerPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"itemId={LegacyChampionsOfWarrRewardItemId.ToString(CultureInfo.InvariantCulture)} " +
                "specialTitles=8030,6017,2235 " +
                "sourceReward=NewSystem.PrizeAllTops.ChampionWar " +
                "sourceBroadcast=Center+SlideCrosTheServer.")
            .ConfigureAwait(false);
    }

    private static bool TryReadLegacyChampionsOfWarrWinnerGuildId(
        CharacterSnapshot character,
        out ulong winnerGuildId)
    {
        if (TryReadUInt64(
                character.Attributes,
                out winnerGuildId,
                "LegacyChampionsOfWarrRoundOwnerGuildID",
                "LegacyChampionsOfWarrRoundOwnerGuildId",
                "ChampionsOfWarrRoundOwnerGuildID",
                "ChampionsOfWarrRoundOwnerGuildId",
                "LegacyChampionWarrRoundOwnerGuildID",
                "ChampionWarrRoundOwnerGuildID") &&
            winnerGuildId != 0)
        {
            return true;
        }

        return TryReadLegacyChampionsOfWarrWinnerGuildIdFromScores(character, out winnerGuildId);
    }

    private static bool TryReadLegacyChampionsOfWarrWinnerGuildIdFromScores(
        CharacterSnapshot character,
        out ulong winnerGuildId)
    {
        winnerGuildId = 0;
        var raw = ReadAttributeString(
            character.Attributes,
            LegacyChampionsOfWarrScoresAttribute,
            "LegacyChampionsOfWarrScores",
            "ChampionWarrScores",
            "LegacyChampionWarrScores");
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

    private static CharacterSnapshot WithLegacyChampionsOfWarrRewardTitles(CharacterSnapshot character)
    {
        var titleStates = ReadLegacySpecialTitleStates(character);
        foreach (var titleId in LegacyChampionsOfWarrSpecialTitleRewards)
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
            LegacyChampionsOfWarrPromiseOfSkyTitleId.ToString(CultureInfo.InvariantCulture));
        updated = WithCharacterAttribute(
            updated,
            "SpecialWingID",
            LegacyChampionsOfWarrSirenSongTitleId.ToString(CultureInfo.InvariantCulture));
        return WithCharacterAttribute(updated, "SpecialTitleScore", "0");
    }

    private static CharacterSnapshot WithLegacyChampionsOfWarrRewardClaimCleared(CharacterSnapshot character)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [LegacyChampionsOfWarrRewardClaimedAttribute] = "true",
            ["ChampionsOfWarrRewardClaimed"] = "true",
            ["LegacyChampionWarrRewardClaimed"] = "true",
            ["ChampionWarrRewardClaimed"] = "true",
            ["LegacyChampionsOfWarrWinnerCleared"] = "true",
            ["ChampionsOfWarrWinnerCleared"] = "true",
            ["LegacyChampionWarrWinnerCleared"] = "true",
            ["ChampionWarrWinnerCleared"] = "true",
        };
        attributes.Remove(LegacyChampionsOfWarrScoresAttribute);
        attributes.Remove("LegacyChampionsOfWarrScores");
        attributes.Remove("ChampionWarrScores");
        attributes.Remove("LegacyChampionWarrScores");
        attributes.Remove("LegacyChampionsOfWarrRoundOwnerGuildID");
        attributes.Remove("LegacyChampionsOfWarrRoundOwnerGuildId");
        attributes.Remove("ChampionsOfWarrRoundOwnerGuildID");
        attributes.Remove("ChampionsOfWarrRoundOwnerGuildId");
        attributes.Remove("LegacyChampionWarrRoundOwnerGuildID");
        attributes.Remove("ChampionWarrRoundOwnerGuildID");
        attributes.Remove("LegacyChampionsOfWarrRoundOwnerGuildName");
        attributes.Remove("ChampionsOfWarrRoundOwnerGuildName");
        attributes.Remove("LegacyChampionWarrRoundOwnerGuildName");
        attributes.Remove("ChampionWarrRoundOwnerGuildName");
        attributes.Remove("LegacyChampionsOfWarrRoundOwnerScore");
        attributes.Remove("ChampionsOfWarrRoundOwnerScore");
        attributes.Remove("LegacyChampionWarrRoundOwnerScore");
        attributes.Remove("ChampionWarrRoundOwnerScore");
        return character with { Attributes = attributes };
    }

    private static async ValueTask PersistLegacyChampionsOfWarrRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["SpecialTitles"] = ReadAttributeString(session.Character.Attributes, "SpecialTitles"),
            ["SpecialTitleID"] = ReadAttributeString(session.Character.Attributes, "SpecialTitleID"),
            ["SpecialWingID"] = ReadAttributeString(session.Character.Attributes, "SpecialWingID"),
            ["SpecialTitleScore"] = ReadAttributeString(session.Character.Attributes, "SpecialTitleScore"),
            [LegacyChampionsOfWarrRewardClaimedAttribute] = "true",
            ["ChampionsOfWarrRewardClaimed"] = "true",
            ["LegacyChampionWarrRewardClaimed"] = "true",
            ["ChampionWarrRewardClaimed"] = "true",
            ["LegacyChampionsOfWarrWinnerCleared"] = "true",
            ["ChampionsOfWarrWinnerCleared"] = "true",
            ["LegacyChampionWarrWinnerCleared"] = "true",
            ["ChampionWarrWinnerCleared"] = "true",
            ["LegacyChampionsOfWarrRoundOwnerGuildID"] = "0",
            ["LegacyChampionsOfWarrRoundOwnerGuildId"] = "0",
            ["ChampionsOfWarrRoundOwnerGuildID"] = "0",
            ["ChampionsOfWarrRoundOwnerGuildId"] = "0",
            ["LegacyChampionWarrRoundOwnerGuildID"] = "0",
            ["ChampionWarrRoundOwnerGuildID"] = "0",
            ["LegacyChampionsOfWarrRoundOwnerGuildName"] = "0",
            ["ChampionsOfWarrRoundOwnerGuildName"] = "0",
            ["LegacyChampionWarrRoundOwnerGuildName"] = "0",
            ["ChampionWarrRoundOwnerGuildName"] = "0",
            ["LegacyChampionsOfWarrRoundOwnerScore"] = "0",
            ["ChampionsOfWarrRoundOwnerScore"] = "0",
            ["LegacyChampionWarrRoundOwnerScore"] = "0",
            ["ChampionWarrRoundOwnerScore"] = "0",
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
                    "PARITY-GAP LegacyNpc ChampionsOfWarr reward persistence " +
                    $"{FormatSession(context)} source={sourceMethod} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNpc ChampionsOfWarr reward " +
                $"{FormatSession(context)} source={sourceMethod} path={Path.GetFileName(path)} keys={string.Join(',', values.Keys)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyChampionsOfWarrRewardDialogAndLogAsync(
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
                LegacyPlayerWarEventCloseOptions,
                LegacyPlayerWarEventAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc ChampionsOfWarr reward " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyChampionsOfWarrRewardBroadcastAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot winner,
        CancellationToken cancellationToken)
    {
        var message =
            $"{winner.Name} has received {LegacyChampionsOfWarrConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs for winning ChampionsOfWar.";
        var centerFrame = LegacyMsgMessagePacket.SystemRedCenter(message).ToFrameBytes();
        var slideFrame = new LegacyMsgMessagePacket(
                LegacyMsgMessagePacket.RedColor,
                LegacyChampionsOfWarrSlideCrossServerChatType,
                MessageUid1: 0,
                MessageUid2: 0,
                ServerId: 0,
                Mesh: 0,
                UnionType: 0,
                Count: 1,
                New1: 0,
                New2: 0,
                ["SYSTEM", "ALL", string.Empty, message, string.Empty, string.Empty, string.Empty])
            .ToFrameBytes();
        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is null || activeSessions.Count == 0)
        {
            await context.Connection.SendAsync(centerFrame, cancellationToken).ConfigureAwait(false);
            await context.Connection.SendAsync(slideFrame, cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (var active in activeSessions)
        {
            await active.Connection.SendAsync(centerFrame, cancellationToken).ConfigureAwait(false);
            await active.Connection.SendAsync(slideFrame, cancellationToken).ConfigureAwait(false);
        }
    }
}
