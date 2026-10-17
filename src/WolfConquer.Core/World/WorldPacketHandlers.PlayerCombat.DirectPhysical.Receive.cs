namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<LegacyPlayerMagicReceiveOutcome>
        ReceiveLegacyDirectPlayerPhysicalDamageAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            WorldActiveSession target,
            uint damage,
            long attackerUid,
            long targetUid,
            CancellationToken cancellationToken)
    {
        var result = new LegacyPlayerMagicDamageResult(
            target,
            target.Session,
            CheckedUInt32(target.Session.Character.MinHitPoints, nameof(target.Session.Character.MinHitPoints)),
            DamageApplied: 0,
            Killed: false);

        // InteractionCreate can reenter handling. Keep the accepted identities,
        // but consume their live receive-time state, not the pre-send snapshots.
        if (!TryRefreshLegacyDirectPhysicalReceiveParticipants(
                context, attackerUid, targetUid, ref session, ref result))
        {
            return CreateLegacyDirectPhysicalReceiveLostOutcome(context, session, attackerUid);
        }

        context = context with { Session = session };
        var combatGearOutcome = await ExecuteLegacyPlayerReceiveCombatGearOpeningAsync(
                context,
                new LegacyPlayerReceiveCombatGearRequest(
                    session,
                    result.ActiveSession with { Session = result.UpdatedSession },
                    attackerUid,
                    targetUid),
                cancellationToken)
            .ConfigureAwait(false);
        if (combatGearOutcome.AttackerSession is { } currentCombatGearAttacker)
        {
            session = currentCombatGearAttacker;
            context = context with { Session = session };
        }

        if (combatGearOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
        {
            return new(null, combatGearOutcome.AttackerSession, combatGearOutcome.AbortReason);
        }

        if (combatGearOutcome.Target is not { } combatGearTarget)
        {
            return new(null, combatGearOutcome.AttackerSession, combatGearOutcome.AbortReason);
        }

        result = result with
        {
            ActiveSession = combatGearTarget,
            UpdatedSession = combatGearTarget.Session,
            UpdatedAttackerSession = session,
        };
        if (!TryRefreshLegacyDirectPhysicalReceiveParticipants(
                context, attackerUid, targetUid, ref session, ref result))
        {
            return CreateLegacyDirectPhysicalReceiveLostOutcome(context, session, attackerUid);
        }

        context = context with { Session = session };
        (session, var speffectResult) = await TrySendLegacyDirectPhysicalReceiveSpeffectAttributesAsync(
                context,
                session,
                result,
                attackerUid,
                targetUid,
                cancellationToken)
            .ConfigureAwait(false);
        if (speffectResult is not { } updatedResult)
        {
            return CreateLegacyDirectPhysicalReceiveLostOutcome(context, session, attackerUid);
        }

        result = updatedResult;
        context = context with { Session = session };
        (session, _) = await TrySendLegacyPlayerReceiveNinjaSageModeAsync(
                context,
                session,
                context.WorldData,
                cancellationToken)
            .ConfigureAwait(false);
        if (!TryRefreshLegacyDirectPhysicalReceiveParticipants(
                context, attackerUid, targetUid, ref session, ref result))
        {
            return CreateLegacyDirectPhysicalReceiveLostOutcome(context, session, attackerUid);
        }

        context = context with { Session = session };
        return await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                context,
                result.ActiveSession with { Session = result.UpdatedSession },
                session.Character,
                damage,
                cancellationToken,
                applyMudWall: false,
                combatGearReceipt: combatGearOutcome.Receipt)
            .ConfigureAwait(false);
    }

    private static bool TryRefreshLegacyDirectPhysicalReceiveParticipants(
        WorldPacketHandlerContext context,
        long attackerUid,
        long targetUid,
        ref WorldSession attackerSession,
        ref LegacyPlayerMagicDamageResult result)
    {
        var target = result.ActiveSession with { Session = result.UpdatedSession };
        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref attackerSession, ref target))
        {
            return false;
        }

        result = result with
        {
            ActiveSession = target,
            UpdatedSession = target.Session,
            PreviousHitpoints = CheckedUInt32(target.Session.Character.MinHitPoints, nameof(target.Session.Character.MinHitPoints)),
        };
        return true;
    }

    private static bool TryRefreshLegacyDirectPhysicalParticipants(
        WorldPacketHandlerContext context,
        long attackerUid,
        long targetUid,
        ref WorldSession attackerSession,
        ref WorldActiveSession target)
    {
        if (context.Sessions is not { } sessions)
        {
            return attackerSession.Character.Uid == attackerUid && target.Session.Character.Uid == targetUid;
        }

        if (!sessions.TryGet(context.Connection, out var currentAttacker) ||
            currentAttacker is null || currentAttacker.Character.Uid != attackerUid ||
            !sessions.TryGet(target.Connection, out var currentTarget) ||
            currentTarget is null || currentTarget.Character.Uid != targetUid)
        {
            return false;
        }

        // Identity only: do not rerun alive, range, PK or counter gates here.
        attackerSession = currentAttacker;
        target = target with { Session = currentTarget };
        return true;
    }

    private static LegacyPlayerMagicReceiveOutcome CreateLegacyDirectPhysicalReceiveLostOutcome(
        WorldPacketHandlerContext context,
        WorldSession session,
        long attackerUid)
    {
        if (context.Sessions is { } sessions)
        {
            if (!sessions.TryGet(context.Connection, out var current) ||
                current is null || current.Character.Uid != attackerUid)
            {
                return new(null, null, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
            }

            return new(null, current, LegacyPlayerMagicReceiveAbortReason.TargetLost);
        }

        return session.Character.Uid == attackerUid
            ? new(null, session, LegacyPlayerMagicReceiveAbortReason.TargetLost)
            : new(null, null, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
    }
}
