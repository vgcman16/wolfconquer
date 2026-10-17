using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyPirateMasterScoresAttribute = "PirateMasterScores";
    private const string LegacyKungfuKingScoresAttribute = "KungfuKingScores";

    private static readonly LegacyClassPolePlayerScoreStateStore LegacyPirateMasterStates = new();
    private static readonly LegacyClassPolePlayerScoreStateStore LegacyKungfuKingStates = new();

    private static bool TryApplyLegacyPirateMasterPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerScoreEntry updatedScore)
    {
        return TryApplyLegacyClassPolePlayerFamilyPoleScore(
            session,
            sobNpc,
            damageApplied,
            "MsgPirateMaster",
            requiredClassFamily: 7,
            LegacyPirateMasterStates,
            LegacyPirateMasterScoresAttribute,
            out updatedSession,
            out updatedScore);
    }

    private static bool TryApplyLegacyKungfuKingPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerScoreEntry updatedScore)
    {
        return TryApplyLegacyClassPolePlayerFamilyPoleScore(
            session,
            sobNpc,
            damageApplied,
            "MsgKungfuKing",
            requiredClassFamily: 8,
            LegacyKungfuKingStates,
            LegacyKungfuKingScoresAttribute,
            out updatedSession,
            out updatedScore);
    }

    private static bool TryApplyLegacyPirateMasterPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerPoleFinishRoundResult finishRound)
    {
        return TryApplyLegacyClassPolePlayerFamilyPoleFinishRound(
            session,
            sobNpc,
            damageResult,
            "MsgPirateMaster",
            requiredClassFamily: 7,
            LegacyPirateMasterStates,
            LegacyPirateMasterScoresAttribute,
            TryApplyLegacyPirateMasterPoleScore,
            out updatedSession,
            out finishRound);
    }

    private static bool TryApplyLegacyKungfuKingPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerPoleFinishRoundResult finishRound)
    {
        return TryApplyLegacyClassPolePlayerFamilyPoleFinishRound(
            session,
            sobNpc,
            damageResult,
            "MsgKungfuKing",
            requiredClassFamily: 8,
            LegacyKungfuKingStates,
            LegacyKungfuKingScoresAttribute,
            TryApplyLegacyKungfuKingPoleScore,
            out updatedSession,
            out finishRound);
    }

    internal static void ResetLegacyPirateMasterScoreStateForTests()
    {
        LegacyPirateMasterStates.Clear();
    }

    internal static void ResetLegacyKungfuKingScoreStateForTests()
    {
        LegacyKungfuKingStates.Clear();
    }

    private static IReadOnlyList<LegacyClassPolePlayerScoreEntry> SnapshotLegacyPirateMasterScores()
    {
        return LegacyPirateMasterStates.SnapshotScores();
    }

    private static IReadOnlyList<LegacyClassPolePlayerScoreEntry> SnapshotLegacyKungfuKingScores()
    {
        return LegacyKungfuKingStates.SnapshotScores();
    }

    private static IReadOnlyList<byte[]> CreateLegacyPirateMasterScorePanelFrames()
    {
        return CreateLegacyClassPolePlayerScorePanelFrames(SnapshotLegacyPirateMasterScores());
    }

    private static IReadOnlyList<byte[]> CreateLegacyKungfuKingScorePanelFrames()
    {
        return CreateLegacyClassPolePlayerScorePanelFrames(SnapshotLegacyKungfuKingScores());
    }

    private static async ValueTask SendLegacyPirateMasterScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerFamilyScorePanelFramesAsync(
                context,
                CreateLegacyPirateMasterScorePanelFrames(),
                requiredClassFamily: 7,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyPirateMasterScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<byte[]> frames,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerFamilyScorePanelFramesAsync(
                context,
                frames,
                requiredClassFamily: 7,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyKungfuKingScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerFamilyScorePanelFramesAsync(
                context,
                CreateLegacyKungfuKingScorePanelFrames(),
                requiredClassFamily: 8,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyKungfuKingScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<byte[]> frames,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerFamilyScorePanelFramesAsync(
                context,
                frames,
                requiredClassFamily: 8,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyPirateMasterFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyClassPolePlayerPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerFinishRoundCenterMessageAsync(context, finishRound, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyKungfuKingFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyClassPolePlayerPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerFinishRoundCenterMessageAsync(context, finishRound, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryApplyLegacyClassPolePlayerFamilyPoleScore(
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
            !IsLegacyClassPolePlayerScorePole(sobNpc, scheduleName) ||
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

    private static bool TryApplyLegacyClassPolePlayerFamilyPoleFinishRound(
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
            !IsLegacyClassPolePlayerScorePole(sobNpc, scheduleName) ||
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

    private static async ValueTask SendLegacyClassPolePlayerFamilyScorePanelFramesAsync(
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
            if (active.Session.Character.MapId != LegacySobNpcEventRules.ClassPoleWarMapId ||
                NormalizeLegacyClassPoleWarClassFamily(active.Session.Character.Class) != requiredClassFamily)
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
