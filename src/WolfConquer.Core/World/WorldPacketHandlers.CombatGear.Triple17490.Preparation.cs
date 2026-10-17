using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(
        WorldPacketHandlerContext Context,
        WorldActiveSession Target,
        LegacyPlayerPlayerPhysicalSpellDamageResult? DamageResult,
        LegacyPlayerMagicReceiveAbortReason AbortReason)>
        PrepareLegacyCombatGearTriple17490PlayerDamageAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            WorldActiveSession target,
            LegacyMagicRuntimeSpell spell,
            long attackerUid,
            long targetUid,
            CancellationToken cancellationToken)
    {
        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref session, ref target))
        {
            return (context, target, null, LegacyPlayerMagicReceiveAbortReason.None);
        }

        context = context with { Session = session };
        if (HasLegacyStatusFlag(target.Session.Character, LegacyShurikenVortexFlag) ||
            HasLegacyStatusFlag(target.Session.Character, LegacyManiacDanceFlag))
        {
            return (context, target, new LegacyPlayerPlayerPhysicalSpellDamageResult(1, MsgAttackEffect.None), LegacyPlayerMagicReceiveAbortReason.None);
        }

        (target, var wildPhoenixDefended) = await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                context, target, cancellationToken)
            .ConfigureAwait(false);
        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref session, ref target))
        {
            return (context, target, null, LegacyPlayerMagicReceiveAbortReason.None);
        }

        context = context with { Session = session };
        if (wildPhoenixDefended)
        {
            return (context, target, new LegacyPlayerPlayerPhysicalSpellDamageResult(1, MsgAttackEffect.None), LegacyPlayerMagicReceiveAbortReason.None);
        }

        var damageResult = CalculateLegacyCombatGearTriple17490PlayerDamage(
            session, target.Session, spell, context.WorldData);
        var effects = await ApplyLegacyPlayerPlayerPhysicalEffectsAsync(
                context, session, target, spell, damageResult, attackerUid, targetUid, cancellationToken)
            .ConfigureAwait(false);
        session = effects.Session;
        target = effects.Target;
        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref session, ref target) ||
            effects.DamageResult is not { } effectDamageResult)
        {
            return (context with { Session = session }, target, null, LegacyPlayerMagicReceiveAbortReason.None);
        }

        context = context with { Session = session };
        damageResult = effectDamageResult;
        var breakthrough = ApplyLegacyPlayerPlayerPhysicalSpellBreakthroughBranch(
            session, target.Session, spell, context.WorldData, damageResult,
            applyPenetrationCompensation: false);
        damageResult = FinishLegacyCombatGearTriple17490PlayerDamage(
            session, target.Session, spell, context.WorldData, breakthrough.DamageResult, breakthrough.State);
        damageResult = ApplyLegacyCombatGearTriple17490BattlePowerAdvantage(
            session, target.Session, context.WorldData, damageResult);
        damageResult = ApplyLegacyPlayerPlayerPhysicalDefensiveStance(target.Session, damageResult);
        damageResult = ApplyLegacyPlayerPlayerPhysicalSpellInfinity(session, spell, damageResult);
        damageResult = ApplyLegacyPlayerPlayerPhysicalSpellNoBreakthroughBonus(breakthrough.State, damageResult);

        var ironShield = await ApplyLegacyPlayerPlayerPhysicalSpellIronShieldAsync(
                context, target, session, damageResult.Damage, cancellationToken)
            .ConfigureAwait(false);
        target = ironShield.Target;
        damageResult = damageResult with { Damage = ironShield.Damage };
        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref session, ref target))
        {
            return (context, target, null, LegacyPlayerMagicReceiveAbortReason.None);
        }

        context = context with { Session = session };
        var postIronShield = await ApplyLegacyPlayerPlayerPhysicalPostIronShieldTailAsync(
                context, session, damageResult.Damage, cancellationToken)
            .ConfigureAwait(false);
        session = postIronShield.Attacker;
        damageResult = damageResult with { Damage = postIronShield.Damage };
        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref session, ref target))
        {
            return (context, target, null, LegacyPlayerMagicReceiveAbortReason.None);
        }

        context = context with { Session = session };
        var dragonSwing = await ApplyLegacyPlayerPlayerPhysicalDragonSwingAsync(
                context, target, spell, damageResult.Damage, cancellationToken)
            .ConfigureAwait(false);
        target = dragonSwing.Target;
        damageResult = damageResult with { Damage = dragonSwing.Damage };
        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref session, ref target))
        {
            return (context, target, null, LegacyPlayerMagicReceiveAbortReason.None);
        }

        context = context with { Session = session };
        var backDamageEnteredAlive = session.Character.MinHitPoints > 0;
        var attacker = session.Character;
        (context, target, attacker, var backDamageAbortReason) = await TryApplyLegacyPlayerReceiveHeavensWonderAsync(
                context, target, attacker, cancellationToken)
            .ConfigureAwait(false);
        if (backDamageAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return (context, target, null, backDamageAbortReason);
        }
        session = context.Session ?? session;
        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref session, ref target))
        {
            return (context, target, null, LegacyPlayerMagicReceiveAbortReason.None);
        }

        context = context with { Session = session };
        attacker = session.Character;
        (context, target, attacker, backDamageAbortReason) = await TryApplyLegacyPlayerReceiveRevengeTailAsync(
                context, target, attacker, damageResult.Damage, cancellationToken,
                backDamageEnteredAlive: backDamageEnteredAlive)
            .ConfigureAwait(false);
        if (backDamageAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return (context, target, null, backDamageAbortReason);
        }
        session = context.Session ?? session;
        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref session, ref target))
        {
            return (context, target, null, LegacyPlayerMagicReceiveAbortReason.None);
        }

        context = context with { Session = session };
        damageResult = damageResult with
        {
            Damage = ApplyLegacyPlayerReceiveMudWall(target.Session, context.WorldData, damageResult.Damage),
        };
        return (context, target, damageResult, LegacyPlayerMagicReceiveAbortReason.None);
    }
}
