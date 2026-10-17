using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyNinjaMasterScoresAttribute = "NinjaMasterScores";

    private static readonly LegacyClassPolePlayerScoreStateStore LegacyNinjaMasterStates = new();

    private static bool TryApplyLegacyNinjaMasterPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            !IsLegacyClassPolePlayerScorePole(sobNpc, "MsgNinjaMaster") ||
            !IsLegacyNinjaClass(session.Character.Class) ||
            !IsLegacyClassPolePlayerProcessAlive(session.Character, "MsgNinjaMaster"))
        {
            return false;
        }

        var playerUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var playerName = session.Character.Name.Trim();
        if (playerUid == 0 || playerName.Length == 0)
        {
            return false;
        }

        updatedScore = LegacyNinjaMasterStates.UpdateScore(playerUid, playerName, damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyClassPolePlayerScoresAttribute(
                session.Character,
                LegacyNinjaMasterScoresAttribute,
                LegacyNinjaMasterStates.SnapshotScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyNinjaMasterPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerPoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !IsLegacyClassPolePlayerScorePole(sobNpc, "MsgNinjaMaster") ||
            !IsLegacyNinjaClass(session.Character.Class) ||
            !TryApplyLegacyNinjaMasterPoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out _))
        {
            return false;
        }

        var scores = LegacyNinjaMasterStates.SnapshotScores();
        if (scores.Count == 0)
        {
            return false;
        }

        var roundOwner = scores[0];
        var scorePanelFrames = CreateLegacyClassPolePlayerScorePanelFrames(scores);
        LegacyNinjaMasterStates.Clear();
        updatedSession = scoredSession with
        {
            Character = WithLegacyClassPolePlayerFinishRoundAttributes(
                WithLegacyClassPolePlayerScoresAttribute(
                    scoredSession.Character,
                    LegacyNinjaMasterScoresAttribute,
                    LegacyNinjaMasterStates.SnapshotScores()),
                "MsgNinjaMaster",
                roundOwner,
                resetPoleName: roundOwner.PlayerName),
        };
        finishRound = new LegacyClassPolePlayerPoleFinishRoundResult(
            "MsgNinjaMaster",
            roundOwner,
            ResetPoleName: roundOwner.PlayerName,
            scorePanelFrames);
        return true;
    }

    internal static void ResetLegacyNinjaMasterScoreStateForTests()
    {
        LegacyNinjaMasterStates.Clear();
    }

    private static IReadOnlyList<LegacyClassPolePlayerScoreEntry> SnapshotLegacyNinjaMasterScores()
    {
        return LegacyNinjaMasterStates.SnapshotScores();
    }

    private static IReadOnlyList<byte[]> CreateLegacyNinjaMasterScorePanelFrames()
    {
        return CreateLegacyClassPolePlayerScorePanelFrames(SnapshotLegacyNinjaMasterScores());
    }

    private static async ValueTask SendLegacyNinjaMasterScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        await SendLegacyNinjaMasterScorePanelFramesAsync(
                context,
                CreateLegacyNinjaMasterScorePanelFrames(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyNinjaMasterScorePanelFramesAsync(
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
            if (active.Session.Character.MapId != LegacySobNpcEventRules.ClassPoleWarMapId ||
                !IsLegacyNinjaClass(active.Session.Character.Class))
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

    private static async ValueTask SendLegacyNinjaMasterFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyClassPolePlayerPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerFinishRoundCenterMessageAsync(context, finishRound, cancellationToken)
            .ConfigureAwait(false);
    }
}
