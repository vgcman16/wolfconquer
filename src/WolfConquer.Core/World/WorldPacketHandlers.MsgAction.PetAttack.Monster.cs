using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<LegacyThundercloudPetAttackResult?>
        TryHandleLegacyThundercloudMonsterPetAttackAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LivePetProjection pet,
        LegacyPlayerMonsterMagicTarget target,
        LegacyMagicRuntimeSpell callPetSpell,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData ||
            context.MonsterStates is not { } monsterStates ||
            !CanLegacyPlayerAttackMonster(
                session.Character,
                target.Monster,
                target.MonsterFamily,
                out _))
        {
            return null;
        }

        var updatedSession = session;
        if (IsLegacyGuardMonsterFamily(target.Monster, target.MonsterFamily))
        {
            updatedSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                    context,
                    updatedSession,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };
        }

        var thunderboltActive = pet.ThunderboltExpiresAtUtc is { } expiresAtUtc && expiresAtUtc > now;
        var rangeDamage = CalculateLegacyThundercloudMonsterRangeDamage(
            updatedSession,
            worldData,
            target.Monster,
            target.MonsterFamily,
            thunderboltActive);
        if ((rangeDamage.Effect & MsgAttackEffect.LuckyStrike) != 0)
        {
            var ownerUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
            var refineEffect = new LegacyMsgRefineEffectPacket(
                ownerUid,
                Param: 0,
                Effect: LegacyRefineEffect.LuckyStrike);
            await SendFrameToPlayerAndVisiblePeersAsync(
                    context,
                    context.Connection,
                    updatedSession,
                    refineEffect.ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var requestedDamage = MulDivToUInt32(rangeDamage.Damage, callPetSpell.Damage2, 50);
        updatedSession = await TryConsumeLegacySlayerAsync(
                context,
                updatedSession,
                rangeDamage.ConsumesSlayer,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var result = ApplyLegacyPlayerMonsterDamage(
            monsterStates,
            target.Monster,
            requestedDamage,
            now,
            updatedSession);
        if (result.AlreadyDead)
        {
            return null;
        }

        if (result.Killed)
        {
            var death = new MsgAttackPacket(new MsgAttackQuery(
                Uid: CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                OpponentUid: target.Monster.Uid,
                X: target.Monster.X,
                Y: target.Monster.Y,
                AttackType: MsgAttackType.Death,
                Damage: 0,
                KilledMonster: 1,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0));
            await SendMonsterCombatFrameToVisibleAsync(
                    context,
                    updatedSession,
                    target.Monster,
                    death,
                    cancellationToken)
                .ConfigureAwait(false);
            await TrySendLegacyMonsterDeathDropAsync(
                    context,
                    updatedSession,
                    target.Monster,
                    target.MonsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyBossDeathRewardsAsync(
                    context,
                    updatedSession,
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
        }
        else
        {
            if (LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
            {
                var damagedMonster = target.Monster with { Hitpoints = result.RemainingHitpoints };
                foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                {
                    await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            if (result.DamageApplied > 0)
            {
                await TrySendLegacyMonsterRetaliationAsync(
                        context,
                        updatedSession,
                        target.Monster,
                        target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return new LegacyThundercloudPetAttackResult(
            updatedSession,
            "monster",
            target.Monster.X,
            target.Monster.Y,
            new LegacySpellAnimationTarget(
                target.Monster.Uid,
                requestedDamage,
                Hit: true,
                rangeDamage.Effect),
            CalculateLegacyStrikeMonsterProgressDamage(updatedSession, target, requestedDamage));
    }
}
