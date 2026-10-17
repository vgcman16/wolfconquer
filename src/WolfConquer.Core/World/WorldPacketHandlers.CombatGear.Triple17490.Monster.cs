using System.Globalization;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> ReceiveLegacyCombatGearPhysicalLineMonstersAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        long attackerUid,
        Func<long, long, bool> isInLine,
        LegacyMagicRuntimeSpell spell,
        List<LegacySpellAnimationTarget> animationTargets,
        CancellationToken cancellationToken,
        bool includeStoneCrackerUpdate = false)
    {
        if (context.MonsterStates is not { } monsterStates)
        {
            return session;
        }

        var scalarSpell = GetLegacyCombatGearTriple17490NonPlayerScalar(spell);
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character).ToArray())
        {
            if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref session))
            {
                return session;
            }

            context = context with { Session = session };
            if (!monsterStates.TryGetVisibleInstance(candidate.Monster, DateTimeOffset.UtcNow, out var monster) ||
                !isInLine(monster.X, monster.Y) ||
                !CanLegacyPlayerAttackMonster(session.Character, monster, candidate.MonsterFamily, out _))
            {
                continue;
            }

            if (IsLegacyGuardMonsterFamily(monster, candidate.MonsterFamily))
            {
                session = await TryApplyLegacyGuardAttackFlashingNameAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);
                if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref session))
                {
                    return session;
                }

                context = context with { Session = session };
            }

            var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                session, context.WorldData, monster, candidate.MonsterFamily, scalarSpell);
            damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                session, spell, damage, LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc);
            (session, _) = await TrySendLegacyMonsterReceiveNinjaSageModeAsync(
                    context, session, context.WorldData, cancellationToken)
                .ConfigureAwait(false);
            if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref session))
            {
                return session;
            }

            session = session with
            {
                Character = WithCharacterAttribute(
                    session.Character, RuntimeLegacyThundercloudSightAttribute,
                    monster.Uid.ToString(CultureInfo.InvariantCulture)),
            };
            context.Sessions?.Set(context.Connection, session);
            context = context with { Session = session };
            if (includeStoneCrackerUpdate)
            {
                var current = await ReceiveLegacyStoneCrackerNonPlayerCounterAsync(
                        context, session, monster.X, monster.Y, cancellationToken).ConfigureAwait(false);
                if (current is null) return session;
                session = current;
                context = context with { Session = session };
            }
            (session, _) = await TrySendLegacyCombatGearDragonhowlReceivePlayerUpdateAsync(
                    context, session, monster.X, monster.Y, cancellationToken)
                .ConfigureAwait(false);
            if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref session))
            {
                return session;
            }

            context = context with { Session = session };
            damage = ApplyLegacySobNpcReceiveDamageModifiers(session, damage);
            session = session with
            {
                Character = WithCharacterAttribute(session.Character, RuntimeLegacyAttackHitAttribute, "true"),
            };
            context.Sessions?.Set(context.Connection, session);
            context = context with { Session = session };
            var result = ApplyLegacyPlayerMonsterDamage(
                monsterStates, monster, damage, DateTimeOffset.UtcNow, session);
            if (result.AlreadyDead)
            {
                continue;
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                monster.Uid, result.RequestedDamage, Hit: true, MsgAttackEffect.None));
            session = await PublishLegacyShockMonsterReceiveAsync(
                    context, session, candidate with { Monster = monster }, result, cancellationToken)
                .ConfigureAwait(false);
        }

        return session;
    }
}
