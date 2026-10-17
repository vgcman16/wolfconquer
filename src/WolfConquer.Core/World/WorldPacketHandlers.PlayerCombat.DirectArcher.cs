using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyDirectPlayerArcherAttackAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        CancellationToken cancellationToken)
    {
        var query = packet.Query;
        if (query.AttackType != MsgAttackType.Archer ||
            query.Damage > 0 ||
            context.Session is not { } session ||
            !TryGetVisibleSessionByUid(context, session, query.OpponentUid, out var activeTarget))
        {
            return false;
        }

        var targetCharacter = activeTarget.Session.Character;
        var attackerUid = session.Character.Uid;
        var targetUid = targetCharacter.Uid;
        if (HasLegacyStatusFlag(session.Character, LegacyNeptuneCurseFlag))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy direct player Archer " +
                    $"{FormatSession(context)} targetUid={targetCharacter.Uid}; " +
                    "source=MsgAttackPacket.cs global NeptuneCurse guard before AttackID dispatch.")
                .ConfigureAwait(false);
            return true;
        }

        if (LegacyGetDistance(
                session.Character.X,
                session.Character.Y,
                targetCharacter.X,
                targetCharacter.Y) > LegacyArcherDirectTargetRange)
        {
            return false;
        }

        if (!CanLegacyDirectPlayerPhysicalDamageTarget(
                context,
                session,
                activeTarget.Session,
                out var failureDiagnostic,
                out var failureEffect))
        {
            if (failureEffect == LegacyPlayerMagicGateFailureEffect.HellVortexCounter)
            {
                await TrySendLegacyPlayerHellVortexCounterAsync(context, session, activeTarget, cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy direct player Archer " +
                    $"{FormatSession(context)} targetUid={targetCharacter.Uid}; {failureDiagnostic}.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                session,
                [new LegacyPlayerMagicTarget(activeTarget)],
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

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

        LegacyPlayerPlayerPhysicalSpellDamageResult damageResult;
        if (statusDefended)
        {
            damageResult = new LegacyPlayerPlayerPhysicalSpellDamageResult(
                CalculateLegacyPlayerRangeStatusReturnDamage(
                    updatedSession,
                    activeTarget.Session,
                    context.WorldData),
                MsgAttackEffect.None);
        }
        else if (wildPhoenixDefended)
        {
            damageResult = new LegacyPlayerPlayerPhysicalSpellDamageResult(1U, MsgAttackEffect.None);
        }
        else
        {
            damageResult = CalculateLegacyDirectPlayerPlayerRangeDamageResult(
                updatedSession,
                activeTarget.Session,
                context.WorldData);
        }

        var requestedDamage = damageResult.Damage;
        var effect = damageResult.Effect;
        var originalAttackerSession = updatedSession;
        var backDamagePrepared = false;
        var backDamageAbortReason = LegacyPlayerMagicReceiveAbortReason.None;
        if (!statusDefended && !wildPhoenixDefended)
        {
            if (HasLegacyStatusFlag(activeTarget.Session.Character, LegacyDefensiveStanceFlag))
            {
                requestedDamage = LegacyCombatDamageCalculator.DecreaseLegacyBless(requestedDamage, 40);
                effect = MsgAttackEffect.Block;
            }
            else
            {
                backDamagePrepared = true;
                var receiveAttacker = updatedSession.Character;
                var backDamageEnteredAlive = receiveAttacker.MinHitPoints > 0;
                (context, activeTarget, receiveAttacker, backDamageAbortReason) =
                    await TryApplyLegacyPlayerReceiveHeavensWonderAsync(
                            context,
                            activeTarget,
                            receiveAttacker,
                            cancellationToken)
                        .ConfigureAwait(false);
                if (backDamageAbortReason == LegacyPlayerMagicReceiveAbortReason.None)
                {
                    (context, activeTarget, receiveAttacker, backDamageAbortReason) =
                        await TryApplyLegacyPlayerReceiveRevengeTailAsync(
                                context,
                                activeTarget,
                                receiveAttacker,
                                requestedDamage,
                                cancellationToken,
                                backDamageEnteredAlive: backDamageEnteredAlive)
                            .ConfigureAwait(false);
                }

                updatedSession = context.Session ?? updatedSession;
                context = context with { Session = updatedSession };
                if (backDamageAbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
                {
                    return true;
                }
            }
        }

        var response = new MsgAttackPacket(query with
        {
            Damage = CheckedInt32(requestedDamage, nameof(requestedDamage)),
            Effect = effect,
        });
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                context.Connection,
                updatedSession,
                response.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref updatedSession, ref activeTarget))
        {
            return true;
        }

        context = context with { Session = updatedSession };
        if (backDamageAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return true;
        }

        var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                context,
                activeTarget,
                updatedSession.Character,
                requestedDamage,
                cancellationToken)
            .ConfigureAwait(false);
        if ((receiveOutcome.AttackerSession ?? receiveOutcome.Result?.UpdatedAttackerSession) is { } currentAttacker)
        {
            updatedSession = currentAttacker;
            context = context with { Session = updatedSession };
        }

        if (receiveOutcome.Result is not { } result)
        {
            return true;
        }

        if (backDamagePrepared && result.UpdatedAttackerSession is null &&
            !Equals(updatedSession, originalAttackerSession))
        {
            result = result with { UpdatedAttackerSession = updatedSession };
        }

        context = context with { Session = updatedSession };

        (updatedSession, result) = await TrySendLegacyReceiveAttackPlayerSpeffectAttributesAsync(
                context,
                updatedSession,
                result,
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

        await SendLegacyPlayerMagicPreAnimationUpdatesAsync(context, updatedSession, result, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyPlayerMagicDamageUpdatesAsync(context, updatedSession, result, cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                requestedDamage,
                cancellationToken,
                activity: "direct player Archer weapon proficiency experience")
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy direct player Archer " +
                $"{FormatSession(context with { Session = updatedSession })} " +
                $"targetUid={result.UpdatedSession.Character.Uid} requestedDamage={requestedDamage} " +
                $"damageApplied={result.DamageApplied} killed={result.Killed} effect={effect}; " +
                "source=MsgAttackPacket.cs AttackID.Archer player fallback -> Range.OnPlayer DBSpell null " +
                "-> ReceiveAttack.Player.Execute.")
            .ConfigureAwait(false);
        return true;
    }

    private static LegacyPlayerPlayerPhysicalSpellDamageResult CalculateLegacyDirectPlayerPlayerRangeDamageResult(
        WorldSession attacker,
        WorldSession target,
        WorldDataCatalog? worldData)
    {
        var attackerFields = CalculateLegacyRuntimeStatusFields(attacker, worldData);
        var targetFields = CalculateLegacyRuntimeStatusFields(target, worldData);
        var adjustedMaxAttack = ApplyLegacyPlayerMonsterAjustMaxAttack(
            attackerFields[LegacyPlayerStatusPacket.MaxAttackIndex],
            attacker.Character);
        var damage = GetLegacyDeterministicAttack(
            adjustedMaxAttack,
            attackerFields[LegacyPlayerStatusPacket.MinAttackIndex]);
        damage = ApplyLegacyPlayerAjustAttack(damage, attackerFields, attacker.Character);
        damage = MulDivToUInt32(damage, LegacyServerConfigPhysicalDamagePercent, 100);

        var tortoiseDamagePercent = ResolveLegacyPlayerPlayerTargetTortoiseDamagePercent(target, targetFields);
        if (tortoiseDamagePercent != 0)
        {
            var rangeReduction = MulDivToUInt32(tortoiseDamagePercent, 30, 100);
            damage = LegacyCombatDamageCalculator.DecreaseLegacyBless(damage, Math.Min(10U, rangeReduction));
        }

        var dodge = targetFields[LegacyPlayerStatusPacket.DodgeIndex];
        damage = dodge >= 130U
            ? 0U
            : MulDivToUInt32(damage, 130U - dodge, 100);
        damage = MulDivToUInt32(damage, 50, 100);
        damage = BigMulDivToUInt32(
            damage,
            attacker.Character.Reborn >= 2 ? 5_000U : 10_000U,
            10_000);
        damage = ApplyLegacyPlayerPlayerPhysicalExtraAttack(
            damage,
            attackerFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex],
            targetFields[LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex]);

        return new LegacyPlayerPlayerPhysicalSpellDamageResult(Math.Max(1U, damage), MsgAttackEffect.None);
    }
}
