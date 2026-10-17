using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyEmperorWarFirstRightCornerChatType = 2_108;
    private const uint LegacyEmperorWarContinueRightCornerChatType = 2_109;
    private const string LegacyEmperorWarScoresAttribute = "EmperorWarScores";

    private static readonly LegacyEmperorWarScoreStateStore LegacyEmperorWarStates = new();

    private static bool TryApplyLegacyEmperorWarPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyEmperorWarScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            !LegacySobNpcEventRules.IsEmperorWarPole(sobNpc) ||
            !IsLegacyEmperorWarProcessAlive(session.Character))
        {
            return false;
        }

        var playerUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var playerName = session.Character.Name.Trim();
        if (playerUid == 0 || playerName.Length == 0)
        {
            return false;
        }

        updatedScore = LegacyEmperorWarStates.UpdateScore(playerUid, playerName, damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyEmperorWarScoresAttribute(
                session.Character,
                SnapshotLegacyEmperorWarScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyEmperorWarPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyEmperorWarPoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !TryApplyLegacyEmperorWarPoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out _))
        {
            return false;
        }

        var scores = SnapshotLegacyEmperorWarScores();
        if (scores.Count == 0)
        {
            return false;
        }

        var roundOwner = scores[0];
        var scorePanelFrames = CreateLegacyEmperorWarScorePanelFrames(scores);
        LegacyEmperorWarStates.Clear();
        updatedSession = scoredSession with
        {
            Character = WithLegacyEmperorWarFinishRoundAttributes(
                WithLegacyEmperorWarScoresAttribute(
                    scoredSession.Character,
                    SnapshotLegacyEmperorWarScores()),
                roundOwner,
                roundOwner.PlayerName),
        };
        finishRound = new LegacyEmperorWarPoleFinishRoundResult(
            roundOwner,
            ResetPoleName: roundOwner.PlayerName,
            scorePanelFrames);
        return true;
    }

    internal static void ResetLegacyEmperorWarScoreStateForTests()
    {
        LegacyEmperorWarStates.Clear();
    }

    private static IReadOnlyList<LegacyEmperorWarScoreEntry> SnapshotLegacyEmperorWarScores()
    {
        return LegacyEmperorWarStates.SnapshotScores()
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.PlayerUid)
            .ToArray();
    }

    private static IReadOnlyList<byte[]> CreateLegacyEmperorWarScorePanelFrames()
    {
        var scores = SnapshotLegacyEmperorWarScores();
        return CreateLegacyEmperorWarScorePanelFrames(scores);
    }

    private static IReadOnlyList<byte[]> CreateLegacyEmperorWarScorePanelFrames(
        IReadOnlyList<LegacyEmperorWarScoreEntry> scores)
    {
        if (scores.Count == 0)
        {
            return [];
        }

        return scores
            .Take(5)
            .Select((score, index) => CreateLegacyEmperorWarScorePanelFrame(index, score))
            .ToArray();
    }

    private static byte[] CreateLegacyEmperorWarScorePanelFrame(
        int index,
        LegacyEmperorWarScoreEntry score)
    {
        var rank = index + 1;
        var chatType = index == 0
            ? LegacyEmperorWarFirstRightCornerChatType
            : LegacyEmperorWarContinueRightCornerChatType;
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

    private static async ValueTask SendLegacyEmperorWarScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        var frames = CreateLegacyEmperorWarScorePanelFrames();
        await SendLegacyEmperorWarScorePanelFramesAsync(context, frames, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyEmperorWarScorePanelFramesAsync(
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
            if (active.Session.Character.MapId != LegacySobNpcEventRules.EmperorWarMapId)
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

    private static async ValueTask SendLegacyEmperorWarFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyEmperorWarPoleFinishRoundResult finishRound,
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

    private static bool IsLegacyEmperorWarProcessAlive(CharacterSnapshot character)
    {
        var process = ReadAttributeString(
                character.Attributes,
                GetLegacyEmperorWarProcessAttributeNames())
            .Trim();
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] GetLegacyEmperorWarProcessAttributeNames()
    {
        return
        [
            "LegacyMsgEmperorWarProcess",
            "MsgEmperorWarProcess",
            "RuntimeMsgEmperorWarProcess",
            "LegacyEmperorWarProcess",
            "EmperorWarProcess",
            "RuntimeEmperorWarProcess",
            "LegacyMsgEmperorWarProces",
            "MsgEmperorWarProces",
            "RuntimeMsgEmperorWarProces",
            "LegacyEmperorWarProces",
            "EmperorWarProces",
            "RuntimeEmperorWarProces",
        ];
    }

    private static CharacterSnapshot WithLegacyEmperorWarScoresAttribute(
        CharacterSnapshot character,
        IReadOnlyList<LegacyEmperorWarScoreEntry> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyEmperorWarScoresAttribute);
        attributes.Remove("LegacyEmperorWarScores");
        if (scores.Count > 0)
        {
            attributes[LegacyEmperorWarScoresAttribute] = string.Join(
                ';',
                scores.Select(score => string.Join(
                    ',',
                    score.PlayerUid.ToString(CultureInfo.InvariantCulture),
                    score.Score.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(score.PlayerName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyEmperorWarFinishRoundAttributes(
        CharacterSnapshot character,
        LegacyEmperorWarScoreEntry roundOwner,
        string resetPoleName)
    {
        var playerUid = roundOwner.PlayerUid.ToString(CultureInfo.InvariantCulture);
        var score = roundOwner.Score.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyEmperorWarRoundOwnerPlayerUID"] = playerUid,
            ["LegacyEmperorWarRoundOwnerPlayerUid"] = playerUid,
            ["EmperorWarRoundOwnerPlayerUID"] = playerUid,
            ["EmperorWarRoundOwnerPlayerUid"] = playerUid,
            ["LegacyEmperorWarRoundOwnerName"] = roundOwner.PlayerName,
            ["EmperorWarRoundOwnerName"] = roundOwner.PlayerName,
            ["LegacyEmperorWarRoundOwnerScore"] = score,
            ["EmperorWarRoundOwnerScore"] = score,
            ["LegacyEmperorWarWinnerPlayerUID"] = playerUid,
            ["LegacyEmperorWarWinnerPlayerUid"] = playerUid,
            ["EmperorWarWinnerPlayerUID"] = playerUid,
            ["EmperorWarWinnerPlayerUid"] = playerUid,
            ["LegacyEmperorWarWinnerName"] = roundOwner.PlayerName,
            ["EmperorWarWinnerName"] = roundOwner.PlayerName,
            ["LegacyEmperorWarWinnerScore"] = score,
            ["EmperorWarWinnerScore"] = score,
            ["LegacyEmperorWarPoleOwnerName"] = resetPoleName,
            ["EmperorWarPoleOwnerName"] = resetPoleName,
            ["LegacyEmperorWarPoleReset"] = "true",
            ["EmperorWarPoleReset"] = "true",
        };
        return character with { Attributes = attributes };
    }

    private readonly record struct LegacyEmperorWarPoleFinishRoundResult(
        LegacyEmperorWarScoreEntry Winner,
        string ResetPoleName,
        IReadOnlyList<byte[]> ScorePanelFrames);
}

internal sealed class LegacyEmperorWarScoreStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<uint, LegacyEmperorWarScoreEntry> _scores = [];

    public LegacyEmperorWarScoreEntry UpdateScore(
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

            var created = new LegacyEmperorWarScoreEntry(playerUid, playerName, damage);
            _scores[playerUid] = created;
            return created;
        }
    }

    public IReadOnlyList<LegacyEmperorWarScoreEntry> SnapshotScores()
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

internal readonly record struct LegacyEmperorWarScoreEntry(
    uint PlayerUid,
    string PlayerName,
    uint Score);
