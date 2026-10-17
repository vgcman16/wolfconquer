using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyMonkMasterScoresAttribute = "MonkMasterScores";

    private static readonly LegacyClassPolePlayerScoreStateStore LegacyMonkMasterStates = new();

    private static bool TryApplyLegacyMonkMasterPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            !IsLegacyClassPolePlayerScorePole(sobNpc, "MsgMonkMaster") ||
            !IsLegacyMonkClass(session.Character.Class) ||
            !IsLegacyClassPolePlayerProcessAlive(session.Character, "MsgMonkMaster"))
        {
            return false;
        }

        var playerUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var playerName = session.Character.Name.Trim();
        if (playerUid == 0 || playerName.Length == 0)
        {
            return false;
        }

        updatedScore = LegacyMonkMasterStates.UpdateScore(playerUid, playerName, damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyClassPolePlayerScoresAttribute(
                session.Character,
                LegacyMonkMasterScoresAttribute,
                LegacyMonkMasterStates.SnapshotScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyMonkMasterPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyClassPolePlayerPoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !IsLegacyClassPolePlayerScorePole(sobNpc, "MsgMonkMaster") ||
            !IsLegacyMonkClass(session.Character.Class) ||
            !TryApplyLegacyMonkMasterPoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out _))
        {
            return false;
        }

        var scores = LegacyMonkMasterStates.SnapshotScores();
        if (scores.Count == 0)
        {
            return false;
        }

        var roundOwner = scores[0];
        var scorePanelFrames = CreateLegacyClassPolePlayerScorePanelFrames(scores);
        LegacyMonkMasterStates.Clear();
        updatedSession = scoredSession with
        {
            Character = WithLegacyClassPolePlayerFinishRoundAttributes(
                WithLegacyClassPolePlayerScoresAttribute(
                    scoredSession.Character,
                    LegacyMonkMasterScoresAttribute,
                    LegacyMonkMasterStates.SnapshotScores()),
                "MsgMonkMaster",
                roundOwner,
                resetPoleName: roundOwner.PlayerName),
        };
        finishRound = new LegacyClassPolePlayerPoleFinishRoundResult(
            "MsgMonkMaster",
            roundOwner,
            ResetPoleName: roundOwner.PlayerName,
            scorePanelFrames);
        return true;
    }

    internal static void ResetLegacyMonkMasterScoreStateForTests()
    {
        LegacyMonkMasterStates.Clear();
    }

    private static IReadOnlyList<LegacyClassPolePlayerScoreEntry> SnapshotLegacyMonkMasterScores()
    {
        return LegacyMonkMasterStates.SnapshotScores();
    }

    private static IReadOnlyList<byte[]> CreateLegacyMonkMasterScorePanelFrames()
    {
        return CreateLegacyClassPolePlayerScorePanelFrames(SnapshotLegacyMonkMasterScores());
    }

    private static async ValueTask SendLegacyMonkMasterScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        await SendLegacyMonkMasterScorePanelFramesAsync(
                context,
                CreateLegacyMonkMasterScorePanelFrames(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyMonkMasterScorePanelFramesAsync(
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
                !IsLegacyMonkClass(active.Session.Character.Class))
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

    private static async ValueTask SendLegacyMonkMasterFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyClassPolePlayerPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        await SendLegacyClassPolePlayerFinishRoundCenterMessageAsync(context, finishRound, cancellationToken)
            .ConfigureAwait(false);
    }
}
