using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession?> ExecuteLegacyBloomOfDeathLayTrapRadiusAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        string logAction,
        CancellationToken cancellationToken)
    {
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result)>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var now = DateTimeOffset.UtcNow;

        if (context.MonsterStates is { } monsterStates)
        {
            foreach (var target in EnumerateVisibleMonsterTargets(context, resourceSession.Character))
            {
                if (LegacyGetDistance(
                        resourceSession.Character.X,
                        resourceSession.Character.Y,
                        target.Monster.X,
                        target.Monster.Y) > LegacyBloomOfDeathRange)
                {
                    continue;
                }

                var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    resourceSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    damage,
                    now,
                    resourceSession);
                monsterResults.Add((target, result));
                if (result.AlreadyDead || result.DamageApplied == 0)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    result.RequestedDamage,
                    Hit: true,
                    MsgAttackEffect.None));
            }
        }

        foreach (var target in EnumerateVisiblePlayerMagicTargets(context, resourceSession, spell))
        {
            var character = target.ActiveSession.Session.Character;
            if (LegacyGetDistance(
                    resourceSession.Character.X,
                    resourceSession.Character.Y,
                    character.X,
                    character.Y) > LegacyBloomOfDeathRange)
            {
                continue;
            }

            var damage = CalculateLegacyPlayerPlayerPhysicalSpellDamage(
                resourceSession,
                target.ActiveSession.Session,
                spell,
                context.WorldData,
                damageDivisor: 5);
            var receive = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    context,
                    target.ActiveSession,
                    resourceSession.Character,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false);
            if (receive.AttackerSession is { } carriedAttacker)
            {
                resourceSession = carriedAttacker;
                context = context with { Session = resourceSession };
            }
            if (receive.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return null;
            }
            if (receive.Result is not { } result)
            {
                continue;
            }
            if (result.DamageApplied == 0)
            {
                continue;
            }

            playerResults.Add(result);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                result.DamageApplied,
                Hit: true,
                MsgAttackEffect.None));
        }

        if (context.SobNpcStates is { } sobNpcStates)
        {
            foreach (var target in EnumerateVisibleSobNpcMagicTargets(context, resourceSession.Character))
            {
                if (LegacyGetDistance(
                        resourceSession.Character.X,
                        resourceSession.Character.Y,
                        target.SobNpc.X,
                        target.SobNpc.Y) > LegacyBloomOfDeathRange)
                {
                    continue;
                }

                var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                    resourceSession,
                    spell,
                    context.WorldData,
                    damageMultiplier: 1);
                var result = sobNpcStates.ApplyDamage(
                    target.SobNpc,
                    damage,
                    now,
                    LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc));
                sobNpcResults.Add((target.SobNpc, result));
                if (result.AlreadyDead || result.DamageApplied == 0)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                    result.DamageApplied,
                    Hit: true,
                    MsgAttackEffect.None));
            }
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets,
            spellEffect: 1);
        await SendPlayerSpellAnimationToVisibleAsync(context, resourceSession, animation, cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = resourceSession;
        foreach (var (target, result) in monsterResults)
        {
            if (result.AlreadyDead)
            {
                continue;
            }

            if (!result.Killed && LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
            {
                var damagedMonster = target.Monster with { Hitpoints = result.RemainingHitpoints };
                foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                {
                    await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                }
            }

            if (result.Killed)
            {
                var death = new MsgAttackPacket(new MsgAttackQuery(
                    Uid: CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
                    OpponentUid: target.Monster.Uid,
                    X: target.Monster.X,
                    Y: target.Monster.Y,
                    AttackType: MsgAttackType.Death,
                    Damage: 0,
                    KilledMonster: 1,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0));
                await SendMonsterCombatFrameToVisibleAsync(context, resourceSession, target.Monster, death, cancellationToken)
                    .ConfigureAwait(false);
                await TrySendLegacyMonsterDeathDropAsync(
                        context,
                        resourceSession,
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
                await TryApplyLegacyMonsterTeamExperienceAsync(
                        context,
                        updatedSession,
                        target.Monster,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                    context,
                    updatedSession,
                    target.Monster,
                    target.MonsterFamily,
                    result.DamageApplied,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var result in playerResults)
        {
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var (sobNpc, result) in sobNpcResults)
        {
            if (result.Killed &&
                context.SobNpcStates is { } bloomSobNpcStates &&
                LegacySobNpcEventRules.IsTrainingSob(sobNpc))
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        context,
                        updatedSession,
                        sobNpc,
                        bloomSobNpcStates.GetVisible(sobNpc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        const ulong sourceSpellExperienceDamage = 10_000UL;
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                sourceSpellExperienceDamage,
                cancellationToken)
            .ConfigureAwait(false);

        var totalDamage =
            monsterResults.Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            playerResults.Aggregate(0UL, (total, result) => total + result.DamageApplied) +
            sobNpcResults.Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy " + logAction + " " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} targetX={query.X} targetY={query.Y} " +
                $"targets={animationTargets.Count} totalDamage={totalDamage} " +
                $"spellExperienceDamage={sourceSpellExperienceDamage}.")
            .ConfigureAwait(false);
        return updatedSession;
    }
}
