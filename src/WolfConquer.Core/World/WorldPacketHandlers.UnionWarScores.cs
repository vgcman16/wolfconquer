using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyUnionWarFirstRightCornerChatType = 2_108;
    private const uint LegacyUnionWarContinueRightCornerChatType = 2_109;
    private const string LegacyUnionWarScoresAttribute = "UnionWarScores";

    private static readonly LegacyUnionWarScoreStateStore LegacyUnionWarStates = new();

    private static bool TryApplyLegacyUnionWarPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyUnionWarScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            !LegacySobNpcEventRules.IsSourceUnionWarPoleSob(sobNpc) ||
            !IsLegacyUnionWarProcessAlive(session.Character))
        {
            return false;
        }

        var unionId = ReadAttributeUInt64(
            session.Character.Attributes,
            "UnionUID",
            "UnionID",
            "UnionUid",
            "UnionId",
            "MyUnionUID",
            "MyUnionID",
            "MyUnionUid",
            "MyUnionId");
        if (unionId == 0)
        {
            return false;
        }

        var unionName = ResolveLegacyUnionWarUnionName(session.Character);
        updatedScore = LegacyUnionWarStates.UpdateScore(unionId, unionName, damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyUnionWarScoresAttribute(
                session.Character,
                LegacyUnionWarStates.SnapshotScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyUnionWarPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyUnionWarPoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !TryApplyLegacyUnionWarPoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out _))
        {
            return false;
        }

        var scores = SnapshotLegacyUnionWarScores();
        if (scores.Count == 0)
        {
            return false;
        }

        var winner = scores[0];
        var scorePanelFrames = CreateLegacyUnionWarScorePanelFrames(scores);
        LegacyUnionWarStates.Clear();
        updatedSession = scoredSession with
        {
            Character = WithLegacyUnionWarFinishRoundAttributes(
                WithLegacyUnionWarScoresAttribute(
                    scoredSession.Character,
                    SnapshotLegacyUnionWarScores()),
                winner,
                winner.UnionName),
        };
        finishRound = new LegacyUnionWarPoleFinishRoundResult(
            winner,
            ResetPoleName: winner.UnionName,
            scorePanelFrames);
        return true;
    }

    internal static void ResetLegacyUnionWarScoreStateForTests()
    {
        LegacyUnionWarStates.Clear();
    }

    private static IReadOnlyList<LegacyUnionWarScoreEntry> SnapshotLegacyUnionWarScores()
    {
        return LegacyUnionWarStates.SnapshotScores()
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.UnionId)
            .ToArray();
    }

    private static IReadOnlyList<byte[]> CreateLegacyUnionWarScorePanelFrames()
    {
        var scores = SnapshotLegacyUnionWarScores();
        return CreateLegacyUnionWarScorePanelFrames(scores);
    }

    private static IReadOnlyList<byte[]> CreateLegacyUnionWarScorePanelFrames(
        IReadOnlyList<LegacyUnionWarScoreEntry> scores)
    {
        if (scores.Count == 0)
        {
            return [];
        }

        return scores
            .Take(5)
            .Select((score, index) => CreateLegacyUnionWarScorePanelFrame(index, score))
            .ToArray();
    }

    private static byte[] CreateLegacyUnionWarScorePanelFrame(
        int index,
        LegacyUnionWarScoreEntry score)
    {
        var rank = index + 1;
        var chatType = index == 0
            ? LegacyUnionWarFirstRightCornerChatType
            : LegacyUnionWarContinueRightCornerChatType;
        var message =
            $"No {rank.ToString(CultureInfo.InvariantCulture)}. {score.UnionName} ({score.Score.ToString(CultureInfo.InvariantCulture)})";
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

    private static async ValueTask SendLegacyUnionWarScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var frames = CreateLegacyUnionWarScorePanelFrames();
        await SendLegacyUnionWarScorePanelFramesAsync(context, frames, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyUnionWarScorePanelFramesAsync(
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
            if (active.Session.Character.MapId != LegacySobNpcEventRules.UnionWarMapId)
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

    private static async ValueTask SendLegacyUnionWarFinishRoundMessagesAsync(
        WorldPacketHandlerContext context,
        LegacyUnionWarPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        var score = finishRound.Winner.Score.ToString(CultureInfo.InvariantCulture);
        var systemFrame = LegacyMsgMessagePacket
            .SystemWhiteTalk($"Congratulations to {finishRound.Winner.UnionName},they've won the UnionWar round with a score of  {score}")
            .ToFrameBytes();
        var centerFrame = LegacyMsgMessagePacket
            .SystemRedCenter($"Congratulations to {finishRound.Winner.UnionName}, they've won the UnionWar round with a score of  {score}")
            .ToFrameBytes();
        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is null || activeSessions.Count == 0)
        {
            await context.Connection.SendAsync(systemFrame, cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(centerFrame, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        foreach (var active in activeSessions)
        {
            await active.Connection.SendAsync(systemFrame, cancellationToken)
                .ConfigureAwait(false);
            await active.Connection.SendAsync(centerFrame, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static bool IsLegacyUnionWarProcessAlive(CharacterSnapshot character)
    {
        var process = ReadAttributeString(
                character.Attributes,
                "LegacyMsgUnionWarProcess",
                "MsgUnionWarProcess",
                "RuntimeMsgUnionWarProcess",
                "LegacyMsgUnionWarProces",
                "MsgUnionWarProces",
                "RuntimeMsgUnionWarProces",
                "LegacyUnionWarProcess",
                "UnionWarProcess",
                "RuntimeUnionWarProcess",
                "LegacyUnionWarProces",
                "UnionWarProces",
                "RuntimeUnionWarProces")
            .Trim();
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) ||
            process == "1";
    }

    private static string ResolveLegacyUnionWarUnionName(CharacterSnapshot character)
    {
        return ReadAttributeString(
                character.Attributes,
                "UnionName",
                "MyUnionName",
                "LegacyUnionName",
                "LegacyMyUnionName",
                "RuntimeUnionName",
                "RuntimeMyUnionName")
            .Trim();
    }

    private static CharacterSnapshot WithLegacyUnionWarScoresAttribute(
        CharacterSnapshot character,
        IReadOnlyList<LegacyUnionWarScoreEntry> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyUnionWarScoresAttribute);
        attributes.Remove("LegacyUnionWarScores");
        if (scores.Count > 0)
        {
            attributes[LegacyUnionWarScoresAttribute] = string.Join(
                ';',
                scores.Select(score => string.Join(
                    ',',
                    score.UnionId.ToString(CultureInfo.InvariantCulture),
                    score.Score.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(score.UnionName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyUnionWarFinishRoundAttributes(
        CharacterSnapshot character,
        LegacyUnionWarScoreEntry winner,
        string resetPoleName)
    {
        var unionId = winner.UnionId.ToString(CultureInfo.InvariantCulture);
        var score = winner.Score.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyUnionWarWinnerUnionID"] = unionId,
            ["LegacyUnionWarWinnerUnionId"] = unionId,
            ["UnionWarWinnerUnionID"] = unionId,
            ["UnionWarWinnerUnionId"] = unionId,
            ["LegacyUnionWarWinnerUnionName"] = winner.UnionName,
            ["UnionWarWinnerUnionName"] = winner.UnionName,
            ["LegacyUnionWarWinnerScore"] = score,
            ["UnionWarWinnerScore"] = score,
            ["LegacyUnionWarPoleOwnerName"] = resetPoleName,
            ["UnionWarPoleOwnerName"] = resetPoleName,
            ["LegacyUnionWarPoleReset"] = "true",
            ["UnionWarPoleReset"] = "true",
        };
        return character with { Attributes = attributes };
    }

    private readonly record struct LegacyUnionWarPoleFinishRoundResult(
        LegacyUnionWarScoreEntry Winner,
        string ResetPoleName,
        IReadOnlyList<byte[]> ScorePanelFrames);
}

internal sealed class LegacyUnionWarScoreStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<ulong, LegacyUnionWarScoreEntry> _scores = [];

    public LegacyUnionWarScoreEntry UpdateScore(
        ulong unionId,
        string unionName,
        uint damage)
    {
        if (unionId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unionId), unionId, "Union id must be non-zero.");
        }

        var normalizedUnionName = unionName.Trim();

        lock (_sync)
        {
            if (_scores.TryGetValue(unionId, out var existing))
            {
                var updated = existing with
                {
                    UnionName = normalizedUnionName.Length == 0 ? existing.UnionName : normalizedUnionName,
                    Score = LegacyCombatDamageCalculator.AddLegacySaturating(existing.Score, damage),
                };
                _scores[unionId] = updated;
                return updated;
            }

            var created = new LegacyUnionWarScoreEntry(unionId, normalizedUnionName, damage);
            _scores[unionId] = created;
            return created;
        }
    }

    public IReadOnlyList<LegacyUnionWarScoreEntry> SnapshotScores()
    {
        lock (_sync)
        {
            return _scores.Values
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.UnionId)
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

internal readonly record struct LegacyUnionWarScoreEntry(
    ulong UnionId,
    string UnionName,
    uint Score);
