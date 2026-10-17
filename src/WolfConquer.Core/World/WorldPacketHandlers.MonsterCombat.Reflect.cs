using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool HasLegacyMonsterResponseReflect(CharacterSnapshot character)
    {
        return IsLegacyWarriorClass(character.SecondClass);
    }

    private static bool CheckLegacyMonsterResponseRateRoll(CharacterSnapshot character)
    {
        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyMonsterResponseReflectRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return roll < 30;
    }

    private static async ValueTask<bool> TrySendLegacyMonsterReflectResponseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (context.MonsterStates is not { } monsterStates ||
            session.Character.MinHitPoints <= 0 ||
            !HasLegacyMonsterResponseReflect(session.Character))
        {
            return false;
        }

        var damageResult = CalculateLegacyMonsterPhysicalDamage(
            monster,
            monsterFamily,
            session,
            context.WorldData,
            melee: true);
        session = await TryConsumeLegacyHawksAmbitionAsync(
                context,
                session,
                damageResult.ConsumesHawksAmbition,
                cancellationToken)
            .ConfigureAwait(false);
        var damage = damageResult.Damage / 60U;
        if (damage == 0)
        {
            damage = 1;
        }

        var result = ApplyLegacyPlayerMonsterDamage(
            monsterStates,
            monster,
            damage,
            DateTimeOffset.UtcNow,
            session);
        if (result.AlreadyDead)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy monster reflect response " +
                    $"{FormatSession(context)} monsterUid={monster.Uid}; monster is waiting to respawn.")
                .ConfigureAwait(false);
            return true;
        }

        var appliedDamage = result.DamageApplied;
        var attack = new MsgAttackPacket(new MsgAttackQuery(
            Uid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            OpponentUid: monster.Uid,
            X: CheckedUInt16(session.Character.X, nameof(session.Character.X)),
            Y: CheckedUInt16(session.Character.Y, nameof(session.Character.Y)),
            AttackType: MsgAttackType.Reflect,
            Damage: checked((int)appliedDamage),
            KilledMonster: 0,
            ResponseDamage: appliedDamage,
            Effect: MsgAttackEffect.None,
            Unknown: 0));
        await SendMonsterCombatFrameToVisibleAsync(context, session, monster, attack, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Killed && LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(monster))
        {
            var updatedMonster = monster with { Hitpoints = result.RemainingHitpoints };
            foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(updatedMonster))
            {
                await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            }
        }

        if (result.Killed)
        {
            var death = new MsgAttackPacket(new MsgAttackQuery(
                Uid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Death,
                Damage: 0,
                KilledMonster: 1,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0));
            await SendMonsterCombatFrameToVisibleAsync(context, session, monster, death, cancellationToken)
                .ConfigureAwait(false);
            await TrySendLegacyMonsterDeathDropAsync(context, session, monster, monsterFamily, cancellationToken)
                .ConfigureAwait(false);
            session = await TryApplyLegacyBossDeathRewardsAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            await TryApplyLegacyMonsterTeamExperienceAsync(context, session, monster, cancellationToken)
                .ConfigureAwait(false);
        }

        session = await TryApplyLegacyMonsterExperienceAsync(
                context,
                session,
                monster,
                monsterFamily,
                damage,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy monster reflect response " +
                $"{FormatSession(context with { Session = session })} monsterUid={monster.Uid} " +
                $"damage={appliedDamage} remainingHitpoints={result.RemainingHitpoints} killed={result.Killed}; " +
                "source=MsgMonster.ActionHandler.CheckRespouseDamage reflect.")
            .ConfigureAwait(false);
        return true;
    }
}
