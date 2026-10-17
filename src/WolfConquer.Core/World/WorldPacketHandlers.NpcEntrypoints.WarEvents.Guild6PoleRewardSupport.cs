using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryReadLegacyGuild6PoleWarWinnerGuildId(
        CharacterSnapshot character,
        string scheduleName,
        out ulong winnerGuildId)
    {
        if (TryReadUInt64(
                character.Attributes,
                out winnerGuildId,
                GetLegacyGuild6PoleWarScheduleAttributeName("Legacy" + scheduleName, "RoundOwnerGuildID"),
                GetLegacyGuild6PoleWarScheduleAttributeName("Legacy" + scheduleName, "RoundOwnerGuildId"),
                GetLegacyGuild6PoleWarScheduleAttributeName(scheduleName, "RoundOwnerGuildID"),
                GetLegacyGuild6PoleWarScheduleAttributeName(scheduleName, "RoundOwnerGuildId"),
                GetLegacyGuild6PoleWarScheduleAttributeName("Runtime" + scheduleName, "RoundOwnerGuildID"),
                GetLegacyGuild6PoleWarScheduleAttributeName("Runtime" + scheduleName, "RoundOwnerGuildId")) &&
            winnerGuildId != 0)
        {
            return true;
        }

        return TryReadLegacyGuild6PoleWarWinnerGuildIdFromScores(character, scheduleName, out winnerGuildId);
    }

    private static bool TryReadLegacyGuild6PoleWarWinnerGuildIdFromScores(
        CharacterSnapshot character,
        string scheduleName,
        out ulong winnerGuildId)
    {
        winnerGuildId = 0;
        var raw = ReadAttributeString(
            character.Attributes,
            LegacyGuild6PoleWarScoresAttribute,
            "LegacyGuild6PoleWarScores");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        ulong bestScore = 0;
        foreach (var entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = entry.Split(',', StringSplitOptions.TrimEntries);
            if (fields.Length < 3 ||
                !fields[0].Equals(scheduleName, StringComparison.Ordinal) ||
                !ulong.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var guildId) ||
                !ulong.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score) ||
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

    private static CharacterSnapshot WithLegacyGuild6PoleWarRewardTitles(CharacterSnapshot character)
    {
        var titleStates = ReadLegacySpecialTitleStates(character);
        foreach (var titleId in LegacyGuild6PoleWarSpecialTitleRewards)
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
            LegacyGuild6PoleWarScholarTitleId.ToString(CultureInfo.InvariantCulture));
        updated = WithCharacterAttribute(
            updated,
            "SpecialWingID",
            LegacyGuild6PoleWarRadiantWingsTitleId.ToString(CultureInfo.InvariantCulture));
        return WithCharacterAttribute(updated, "SpecialTitleScore", "0");
    }

    private static CharacterSnapshot WithLegacyGuild6PoleWarRewardClaimCleared(
        CharacterSnapshot character,
        string scheduleName,
        int poleNumber)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [GetLegacyGuild6PoleWarScheduleAttributeName("Legacy" + scheduleName, "RewardClaimed")] = "true",
            [GetLegacyGuild6PoleWarScheduleAttributeName(scheduleName, "RewardClaimed")] = "true",
            [GetLegacyGuild6PoleWarScheduleAttributeName("Runtime" + scheduleName, "RewardClaimed")] = "true",
            [$"GuildPole{poleNumber.ToString(CultureInfo.InvariantCulture)}WarRewardClaimed"] = "true",
            [GetLegacyGuild6PoleWarScheduleAttributeName("Legacy" + scheduleName, "WinnerCleared")] = "true",
            [GetLegacyGuild6PoleWarScheduleAttributeName(scheduleName, "WinnerCleared")] = "true",
            [GetLegacyGuild6PoleWarScheduleAttributeName("Runtime" + scheduleName, "WinnerCleared")] = "true",
        };

        RemoveLegacyGuild6PoleWarScheduleScores(attributes, scheduleName);
        RemoveLegacyGuild6PoleWarRoundOwnerAttributes(attributes, scheduleName);
        return character with { Attributes = attributes };
    }

    private static async ValueTask PersistLegacyGuild6PoleWarRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyGuild6PoleWarSchedule schedule)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["Money"] = session.Character.Money.ToString(CultureInfo.InvariantCulture),
            ["SpecialTitles"] = ReadAttributeString(session.Character.Attributes, "SpecialTitles"),
            ["SpecialTitleID"] = ReadAttributeString(session.Character.Attributes, "SpecialTitleID"),
            ["SpecialWingID"] = ReadAttributeString(session.Character.Attributes, "SpecialWingID"),
            ["SpecialTitleScore"] = ReadAttributeString(session.Character.Attributes, "SpecialTitleScore"),
            [GetLegacyGuild6PoleWarScheduleAttributeName("Legacy" + schedule.ScheduleName, "RewardClaimed")] = "true",
            [GetLegacyGuild6PoleWarScheduleAttributeName(schedule.ScheduleName, "RewardClaimed")] = "true",
            [GetLegacyGuild6PoleWarScheduleAttributeName("Runtime" + schedule.ScheduleName, "RewardClaimed")] = "true",
            [$"GuildPole{schedule.PoleNumber.ToString(CultureInfo.InvariantCulture)}WarRewardClaimed"] = "true",
            [GetLegacyGuild6PoleWarScheduleAttributeName("Legacy" + schedule.ScheduleName, "WinnerCleared")] = "true",
            [GetLegacyGuild6PoleWarScheduleAttributeName(schedule.ScheduleName, "WinnerCleared")] = "true",
            [GetLegacyGuild6PoleWarScheduleAttributeName("Runtime" + schedule.ScheduleName, "WinnerCleared")] = "true",
        };

        foreach (var key in EnumerateLegacyGuild6PoleWarRoundOwnerAttributeNames(schedule.ScheduleName))
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
                    "PARITY-GAP LegacyNpc Guild6PoleWar reward persistence " +
                    $"{FormatSession(context)} source={LegacyGuild6PoleWarSourceMethod} " +
                    $"schedule={schedule.ScheduleName} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNpc Guild6PoleWar reward " +
                $"{FormatSession(context)} source={LegacyGuild6PoleWarSourceMethod} " +
                $"schedule={schedule.ScheduleName} path={Path.GetFileName(path)} keys={string.Join(',', values.Keys)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyGuild6PoleWarRewardDialogAndLogAsync(
        WorldPacketHandlerContext context,
        LegacyGuild6PoleWarSchedule schedule,
        byte optionId,
        string text,
        string reason,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyGuild6PoleWarSourceMethod,
                [text],
                LegacyPoleWarEventOkayOptions,
                LegacyGuild6PoleWarAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc Guild6PoleWar reward " +
                $"{FormatSession(context)} source={LegacyGuild6PoleWarSourceMethod} option={optionId} " +
                $"schedule={schedule.ScheduleName}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyGuild6PoleWarRewardBroadcastAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot winner,
        LegacyGuild6PoleWarSchedule schedule,
        CancellationToken cancellationToken)
    {
        var pole = schedule.PoleNumber.ToString(CultureInfo.InvariantCulture);
        var message =
            $"{winner.Name} has received {LegacyGuild6PoleWarConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs for winning GuildPole[{pole}].";
        var centerFrame = LegacyMsgMessagePacket.SystemRedCenter(message).ToFrameBytes();
        var slideFrame = new LegacyMsgMessagePacket(
                LegacyMsgMessagePacket.RedColor,
                LegacyGuild6PoleWarSlideCrossServerChatType,
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

    private static void RemoveLegacyGuild6PoleWarRoundOwnerAttributes(
        IDictionary<string, string> attributes,
        string scheduleName)
    {
        foreach (var key in EnumerateLegacyGuild6PoleWarRoundOwnerAttributeNames(scheduleName))
        {
            attributes.Remove(key);
        }
    }

    private static IEnumerable<string> EnumerateLegacyGuild6PoleWarRoundOwnerAttributeNames(string scheduleName)
    {
        foreach (var prefix in new[] { "Legacy" + scheduleName, scheduleName, "Runtime" + scheduleName })
        {
            yield return GetLegacyGuild6PoleWarScheduleAttributeName(prefix, "RoundOwnerGuildID");
            yield return GetLegacyGuild6PoleWarScheduleAttributeName(prefix, "RoundOwnerGuildId");
            yield return GetLegacyGuild6PoleWarScheduleAttributeName(prefix, "RoundOwnerGuildName");
            yield return GetLegacyGuild6PoleWarScheduleAttributeName(prefix, "RoundOwnerScore");
        }
    }

    private static void RemoveLegacyGuild6PoleWarScheduleScores(
        IDictionary<string, string> attributes,
        string scheduleName)
    {
        if (!attributes.TryGetValue(LegacyGuild6PoleWarScoresAttribute, out var raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            attributes.Remove("LegacyGuild6PoleWarScores");
            return;
        }

        var remaining = raw
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(entry =>
            {
                var fields = entry.Split(',', StringSplitOptions.TrimEntries);
                return fields.Length == 0 || !fields[0].Equals(scheduleName, StringComparison.Ordinal);
            })
            .ToArray();
        if (remaining.Length == 0)
        {
            attributes.Remove(LegacyGuild6PoleWarScoresAttribute);
            attributes.Remove("LegacyGuild6PoleWarScores");
            return;
        }

        attributes[LegacyGuild6PoleWarScoresAttribute] = string.Join(';', remaining);
        attributes["LegacyGuild6PoleWarScores"] = attributes[LegacyGuild6PoleWarScoresAttribute];
    }

    private static string GetLegacyGuild6PoleWarScheduleAttributeName(string scheduleName, string suffix)
    {
        return scheduleName + suffix;
    }
}
