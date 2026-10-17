using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyTopPoleFirstRightCornerChatType = 2_108;
    private const uint LegacyTopPoleContinueRightCornerChatType = 2_109;
    private const string LegacyTopPoleScoresAttribute = "TopPoleScores";

    private static readonly LegacyTopPoleScoreStateStore LegacyTopPoleStates = new();

    private static bool TryApplyLegacyTopPolePoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyTopPoleScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            !LegacySobNpcEventRules.IsTopPole(sobNpc) ||
            !IsLegacyTopPoleProcessAlive(session.Character))
        {
            return false;
        }

        var playerUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var playerName = session.Character.Name.Trim();
        if (playerUid == 0 || playerName.Length == 0)
        {
            return false;
        }

        updatedScore = LegacyTopPoleStates.UpdateScore(playerUid, playerName, damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyTopPoleScoresAttribute(
                session.Character,
                SnapshotLegacyTopPoleScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyTopPolePoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyTopPolePoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !TryApplyLegacyTopPolePoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out _))
        {
            return false;
        }

        var scores = SnapshotLegacyTopPoleScores();
        if (scores.Count == 0)
        {
            return false;
        }

        var roundOwner = scores[0];
        var scorePanelFrames = CreateLegacyTopPoleScorePanelFrames(scores);
        LegacyTopPoleStates.Clear();
        updatedSession = scoredSession with
        {
            Character = WithLegacyTopPoleFinishRoundAttributes(
                WithLegacyTopPoleScoresAttribute(
                    scoredSession.Character,
                    SnapshotLegacyTopPoleScores()),
                roundOwner,
                roundOwner.PlayerName),
        };
        finishRound = new LegacyTopPolePoleFinishRoundResult(
            roundOwner,
            ResetPoleName: roundOwner.PlayerName,
            scorePanelFrames);
        return true;
    }

    internal static void ResetLegacyTopPoleScoreStateForTests()
    {
        LegacyTopPoleStates.Clear();
    }

    private static IReadOnlyList<LegacyTopPoleScoreEntry> SnapshotLegacyTopPoleScores()
    {
        return LegacyTopPoleStates.SnapshotScores()
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.PlayerUid)
            .ToArray();
    }

    private static IReadOnlyList<byte[]> CreateLegacyTopPoleScorePanelFrames()
    {
        var scores = SnapshotLegacyTopPoleScores();
        return CreateLegacyTopPoleScorePanelFrames(scores);
    }

    private static IReadOnlyList<byte[]> CreateLegacyTopPoleScorePanelFrames(
        IReadOnlyList<LegacyTopPoleScoreEntry> scores)
    {
        if (scores.Count == 0)
        {
            return [];
        }

        return scores
            .Take(5)
            .Select((score, index) => CreateLegacyTopPoleScorePanelFrame(index, score))
            .ToArray();
    }

    private static byte[] CreateLegacyTopPoleScorePanelFrame(
        int index,
        LegacyTopPoleScoreEntry score)
    {
        var rank = index + 1;
        var chatType = index == 0
            ? LegacyTopPoleFirstRightCornerChatType
            : LegacyTopPoleContinueRightCornerChatType;
        var message =
            $"No {rank.ToString(CultureInfo.InvariantCulture)}. {score.PlayerName} ({score.Score.ToString(CultureInfo.InvariantCulture)})";
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

    private static async ValueTask SendLegacyTopPoleScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        var frames = CreateLegacyTopPoleScorePanelFrames();
        await SendLegacyTopPoleScorePanelFramesAsync(context, frames, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyTopPoleScorePanelFramesAsync(
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
            if (active.Session.Character.MapId != LegacySobNpcEventRules.TopPoleMapId)
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

    private static async ValueTask SendLegacyTopPoleFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyTopPolePoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket
            .SystemRedCenter($"The Round Ownered by {finishRound.Winner.PlayerName}.")
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

    private static bool IsLegacyTopPoleProcessAlive(CharacterSnapshot character)
    {
        var process = ReadAttributeString(
                character.Attributes,
                GetLegacyTopPoleProcessAttributeNames())
            .Trim();
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] GetLegacyTopPoleProcessAttributeNames()
    {
        return
        [
            "LegacyMsgTopPoleProcess",
            "MsgTopPoleProcess",
            "RuntimeMsgTopPoleProcess",
            "LegacyTopPoleProcess",
            "TopPoleProcess",
            "RuntimeTopPoleProcess",
            "LegacyMsgTopPoleProces",
            "MsgTopPoleProces",
            "RuntimeMsgTopPoleProces",
            "LegacyTopPoleProces",
            "TopPoleProces",
            "RuntimeTopPoleProces",
        ];
    }

    private static CharacterSnapshot WithLegacyTopPoleScoresAttribute(
        CharacterSnapshot character,
        IReadOnlyList<LegacyTopPoleScoreEntry> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyTopPoleScoresAttribute);
        attributes.Remove("LegacyTopPoleScores");
        if (scores.Count > 0)
        {
            attributes[LegacyTopPoleScoresAttribute] = string.Join(
                ';',
                scores.Select(score => string.Join(
                    ',',
                    score.PlayerUid.ToString(CultureInfo.InvariantCulture),
                    score.Score.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(score.PlayerName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyTopPoleFinishRoundAttributes(
        CharacterSnapshot character,
        LegacyTopPoleScoreEntry roundOwner,
        string resetPoleName)
    {
        var playerUid = roundOwner.PlayerUid.ToString(CultureInfo.InvariantCulture);
        var score = roundOwner.Score.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyTopPoleRoundOwnerPlayerUID"] = playerUid,
            ["LegacyTopPoleRoundOwnerPlayerUid"] = playerUid,
            ["TopPoleRoundOwnerPlayerUID"] = playerUid,
            ["TopPoleRoundOwnerPlayerUid"] = playerUid,
            ["LegacyTopPoleRoundOwnerName"] = roundOwner.PlayerName,
            ["TopPoleRoundOwnerName"] = roundOwner.PlayerName,
            ["LegacyTopPoleRoundOwnerScore"] = score,
            ["TopPoleRoundOwnerScore"] = score,
            ["LegacyTopPoleWinnerPlayerUID"] = playerUid,
            ["LegacyTopPoleWinnerPlayerUid"] = playerUid,
            ["TopPoleWinnerPlayerUID"] = playerUid,
            ["TopPoleWinnerPlayerUid"] = playerUid,
            ["LegacyTopPoleWinnerName"] = roundOwner.PlayerName,
            ["TopPoleWinnerName"] = roundOwner.PlayerName,
            ["LegacyTopPoleWinnerScore"] = score,
            ["TopPoleWinnerScore"] = score,
            ["LegacyTopPolePoleOwnerName"] = resetPoleName,
            ["TopPolePoleOwnerName"] = resetPoleName,
            ["LegacyTopPolePoleReset"] = "true",
            ["TopPolePoleReset"] = "true",
        };
        return character with { Attributes = attributes };
    }

    private readonly record struct LegacyTopPolePoleFinishRoundResult(
        LegacyTopPoleScoreEntry Winner,
        string ResetPoleName,
        IReadOnlyList<byte[]> ScorePanelFrames);
}

internal sealed class LegacyTopPoleScoreStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<uint, LegacyTopPoleScoreEntry> _scores = [];

    public LegacyTopPoleScoreEntry UpdateScore(
        uint playerUid,
        string playerName,
        uint damage)
    {
        if (playerUid == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playerUid), playerUid, "Player UID must be non-zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

        lock (_sync)
        {
            if (_scores.TryGetValue(playerUid, out var existing))
            {
                var updated = existing with
                {
                    PlayerName = playerName,
                    Score = SaturatingAdd(existing.Score, damage),
                };
                _scores[playerUid] = updated;
                return updated;
            }

            var created = new LegacyTopPoleScoreEntry(playerUid, playerName, damage);
            _scores[playerUid] = created;
            return created;
        }
    }

    public IReadOnlyList<LegacyTopPoleScoreEntry> SnapshotScores()
    {
        lock (_sync)
        {
            return _scores.Values
                .OrderBy(entry => entry.PlayerUid)
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

    private static uint SaturatingAdd(uint left, uint right)
    {
        return left > uint.MaxValue - right ? uint.MaxValue : left + right;
    }
}

internal readonly record struct LegacyTopPoleScoreEntry(
    uint PlayerUid,
    string PlayerName,
    uint Score);
