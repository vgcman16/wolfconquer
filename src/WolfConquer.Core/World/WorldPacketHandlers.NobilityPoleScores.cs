using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyNobilityPoleFirstRightCornerChatType = 2_108;
    private const uint LegacyNobilityPoleContinueRightCornerChatType = 2_109;
    private const string LegacyNobilityPoleScoresAttribute = "NobilityPoleScores";

    private static readonly LegacyNobilityPoleScoreStateStore LegacyNobilityPoleStates = new();

    private static bool TryApplyLegacyNobilityPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyNobilityPoleScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            !LegacySobNpcEventRules.TryGetNobilityPoleSchedule(sobNpc, out var scheduleName, out var requiredRank) ||
            !IsLegacyNobilityPoleProcessAlive(session.Character, scheduleName) ||
            ReadLegacyNobilityRank(session.Character) != requiredRank)
        {
            return false;
        }

        var playerUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var playerName = session.Character.Name.Trim();
        if (playerUid == 0 || playerName.Length == 0)
        {
            return false;
        }

        updatedScore = LegacyNobilityPoleStates.UpdateScore(
            scheduleName,
            GetLegacyNobilityPoleDisplayRank(scheduleName),
            playerUid,
            playerName,
            damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyNobilityPoleScoresAttribute(
                session.Character,
                LegacyNobilityPoleStates.SnapshotScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyNobilityPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyNobilityPoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !LegacySobNpcEventRules.TryGetNobilityPoleSchedule(sobNpc, out var scheduleName, out _) ||
            !TryApplyLegacyNobilityPoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out _))
        {
            return false;
        }

        var scores = LegacyNobilityPoleStates.SnapshotScores(scheduleName);
        if (scores.Count == 0)
        {
            return false;
        }

        var roundOwner = scores[0];
        var scorePanelFrames = CreateLegacyNobilityPoleScorePanelFrames(scores);
        LegacyNobilityPoleStates.Clear(scheduleName);
        updatedSession = scoredSession with
        {
            Character = WithLegacyNobilityPoleFinishRoundAttributes(
                WithLegacyNobilityPoleScoresAttribute(
                    scoredSession.Character,
                    LegacyNobilityPoleStates.SnapshotScores()),
                roundOwner,
                roundOwner.PlayerName),
        };
        finishRound = new LegacyNobilityPoleFinishRoundResult(
            scheduleName,
            roundOwner.DisplayRank,
            roundOwner,
            ResetPoleName: roundOwner.PlayerName,
            scorePanelFrames);
        return true;
    }

    internal static void ResetLegacyNobilityPoleScoreStateForTests()
    {
        LegacyNobilityPoleStates.Clear();
    }

    private static IReadOnlyList<byte[]> CreateLegacyNobilityPoleScorePanelFrames(
        string scheduleName)
    {
        var scores = LegacyNobilityPoleStates.SnapshotScores(scheduleName);
        return CreateLegacyNobilityPoleScorePanelFrames(scores);
    }

    private static IReadOnlyList<byte[]> CreateLegacyNobilityPoleScorePanelFrames(
        IReadOnlyList<LegacyNobilityPoleScoreEntry> scores)
    {
        if (scores.Count == 0)
        {
            return [];
        }

        return scores
            .Take(5)
            .Select((score, index) => CreateLegacyNobilityPoleScorePanelFrame(index, score))
            .ToArray();
    }

    private static byte[] CreateLegacyNobilityPoleScorePanelFrame(
        int index,
        LegacyNobilityPoleScoreEntry score)
    {
        var rank = index + 1;
        var chatType = index == 0
            ? LegacyNobilityPoleFirstRightCornerChatType
            : LegacyNobilityPoleContinueRightCornerChatType;
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

    private static async ValueTask SendLegacyNobilityPoleScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        string scheduleName,
        CancellationToken cancellationToken)
    {
        var frames = CreateLegacyNobilityPoleScorePanelFrames(scheduleName);
        await SendLegacyNobilityPoleScorePanelFramesAsync(context, frames, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyNobilityPoleScorePanelFramesAsync(
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
            if (active.Session.Character.MapId != LegacySobNpcEventRules.NobilityPoleMapId)
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

    private static async ValueTask SendLegacyNobilityPoleFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyNobilityPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket
            .SystemRedCenter(GetLegacyNobilityPoleFinishRoundMessage(
                finishRound.ScheduleName,
                finishRound.Winner.PlayerName))
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

    private static bool IsLegacyNobilityPoleProcessAlive(
        CharacterSnapshot character,
        string scheduleName)
    {
        var process = ReadAttributeString(
                character.Attributes,
                "Legacy" + scheduleName + "Process",
                scheduleName + "Process",
                "Runtime" + scheduleName + "Process",
                "Legacy" + scheduleName + "Proces",
                scheduleName + "Proces",
                "Runtime" + scheduleName + "Proces",
                "LegacyNobilityPoleProcess",
                "NobilityPoleProcess",
                "RuntimeNobilityPoleProcess",
                "LegacyNobilityPoleProces",
                "NobilityPoleProces",
                "RuntimeNobilityPoleProces")
            .Trim();
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLegacyNobilityPoleDisplayRank(string scheduleName)
    {
        return scheduleName switch
        {
            "MsgNobilityPole" => "King",
            "MsgNobilityPole1" => "Prince",
            "MsgNobilityPole2" => "Duke",
            "MsgNobilityPole3" => "Earl",
            _ => string.Empty,
        };
    }

    private static string GetLegacyNobilityPoleFinishRoundMessage(
        string scheduleName,
        string playerName)
    {
        return scheduleName switch
        {
            "MsgNobilityPole" => "The [ King ]Round Ownered by " + playerName + ".",
            "MsgNobilityPole1" => "The [ Prince ]Round Ownered by " + playerName + ".",
            "MsgNobilityPole2" => "The [ Duke ]Round Ownered by " + playerName + ".",
            "MsgNobilityPole3" => "The [ Earl ] Round Ownered by " + playerName + ".",
            _ => "The Round Ownered by " + playerName + ".",
        };
    }

    private static CharacterSnapshot WithLegacyNobilityPoleScoresAttribute(
        CharacterSnapshot character,
        IReadOnlyList<LegacyNobilityPoleScoreEntry> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyNobilityPoleScoresAttribute);
        attributes.Remove("LegacyNobilityPoleScores");
        if (scores.Count > 0)
        {
            attributes[LegacyNobilityPoleScoresAttribute] = string.Join(
                ';',
                scores.Select(score => string.Join(
                    ',',
                    score.ScheduleName,
                    score.PlayerUid.ToString(CultureInfo.InvariantCulture),
                    score.Score.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(score.PlayerName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyNobilityPoleFinishRoundAttributes(
        CharacterSnapshot character,
        LegacyNobilityPoleScoreEntry roundOwner,
        string resetPoleName)
    {
        var playerUid = roundOwner.PlayerUid.ToString(CultureInfo.InvariantCulture);
        var score = roundOwner.Score.ToString(CultureInfo.InvariantCulture);
        var scheduleName = roundOwner.ScheduleName;
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["Legacy" + scheduleName + "RoundOwnerPlayerUID"] = playerUid,
            ["Legacy" + scheduleName + "RoundOwnerPlayerUid"] = playerUid,
            [scheduleName + "RoundOwnerPlayerUID"] = playerUid,
            [scheduleName + "RoundOwnerPlayerUid"] = playerUid,
            ["Runtime" + scheduleName + "RoundOwnerPlayerUID"] = playerUid,
            ["Runtime" + scheduleName + "RoundOwnerPlayerUid"] = playerUid,
            ["Legacy" + scheduleName + "RoundOwnerName"] = roundOwner.PlayerName,
            [scheduleName + "RoundOwnerName"] = roundOwner.PlayerName,
            ["Runtime" + scheduleName + "RoundOwnerName"] = roundOwner.PlayerName,
            ["Legacy" + scheduleName + "RoundOwnerScore"] = score,
            [scheduleName + "RoundOwnerScore"] = score,
            ["Runtime" + scheduleName + "RoundOwnerScore"] = score,
            ["Legacy" + scheduleName + "PoleOwnerName"] = resetPoleName,
            [scheduleName + "PoleOwnerName"] = resetPoleName,
            ["Runtime" + scheduleName + "PoleOwnerName"] = resetPoleName,
            ["Legacy" + scheduleName + "PoleReset"] = "true",
            [scheduleName + "PoleReset"] = "true",
            ["Runtime" + scheduleName + "PoleReset"] = "true",
            ["LegacyNobilityPoleLastFinishedSchedule"] = scheduleName,
            ["NobilityPoleLastFinishedSchedule"] = scheduleName,
            ["LegacyNobilityPoleLastFinishedRank"] = roundOwner.DisplayRank,
            ["NobilityPoleLastFinishedRank"] = roundOwner.DisplayRank,
            ["LegacyNobilityPoleRoundOwnerPlayerUID"] = playerUid,
            ["LegacyNobilityPoleRoundOwnerPlayerUid"] = playerUid,
            ["NobilityPoleRoundOwnerPlayerUID"] = playerUid,
            ["NobilityPoleRoundOwnerPlayerUid"] = playerUid,
            ["LegacyNobilityPoleRoundOwnerName"] = roundOwner.PlayerName,
            ["NobilityPoleRoundOwnerName"] = roundOwner.PlayerName,
            ["LegacyNobilityPoleRoundOwnerScore"] = score,
            ["NobilityPoleRoundOwnerScore"] = score,
            ["LegacyNobilityPolePoleOwnerName"] = resetPoleName,
            ["NobilityPolePoleOwnerName"] = resetPoleName,
            ["LegacyNobilityPolePoleReset"] = "true",
            ["NobilityPolePoleReset"] = "true",
        };
        return character with { Attributes = attributes };
    }

    private readonly record struct LegacyNobilityPoleFinishRoundResult(
        string ScheduleName,
        string DisplayRank,
        LegacyNobilityPoleScoreEntry Winner,
        string ResetPoleName,
        IReadOnlyList<byte[]> ScorePanelFrames);
}

internal sealed class LegacyNobilityPoleScoreStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<LegacyNobilityPoleScoreKey, LegacyNobilityPoleScoreEntry> _scores = [];

    public LegacyNobilityPoleScoreEntry UpdateScore(
        string scheduleName,
        string displayRank,
        uint playerUid,
        string playerName,
        uint damage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayRank);
        if (playerUid == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playerUid), playerUid, "Player UID must be non-zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

        var key = new LegacyNobilityPoleScoreKey(scheduleName, playerUid);
        lock (_sync)
        {
            if (_scores.TryGetValue(key, out var existing))
            {
                var updated = existing with
                {
                    DisplayRank = displayRank,
                    PlayerName = playerName,
                    Score = SaturatingAdd(existing.Score, damage),
                };
                _scores[key] = updated;
                return updated;
            }

            var created = new LegacyNobilityPoleScoreEntry(
                scheduleName,
                displayRank,
                playerUid,
                playerName,
                damage);
            _scores[key] = created;
            return created;
        }
    }

    public IReadOnlyList<LegacyNobilityPoleScoreEntry> SnapshotScores()
    {
        lock (_sync)
        {
            return _scores.Values
                .OrderBy(entry => entry.ScheduleName, StringComparer.Ordinal)
                .ThenByDescending(entry => entry.Score)
                .ThenBy(entry => entry.PlayerUid)
                .ToArray();
        }
    }

    public IReadOnlyList<LegacyNobilityPoleScoreEntry> SnapshotScores(string scheduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheduleName);

        lock (_sync)
        {
            return _scores.Values
                .Where(entry => entry.ScheduleName.Equals(scheduleName, StringComparison.Ordinal))
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

    private readonly record struct LegacyNobilityPoleScoreKey(
        string ScheduleName,
        uint PlayerUid);
}

internal readonly record struct LegacyNobilityPoleScoreEntry(
    string ScheduleName,
    string DisplayRank,
    uint PlayerUid,
    string PlayerName,
    uint Score);
