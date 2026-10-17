using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(
        WorldPacketHandlerContext Context,
        WorldActiveSession Target,
        LegacyPlayerPlayerPhysicalSpellDamageResult? DamageResult,
        LegacyPlayerMagicReceiveAbortReason AbortReason)>
        PrepareLegacyDirectPlayerPhysicalDamageAsync(
            WorldPacketHandlerContext context,
            WorldSession attackerSession,
            WorldActiveSession target,
            long attackerUid,
            long targetUid,
            CancellationToken cancellationToken)
    {
        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref attackerSession, ref target))
        {
            return (context, target, null, LegacyPlayerMagicReceiveAbortReason.None);
        }

        context = context with { Session = attackerSession };
        // Physical.OnPlayer returns before WildPhoenix, BackDmg and MudWall.
        if (HasLegacyStatusFlag(target.Session.Character, LegacyShurikenVortexFlag) ||
            HasLegacyStatusFlag(target.Session.Character, LegacyManiacDanceFlag))
        {
            return (context, target, new LegacyPlayerPlayerPhysicalSpellDamageResult(1, MsgAttackEffect.None), LegacyPlayerMagicReceiveAbortReason.None);
        }

        (target, var wildPhoenixDefended) = await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                context,
                target,
                cancellationToken)
            .ConfigureAwait(false);
        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref attackerSession, ref target))
        {
            return (context, target, null, LegacyPlayerMagicReceiveAbortReason.None);
        }

        context = context with { Session = attackerSession };
        if (wildPhoenixDefended)
        {
            return (context, target, new LegacyPlayerPlayerPhysicalSpellDamageResult(1, MsgAttackEffect.None), LegacyPlayerMagicReceiveAbortReason.None);
        }

        var damageResult = CalculateLegacyDirectPlayerPlayerPhysicalDamageResult(
            attackerSession,
            target.Session,
            context.WorldData);
        var attacker = attackerSession.Character;
        // BackDmg checks alive once, not again after a potentially lethal Wonder.
        var backDamageEnteredAlive = attacker.MinHitPoints > 0;
        (context, target, attacker, var backDamageAbortReason) = await TryApplyLegacyPlayerReceiveHeavensWonderAsync(
                context,
                target,
                attacker,
                cancellationToken)
            .ConfigureAwait(false);
        if (backDamageAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return (context, target, null, backDamageAbortReason);
        }
        attackerSession = context.Session ?? attackerSession;
        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref attackerSession, ref target))
        {
            return (context, target, null, LegacyPlayerMagicReceiveAbortReason.None);
        }

        context = context with { Session = attackerSession };
        attacker = attackerSession.Character;
        (context, target, attacker, backDamageAbortReason) = await TryApplyLegacyPlayerReceiveRevengeTailAsync(
                context,
                target,
                attacker,
                damageResult.Damage,
                cancellationToken,
                backDamageEnteredAlive: backDamageEnteredAlive)
            .ConfigureAwait(false);
        if (backDamageAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return (context, target, null, backDamageAbortReason);
        }
        attackerSession = context.Session ?? attackerSession;
        if (!TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref attackerSession, ref target))
        {
            return (context, target, null, LegacyPlayerMagicReceiveAbortReason.None);
        }

        context = context with { Session = attackerSession };

        // This physical tail precedes the incoming interaction and receive hooks.
        damageResult = damageResult with
        {
            Damage = ApplyLegacyPlayerReceiveMudWall(target.Session, context.WorldData, damageResult.Damage),
        };
        return (context, target, damageResult, LegacyPlayerMagicReceiveAbortReason.None);
    }
}
