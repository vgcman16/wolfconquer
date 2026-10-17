using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyTaoistMasterScheduleName = "MsgTaoistMaster";
    private const string LegacyFireMasterScheduleName = "MsgFireMaster";
    private const string LegacyTaoistMasterScoresAttribute = "TaoistMasterScores";
    private const string LegacyFireMasterScoresAttribute = "FireMasterScores";
    private const int LegacyTaoistMasterClassFamily = 13;
    private const int LegacyFireMasterClassFamily = 14;

    private static readonly LegacyClassPolePlayerScoreStateStore LegacyTaoistMasterStates = new();
    private static readonly LegacyClassPolePlayerScoreStateStore LegacyFireMasterStates = new();

    private static bool TryApplyLegacyTaoistMasterPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerScoreEntry updatedScore)
    {
        return TryApplyLegacyTaoistFirePoleScore(
            session,
            sobNpc,
            damageApplied,
            LegacyTaoistMasterScheduleName,
            LegacyTaoistMasterClassFamily,
            LegacyTaoistMasterStates,
            LegacyTaoistMasterScoresAttribute,
            out updatedSession,
            out updatedScore);
    }

    private static bool TryApplyLegacyFireMasterPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerScoreEntry updatedScore)
    {
        return TryApplyLegacyTaoistFirePoleScore(
            session,
            sobNpc,
            damageApplied,
            LegacyFireMasterScheduleName,
            LegacyFireMasterClassFamily,
            LegacyFireMasterStates,
            LegacyFireMasterScoresAttribute,
            out updatedSession,
            out updatedScore);
    }

    private static bool TryApplyLegacyTaoistMasterPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerPoleFinishRoundResult finishRound)
    {
        return TryApplyLegacyTaoistFirePoleFinishRound(
            session,
            sobNpc,
            damageResult,
            LegacyTaoistMasterScheduleName,
            LegacyTaoistMasterClassFamily,
            LegacyTaoistMasterStates,
            LegacyTaoistMasterScoresAttribute,
            TryApplyLegacyTaoistMasterPoleScore,
            out updatedSession,
            out finishRound);
    }

    private static bool TryApplyLegacyFireMasterPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerPoleFinishRoundResult finishRound)
    {
        return TryApplyLegacyTaoistFirePoleFinishRound(
            session,
            sobNpc,
            damageResult,
            LegacyFireMasterScheduleName,
            LegacyFireMasterClassFamily,
            LegacyFireMasterStates,
            LegacyFireMasterScoresAttribute,
            TryApplyLegacyFireMasterPoleScore,
            out updatedSession,
            out finishRound);
    }

    internal static void ResetLegacyTaoistMasterScoreStateForTests()
    {
        LegacyTaoistMasterStates.Clear();
    }

    internal static void ResetLegacyFireMasterScoreStateForTests()
    {
        LegacyFireMasterStates.Clear();
    }

    private static IReadOnlyList<LegacyClassPolePlayerScoreEntry> SnapshotLegacyTaoistMasterScores()
    {
        return LegacyTaoistMasterStates.SnapshotScores();
    }

    private static IReadOnlyList<LegacyClassPolePlayerScoreEntry> SnapshotLegacyFireMasterScores()
    {
        return LegacyFireMasterStates.SnapshotScores();
    }

    private static IReadOnlyList<byte[]> CreateLegacyTaoistMasterScorePanelFrames()
    {
        return CreateLegacyClassPolePlayerScorePanelFrames(SnapshotLegacyTaoistMasterScores());
    }

    private static IReadOnlyList<byte[]> CreateLegacyFireMasterScorePanelFrames()
    {
        return CreateLegacyClassPolePlayerScorePanelFrames(SnapshotLegacyFireMasterScores());
    }

    private static async ValueTask SendLegacyTaoistMasterScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        await SendLegacyTaoistMasterScorePanelFramesAsync(
                context,
                CreateLegacyTaoistMasterScorePanelFrames(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyTaoistMasterScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<byte[]> frames,
        CancellationToken cancellationToken)
    {
        await SendLegacyTaoistFireScorePanelFramesAsync(
                context,
                frames,
                LegacyTaoistMasterClassFamily,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyFireMasterScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        await SendLegacyFireMasterScorePanelFramesAsync(
                context,
                CreateLegacyFireMasterScorePanelFrames(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyFireMasterScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<byte[]> frames,
        CancellationToken cancellationToken)
    {
        await SendLegacyTaoistFireScorePanelFramesAsync(
                context,
                frames,
                LegacyFireMasterClassFamily,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyTaoistMasterFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyClassPolePlayerPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerFinishRoundCenterMessageAsync(context, finishRound, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyFireMasterFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyClassPolePlayerPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerFinishRoundCenterMessageAsync(context, finishRound, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryApplyLegacyTaoistFirePoleScore(
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
            !IsLegacyTaoistFirePole(sobNpc, scheduleName, requiredClassFamily) ||
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

    private static bool TryApplyLegacyTaoistFirePoleFinishRound(
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
            !IsLegacyTaoistFirePole(sobNpc, scheduleName, requiredClassFamily) ||
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

    private static bool IsLegacyTaoistFirePole(
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

    private static async ValueTask SendLegacyTaoistFireScorePanelFramesAsync(
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
