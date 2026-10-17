using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyTrepilWarFirstRightCornerChatType = 2_108;
    private const uint LegacyTrepilWarContinueRightCornerChatType = 2_109;
    private const string LegacyTrepilWarScoresAttribute = "TrepilWarScores";

    private static readonly LegacyTrepilWarScoreStateStore LegacyTrepilWarStates = new();

    private static bool TryApplyLegacyTrepilWarPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyTrepilWarScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            sobNpc.MapId != LegacySobNpcEventRules.TrepilWarMapId ||
            !LegacySobNpcEventRules.IsTrepilWarPole(sobNpc) ||
            !IsLegacyTrepilWarProcessAlive(session.Character))
        {
            return false;
        }

        var guildId = ReadAttributeUInt64(session.Character.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            return false;
        }

        var guildName = ResolveLegacyTrepilWarGuildName(session, guildId);
        if (guildName.Length == 0)
        {
            return false;
        }

        updatedScore = LegacyTrepilWarStates.UpdateScore(guildId, guildName, damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyTrepilWarScoresAttribute(
                session.Character,
                SnapshotLegacyTrepilWarScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyTrepilWarPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyTrepilWarPoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !TryApplyLegacyTrepilWarPoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out _))
        {
            return false;
        }

        var scores = SnapshotLegacyTrepilWarScores();
        if (scores.Count == 0)
        {
            return false;
        }

        var roundOwner = scores[0];
        var scorePanelFrames = CreateLegacyTrepilWarScorePanelFrames(scores);
        LegacyTrepilWarStates.Clear();
        updatedSession = scoredSession with
        {
            Character = WithLegacyTrepilWarFinishRoundAttributes(
                WithLegacyTrepilWarScoresAttribute(
                    scoredSession.Character,
                    SnapshotLegacyTrepilWarScores()),
                roundOwner,
                roundOwner.GuildName),
        };
        finishRound = new LegacyTrepilWarPoleFinishRoundResult(
            roundOwner,
            ResetPoleName: roundOwner.GuildName,
            scorePanelFrames);
        return true;
    }

    internal static void ResetLegacyTrepilWarScoreStateForTests()
    {
        LegacyTrepilWarStates.Clear();
    }

    private static IReadOnlyList<LegacyTrepilWarScoreEntry> SnapshotLegacyTrepilWarScores()
    {
        return LegacyTrepilWarStates.SnapshotScores()
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.GuildId)
            .ToArray();
    }

    private static IReadOnlyList<byte[]> CreateLegacyTrepilWarScorePanelFrames()
    {
        var scores = SnapshotLegacyTrepilWarScores();
        return CreateLegacyTrepilWarScorePanelFrames(scores);
    }

    private static IReadOnlyList<byte[]> CreateLegacyTrepilWarScorePanelFrames(
        IReadOnlyList<LegacyTrepilWarScoreEntry> scores)
    {
        if (scores.Count == 0)
        {
            return [];
        }

        return scores
            .Take(5)
            .Select((score, index) => CreateLegacyTrepilWarScorePanelFrame(index, score))
            .ToArray();
    }

    private static byte[] CreateLegacyTrepilWarScorePanelFrame(
        int index,
        LegacyTrepilWarScoreEntry score)
    {
        var rank = index + 1;
        var chatType = index == 0
            ? LegacyTrepilWarFirstRightCornerChatType
            : LegacyTrepilWarContinueRightCornerChatType;
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

    private static async ValueTask SendLegacyTrepilWarScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        var frames = CreateLegacyTrepilWarScorePanelFrames();
        await SendLegacyTrepilWarScorePanelFramesAsync(context, frames, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyTrepilWarScorePanelFramesAsync(
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
            if (active.Session.Character.MapId != LegacySobNpcEventRules.TrepilWarMapId)
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

    private static async ValueTask SendLegacyTrepilWarFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyTrepilWarPoleFinishRoundResult finishRound,
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

    private static bool IsLegacyTrepilWarProcessAlive(CharacterSnapshot character)
    {
        var process = ReadAttributeString(
                character.Attributes,
                "LegacyMsgTrepilWarProcess",
                "MsgTrepilWarProcess",
                "RuntimeMsgTrepilWarProcess",
                "LegacyTrepilWarProcess",
                "TrepilWarProcess",
                "RuntimeTrepilWarProcess",
                "LegacyMsgTrepilWarProces",
                "MsgTrepilWarProces",
                "RuntimeMsgTrepilWarProces",
                "LegacyTrepilWarProces",
                "TrepilWarProces",
                "RuntimeTrepilWarProces")
            .Trim();
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) ||
            process == "1";
    }

    private static string ResolveLegacyTrepilWarGuildName(WorldSession session, ulong guildId)
    {
        var guildName = ReadAttributeString(
                session.Character.Attributes,
                "GuildName",
                "LegacyGuildName",
                "RuntimeGuildName")
            .Trim();
        if (guildName.Length == 0 &&
            guildId <= uint.MaxValue &&
            TryGetLegacyGuildInfo(session, (uint)guildId, out var guild) &&
            !string.IsNullOrWhiteSpace(guild.Name))
        {
            guildName = guild.Name.Trim();
        }

        return guildName;
    }

    private static CharacterSnapshot WithLegacyTrepilWarScoresAttribute(
        CharacterSnapshot character,
        IReadOnlyList<LegacyTrepilWarScoreEntry> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyTrepilWarScoresAttribute);
        attributes.Remove("LegacyTrepilWarScores");
        if (scores.Count > 0)
        {
            attributes[LegacyTrepilWarScoresAttribute] = string.Join(
                ';',
                scores.Select(score => string.Join(
                    ',',
                    score.GuildId.ToString(CultureInfo.InvariantCulture),
                    score.Score.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(score.GuildName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyTrepilWarFinishRoundAttributes(
        CharacterSnapshot character,
        LegacyTrepilWarScoreEntry roundOwner,
        string resetPoleName)
    {
        var guildId = roundOwner.GuildId.ToString(CultureInfo.InvariantCulture);
        var score = roundOwner.Score.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyMsgTrepilWarRoundOwnerGuildID"] = guildId,
            ["LegacyMsgTrepilWarRoundOwnerGuildId"] = guildId,
            ["MsgTrepilWarRoundOwnerGuildID"] = guildId,
            ["MsgTrepilWarRoundOwnerGuildId"] = guildId,
            ["LegacyTrepilWarRoundOwnerGuildID"] = guildId,
            ["LegacyTrepilWarRoundOwnerGuildId"] = guildId,
            ["TrepilWarRoundOwnerGuildID"] = guildId,
            ["TrepilWarRoundOwnerGuildId"] = guildId,
            ["LegacyMsgTrepilWarRoundOwnerGuildName"] = roundOwner.GuildName,
            ["MsgTrepilWarRoundOwnerGuildName"] = roundOwner.GuildName,
            ["LegacyTrepilWarRoundOwnerGuildName"] = roundOwner.GuildName,
            ["TrepilWarRoundOwnerGuildName"] = roundOwner.GuildName,
            ["LegacyMsgTrepilWarRoundOwnerScore"] = score,
            ["MsgTrepilWarRoundOwnerScore"] = score,
            ["LegacyTrepilWarRoundOwnerScore"] = score,
            ["TrepilWarRoundOwnerScore"] = score,
            ["LegacyMsgTrepilWarPoleOwnerName"] = resetPoleName,
            ["MsgTrepilWarPoleOwnerName"] = resetPoleName,
            ["LegacyTrepilWarPoleOwnerName"] = resetPoleName,
            ["TrepilWarPoleOwnerName"] = resetPoleName,
            ["LegacyMsgTrepilWarPoleReset"] = "true",
            ["MsgTrepilWarPoleReset"] = "true",
            ["LegacyTrepilWarPoleReset"] = "true",
            ["TrepilWarPoleReset"] = "true",
        };
        return character with { Attributes = attributes };
    }

    private readonly record struct LegacyTrepilWarPoleFinishRoundResult(
        LegacyTrepilWarScoreEntry Winner,
        string ResetPoleName,
        IReadOnlyList<byte[]> ScorePanelFrames);
}

internal sealed class LegacyTrepilWarScoreStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<ulong, LegacyTrepilWarScoreEntry> _scores = [];

    public LegacyTrepilWarScoreEntry UpdateScore(
        ulong guildId,
        string guildName,
        uint damage)
    {
        if (guildId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(guildId), guildId, "Guild id must be non-zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(guildName);

        lock (_sync)
        {
            if (_scores.TryGetValue(guildId, out var existing))
            {
                var updated = existing with
                {
                    GuildName = guildName,
                    Score = LegacyCombatDamageCalculator.AddLegacySaturating(existing.Score, damage),
                };
                _scores[guildId] = updated;
                return updated;
            }

            var created = new LegacyTrepilWarScoreEntry(guildId, guildName, damage);
            _scores[guildId] = created;
            return created;
        }
    }

    public IReadOnlyList<LegacyTrepilWarScoreEntry> SnapshotScores()
    {
        lock (_sync)
        {
            return _scores.Values
                .OrderBy(entry => entry.GuildId)
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
}

internal readonly record struct LegacyTrepilWarScoreEntry(
    ulong GuildId,
    string GuildName,
    uint Score);
