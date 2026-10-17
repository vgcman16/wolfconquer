using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(
        WorldSession Session,
        LegacyPlayerMagicDamageResult? Result,
        LegacySpellAnimationTarget? Animation,
        LegacyPlayerMagicReceiveAbortReason AbortReason)> TryReceiveLegacyHellVortexPlayerHitAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyPlayerMagicTarget target,
        uint selectedUid,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        HashSet<uint> activeHellVortexCasters,
        CancellationToken cancellationToken)
    {
        var attackerUid = session.Character.Uid;
        var targetUid = target.ActiveSession.Session.Character.Uid;
        var updatedSession = session;
        var activeTarget = target.ActiveSession;
        if (context.Sessions is not { } sessions ||
            !sessions.TryGet(activeTarget.Connection, out var refreshedTarget) ||
            refreshedTarget is null)
        {
            return (updatedSession, null, null, LegacyPlayerMagicReceiveAbortReason.None);
        }

        activeTarget = activeTarget with { Session = refreshedTarget };
        var selected = FindLegacyMapBlockPlayer(sessions, updatedSession, selectedUid);
        if (selected is null ||
            !CanSeePlayer(updatedSession.Character, activeTarget.Session.Character) ||
            ReadCharacterDynamicId(updatedSession.Character) != ReadCharacterDynamicId(activeTarget.Session.Character) ||
            LegacyGetDistance(
                selected.Session.Character.X,
                selected.Session.Character.Y,
                activeTarget.Session.Character.X,
                activeTarget.Session.Character.Y) > LegacyHellVortexFanoutRadius)
        {
            return (updatedSession, null, null, LegacyPlayerMagicReceiveAbortReason.None);
        }

        if (!CanLegacyPlayerMagicDamageTarget(
                context,
                updatedSession,
                activeTarget.Session,
                spell,
                out _,
                out var failureEffect))
        {
            if (failureEffect == LegacyPlayerMagicGateFailureEffect.ForbiddenPirate)
            {
                await SendScalarUpdateAsync(
                        context,
                        updatedSession.Character,
                        LegacyMsgUpdateDataType.ForbiddenPirate,
                        1,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (failureEffect == LegacyPlayerMagicGateFailureEffect.HellVortexCounter &&
                !activeHellVortexCasters.Contains(CheckedUInt32(
                    activeTarget.Session.Character.Uid,
                    nameof(activeTarget.Session.Character.Uid))))
            {
                await TrySendLegacyPlayerHellVortexCounterAsync(
                        context,
                        updatedSession,
                        activeTarget,
                        cancellationToken,
                        activeHellVortexCasters)
                    .ConfigureAwait(false);
                if (sessions.TryGet(context.Connection, out var refreshedCaster) && refreshedCaster is not null)
                {
                    updatedSession = refreshedCaster;
                }
            }

            return (updatedSession, null, null, LegacyPlayerMagicReceiveAbortReason.None);
        }

        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                [new LegacyPlayerMagicTarget(activeTarget)],
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };
        var originalAttackerSession = updatedSession;
        var statusDefended =
            HasLegacyStatusFlag(activeTarget.Session.Character, LegacyShurikenVortexFlag) ||
            HasLegacyStatusFlag(activeTarget.Session.Character, LegacyManiacDanceFlag);
        var wildPhoenixDefended = false;
        if (!statusDefended)
        {
            (activeTarget, wildPhoenixDefended) =
                await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                        context,
                        activeTarget,
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        var physicalDamage = statusDefended || wildPhoenixDefended
            ? new LegacyPlayerPlayerPhysicalSpellDamageResult(1U, MsgAttackEffect.None)
            : CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
                updatedSession,
                activeTarget.Session,
                spell,
                context.WorldData,
                damageDivisor: 1);
        var requestedDamage = physicalDamage.Damage;
        if (!statusDefended && !wildPhoenixDefended)
        {
            var breakthrough = ApplyLegacyPlayerPlayerPhysicalSpellBreakthroughBranch(
                updatedSession,
                activeTarget.Session,
                spell,
                context.WorldData,
                physicalDamage);
            physicalDamage = ApplyLegacyPlayerPlayerPhysicalDefensiveStance(
                activeTarget.Session,
                breakthrough.DamageResult);
            physicalDamage = ApplyLegacyPlayerPlayerPhysicalSpellInfinity(
                updatedSession,
                spell,
                physicalDamage);
            physicalDamage = ApplyLegacyPlayerPlayerPhysicalSpellNoBreakthroughBonus(
                breakthrough.State,
                physicalDamage);

            var receiveAttacker = updatedSession.Character;
            var backDamageEnteredAlive = receiveAttacker.MinHitPoints > 0;
            (context, activeTarget, receiveAttacker, var backDamageAbortReason) =
                await TryApplyLegacyPlayerReceiveHeavensWonderAsync(
                        context,
                        activeTarget,
                        receiveAttacker,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (backDamageAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
            {
                return (context.Session ?? updatedSession, null, null, backDamageAbortReason);
            }
            (context, activeTarget, receiveAttacker, backDamageAbortReason) =
                await TryApplyLegacyPlayerReceiveRevengeTailAsync(
                        context,
                        activeTarget,
                        receiveAttacker,
                        physicalDamage.Damage,
                        cancellationToken,
                        backDamageEnteredAlive: backDamageEnteredAlive)
                    .ConfigureAwait(false);
            if (backDamageAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
            {
                return (context.Session ?? updatedSession, null, null, backDamageAbortReason);
            }
            updatedSession = context.Session ?? updatedSession;
            context = context with { Session = updatedSession };

            // Physical.BackDmg and MudWall run before the caller's soul and 12/28 scaling.
            requestedDamage = ApplyLegacyPlayerReceiveMudWall(
                activeTarget.Session,
                context.WorldData,
                physicalDamage.Damage);
        }

        requestedDamage = MulDivToUInt32(
            CalculateLegacyHellVortexSoulDamage(requestedDamage, playerSpell.UseSpellSoul),
            12,
            28);

        var openingReceiveResult = new LegacyPlayerMagicDamageResult(
            activeTarget,
            activeTarget.Session,
            CheckedUInt32(activeTarget.Session.Character.MinHitPoints, nameof(activeTarget.Session.Character.MinHitPoints)),
            DamageApplied: 0,
            Killed: false);
        var combatGearOutcome = await ExecuteLegacyPlayerReceiveCombatGearOpeningAsync(
                context,
                new LegacyPlayerReceiveCombatGearRequest(
                    updatedSession,
                    activeTarget,
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
            return (updatedSession, null, null, combatGearOutcome.AbortReason);
        }

        if (combatGearOutcome.Target is not { } combatGearTarget)
        {
            return (updatedSession, null, null, combatGearOutcome.AbortReason);
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
        if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
        {
            updatedSession = currentReceiveAttacker;
            context = context with { Session = updatedSession };
        }
        if (receiveOutcome.Result is not { } result)
        {
            return (updatedSession, null, null, receiveOutcome.AbortReason);
        }
        if (result.UpdatedAttackerSession is null && !Equals(updatedSession, originalAttackerSession))
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
        return (updatedSession, result, new LegacySpellAnimationTarget(
            CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
            result.ReceiveDamage ?? requestedDamage,
            Hit: true,
            physicalDamage.Effect), LegacyPlayerMagicReceiveAbortReason.None);
    }
}
