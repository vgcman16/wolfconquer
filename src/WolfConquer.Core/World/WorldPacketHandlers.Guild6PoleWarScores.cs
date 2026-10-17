using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyGuild6PoleWarFirstRightCornerChatType = 2_108;
    private const uint LegacyGuild6PoleWarContinueRightCornerChatType = 2_109;
    private const string LegacyGuild6PoleWarScoresAttribute = "Guild6PoleWarScores";

    private static readonly LegacyGuild6PoleWarScoreStateStore LegacyGuild6PoleWarStates = new();

    private static bool TryApplyLegacyGuild6PoleWarPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyGuild6PoleWarScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            !LegacySobNpcEventRules.TryGetGuild6PoleWarPoleSchedule(sobNpc, out var scheduleName, out _) ||
            !IsLegacyGuild6PoleWarProcessAlive(session.Character, scheduleName))
        {
            return false;
        }

        var guildId = ReadAttributeUInt64(session.Character.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            return false;
        }

        var guildName = ResolveLegacyGuild6PoleWarGuildName(session, guildId);
        if (guildName.Length == 0)
        {
            return false;
        }

        updatedScore = LegacyGuild6PoleWarStates.UpdateScore(
            scheduleName,
            guildId,
            guildName,
            damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyGuild6PoleWarScoresAttribute(
                session.Character,
                LegacyGuild6PoleWarStates.SnapshotScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyGuild6PoleWarPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyGuild6PoleWarPoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !LegacySobNpcEventRules.TryGetGuild6PoleWarPoleSchedule(sobNpc, out var scheduleName, out _) ||
            !TryApplyLegacyGuild6PoleWarPoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out _))
        {
            return false;
        }

        var scores = LegacyGuild6PoleWarStates.SnapshotScores(scheduleName);
        if (scores.Count == 0)
        {
            return false;
        }

        var roundOwner = scores[0];
        var scorePanelFrames = CreateLegacyGuild6PoleWarScorePanelFrames(scores);
        LegacyGuild6PoleWarStates.Clear(scheduleName);
        updatedSession = scoredSession with
        {
            Character = WithLegacyGuild6PoleWarFinishRoundAttributes(
                WithLegacyGuild6PoleWarScoresAttribute(
                    scoredSession.Character,
                    LegacyGuild6PoleWarStates.SnapshotScores()),
                roundOwner,
                roundOwner.GuildName),
        };
        finishRound = new LegacyGuild6PoleWarPoleFinishRoundResult(
            scheduleName,
            roundOwner,
            ResetPoleName: roundOwner.GuildName,
            scorePanelFrames);
        return true;
    }

    internal static void ResetLegacyGuild6PoleWarScoreStateForTests()
    {
        LegacyGuild6PoleWarStates.Clear();
    }

    private static IReadOnlyList<byte[]> CreateLegacyGuild6PoleWarScorePanelFrames(
        string scheduleName)
    {
        var scores = LegacyGuild6PoleWarStates.SnapshotScores(scheduleName);
        return CreateLegacyGuild6PoleWarScorePanelFrames(scores);
    }

    private static IReadOnlyList<byte[]> CreateLegacyGuild6PoleWarScorePanelFrames(
        IReadOnlyList<LegacyGuild6PoleWarScoreEntry> scores)
    {
        if (scores.Count == 0)
        {
            return [];
        }

        return scores
            .Take(5)
            .Select((score, index) => CreateLegacyGuild6PoleWarScorePanelFrame(index, score))
            .ToArray();
    }

    private static byte[] CreateLegacyGuild6PoleWarScorePanelFrame(
        int index,
        LegacyGuild6PoleWarScoreEntry score)
    {
        var rank = index + 1;
        var chatType = index == 0
            ? LegacyGuild6PoleWarFirstRightCornerChatType
            : LegacyGuild6PoleWarContinueRightCornerChatType;
        var message =
            $"No {rank.ToString(CultureInfo.InvariantCulture)}. {score.GuildName} ({score.Score.ToString(CultureInfo.InvariantCulture)})";
        return new LegacyMsgMessagePacket(
            LegacyMsgMessagePacket.YellowColor,
            chatType,
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
    }

    private static async ValueTask SendLegacyGuild6PoleWarScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        string scheduleName,
        CancellationToken cancellationToken)
    {
        var frames = CreateLegacyGuild6PoleWarScorePanelFrames(scheduleName);
        await SendLegacyGuild6PoleWarScorePanelFramesAsync(context, frames, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyGuild6PoleWarScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<byte[]> frames,
        CancellationToken cancellationToken)
    {
        if (frames.Count == 0)
        {
            return;
        }

        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is null || activeSessions.Count == 0)
        {
            foreach (var frame in frames)
            {
                await context.Connection.SendAsync(frame, cancellationToken)
                    .ConfigureAwait(false);
            }

            return;
        }

        foreach (var active in activeSessions)
        {
            if (active.Session.Character.MapId != LegacySobNpcEventRules.Guild6PoleWarMapId)
            {
                continue;
            }

            foreach (var frame in frames)
            {
                await active.Connection.SendAsync(frame, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask SendLegacyGuild6PoleWarFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyGuild6PoleWarPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket
            .SystemRedCenter($"The Round Ownered by guild {finishRound.Winner.GuildName}.")
            .ToFrameBytes();
        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is null || activeSessions.Count == 0)
        {
            await context.Connection.SendAsync(frame, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        foreach (var active in activeSessions)
        {
            await active.Connection.SendAsync(frame, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static bool IsLegacyGuild6PoleWarProcessAlive(
        CharacterSnapshot character,
        string scheduleName)
    {
        var process = ReadAttributeString(
                character.Attributes,
                "Legacy" + scheduleName + "Process",
                "Legacy" + scheduleName + "Proces",
                scheduleName + "Process",
                scheduleName + "Proces",
                "Runtime" + scheduleName + "Process",
                "Runtime" + scheduleName + "Proces")
            .Trim();
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveLegacyGuild6PoleWarGuildName(WorldSession session, ulong guildId)
    {
        var guildName = ReadAttributeString(session.Character.Attributes, "GuildName", "LegacyGuildName").Trim();
        if (guildName.Length == 0 &&
            guildId <= uint.MaxValue &&
            TryGetLegacyGuildInfo(session, (uint)guildId, out var guild) &&
            !string.IsNullOrWhiteSpace(guild.Name))
        {
            guildName = guild.Name.Trim();
        }

        return guildName;
    }

    private static CharacterSnapshot WithLegacyGuild6PoleWarScoresAttribute(
        CharacterSnapshot character,
        IReadOnlyList<LegacyGuild6PoleWarScoreEntry> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyGuild6PoleWarScoresAttribute);
        attributes.Remove("LegacyGuild6PoleWarScores");
        if (scores.Count > 0)
        {
            attributes[LegacyGuild6PoleWarScoresAttribute] = string.Join(
                ';',
                scores.Select(score => string.Join(
                    ',',
                    score.ScheduleName,
                    score.GuildId.ToString(CultureInfo.InvariantCulture),
                    score.Score.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(score.GuildName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyGuild6PoleWarFinishRoundAttributes(
        CharacterSnapshot character,
        LegacyGuild6PoleWarScoreEntry roundOwner,
        string resetPoleName)
    {
        var guildId = roundOwner.GuildId.ToString(CultureInfo.InvariantCulture);
        var score = roundOwner.Score.ToString(CultureInfo.InvariantCulture);
        var scheduleName = roundOwner.ScheduleName;
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["Legacy" + scheduleName + "RoundOwnerGuildID"] = guildId,
            ["Legacy" + scheduleName + "RoundOwnerGuildId"] = guildId,
            [scheduleName + "RoundOwnerGuildID"] = guildId,
            [scheduleName + "RoundOwnerGuildId"] = guildId,
            ["Runtime" + scheduleName + "RoundOwnerGuildID"] = guildId,
            ["Runtime" + scheduleName + "RoundOwnerGuildId"] = guildId,
            ["Legacy" + scheduleName + "RoundOwnerGuildName"] = roundOwner.GuildName,
            [scheduleName + "RoundOwnerGuildName"] = roundOwner.GuildName,
            ["Runtime" + scheduleName + "RoundOwnerGuildName"] = roundOwner.GuildName,
            ["Legacy" + scheduleName + "RoundOwnerScore"] = score,
            [scheduleName + "RoundOwnerScore"] = score,
            ["Runtime" + scheduleName + "RoundOwnerScore"] = score,
            ["Legacy" + scheduleName + "PoleOwnerName"] = resetPoleName,
            [scheduleName + "PoleOwnerName"] = resetPoleName,
            ["Runtime" + scheduleName + "PoleOwnerName"] = resetPoleName,
            ["Legacy" + scheduleName + "PoleReset"] = "true",
            [scheduleName + "PoleReset"] = "true",
            ["Runtime" + scheduleName + "PoleReset"] = "true",
        };
        return character with { Attributes = attributes };
    }

    private readonly record struct LegacyGuild6PoleWarPoleFinishRoundResult(
        string ScheduleName,
        LegacyGuild6PoleWarScoreEntry Winner,
        string ResetPoleName,
        IReadOnlyList<byte[]> ScorePanelFrames);
}

internal sealed class LegacyGuild6PoleWarScoreStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<LegacyGuild6PoleWarScoreKey, LegacyGuild6PoleWarScoreEntry> _scores = [];

    public LegacyGuild6PoleWarScoreEntry UpdateScore(
        string scheduleName,
        ulong guildId,
        string guildName,
        uint damage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheduleName);
        if (guildId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(guildId), guildId, "Guild id must be non-zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(guildName);

        var key = new LegacyGuild6PoleWarScoreKey(scheduleName, guildId);
        lock (_sync)
        {
            if (_scores.TryGetValue(key, out var existing))
            {
                var updated = existing with
                {
                    GuildName = guildName,
                    Score = SaturatingAdd(existing.Score, damage),
                };
                _scores[key] = updated;
                return updated;
            }

            var created = new LegacyGuild6PoleWarScoreEntry(scheduleName, guildId, guildName, damage);
            _scores[key] = created;
            return created;
        }
    }

    public IReadOnlyList<LegacyGuild6PoleWarScoreEntry> SnapshotScores()
    {
        lock (_sync)
        {
            return _scores.Values
                .OrderBy(entry => entry.ScheduleName, StringComparer.Ordinal)
                .ThenByDescending(entry => entry.Score)
                .ThenBy(entry => entry.GuildId)
                .ToArray();
        }
    }

    public IReadOnlyList<LegacyGuild6PoleWarScoreEntry> SnapshotScores(string scheduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheduleName);

        lock (_sync)
        {
            return _scores.Values
                .Where(entry => entry.ScheduleName.Equals(scheduleName, StringComparison.Ordinal))
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.GuildId)
                .ToArray();
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _scores.Clear();
        }
    }

    public void Clear(string scheduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheduleName);

        lock (_sync)
        {
            foreach (var key in _scores.Keys
                .Where(key => key.ScheduleName.Equals(scheduleName, StringComparison.Ordinal))
                .ToArray())
            {
                _scores.Remove(key);
            }
        }
    }

    private static uint SaturatingAdd(uint left, uint right)
    {
        return left > uint.MaxValue - right ? uint.MaxValue : left + right;
    }

    private readonly record struct LegacyGuild6PoleWarScoreKey(
        string ScheduleName,
        ulong GuildId);
}

internal readonly record struct LegacyGuild6PoleWarScoreEntry(
    string ScheduleName,
    ulong GuildId,
    string GuildName,
    uint Score);
