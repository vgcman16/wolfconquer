using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyWarriorGodFirstRightCornerChatType = 2_108;
    private const uint LegacyWarriorGodContinueRightCornerChatType = 2_109;
    private const string LegacyWarriorGodScoresAttribute = "WarriorGodScores";
    private const string LegacyClassPolaWarScoresAttribute = "ClassPolaWarScores";

    private static readonly LegacyClassPolePlayerScoreStateStore LegacyWarriorGodStates = new();
    private static readonly LegacyClassPolePlayerScoreStateStore LegacyClassPolaWarStates = new();

    private static bool TryApplyLegacyWarriorGodPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerScoreEntry updatedScore)
    {
        return TryApplyLegacyClassPolePlayerPoleScore(
            session,
            sobNpc,
            damageApplied,
            "MsgWarriorGod",
            requireWarriorClass: true,
            LegacyWarriorGodStates,
            LegacyWarriorGodScoresAttribute,
            out updatedSession,
            out updatedScore);
    }

    private static bool TryApplyLegacyClassPolaWarPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerScoreEntry updatedScore)
    {
        return TryApplyLegacyClassPolePlayerPoleScore(
            session,
            sobNpc,
            damageApplied,
            "MsgClassPolaWar",
            requireWarriorClass: false,
            LegacyClassPolaWarStates,
            LegacyClassPolaWarScoresAttribute,
            out updatedSession,
            out updatedScore);
    }

    private static bool TryApplyLegacyWarriorGodPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerPoleFinishRoundResult finishRound)
    {
        return TryApplyLegacyClassPolePlayerPoleFinishRound(
            session,
            sobNpc,
            damageResult,
            "MsgWarriorGod",
            requireWarriorClass: true,
            LegacyWarriorGodStates,
            LegacyWarriorGodScoresAttribute,
            TryApplyLegacyWarriorGodPoleScore,
            out updatedSession,
            out finishRound);
    }

    private static bool TryApplyLegacyClassPolaWarPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerPoleFinishRoundResult finishRound)
    {
        return TryApplyLegacyClassPolePlayerPoleFinishRound(
            session,
            sobNpc,
            damageResult,
            "MsgClassPolaWar",
            requireWarriorClass: false,
            LegacyClassPolaWarStates,
            LegacyClassPolaWarScoresAttribute,
            TryApplyLegacyClassPolaWarPoleScore,
            out updatedSession,
            out finishRound);
    }

    internal static void ResetLegacyWarriorGodScoreStateForTests()
    {
        LegacyWarriorGodStates.Clear();
    }

    internal static void ResetLegacyClassPolaWarScoreStateForTests()
    {
        LegacyClassPolaWarStates.Clear();
    }

    private static IReadOnlyList<LegacyClassPolePlayerScoreEntry> SnapshotLegacyWarriorGodScores()
    {
        return LegacyWarriorGodStates.SnapshotScores();
    }

    private static IReadOnlyList<LegacyClassPolePlayerScoreEntry> SnapshotLegacyClassPolaWarScores()
    {
        return LegacyClassPolaWarStates.SnapshotScores();
    }

    private static IReadOnlyList<byte[]> CreateLegacyWarriorGodScorePanelFrames()
    {
        return CreateLegacyClassPolePlayerScorePanelFrames(SnapshotLegacyWarriorGodScores());
    }

    private static IReadOnlyList<byte[]> CreateLegacyClassPolaWarScorePanelFrames()
    {
        return CreateLegacyClassPolePlayerScorePanelFrames(SnapshotLegacyClassPolaWarScores());
    }

    private static async ValueTask SendLegacyWarriorGodScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerScorePanelFramesAsync(
                context,
                CreateLegacyWarriorGodScorePanelFrames(),
                restrictToWarriors: true,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWarriorGodScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<byte[]> frames,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerScorePanelFramesAsync(
                context,
                frames,
                restrictToWarriors: true,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyClassPolaWarScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerScorePanelFramesAsync(
                context,
                CreateLegacyClassPolaWarScorePanelFrames(),
                restrictToWarriors: false,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyClassPolaWarScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<byte[]> frames,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerScorePanelFramesAsync(
                context,
                frames,
                restrictToWarriors: false,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWarriorGodFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyClassPolePlayerPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerFinishRoundCenterMessageAsync(context, finishRound, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyClassPolaWarFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyClassPolePlayerPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerFinishRoundCenterMessageAsync(context, finishRound, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryApplyLegacyClassPolePlayerPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        string scheduleName,
        bool requireWarriorClass,
        LegacyClassPolePlayerScoreStateStore states,
        string scoresAttribute,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            !IsLegacyClassPolePlayerScorePole(sobNpc, scheduleName) ||
            (requireWarriorClass && !IsLegacyWarriorClass(session.Character.Class)) ||
            !IsLegacyClassPolePlayerProcessAlive(session.Character, scheduleName))
        {
            return false;
        }

        var playerUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var playerName = session.Character.Name.Trim();
        if (playerUid == 0 || playerName.Length == 0)
        {
            return false;
        }

        updatedScore = states.UpdateScore(playerUid, playerName, damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyClassPolePlayerScoresAttribute(
                session.Character,
                scoresAttribute,
                states.SnapshotScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyClassPolePlayerPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        string scheduleName,
        bool requireWarriorClass,
        LegacyClassPolePlayerScoreStateStore states,
        string scoresAttribute,
        LegacyClassPolePlayerPoleScoreUpdater scoreUpdater,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerPoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !IsLegacyClassPolePlayerScorePole(sobNpc, scheduleName) ||
            (requireWarriorClass && !IsLegacyWarriorClass(session.Character.Class)) ||
            !scoreUpdater(session, sobNpc, damageResult.DamageApplied, out var scoredSession, out _))
        {
            return false;
        }

        var scores = states.SnapshotScores();
        if (scores.Count == 0)
        {
            return false;
        }

        var roundOwner = scores[0];
        var scorePanelFrames = CreateLegacyClassPolePlayerScorePanelFrames(scores);
        states.Clear();
        updatedSession = scoredSession with
        {
            Character = WithLegacyClassPolePlayerFinishRoundAttributes(
                WithLegacyClassPolePlayerScoresAttribute(
                    scoredSession.Character,
                    scoresAttribute,
                    states.SnapshotScores()),
                scheduleName,
                roundOwner,
                resetPoleName: roundOwner.PlayerName),
        };
        finishRound = new LegacyClassPolePlayerPoleFinishRoundResult(
            scheduleName,
            roundOwner,
            ResetPoleName: roundOwner.PlayerName,
            scorePanelFrames);
        return true;
    }

    private static bool IsLegacyClassPolePlayerScorePole(SobNpc sobNpc, string scheduleName)
    {
        return LegacySobNpcEventRules.TryGetClassPoleWarPole(
                sobNpc,
                out var actualScheduleName,
                out _,
                out _) &&
            actualScheduleName.Equals(scheduleName, StringComparison.Ordinal);
    }

    private static bool IsLegacyClassPolePlayerProcessAlive(CharacterSnapshot character, string scheduleName)
    {
        var process = ReadLegacyClassPoleWarProcess(character.Attributes, scheduleName).Trim();
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) ||
            process == "1";
    }

    private static IReadOnlyList<byte[]> CreateLegacyClassPolePlayerScorePanelFrames(
        IReadOnlyList<LegacyClassPolePlayerScoreEntry> scores)
    {
        if (scores.Count == 0)
        {
            return [];
        }

        return scores
            .Take(5)
            .Select((score, index) => CreateLegacyClassPolePlayerScorePanelFrame(index, score))
            .ToArray();
    }

    private static byte[] CreateLegacyClassPolePlayerScorePanelFrame(
        int index,
        LegacyClassPolePlayerScoreEntry score)
    {
        var rank = index + 1;
        var chatType = index == 0
            ? LegacyWarriorGodFirstRightCornerChatType
            : LegacyWarriorGodContinueRightCornerChatType;
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

    private static async ValueTask SendLegacyClassPolePlayerScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<byte[]> frames,
        bool restrictToWarriors,
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
            if (active.Session.Character.MapId != LegacySobNpcEventRules.ClassPoleWarMapId ||
                (restrictToWarriors && !IsLegacyWarriorClass(active.Session.Character.Class)))
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

    private static async ValueTask SendLegacyClassPolePlayerFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyClassPolePlayerPoleFinishRoundResult finishRound,
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

    private static CharacterSnapshot WithLegacyClassPolePlayerScoresAttribute(
        CharacterSnapshot character,
        string scoresAttribute,
        IReadOnlyList<LegacyClassPolePlayerScoreEntry> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(scoresAttribute);
        attributes.Remove("Legacy" + scoresAttribute);
        if (scores.Count > 0)
        {
            attributes[scoresAttribute] = string.Join(
                ';',
                scores.Select(score => string.Join(
                    ',',
                    score.PlayerUid.ToString(CultureInfo.InvariantCulture),
                    score.Score.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(score.PlayerName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyClassPolePlayerFinishRoundAttributes(
        CharacterSnapshot character,
        string scheduleName,
        LegacyClassPolePlayerScoreEntry roundOwner,
        string resetPoleName)
    {
        var playerUid = roundOwner.PlayerUid.ToString(CultureInfo.InvariantCulture);
        var score = roundOwner.Score.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);

        foreach (var alias in GetLegacyClassPoleWarScheduleAliases(scheduleName))
        {
            attributes["Legacy" + alias + "RoundOwnerPlayerUID"] = playerUid;
            attributes["Legacy" + alias + "RoundOwnerPlayerUid"] = playerUid;
            attributes[alias + "RoundOwnerPlayerUID"] = playerUid;
            attributes[alias + "RoundOwnerPlayerUid"] = playerUid;
            attributes["Runtime" + alias + "RoundOwnerPlayerUID"] = playerUid;
            attributes["Runtime" + alias + "RoundOwnerPlayerUid"] = playerUid;
            attributes["Legacy" + alias + "RoundOwnerName"] = roundOwner.PlayerName;
            attributes[alias + "RoundOwnerName"] = roundOwner.PlayerName;
            attributes["Runtime" + alias + "RoundOwnerName"] = roundOwner.PlayerName;
            attributes["Legacy" + alias + "RoundOwnerScore"] = score;
            attributes[alias + "RoundOwnerScore"] = score;
            attributes["Runtime" + alias + "RoundOwnerScore"] = score;
            attributes["Legacy" + alias + "PoleOwnerName"] = resetPoleName;
            attributes[alias + "PoleOwnerName"] = resetPoleName;
            attributes["Runtime" + alias + "PoleOwnerName"] = resetPoleName;
            attributes["Legacy" + alias + "PoleReset"] = "true";
            attributes[alias + "PoleReset"] = "true";
            attributes["Runtime" + alias + "PoleReset"] = "true";
            attributes["Legacy" + alias + "PoleHitPoints"] = "max";
            attributes[alias + "PoleHitPoints"] = "max";
            attributes["Runtime" + alias + "PoleHitPoints"] = "max";
            attributes["Legacy" + alias + "PoleName"] = resetPoleName;
            attributes[alias + "PoleName"] = resetPoleName;
            attributes["Runtime" + alias + "PoleName"] = resetPoleName;
        }

        return character with { Attributes = attributes };
    }

    private delegate bool LegacyClassPolePlayerPoleScoreUpdater(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerScoreEntry updatedScore);

    private readonly record struct LegacyClassPolePlayerPoleFinishRoundResult(
        string ScheduleName,
        LegacyClassPolePlayerScoreEntry Winner,
        string ResetPoleName,
        IReadOnlyList<byte[]> ScorePanelFrames);
}

internal sealed class LegacyClassPolePlayerScoreStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<uint, LegacyClassPolePlayerScoreEntry> _scores = [];

    public LegacyClassPolePlayerScoreEntry UpdateScore(
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
                    Score = LegacyCombatDamageCalculator.AddLegacySaturating(existing.Score, damage),
                };
                _scores[playerUid] = updated;
                return updated;
            }

            var created = new LegacyClassPolePlayerScoreEntry(playerUid, playerName, damage);
            _scores[playerUid] = created;
            return created;
        }
    }

    public IReadOnlyList<LegacyClassPolePlayerScoreEntry> SnapshotScores()
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
}

internal readonly record struct LegacyClassPolePlayerScoreEntry(
    uint PlayerUid,
    string PlayerName,
    uint Score);
