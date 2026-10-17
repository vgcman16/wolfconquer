using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyWindWalkerScheduleName = "MsgWindWalker";
    private const string LegacyThnderStrikeScheduleName = "MsgThnderStrike";
    private const string LegacyWindWalkerScoresAttribute = "WindWalkerScores";
    private const string LegacyThnderStrikeScoresAttribute = "ThnderStrikeScores";
    private const int LegacyWindWalkerClassFamily = 16;
    private const int LegacyThnderStrikeClassFamily = 9;

    private static readonly LegacyClassPolePlayerScoreStateStore LegacyWindWalkerStates = new();
    private static readonly LegacyClassPolePlayerScoreStateStore LegacyThnderStrikeStates = new();

    private static bool TryApplyLegacyWindWalkerPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerScoreEntry updatedScore)
    {
        return TryApplyLegacyWindThunderPoleScore(
            session,
            sobNpc,
            damageApplied,
            LegacyWindWalkerScheduleName,
            LegacyWindWalkerClassFamily,
            LegacyWindWalkerStates,
            LegacyWindWalkerScoresAttribute,
            out updatedSession,
            out updatedScore);
    }

    private static bool TryApplyLegacyThnderStrikePoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerScoreEntry updatedScore)
    {
        return TryApplyLegacyWindThunderPoleScore(
            session,
            sobNpc,
            damageApplied,
            LegacyThnderStrikeScheduleName,
            LegacyThnderStrikeClassFamily,
            LegacyThnderStrikeStates,
            LegacyThnderStrikeScoresAttribute,
            out updatedSession,
            out updatedScore);
    }

    private static bool TryApplyLegacyWindWalkerPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerPoleFinishRoundResult finishRound)
    {
        return TryApplyLegacyWindThunderPoleFinishRound(
            session,
            sobNpc,
            damageResult,
            LegacyWindWalkerScheduleName,
            LegacyWindWalkerClassFamily,
            LegacyWindWalkerStates,
            LegacyWindWalkerScoresAttribute,
            TryApplyLegacyWindWalkerPoleScore,
            out updatedSession,
            out finishRound);
    }

    private static bool TryApplyLegacyThnderStrikePoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerPoleFinishRoundResult finishRound)
    {
        return TryApplyLegacyWindThunderPoleFinishRound(
            session,
            sobNpc,
            damageResult,
            LegacyThnderStrikeScheduleName,
            LegacyThnderStrikeClassFamily,
            LegacyThnderStrikeStates,
            LegacyThnderStrikeScoresAttribute,
            TryApplyLegacyThnderStrikePoleScore,
            out updatedSession,
            out finishRound);
    }

    internal static void ResetLegacyWindWalkerScoreStateForTests()
    {
        LegacyWindWalkerStates.Clear();
    }

    internal static void ResetLegacyThnderStrikeScoreStateForTests()
    {
        LegacyThnderStrikeStates.Clear();
    }

    private static IReadOnlyList<LegacyClassPolePlayerScoreEntry> SnapshotLegacyWindWalkerScores()
    {
        return LegacyWindWalkerStates.SnapshotScores();
    }

    private static IReadOnlyList<LegacyClassPolePlayerScoreEntry> SnapshotLegacyThnderStrikeScores()
    {
        return LegacyThnderStrikeStates.SnapshotScores();
    }

    private static IReadOnlyList<byte[]> CreateLegacyWindWalkerScorePanelFrames()
    {
        return CreateLegacyClassPolePlayerScorePanelFrames(SnapshotLegacyWindWalkerScores());
    }

    private static IReadOnlyList<byte[]> CreateLegacyThnderStrikeScorePanelFrames()
    {
        return CreateLegacyClassPolePlayerScorePanelFrames(SnapshotLegacyThnderStrikeScores());
    }

    private static async ValueTask SendLegacyWindWalkerScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        await SendLegacyWindWalkerScorePanelFramesAsync(
                context,
                CreateLegacyWindWalkerScorePanelFrames(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWindWalkerScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<byte[]> frames,
        CancellationToken cancellationToken)
    {
        await SendLegacyWindThunderScorePanelFramesAsync(
                context,
                frames,
                LegacyWindWalkerClassFamily,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyThnderStrikeScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        await SendLegacyThnderStrikeScorePanelFramesAsync(
                context,
                CreateLegacyThnderStrikeScorePanelFrames(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyThnderStrikeScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<byte[]> frames,
        CancellationToken cancellationToken)
    {
        await SendLegacyWindThunderScorePanelFramesAsync(
                context,
                frames,
                LegacyThnderStrikeClassFamily,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWindWalkerFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyClassPolePlayerPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerFinishRoundCenterMessageAsync(context, finishRound, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyThnderStrikeFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyClassPolePlayerPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerFinishRoundCenterMessageAsync(context, finishRound, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryApplyLegacyWindThunderPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        string scheduleName,
        int requiredClassFamily,
        LegacyClassPolePlayerScoreStateStore states,
        string scoresAttribute,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            !IsLegacyWindThunderPole(sobNpc, scheduleName, requiredClassFamily) ||
            NormalizeLegacyClassPoleWarClassFamily(session.Character.Class) != requiredClassFamily ||
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

    private static bool TryApplyLegacyWindThunderPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        string scheduleName,
        int requiredClassFamily,
        LegacyClassPolePlayerScoreStateStore states,
        string scoresAttribute,
        LegacyClassPolePlayerPoleScoreUpdater scoreUpdater,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerPoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !IsLegacyWindThunderPole(sobNpc, scheduleName, requiredClassFamily) ||
            NormalizeLegacyClassPoleWarClassFamily(session.Character.Class) != requiredClassFamily ||
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

    private static bool IsLegacyWindThunderPole(
        SobNpc sobNpc,
        string scheduleName,
        int requiredClassFamily)
    {
        return LegacySobNpcEventRules.TryGetClassPoleWarPole(
                sobNpc,
                out var actualScheduleName,
                out var actualRequiredClassFamily,
                out _) &&
            actualScheduleName.Equals(scheduleName, StringComparison.Ordinal) &&
            actualRequiredClassFamily == requiredClassFamily;
    }

    private static async ValueTask SendLegacyWindThunderScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<byte[]> frames,
        int requiredClassFamily,
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
            var character = active.Session.Character;
            if (character.MapId != LegacySobNpcEventRules.ClassPoleWarMapId ||
                NormalizeLegacyClassPoleWarClassFamily(character.Class) != requiredClassFamily)
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
}
