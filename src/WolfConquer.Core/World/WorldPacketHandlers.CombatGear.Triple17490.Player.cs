using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession?> ReceiveLegacyCombatGearPhysicalLinePlayersAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        long attackerUid,
        Func<long, long, bool> isInLine,
        LegacyMagicRuntimeSpell spell,
        List<LegacySpellAnimationTarget> animationTargets,
        CancellationToken cancellationToken,
        uint playerDamagePercent = 100)
    {
        if (context.Sessions is not { } sessions)
        {
            return session;
        }

        foreach (var candidate in sessions.Snapshot())
        {
            var target = candidate;
            var targetUid = target.Session.Character.Uid;
            if (ReferenceEquals(target.Connection, context.Connection) || targetUid == attackerUid)
            {
                continue;
            }

            if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref session))
            {
                return null;
            }

            if (!TryRefreshLegacyDirectPhysicalParticipants(
                    context, attackerUid, targetUid, ref session, ref target))
            {
                continue;
            }

            context = context with { Session = session };
            var character = target.Session.Character;
            if (!CanSeePlayer(session.Character, character) ||
                ReadCharacterDynamicId(session.Character) != ReadCharacterDynamicId(character) ||
                !isInLine(character.X, character.Y) ||
                character.MinHitPoints <= 0 ||
                HasLegacyStatusFlag(character, LegacyDeadFlag) ||
                HasLegacyStatusFlag(character, LegacyGhostFlag))
            {
                continue;
            }

            if (!CanLegacyPlayerMagicDamageTarget(
                    context, session, target.Session, spell, out _, out var failureEffect))
            {
                if (failureEffect == LegacyPlayerMagicGateFailureEffect.ForbiddenPirate)
                {
                    await SendScalarUpdateAsync(
                            context, session.Character, LegacyMsgUpdateDataType.ForbiddenPirate, 1, cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (failureEffect == LegacyPlayerMagicGateFailureEffect.HellVortexCounter)
                {
                    await TrySendLegacyPlayerHellVortexCounterAsync(context, session, target, cancellationToken)
                        .ConfigureAwait(false);
                }

                continue;
            }

            session = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                    context, session, [new LegacyPlayerMagicTarget(target)], cancellationToken)
                .ConfigureAwait(false);
            if (!TryRefreshLegacyDirectPhysicalParticipants(
                    context, attackerUid, targetUid, ref session, ref target))
            {
                continue;
            }

            context = context with { Session = session };
            var prepared = await PrepareLegacyCombatGearTriple17490PlayerDamageAsync(
                    context, session, target, spell, attackerUid, targetUid, cancellationToken)
                .ConfigureAwait(false);
            session = prepared.Context.Session ?? session;
            context = prepared.Context;
            target = prepared.Target;
            if (prepared.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return null;
            }

            if (prepared.DamageResult is not { } damageResult)
            {
                continue;
            }

            var receiveDamage = playerDamagePercent == 100
                ? damageResult.Damage
                : MulDivToUInt32(damageResult.Damage, playerDamagePercent, 100);
            var receiveOutcome = await ReceiveLegacyDirectPlayerPhysicalDamageAsync(
                    context, session, target, receiveDamage, attackerUid, targetUid, cancellationToken)
                .ConfigureAwait(false);
            if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
            {
                session = currentReceiveAttacker;
                context = context with { Session = session };
            }

            if (receiveOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return null;
            }

            if (receiveOutcome.Result is not { } result)
            {
                continue;
            }

            context = context with { Session = session };
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(targetUid, nameof(targetUid)),
                result.ReceiveDamage ?? receiveDamage,
                Hit: true,
                damageResult.Effect));
            await SendLegacyPlayerMagicPreAnimationUpdatesAsync(context, session, result, cancellationToken)
                .ConfigureAwait(false);
            if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref session))
            {
                return null;
            }

            context = context with { Session = session };
            await SendLegacyPlayerMagicDamageUpdatesAsync(context, session, result, cancellationToken)
                .ConfigureAwait(false);
        }

        return session;
    }
}
