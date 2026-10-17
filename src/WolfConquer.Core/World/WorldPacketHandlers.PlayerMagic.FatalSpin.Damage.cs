using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<LegacyFatalSpinPassResult?> ExecuteLegacyFatalSpinDamagePassAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        byte lineRange,
        LegacyFatalSpinPassProfile profile,
        CancellationToken cancellationToken)
    {
        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyFatalSpinMonsterTargets(
                context,
                session,
                query,
                spell,
                lineRange,
                profile.LineWidth)
            : [];
        var playerTargets = SelectLegacyFatalSpinPlayerTargets(
            context,
            session,
            query,
            spell,
            lineRange,
            profile.LineWidth,
            profile.PlayerDistanceLimit,
            profile.PlayerDistanceInclusive);
        var sobNpcTargets = profile.IncludeSobNpcTargets && context.SobNpcStates is not null
            ? SelectLegacyFatalSpinSobNpcTargets(
                context,
                session,
                query,
                spell,
                lineRange,
                profile.LineWidth)
            : [];

        var followUpAnchor = profile.TrackFollowUpAnchor
            ? SelectLegacyFatalSpinFollowUpAnchor(monsterTargets, playerTargets)
            : null;
        var monsterPass = await ApplyLegacyFatalSpinMonsterDamageAsync(
                context,
                session,
                monsterTargets,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = monsterPass.Session };
        var playerPass = await ApplyLegacyFatalSpinPlayerDamageAsync(
                context,
                monsterPass.Session,
                playerTargets,
                playerSpell,
                spell,
                profile.PlayerDamageNumerator,
                profile.PlayerDamageDenominator,
                profile.ApplyPlayerCanAttackSideEffects,
                cancellationToken)
            .ConfigureAwait(false);
        if (playerPass is null)
        {
            return null;
        }
        context = context with { Session = playerPass.Session };
        var sobNpcPass = await ApplyLegacyFatalSpinSobNpcDamageAsync(
                context,
                playerPass.Session,
                sobNpcTargets,
                playerSpell,
                spell,
                profile.SobNpcDamageNumerator,
                profile.SobNpcDamageDenominator,
                cancellationToken)
            .ConfigureAwait(false);

        var animationTargets = new List<LegacySpellAnimationTarget>(
            monsterPass.AnimationTargets.Count +
            playerPass.AnimationTargets.Count +
            sobNpcPass.AnimationTargets.Count);
        animationTargets.AddRange(monsterPass.AnimationTargets);
        animationTargets.AddRange(playerPass.AnimationTargets);
        animationTargets.AddRange(sobNpcPass.AnimationTargets);

        return new LegacyFatalSpinPassResult(
            sobNpcPass.Session,
            animationTargets,
            unchecked(monsterPass.ProgressDamage + sobNpcPass.ProgressDamage),
            monsterTargets.Count,
            playerTargets.Count,
            sobNpcTargets.Count,
            unchecked(
                monsterPass.TotalAppliedDamage +
                playerPass.TotalAppliedDamage +
                sobNpcPass.TotalAppliedDamage),
            playerPass.CounterHitShoots,
            followUpAnchor);
    }

    private static async ValueTask<LegacyFatalSpinMonsterPassResult> ApplyLegacyFatalSpinMonsterDamageAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        IReadOnlyList<LegacyPlayerMonsterMagicTarget> targets,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var updatedSession = session;
        foreach (var target in targets)
        {
            if (!IsLegacyGuardMonsterFamily(target.Monster, target.MonsterFamily))
            {
                continue;
            }

            updatedSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                    context,
                    updatedSession,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };
            break;
        }

        var animations = new List<LegacySpellAnimationTarget>(targets.Count);
        var progressDamage = 0U;
        var totalAppliedDamage = 0UL;
        var now = DateTimeOffset.UtcNow;
        if (context.MonsterStates is not { } monsterStates)
        {
            return new LegacyFatalSpinMonsterPassResult(
                updatedSession,
                animations,
                progressDamage,
                totalAppliedDamage);
        }

        foreach (var target in targets)
        {
            var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                updatedSession,
                context.WorldData,
                target.Monster,
                target.MonsterFamily,
                spell);
            damage = CalculateLegacyFatalSpinSoulDamage(damage, playerSpell.UseSpellSoul);
            var result = ApplyLegacyPlayerMonsterDamage(
                monsterStates,
                target.Monster,
                damage,
                now,
                updatedSession);
            if (result.AlreadyDead)
            {
                continue;
            }

            progressDamage = unchecked(
                progressDamage + CalculateLegacyFatalSpinMonsterProgressDamage(updatedSession, target, damage));
            totalAppliedDamage = unchecked(totalAppliedDamage + result.DamageApplied);
            animations.Add(new LegacySpellAnimationTarget(
                target.Monster.Uid,
                damage,
                Hit: damage > 0,
                MsgAttackEffect.None));

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
            else if (LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
            {
                var damagedMonster = target.Monster with { Hitpoints = result.RemainingHitpoints };
                foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                {
                    await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        return new LegacyFatalSpinMonsterPassResult(
            updatedSession,
            animations,
            progressDamage,
            totalAppliedDamage);
    }

    private static async ValueTask<LegacyFatalSpinPlayerPassResult?> ApplyLegacyFatalSpinPlayerDamageAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        IReadOnlyList<LegacyPlayerMagicTarget> targets,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        uint damageNumerator,
        uint damageDenominator,
        bool applyCanAttackSideEffects,
        CancellationToken cancellationToken)
    {
        var updatedSession = session;
        if (applyCanAttackSideEffects)
        {
            updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                    context,
                    updatedSession,
                    targets,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };
        }

        updatedSession = ApplyLegacyCounterHitShoot(
            context,
            updatedSession,
            targets.Count,
            out var counterHitShoots);
        context = context with { Session = updatedSession };

        var animations = new List<LegacySpellAnimationTarget>(targets.Count);
        var playerResults = new List<LegacyPlayerMagicDamageResult>(targets.Count);
        var totalAppliedDamage = 0UL;
        foreach (var target in targets)
        {
            var activeTarget = target.ActiveSession;
            (activeTarget, var wildPhoenixDefended) =
                await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                        context,
                        activeTarget,
                        cancellationToken)
                    .ConfigureAwait(false);
            var physicalDamage = wildPhoenixDefended
                ? new LegacyPlayerPlayerPhysicalSpellDamageResult(1U, MsgAttackEffect.None)
                : CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData,
                    damageDivisor: 1);
            var damage = MulDivToUInt32(
                CalculateLegacyFatalSpinSoulDamage(physicalDamage.Damage, playerSpell.UseSpellSoul),
                damageNumerator,
                damageDenominator);
            var receive = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    context,
                    activeTarget,
                    updatedSession.Character,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false);
            if (receive.AttackerSession is { } carriedAttacker)
            {
                updatedSession = carriedAttacker;
                context = context with { Session = updatedSession };
            }
            if (receive.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return null;
            }
            if (receive.Result is not { } result)
            {
                continue;
            }

            playerResults.Add(result);
            totalAppliedDamage = unchecked(totalAppliedDamage + result.DamageApplied);
            animations.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                damage,
                Hit: true,
                physicalDamage.Effect));
            await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await TryApplyLegacyPlayerMagicChillingSnowAsync(
                context,
                updatedSession,
                playerResults,
                cancellationToken)
            .ConfigureAwait(false);
        return new LegacyFatalSpinPlayerPassResult(
            updatedSession,
            animations,
            totalAppliedDamage,
            counterHitShoots);
    }

    private static async ValueTask<LegacyFatalSpinSobNpcPassResult> ApplyLegacyFatalSpinSobNpcDamageAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        IReadOnlyList<LegacyPlayerSobNpcMagicTarget> targets,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        uint damageNumerator,
        uint damageDenominator,
        CancellationToken cancellationToken)
    {
        var animations = new List<LegacySpellAnimationTarget>(targets.Count);
        var progressDamage = 0U;
        var totalAppliedDamage = 0UL;
        if (context.SobNpcStates is not { } sobNpcStates)
        {
            return new LegacyFatalSpinSobNpcPassResult(
                session,
                animations,
                progressDamage,
                totalAppliedDamage);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var target in targets)
        {
            var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                session,
                spell,
                context.WorldData,
                damageMultiplier: 1);
            damage = CalculateLegacyFatalSpinSoulDamage(damage, playerSpell.UseSpellSoul);
            damage = MulDivToUInt32(damage, damageNumerator, damageDenominator);
            var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(
                target.SobNpc,
                context.WorldData?.StaticStatues);
            var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
            if (result.AlreadyDead)
            {
                continue;
            }

            progressDamage = unchecked(
                progressDamage + (uint)CalculateLegacySobNpcExperienceDamage(target.SobNpc, result));
            totalAppliedDamage = unchecked(totalAppliedDamage + result.DamageApplied);
            animations.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                damage,
                Hit: damage > 0,
                MsgAttackEffect.None));

            if (result.DamageApplied == 0)
            {
                continue;
            }

            if (result.Killed && LegacySobNpcEventRules.IsTrainingSob(target.SobNpc))
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        context,
                        session,
                        target.SobNpc,
                        sobNpcStates.GetVisible(target.SobNpc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (!LegacySobNpcEventRules.IsTrainingSob(target.SobNpc))
            {
                await LogLegacySobNpcEventSideEffectGapAsync(context, target.SobNpc, result, spell)
                    .ConfigureAwait(false);
            }
        }

        return new LegacyFatalSpinSobNpcPassResult(
            session,
            animations,
            progressDamage,
            totalAppliedDamage);
    }
}
