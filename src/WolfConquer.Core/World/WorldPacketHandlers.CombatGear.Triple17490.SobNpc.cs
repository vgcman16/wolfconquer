using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession?> ReceiveLegacyCombatGearPhysicalLineSobNpcsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        long attackerUid,
        Func<long, long, bool> isInLine,
        LegacyMagicRuntimeSpell spell,
        List<LegacySpellAnimationTarget> animationTargets,
        CancellationToken cancellationToken)
    {
        if (context.SobNpcStates is not { } sobNpcStates)
        {
            return session;
        }

        var scalarSpell = GetLegacyCombatGearTriple17490NonPlayerScalar(spell);
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character).ToArray())
        {
            if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref session))
            {
                return session;
            }

            context = context with { Session = session };
            var sobNpc = sobNpcStates.GetVisible(candidate.SobNpc);
            if (!isInLine(sobNpc.X, sobNpc.Y) ||
                !CanLegacySobNpcMagicDamageTarget(context, session.Character, sobNpc, out _))
            {
                continue;
            }

            var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                session, scalarSpell, context.WorldData, damageMultiplier: 1);
            damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                session, spell, damage, LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc);
            var receiveGearOutcome = await TrySendLegacySobNpcReceiveCombatGearUpdateAsync(
                    context, session, sobNpc, cancellationToken)
                .ConfigureAwait(false);
            if (receiveGearOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return null;
            }

            session = receiveGearOutcome.Session;
            context = context with { Session = session };
            if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref session))
            {
                return session;
            }

            context = context with { Session = session };
            (session, _) = await TrySendLegacySobNpcReceiveNinjaSageModeAsync(
                    context, session, context.WorldData, cancellationToken)
                .ConfigureAwait(false);
            if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref session))
            {
                return session;
            }

            context = context with { Session = session };
            session = WithLegacySobNpcReceiveHitAttributes(context, session, sobNpc);
            context = context with { Session = session };
            damage = ApplyLegacySobNpcReceiveDamageModifiers(session, damage);
            var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(sobNpc, context.WorldData?.StaticStatues);
            var result = sobNpcStates.ApplyDamage(sobNpc, damage, DateTimeOffset.UtcNow, deathBehavior);
            if (result.AlreadyDead)
            {
                continue;
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(sobNpc.Id, nameof(sobNpc.Id)), damage, Hit: true, MsgAttackEffect.None));
            if (result.Killed && LegacySobNpcEventRules.IsTrainingSob(sobNpc))
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        context, session, sobNpc, sobNpcStates.GetVisible(sobNpc), cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (!LegacySobNpcEventRules.IsTrainingSob(sobNpc))
            {
                session = await ApplyLegacySobNpcBoundedEventSideEffectsAsync(
                        context, session, sobNpc, result, deathBehavior, cancellationToken)
                    .ConfigureAwait(false);
                await LogLegacySobNpcEventSideEffectGapAsync(
                        context with { Session = session }, sobNpc, result, spell)
                    .ConfigureAwait(false);
            }
        }

        return session;
    }
}
