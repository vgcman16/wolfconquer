namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(WorldSession OwnerSession, WorldActiveSession Target,
        bool Allowed, LegacyPlayerMagicReceiveAbortReason AbortReason)>
        TryEnterLegacyStarFlowPlayerTargetAsync(
            WorldPacketHandlerContext context, WorldSession session, WorldActiveSession target,
            LegacyMagicRuntimeSpell spell, CancellationToken cancellationToken)
    {
        var ownerUid = session.Character.Uid;
        var targetUid = target.Session.Character.Uid;
        LegacyPlayerMagicReceiveAbortReason Refresh()
        {
            if (!TryRefreshLegacyPlayerMagicResourceSession(context, ownerUid, ref session))
                return LegacyPlayerMagicReceiveAbortReason.AttackerLost;
            if (!TryRefreshLegacyDirectPhysicalParticipants(
                    context, ownerUid, targetUid, ref session, ref target))
                return LegacyPlayerMagicReceiveAbortReason.TargetLost;
            context = context with { Session = session };
            return LegacyPlayerMagicReceiveAbortReason.None;
        }
        (WorldSession, WorldActiveSession, bool, LegacyPlayerMagicReceiveAbortReason) Result(
            bool allowed, LegacyPlayerMagicReceiveAbortReason reason = LegacyPlayerMagicReceiveAbortReason.None) =>
            (session, target, allowed, reason);

        var reason = Refresh();
        if (reason != LegacyPlayerMagicReceiveAbortReason.None) return Result(false, reason);
        var allowed = CanLegacyPlayerMagicDamageTarget(
            context, session, target.Session, spell, out _, out var effect,
            out var reachedFlashingNameStage, out var safeZoneDenied, deferFinalBlockAttackMap: true);
        if (!allowed)
        {
            if (effect == LegacyPlayerMagicGateFailureEffect.ForbiddenPirate)
                await SendScalarUpdateAsync(context, session.Character,
                    LegacyMsgUpdateDataType.ForbiddenPirate, 1, cancellationToken).ConfigureAwait(false);
            else if (effect == LegacyPlayerMagicGateFailureEffect.HellVortexCounter)
                await TrySendLegacyPlayerHellVortexCounterAsync(
                    context, session, target, cancellationToken).ConfigureAwait(false);
            else if (safeZoneDenied)
                await context.Connection.SendAsync(
                    (LegacyMsgMessagePacket.SystemTalk("You can't PK in the safe zone.") with
                    { ChatType = LegacyMsgMessagePacket.WhisperChatType }).ToFrameBytes(),
                    cancellationToken).ConfigureAwait(false);
            return Result(false, Refresh());
        }
        // Capture/Jiang early true bypasses BOTH flashing and the final map block.
        if (!reachedFlashingNameStage) return Result(true);
        var currentOwner = await TryApplyLegacyStarFlowFlashingNameAsync(
            context, session, target.Session, cancellationToken).ConfigureAwait(false);
        if (currentOwner is null) return Result(false, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
        session = currentOwner;
        reason = Refresh();
        if (reason != LegacyPlayerMagicReceiveAbortReason.None) return Result(false, reason);
        // This is the owed post-AddFlag phase, not an Alive/range/PK re-gate.
        return Result(!IsLegacyPlayerMagicBlockAttackMapBlocked(session.Character, out _));
    }
}
