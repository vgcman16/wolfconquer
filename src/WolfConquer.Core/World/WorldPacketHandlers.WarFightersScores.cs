using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyWarFightersFirstRightCornerChatType = 2_108;
    private const uint LegacyWarFightersContinueRightCornerChatType = 2_109;
    private const string LegacyWarFightersScoresAttribute = "WarFightersScores";

    private static readonly LegacyWarFightersScoreStateStore LegacyWarFightersStates = new();

    private static bool TryApplyLegacyWarFightersPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyWarFightersScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            !LegacySobNpcEventRules.IsWarFightersPole(sobNpc) ||
            !IsLegacyWarFightersProcessAlive(session.Character))
        {
            return false;
        }

        var playerUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var playerName = session.Character.Name.Trim();
        if (playerUid == 0 || playerName.Length == 0)
        {
            return false;
        }

        updatedScore = LegacyWarFightersStates.UpdateScore(playerUid, playerName, damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyWarFightersScoresAttribute(
                session.Character,
                SnapshotLegacyWarFightersScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyWarFightersPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyWarFightersPoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !TryApplyLegacyWarFightersPoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out _))
        {
            return false;
        }

        var scores = SnapshotLegacyWarFightersScores();
        if (scores.Count == 0)
        {
            return false;
        }

        var roundOwner = scores[0];
        var scorePanelFrames = CreateLegacyWarFightersScorePanelFrames(scores);
        LegacyWarFightersStates.Clear();
        updatedSession = scoredSession with
        {
            Character = WithLegacyWarFightersFinishRoundAttributes(
                WithLegacyWarFightersScoresAttribute(
                    scoredSession.Character,
                    SnapshotLegacyWarFightersScores()),
                roundOwner,
                roundOwner.PlayerName),
        };
        finishRound = new LegacyWarFightersPoleFinishRoundResult(
            roundOwner,
            ResetPoleName: roundOwner.PlayerName,
            scorePanelFrames);
        return true;
    }

    internal static void ResetLegacyWarFightersScoreStateForTests()
    {
        LegacyWarFightersStates.Clear();
    }

    private static IReadOnlyList<LegacyWarFightersScoreEntry> SnapshotLegacyWarFightersScores()
    {
        return LegacyWarFightersStates.SnapshotScores()
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.PlayerUid)
            .ToArray();
    }

    private static IReadOnlyList<byte[]> CreateLegacyWarFightersScorePanelFrames()
    {
        var scores = SnapshotLegacyWarFightersScores();
        return CreateLegacyWarFightersScorePanelFrames(scores);
    }

    private static IReadOnlyList<byte[]> CreateLegacyWarFightersScorePanelFrames(
        IReadOnlyList<LegacyWarFightersScoreEntry> scores)
    {
        if (scores.Count == 0)
        {
            return [];
        }

        return scores
            .Take(5)
            .Select((score, index) => CreateLegacyWarFightersScorePanelFrame(index, score))
            .ToArray();
    }

    private static byte[] CreateLegacyWarFightersScorePanelFrame(
        int index,
        LegacyWarFightersScoreEntry score)
    {
        var rank = index + 1;
        var chatType = index == 0
            ? LegacyWarFightersFirstRightCornerChatType
            : LegacyWarFightersContinueRightCornerChatType;
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

    private static async ValueTask SendLegacyWarFightersScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        var frames = CreateLegacyWarFightersScorePanelFrames();
        await SendLegacyWarFightersScorePanelFramesAsync(context, frames, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWarFightersScorePanelFramesAsync(
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
            if (active.Session.Character.MapId != LegacySobNpcEventRules.WarFightersMapId)
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

    private static async ValueTask SendLegacyWarFightersFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyWarFightersPoleFinishRoundResult finishRound,
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

    private static bool IsLegacyWarFightersProcessAlive(CharacterSnapshot character)
    {
        var process = ReadAttributeString(
                character.Attributes,
                GetLegacyWarFightersProcessAttributeNames())
            .Trim();
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] GetLegacyWarFightersProcessAttributeNames()
    {
        return
        [
            "LegacyMsgWarFightersProcess",
            "MsgWarFightersProcess",
            "RuntimeMsgWarFightersProcess",
            "LegacyWarFightersProcess",
            "WarFightersProcess",
            "RuntimeWarFightersProcess",
            "LegacyMsgWarFightersProces",
            "MsgWarFightersProces",
            "RuntimeMsgWarFightersProces",
            "LegacyWarFightersProces",
            "WarFightersProces",
            "RuntimeWarFightersProces",
        ];
    }

    private static CharacterSnapshot WithLegacyWarFightersScoresAttribute(
        CharacterSnapshot character,
        IReadOnlyList<LegacyWarFightersScoreEntry> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyWarFightersScoresAttribute);
        attributes.Remove("LegacyWarFightersScores");
        if (scores.Count > 0)
        {
            attributes[LegacyWarFightersScoresAttribute] = string.Join(
                ';',
                scores.Select(score => string.Join(
                    ',',
                    score.PlayerUid.ToString(CultureInfo.InvariantCulture),
                    score.Score.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(score.PlayerName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyWarFightersFinishRoundAttributes(
        CharacterSnapshot character,
        LegacyWarFightersScoreEntry roundOwner,
        string resetPoleName)
    {
        var playerUid = roundOwner.PlayerUid.ToString(CultureInfo.InvariantCulture);
        var score = roundOwner.Score.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyMsgWarFightersRoundOwnerPlayerUID"] = playerUid,
            ["LegacyMsgWarFightersRoundOwnerPlayerUid"] = playerUid,
            ["MsgWarFightersRoundOwnerPlayerUID"] = playerUid,
            ["MsgWarFightersRoundOwnerPlayerUid"] = playerUid,
            ["LegacyWarFightersRoundOwnerPlayerUID"] = playerUid,
            ["LegacyWarFightersRoundOwnerPlayerUid"] = playerUid,
            ["WarFightersRoundOwnerPlayerUID"] = playerUid,
            ["WarFightersRoundOwnerPlayerUid"] = playerUid,
            ["LegacyMsgWarFightersRoundOwnerName"] = roundOwner.PlayerName,
            ["MsgWarFightersRoundOwnerName"] = roundOwner.PlayerName,
            ["LegacyWarFightersRoundOwnerName"] = roundOwner.PlayerName,
            ["WarFightersRoundOwnerName"] = roundOwner.PlayerName,
            ["LegacyMsgWarFightersRoundOwnerScore"] = score,
            ["MsgWarFightersRoundOwnerScore"] = score,
            ["LegacyWarFightersRoundOwnerScore"] = score,
            ["WarFightersRoundOwnerScore"] = score,
            ["LegacyMsgWarFightersPoleOwnerName"] = resetPoleName,
            ["MsgWarFightersPoleOwnerName"] = resetPoleName,
            ["LegacyWarFightersPoleOwnerName"] = resetPoleName,
            ["WarFightersPoleOwnerName"] = resetPoleName,
            ["LegacyMsgWarFightersPoleReset"] = "true",
            ["MsgWarFightersPoleReset"] = "true",
            ["LegacyWarFightersPoleReset"] = "true",
            ["WarFightersPoleReset"] = "true",
        };
        return character with { Attributes = attributes };
    }

    private readonly record struct LegacyWarFightersPoleFinishRoundResult(
        LegacyWarFightersScoreEntry Winner,
        string ResetPoleName,
        IReadOnlyList<byte[]> ScorePanelFrames);
}

internal sealed class LegacyWarFightersScoreStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<uint, LegacyWarFightersScoreEntry> _scores = [];

    public LegacyWarFightersScoreEntry UpdateScore(
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

            var created = new LegacyWarFightersScoreEntry(playerUid, playerName, damage);
            _scores[playerUid] = created;
            return created;
        }
    }

    public IReadOnlyList<LegacyWarFightersScoreEntry> SnapshotScores()
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

internal readonly record struct LegacyWarFightersScoreEntry(
    uint PlayerUid,
    string PlayerName,
    uint Score);
