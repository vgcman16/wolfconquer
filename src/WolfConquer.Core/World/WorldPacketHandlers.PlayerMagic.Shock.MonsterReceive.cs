using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> PublishLegacyShockMonsterReceiveAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyPlayerMonsterMagicTarget target,
        MonsterDamageResult result,
        CancellationToken cancellationToken)
    {
        if (!result.Killed)
        {
            if (LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
            {
                var damagedMonster = target.Monster with { Hitpoints = result.RemainingHitpoints };
                foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                {
                    await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                }
            }

            return session;
        }

        var death = new MsgAttackPacket(new MsgAttackQuery(
            Uid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            OpponentUid: target.Monster.Uid,
            X: target.Monster.X,
            Y: target.Monster.Y,
            AttackType: MsgAttackType.Death,
            Damage: 0,
            KilledMonster: 1,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0));
        await SendMonsterCombatFrameToVisibleAsync(context, session, target.Monster, death, cancellationToken)
            .ConfigureAwait(false);
        await TrySendLegacyMonsterDeathDropAsync(
                context,
                session,
                target.Monster,
                target.MonsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        var updatedSession = await TryApplyLegacyBossDeathRewardsAsync(
                context,
                session,
                target.Monster,
                target.MonsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };
        await TryApplyLegacyMonsterTeamExperienceAsync(
                context,
                updatedSession,
                target.Monster,
                cancellationToken)
            .ConfigureAwait(false);
        return updatedSession;
    }
}
