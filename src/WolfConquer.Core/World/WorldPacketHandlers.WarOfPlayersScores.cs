using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyWarOfPlayersFirstRightCornerChatType = 2_108;
    private const uint LegacyWarOfPlayersContinueRightCornerChatType = 2_109;
    private const string LegacyWarOfPlayersScoresAttribute = "WarOfPlayersScores";

    private static readonly LegacyWarOfPlayersScoreStateStore LegacyWarOfPlayersStates = new();

    private static bool TryApplyLegacyWarOfPlayersPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyWarOfPlayersScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            !LegacySobNpcEventRules.IsWarOfPlayersPole(sobNpc) ||
            !IsLegacyWarOfPlayersProcessAlive(session.Character))
        {
            return false;
        }

        var playerUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var playerName = session.Character.Name.Trim();
        if (playerUid == 0 || playerName.Length == 0)
        {
            return false;
        }

        updatedScore = LegacyWarOfPlayersStates.UpdateScore(playerUid, playerName, damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyWarOfPlayersScoresAttribute(
                session.Character,
                LegacyWarOfPlayersStates.SnapshotScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyWarOfPlayersPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyWarOfPlayersPoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !TryApplyLegacyWarOfPlayersPoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out _))
        {
            return false;
        }

        var scores = SnapshotLegacyWarOfPlayersScores();
        if (scores.Count == 0)
        {
            return false;
        }

        var roundOwner = scores[0];
        var scorePanelFrames = CreateLegacyWarOfPlayersScorePanelFrames(scores);
        LegacyWarOfPlayersStates.Clear();
        updatedSession = scoredSession with
        {
            Character = WithLegacyWarOfPlayersFinishRoundAttributes(
                WithLegacyWarOfPlayersScoresAttribute(
                    scoredSession.Character,
                    SnapshotLegacyWarOfPlayersScores()),
                roundOwner,
                roundOwner.PlayerName),
        };
        finishRound = new LegacyWarOfPlayersPoleFinishRoundResult(
            roundOwner,
            ResetPoleName: roundOwner.PlayerName,
            scorePanelFrames);
        return true;
    }

    internal static void ResetLegacyWarOfPlayersScoreStateForTests()
    {
        LegacyWarOfPlayersStates.Clear();
    }

    private static IReadOnlyList<LegacyWarOfPlayersScoreEntry> SnapshotLegacyWarOfPlayersScores()
    {
        return LegacyWarOfPlayersStates.SnapshotScores();
    }

    private static IReadOnlyList<byte[]> CreateLegacyWarOfPlayersScorePanelFrames()
    {
        var scores = SnapshotLegacyWarOfPlayersScores();
        return CreateLegacyWarOfPlayersScorePanelFrames(scores);
    }

    private static IReadOnlyList<byte[]> CreateLegacyWarOfPlayersScorePanelFrames(
        IReadOnlyList<LegacyWarOfPlayersScoreEntry> scores)
    {
        if (scores.Count == 0)
        {
            return [];
        }

        return scores
            .Take(5)
            .Select((score, index) => CreateLegacyWarOfPlayersScorePanelFrame(index, score))
            .ToArray();
    }

    private static byte[] CreateLegacyWarOfPlayersScorePanelFrame(
        int index,
        LegacyWarOfPlayersScoreEntry score)
    {
        var rank = index + 1;
        var chatType = index == 0
            ? LegacyWarOfPlayersFirstRightCornerChatType
            : LegacyWarOfPlayersContinueRightCornerChatType;
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

    private static async ValueTask SendLegacyWarOfPlayersScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        var frames = CreateLegacyWarOfPlayersScorePanelFrames();
        await SendLegacyWarOfPlayersScorePanelFramesAsync(context, frames, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWarOfPlayersScorePanelFramesAsync(
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
            if (active.Session.Character.MapId != LegacySobNpcEventRules.WarOfPlayersMapId)
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

    private static bool IsLegacyWarOfPlayersProcessAlive(CharacterSnapshot character)
    {
        var process = ReadAttributeString(
                character.Attributes,
                "LegacyMsgWarOfPlayersProcess",
                "MsgWarOfPlayersProcess",
                "RuntimeMsgWarOfPlayersProcess",
                "LegacyWarOfPlayersProcess",
                "WarOfPlayersProcess",
                "RuntimeWarOfPlayersProcess")
            .Trim();
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase);
    }

    private static CharacterSnapshot WithLegacyWarOfPlayersScoresAttribute(
        CharacterSnapshot character,
        IReadOnlyList<LegacyWarOfPlayersScoreEntry> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyWarOfPlayersScoresAttribute);
        attributes.Remove("LegacyWarOfPlayersScores");
        if (scores.Count > 0)
        {
            attributes[LegacyWarOfPlayersScoresAttribute] = string.Join(
                ';',
                scores.Select(score => string.Join(
                    ',',
                    score.PlayerUid.ToString(CultureInfo.InvariantCulture),
                    score.Score.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(score.PlayerName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyWarOfPlayersFinishRoundAttributes(
        CharacterSnapshot character,
        LegacyWarOfPlayersScoreEntry roundOwner,
        string resetPoleName)
    {
        var playerUid = roundOwner.PlayerUid.ToString(CultureInfo.InvariantCulture);
        var score = roundOwner.Score.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyWarOfPlayersRoundOwnerPlayerUID"] = playerUid,
            ["LegacyWarOfPlayersRoundOwnerPlayerUid"] = playerUid,
            ["WarOfPlayersRoundOwnerPlayerUID"] = playerUid,
            ["WarOfPlayersRoundOwnerPlayerUid"] = playerUid,
            ["LegacyWarOfPlayersRoundOwnerName"] = roundOwner.PlayerName,
            ["WarOfPlayersRoundOwnerName"] = roundOwner.PlayerName,
            ["LegacyWarOfPlayersRoundOwnerScore"] = score,
            ["WarOfPlayersRoundOwnerScore"] = score,
            ["LegacyWarOfPlayersWinnerPlayerUID"] = playerUid,
            ["LegacyWarOfPlayersWinnerPlayerUid"] = playerUid,
            ["WarOfPlayersWinnerPlayerUID"] = playerUid,
            ["WarOfPlayersWinnerPlayerUid"] = playerUid,
            ["LegacyWarOfPlayersWinnerName"] = roundOwner.PlayerName,
            ["WarOfPlayersWinnerName"] = roundOwner.PlayerName,
            ["LegacyWarOfPlayersWinnerScore"] = score,
            ["WarOfPlayersWinnerScore"] = score,
            ["LegacyWarOfPlayersPoleOwnerName"] = resetPoleName,
            ["WarOfPlayersPoleOwnerName"] = resetPoleName,
            ["LegacyWarOfPlayersPoleReset"] = "true",
            ["WarOfPlayersPoleReset"] = "true",
        };
        return character with { Attributes = attributes };
    }

    private readonly record struct LegacyWarOfPlayersPoleFinishRoundResult(
        LegacyWarOfPlayersScoreEntry Winner,
        string ResetPoleName,
        IReadOnlyList<byte[]> ScorePanelFrames);
}

internal sealed class LegacyWarOfPlayersScoreStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<uint, LegacyWarOfPlayersScoreEntry> _scores = [];

    public LegacyWarOfPlayersScoreEntry UpdateScore(
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

            var created = new LegacyWarOfPlayersScoreEntry(playerUid, playerName, damage);
            _scores[playerUid] = created;
            return created;
        }
    }

    public IReadOnlyList<LegacyWarOfPlayersScoreEntry> SnapshotScores()
    {
        lock (_sync)
        {
            return _scores.Values
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.PlayerUid)
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

internal readonly record struct LegacyWarOfPlayersScoreEntry(
    uint PlayerUid,
    string PlayerName,
    uint Score);
