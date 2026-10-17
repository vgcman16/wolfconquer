using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool HasLegacyNinjaHellVortexEquipped(CharacterSnapshot character)
    {
        return ReadAttributeBool(
            character.Attributes,
            RuntimeLegacyNinjaHellVortexEquippedAttribute,
            "NinjaHellVortexEquipped",
            "HellVortexEquipped");
    }

    private static bool CheckLegacyHellVortexRateRoll(CharacterSnapshot character)
    {
        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyHellVortexRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return roll < 10;
    }

    private static bool IsLegacyHellVortexSpell(LegacyMagicRuntimeSpell spell)
    {
        return spell.Id == LegacyHellVortexSpellId && spell.Sort == LegacyMagicSort.HellVortex;
    }

    private static async ValueTask<LegacyMonsterResponseDamageResult> TrySendLegacyMonsterHellVortexResponseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (context.MonsterStates is not { } monsterStates ||
            context.WorldData is not { } worldData ||
            session.Character.MinHitPoints <= 0 ||
            !HasLegacyNinjaHellVortexEquipped(session.Character) ||
            !CheckLegacyHellVortexRateRoll(session.Character) ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                LegacyHellVortexSpellId,
                out var playerSpell,
                out var spell) ||
            !IsLegacyHellVortexSpell(spell))
        {
            return new LegacyMonsterResponseDamageResult(session, Matched: false, InterruptsRetaliation: false);
        }

        var selectedTargets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(monster.X, monster.Y, candidate.Monster.X, candidate.Monster.Y) > LegacyHellVortexRadius)
            {
                continue;
            }

            selectedTargets.Add(candidate);
        }

        var results = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result)>();
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var now = DateTimeOffset.UtcNow;
        var updatedSession = session;
        foreach (var target in selectedTargets)
        {
            var requestedDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                updatedSession,
                worldData,
                target.Monster,
                target.MonsterFamily,
                spell);
            var result = ApplyLegacyPlayerMonsterDamage(
                monsterStates,
                target.Monster,
                requestedDamage,
                now,
                updatedSession);
            results.Add((target, result));
            if (result.AlreadyDead)
            {
                continue;
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                target.Monster.Uid,
                result.DamageApplied,
                Hit: result.DamageApplied > 0,
                MsgAttackEffect.None));
            updatedSession = WithLegacyThundercloudSight(context, updatedSession, target.Monster);
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            0,
            0,
            LegacyHellVortexSpellId,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        foreach (var (target, result) in results)
        {
            if (result.AlreadyDead)
            {
                continue;
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
                await SendMonsterCombatFrameToVisibleAsync(context, updatedSession, target.Monster, death, cancellationToken)
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
                await TryApplyLegacyMonsterTeamExperienceAsync(context, updatedSession, target.Monster, cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
            {
                var updatedMonster = target.Monster with { Hitpoints = result.RemainingHitpoints };
                foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(updatedMonster))
                {
                    await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        var totalDamage = results
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer sent legacy monster HellVortex response " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                $"spellId={LegacyHellVortexSpellId} targets={animationTargets.Count} totalDamage={totalDamage}; " +
                "source=MsgMonster.ActionHandler.CheckRespouseDamage HellVortex.")
            .ConfigureAwait(false);
        return new LegacyMonsterResponseDamageResult(updatedSession, Matched: true, InterruptsRetaliation: true);
    }
}
