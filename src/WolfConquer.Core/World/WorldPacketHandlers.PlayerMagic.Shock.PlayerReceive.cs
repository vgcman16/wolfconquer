using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(WorldSession Session, WorldActiveSession? Target)>
        TryPrepareLegacyShockPlayerReceiveAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            WorldActiveSession target,
            MsgAttackQuery query,
            LegacyMagicRuntimeSpell spell,
            CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions ||
            !sessions.TryGet(target.Connection, out var refreshedTarget) ||
            refreshedTarget is null)
        {
            return (session, null);
        }

        target = target with { Session = refreshedTarget };
        // Shock.cs:44 tests the current target's query radius before CanAttackPlayer.
        if (!CanSeePlayer(session.Character, target.Session.Character) ||
            ReadCharacterDynamicId(session.Character) != ReadCharacterDynamicId(target.Session.Character) ||
            LegacyGetDistance(query.X, query.Y, target.Session.Character.X, target.Session.Character.Y) > LegacyShockRadius)
        {
            return (session, null);
        }

        if (!CanLegacyPlayerMagicDamageTarget(
                context,
                session,
                target.Session,
                spell,
                out _,
                out var failureEffect))
        {
            if (failureEffect == LegacyPlayerMagicGateFailureEffect.ForbiddenPirate)
            {
                await SendScalarUpdateAsync(
                        context,
                        session.Character,
                        LegacyMsgUpdateDataType.ForbiddenPirate,
                        1,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (failureEffect == LegacyPlayerMagicGateFailureEffect.HellVortexCounter)
            {
                await TrySendLegacyPlayerHellVortexCounterAsync(
                        context,
                        session,
                        target,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (sessions.TryGet(context.Connection, out var refreshedCaster) && refreshedCaster is not null)
                {
                    session = refreshedCaster;
                }
            }

            return (session, null);
        }

        session = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                session,
                [new LegacyPlayerMagicTarget(target)],
                cancellationToken)
            .ConfigureAwait(false);
        return (session, target);
    }

    private static async ValueTask<LegacyPlayerMagicReceiveOutcome>
        ApplyLegacyShockPlayerReceiveAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            WorldActiveSession target,
            uint requestedDamage,
            CancellationToken cancellationToken)
    {
        var attackerUid = session.Character.Uid;
        var targetUid = target.Session.Character.Uid;
        var updatedSession = session;
        var openingReceiveResult = new LegacyPlayerMagicDamageResult(
            target,
            target.Session,
            CheckedUInt32(target.Session.Character.MinHitPoints, nameof(target.Session.Character.MinHitPoints)),
            DamageApplied: 0,
            Killed: false);
        var combatGearOutcome = await ExecuteLegacyPlayerReceiveCombatGearOpeningAsync(
                context,
                new LegacyPlayerReceiveCombatGearRequest(
                    updatedSession,
                    target,
                    attackerUid,
                    targetUid),
                cancellationToken)
            .ConfigureAwait(false);
        if (combatGearOutcome.AttackerSession is { } currentCombatGearAttacker)
        {
            updatedSession = currentCombatGearAttacker;
            context = context with { Session = updatedSession };
        }

        if (combatGearOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
        {
            return new(null, combatGearOutcome.AttackerSession, combatGearOutcome.AbortReason);
        }

        if (combatGearOutcome.Target is not { } combatGearTarget)
        {
            return new(null, combatGearOutcome.AttackerSession, combatGearOutcome.AbortReason);
        }

        openingReceiveResult = openingReceiveResult with
        {
            ActiveSession = combatGearTarget,
            UpdatedSession = combatGearTarget.Session,
            UpdatedAttackerSession = updatedSession,
        };
        context = context with { Session = updatedSession };
        (updatedSession, openingReceiveResult) = await TrySendLegacyReceiveAttackPlayerSpeffectAttributesAsync(
                context,
                updatedSession,
                openingReceiveResult,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };
        (updatedSession, _) = await TrySendLegacyPlayerReceiveNinjaSageModeAsync(
                context,
                updatedSession,
                context.WorldData,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
            context,
            openingReceiveResult.ActiveSession with { Session = openingReceiveResult.UpdatedSession },
            updatedSession.Character,
            requestedDamage,
            cancellationToken,
            applyMudWall: false,
            combatGearReceipt: combatGearOutcome.Receipt)
        .ConfigureAwait(false);
        if (receiveOutcome.Result is not { } result)
        {
            return receiveOutcome;
        }
        updatedSession = receiveOutcome.AttackerSession ?? result.UpdatedAttackerSession ?? updatedSession;
        context = context with { Session = updatedSession };
        if (result.UpdatedAttackerSession is null && !Equals(updatedSession, session))
        {
            result = result with { UpdatedAttackerSession = updatedSession };
        }

        updatedSession = result.UpdatedAttackerSession ?? updatedSession;
        context = context with { Session = updatedSession };
        await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                context,
                updatedSession,
                result,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyPlayerMagicDamageUpdatesAsync(
                context,
                updatedSession,
                result,
                cancellationToken)
            .ConfigureAwait(false);
        return receiveOutcome with { Result = result, AttackerSession = updatedSession };
    }
}
