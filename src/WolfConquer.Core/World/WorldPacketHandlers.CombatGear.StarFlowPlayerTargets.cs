using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(WorldSession OwnerSession, LegacyPlayerMagicReceiveAbortReason AbortReason)>
        ReceiveLegacyStarFlowPlayerTargetsAsync(
            WorldPacketHandlerContext context, WorldSession session, LegacyMagicRuntimeSpell spell,
            WorldDataCatalog worldData, ushort originX, ushort originY,
            List<LegacyPlayerMagicDamageResult> playerResults,
            List<LegacySpellAnimationTarget> animationTargets, CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
            return (session, LegacyPlayerMagicReceiveAbortReason.None);
        var ownerUid = session.Character.Uid;
        foreach (var candidate in sessions.Snapshot())
        {
            var target = candidate;
            var targetUid = target.Session.Character.Uid;
            if (ReferenceEquals(target.Connection, context.Connection) || targetUid == ownerUid) continue;
            if (!TryRefreshLegacyPlayerMagicResourceSession(context, ownerUid, ref session))
                return (session, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
            if (!TryRefreshLegacyDirectPhysicalParticipants(
                    context, ownerUid, targetUid, ref session, ref target)) continue;
            // Native case10950 tests its captured query radius BEFORE Verified/effects/RNG.
            if (!CanSeePlayer(session.Character, target.Session.Character) ||
                LegacyGetDistance(originX, originY, target.Session.Character.X, target.Session.Character.Y) > 2.5)
                continue;
            context = context with { Session = session };
            var entry = await TryEnterLegacyStarFlowPlayerTargetAsync(
                    context, session, target, spell, cancellationToken).ConfigureAwait(false);
            session = entry.OwnerSession;
            target = entry.Target;
            context = context with { Session = session };
            if (entry.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
                return (session, entry.AbortReason);
            if (!entry.Allowed || entry.AbortReason == LegacyPlayerMagicReceiveAbortReason.TargetLost) continue;

            var damage = CalculateLegacyPlayerPlayerPhysicalSpellDamage(
                session, target.Session, spell, worldData, damageDivisor: 1);
            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    context, target, session.Character, damage, cancellationToken)
                .ConfigureAwait(false);
            if (receiveOutcome.AttackerSession is { } currentAttacker)
            {
                session = currentAttacker;
                context = context with { Session = session };
            }
            if (receiveOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
                return (session, receiveOutcome.AbortReason);
            if (receiveOutcome.Result is not { } result) continue;
            playerResults.Add(result);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                result.DamageApplied, Hit: result.DamageApplied > 0, MsgAttackEffect.None));
        }
        return (session, LegacyPlayerMagicReceiveAbortReason.None);
    }
}
