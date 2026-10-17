using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession?> TryApplyLegacyRemainingClassPoleWarPoleScoreAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        CancellationToken cancellationToken)
    {
        if (TryApplyLegacyTaoistMasterPoleScore(session, sobNpc, damageApplied, out var taoistSession, out var taoistScore))
        {
            return await ApplyLegacyRemainingClassPoleWarPoleScoreAsync(
                    context,
                    taoistSession,
                    sobNpc,
                    damageApplied,
                    taoistScore,
                    "TaoistMaster",
                    "MsgTaoistMaster",
                    SendLegacyTaoistMasterScorePanelFramesAsync,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (TryApplyLegacyFireMasterPoleScore(session, sobNpc, damageApplied, out var fireSession, out var fireScore))
        {
            return await ApplyLegacyRemainingClassPoleWarPoleScoreAsync(
                    context,
                    fireSession,
                    sobNpc,
                    damageApplied,
                    fireScore,
                    "FireMaster",
                    "MsgFireMaster",
                    SendLegacyFireMasterScorePanelFramesAsync,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (TryApplyLegacyPirateMasterPoleScore(session, sobNpc, damageApplied, out var pirateSession, out var pirateScore))
        {
            return await ApplyLegacyRemainingClassPoleWarPoleScoreAsync(
                    context,
                    pirateSession,
                    sobNpc,
                    damageApplied,
                    pirateScore,
                    "PirateMaster",
                    "MsgPirateMaster",
                    SendLegacyPirateMasterScorePanelFramesAsync,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (TryApplyLegacyKungfuKingPoleScore(session, sobNpc, damageApplied, out var kungfuSession, out var kungfuScore))
        {
            return await ApplyLegacyRemainingClassPoleWarPoleScoreAsync(
                    context,
                    kungfuSession,
                    sobNpc,
                    damageApplied,
                    kungfuScore,
                    "KungfuKing",
                    "MsgKungfuKing",
                    SendLegacyKungfuKingScorePanelFramesAsync,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (TryApplyLegacyWindWalkerPoleScore(session, sobNpc, damageApplied, out var windSession, out var windScore))
        {
            return await ApplyLegacyRemainingClassPoleWarPoleScoreAsync(
                    context,
                    windSession,
                    sobNpc,
                    damageApplied,
                    windScore,
                    "WindWalker",
                    "MsgWindWalker",
                    SendLegacyWindWalkerScorePanelFramesAsync,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (TryApplyLegacyThnderStrikePoleScore(session, sobNpc, damageApplied, out var thunderSession, out var thunderScore))
        {
            return await ApplyLegacyRemainingClassPoleWarPoleScoreAsync(
                    context,
                    thunderSession,
                    sobNpc,
                    damageApplied,
                    thunderScore,
                    "ThnderStrike",
                    "MsgThnderStrike",
                    SendLegacyThnderStrikeScorePanelFramesAsync,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (TryApplyLegacyMonkMasterPoleScore(session, sobNpc, damageApplied, out var monkSession, out var monkScore))
        {
            return await ApplyLegacyRemainingClassPoleWarPoleScoreAsync(
                    context,
                    monkSession,
                    sobNpc,
                    damageApplied,
                    monkScore,
                    "MonkMaster",
                    "MsgMonkMaster",
                    SendLegacyMonkMasterScorePanelFramesAsync,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return null;
    }

    private static async ValueTask<WorldSession?> TryApplyLegacyRemainingClassPoleWarPoleFinishRoundAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        SobNpcRuntimeStateStore sobNpcStates,
        SobNpc sobNpc,
        SobNpcDamageResult result,
        CancellationToken cancellationToken)
    {
        if (TryApplyLegacyTaoistMasterPoleFinishRound(session, sobNpc, result, out var taoistSession, out var taoistFinishRound))
        {
            return await ApplyLegacyRemainingClassPoleWarPoleFinishRoundAsync(
                    context,
                    taoistSession,
                    sobNpcStates,
                    sobNpc,
                    taoistFinishRound,
                    "TaoistMaster",
                    "MsgTaoistMaster",
                    SendLegacyTaoistMasterScorePanelFramesAsync,
                    SendLegacyTaoistMasterFinishRoundCenterMessageAsync,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (TryApplyLegacyFireMasterPoleFinishRound(session, sobNpc, result, out var fireSession, out var fireFinishRound))
        {
            return await ApplyLegacyRemainingClassPoleWarPoleFinishRoundAsync(
                    context,
                    fireSession,
                    sobNpcStates,
                    sobNpc,
                    fireFinishRound,
                    "FireMaster",
                    "MsgFireMaster",
                    SendLegacyFireMasterScorePanelFramesAsync,
                    SendLegacyFireMasterFinishRoundCenterMessageAsync,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (TryApplyLegacyPirateMasterPoleFinishRound(session, sobNpc, result, out var pirateSession, out var pirateFinishRound))
        {
            return await ApplyLegacyRemainingClassPoleWarPoleFinishRoundAsync(
                    context,
                    pirateSession,
                    sobNpcStates,
                    sobNpc,
                    pirateFinishRound,
                    "PirateMaster",
                    "MsgPirateMaster",
                    SendLegacyPirateMasterScorePanelFramesAsync,
                    SendLegacyPirateMasterFinishRoundCenterMessageAsync,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (TryApplyLegacyKungfuKingPoleFinishRound(session, sobNpc, result, out var kungfuSession, out var kungfuFinishRound))
        {
            return await ApplyLegacyRemainingClassPoleWarPoleFinishRoundAsync(
                    context,
                    kungfuSession,
                    sobNpcStates,
                    sobNpc,
                    kungfuFinishRound,
                    "KungfuKing",
                    "MsgKungfuKing",
                    SendLegacyKungfuKingScorePanelFramesAsync,
                    SendLegacyKungfuKingFinishRoundCenterMessageAsync,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (TryApplyLegacyWindWalkerPoleFinishRound(session, sobNpc, result, out var windSession, out var windFinishRound))
        {
            return await ApplyLegacyRemainingClassPoleWarPoleFinishRoundAsync(
                    context,
                    windSession,
                    sobNpcStates,
                    sobNpc,
                    windFinishRound,
                    "WindWalker",
                    "MsgWindWalker",
                    SendLegacyWindWalkerScorePanelFramesAsync,
                    SendLegacyWindWalkerFinishRoundCenterMessageAsync,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (TryApplyLegacyThnderStrikePoleFinishRound(session, sobNpc, result, out var thunderSession, out var thunderFinishRound))
        {
            return await ApplyLegacyRemainingClassPoleWarPoleFinishRoundAsync(
                    context,
                    thunderSession,
                    sobNpcStates,
                    sobNpc,
                    thunderFinishRound,
                    "ThnderStrike",
                    "MsgThnderStrike",
                    SendLegacyThnderStrikeScorePanelFramesAsync,
                    SendLegacyThnderStrikeFinishRoundCenterMessageAsync,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (TryApplyLegacyMonkMasterPoleFinishRound(session, sobNpc, result, out var monkSession, out var monkFinishRound))
        {
            return await ApplyLegacyRemainingClassPoleWarPoleFinishRoundAsync(
                    context,
                    monkSession,
                    sobNpcStates,
                    sobNpc,
                    monkFinishRound,
                    "MonkMaster",
                    "MsgMonkMaster",
                    SendLegacyMonkMasterScorePanelFramesAsync,
                    SendLegacyMonkMasterFinishRoundCenterMessageAsync,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return null;
    }

    private static async ValueTask<WorldSession> ApplyLegacyRemainingClassPoleWarPoleScoreAsync(
        WorldPacketHandlerContext context,
        WorldSession updatedSession,
        SobNpc sobNpc,
        uint damageApplied,
        LegacyClassPolePlayerScoreEntry score,
        string logName,
        string scheduleName,
        LegacyClassPoleScorePanelSender scorePanelSender,
        CancellationToken cancellationToken)
    {
        context.Sessions?.Set(context.Connection, updatedSession);
        await scorePanelSender(context, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer updated legacy " + logName + " pole score " +
                $"{FormatSession(context with { Session = updatedSession })} sobNpcUid={sobNpc.Id} " +
                $"playerUid={score.PlayerUid} playerName={score.PlayerName} " +
                $"damage={damageApplied} score={score.Score}; " +
                "source " + scheduleName + ".UpdateScore modeled.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> ApplyLegacyRemainingClassPoleWarPoleFinishRoundAsync(
        WorldPacketHandlerContext context,
        WorldSession updatedSession,
        SobNpcRuntimeStateStore sobNpcStates,
        SobNpc sobNpc,
        LegacyClassPolePlayerPoleFinishRoundResult finishRound,
        string logName,
        string scheduleName,
        LegacyClassPoleFinishScorePanelSender scorePanelSender,
        LegacyClassPoleFinishMessageSender finishMessageSender,
        CancellationToken cancellationToken)
    {
        context.Sessions?.Set(context.Connection, updatedSession);
        await scorePanelSender(context, finishRound.ScorePanelFrames, cancellationToken)
            .ConfigureAwait(false);
        await finishMessageSender(context, finishRound, cancellationToken)
            .ConfigureAwait(false);
        sobNpcStates.ResetHitpointsToMax(sobNpc, finishRound.ResetPoleName);
        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        await SendFrameToVisiblePlayersAsync(
                context,
                updatedSession,
                LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer finished legacy " + logName + " pole round " +
                $"{FormatSession(context with { Session = updatedSession })} sobNpcUid={sobNpc.Id} " +
                $"roundOwnerPlayerUid={finishRound.Winner.PlayerUid} " +
                $"roundOwnerName={finishRound.Winner.PlayerName} " +
                $"score={finishRound.Winner.Score} resetHitpoints={visibleSobNpc.Life}; " +
                "source " + scheduleName + ".UpdateScore modeled; source " + scheduleName + ".FinishRound modeled.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private delegate ValueTask LegacyClassPoleScorePanelSender(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken);

    private delegate ValueTask LegacyClassPoleFinishScorePanelSender(
        WorldPacketHandlerContext context,
        IReadOnlyList<byte[]> frames,
        CancellationToken cancellationToken);

    private delegate ValueTask LegacyClassPoleFinishMessageSender(
        WorldPacketHandlerContext context,
        LegacyClassPolePlayerPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken);
}
