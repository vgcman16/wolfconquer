using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyClassicBombSnowSpellId = 5_010;
    private const ushort LegacyClassicBombRageSpellId = 7_020;
    private const ushort LegacyClassicBombArrowRainSpellId = 8_030;

    private static async ValueTask<bool> HandleLegacyPlayerClassicBombMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.Bomb ||
            spell.Id is not (LegacyClassicBombSnowSpellId or
                LegacyClassicBombRageSpellId or
                LegacyClassicBombArrowRainSpellId))
        {
            return false;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        var updatedSession = resourceSession;
        var updatedContext = context with { Session = updatedSession };
        var removedXpList = false;
        if (spell.Id == LegacyClassicBombArrowRainSpellId &&
            HasLegacyStatusFlag(updatedSession.Character, LegacyRuntimeUpdateTicker.XpListFlag))
        {
            removedXpList = true;
            updatedSession = updatedSession with
            {
                Character = WithoutLegacyStatusFlag(
                    updatedSession.Character,
                    LegacyRuntimeUpdateTicker.XpListFlag),
            };
            context.Sessions?.Set(context.Connection, updatedSession);
            updatedContext = context with { Session = updatedSession };

            var casterUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(casterUid, value: 0).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    updatedContext,
                    updatedSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result, uint Damage)>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var killedTrainingSobNpcTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var eventSobNpcSideEffectTargets = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();
        var now = DateTimeOffset.UtcNow;

        if (updatedContext.MonsterStates is { } monsterStates)
        {
            foreach (var target in SelectLegacyAttackStatus7000MonsterTargets(
                         updatedContext,
                         updatedSession,
                         spell))
            {
                var rangeDamage = CalculateLegacyAttackStatus7000MonsterRangeDamage(
                    updatedSession,
                    updatedContext.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    rangeDamage.Damage,
                    now,
                    updatedSession);
                monsterResults.Add((target, result, rangeDamage.Damage));
                if (result.AlreadyDead)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    result.RequestedDamage,
                    Hit: result.RequestedDamage > 0,
                    rangeDamage.Effect));
            }
        }

        var playerTargets = SelectLegacyAttackStatus7000PlayerTargets(
            updatedContext,
            updatedSession,
            spell);
        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                updatedContext,
                updatedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = context with { Session = updatedSession };

        foreach (var target in playerTargets)
        {
            var activeTarget = target.ActiveSession;
            if (updatedContext.Sessions?.TryGet(target.ActiveSession.Connection, out var refreshedTarget) == true &&
                refreshedTarget is not null)
            {
                activeTarget = target.ActiveSession with { Session = refreshedTarget };
            }

            var statusDefended =
                HasLegacyStatusFlag(activeTarget.Session.Character, LegacyShurikenVortexFlag) ||
                HasLegacyStatusFlag(activeTarget.Session.Character, LegacyManiacDanceFlag);
            var wildPhoenixDefended = false;
            if (!statusDefended)
            {
                (activeTarget, wildPhoenixDefended) =
                    await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                            updatedContext,
                            activeTarget,
                            cancellationToken)
                        .ConfigureAwait(false);
            }

            LegacyPlayerPlayerPhysicalSpellDamageResult physicalDamage;
            if (statusDefended)
            {
                physicalDamage = new LegacyPlayerPlayerPhysicalSpellDamageResult(
                    CalculateLegacyPlayerRangeStatusReturnDamage(
                        updatedSession,
                        activeTarget.Session,
                        updatedContext.WorldData),
                    MsgAttackEffect.None);
            }
            else if (wildPhoenixDefended)
            {
                physicalDamage = new LegacyPlayerPlayerPhysicalSpellDamageResult(1U, MsgAttackEffect.None);
            }
            else
            {
                physicalDamage = CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    updatedContext.WorldData,
                    damageDivisor: 1);
            }

            var damage = CalculateLegacyAttackStatus7000SoulDamage(
                physicalDamage.Damage,
                playerSpell.UseSpellSoul);
            var effect = physicalDamage.Effect;
            LegacyPlayerMagicReceiveOutcome receiveOutcome;
            if (statusDefended || wildPhoenixDefended)
            {
                receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    updatedContext,
                    activeTarget,
                    updatedSession.Character,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false);
            }
            else if (HasLegacyStatusFlag(
                         activeTarget.Session.Character,
                         LegacyDefensiveStanceFlag))
            {
                damage = LegacyCombatDamageCalculator.DecreaseLegacyBless(damage, 40);
                effect = MsgAttackEffect.Block;
                receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    updatedContext,
                    activeTarget,
                    updatedSession.Character,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false);
            }
            else
            {
                receiveOutcome = await ApplyLegacyBackDamageEligiblePlayerDamageAsync(
                        updatedContext,
                        activeTarget,
                        updatedSession.Character,
                        damage,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
            {
                updatedSession = currentReceiveAttacker;
                updatedContext = updatedContext with { Session = updatedSession };
            }

            if (receiveOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return true;
            }

            if (receiveOutcome.Result is not { } result)
            {
                continue;
            }

            if (result.UpdatedAttackerSession is not null)
            {
                updatedSession = result.UpdatedAttackerSession;
                updatedContext = context with { Session = updatedSession };
            }

            playerResults.Add(result);
            await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                    updatedContext,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    updatedContext,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                damage,
                Hit: true,
                effect));
        }

        if (updatedContext.SobNpcStates is { } sobNpcStates)
        {
            foreach (var target in SelectLegacyAttackStatus7000SobNpcTargets(
                         updatedContext,
                         updatedSession,
                         spell))
            {
                var damage = CalculateLegacyAttackStatus7000SobNpcRangeDamage(
                    updatedSession,
                    spell,
                    updatedContext.WorldData);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
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

                if (result.Killed && LegacySobNpcEventRules.IsTrainingSob(target.SobNpc))
                {
                    killedTrainingSobNpcTargets.Add((target.SobNpc, result));
                }
                else if (!LegacySobNpcEventRules.IsTrainingSob(target.SobNpc))
                {
                    eventSobNpcSideEffectTargets.Add((target.SobNpc, result, deathBehavior));
                }
            }
        }

        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
        foreach (var (target, result, damage) in monsterResults)
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
                    await updatedContext.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                        .ConfigureAwait(false);
                }
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
                        updatedContext,
                        updatedSession,
                        target.Monster,
                        death,
                        cancellationToken)
                    .ConfigureAwait(false);
                await TrySendLegacyMonsterDeathDropAsync(
                        updatedContext,
                        updatedSession,
                        target.Monster,
                        target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                updatedSession = await TryApplyLegacyBossDeathRewardsAsync(
                        updatedContext,
                        updatedSession,
                        target.Monster,
                        target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                updatedContext = context with { Session = updatedSession };
                await TryApplyLegacyMonsterTeamExperienceAsync(
                        updatedContext,
                        updatedSession,
                        target.Monster,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (firstRetaliationTarget is null)
            {
                firstRetaliationTarget = target;
            }

            updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                    updatedContext,
                    updatedSession,
                    target.Monster,
                    target.MonsterFamily,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = context with { Session = updatedSession };
        }

        if (firstRetaliationTarget is { } retaliationTarget)
        {
            await TrySendLegacyMonsterRetaliationAsync(
                    updatedContext,
                    updatedSession,
                    retaliationTarget.Monster,
                    retaliationTarget.MonsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await TryApplyLegacyPlayerMagicChillingSnowAsync(
                updatedContext,
                updatedSession,
                playerResults,
                cancellationToken)
            .ConfigureAwait(false);

        if (updatedContext.SobNpcStates is { } currentSobNpcStates)
        {
            foreach (var killed in killedTrainingSobNpcTargets)
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        updatedContext,
                        updatedSession,
                        killed.SobNpc,
                        currentSobNpcStates.GetVisible(killed.SobNpc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var eventTarget in eventSobNpcSideEffectTargets)
            {
                await LogLegacySobNpcEventSideEffectGapAsync(
                        updatedContext,
                        eventTarget.SobNpc,
                        eventTarget.Result,
                        spell)
                    .ConfigureAwait(false);
            }
        }

        var totalMonsterDamage = monsterResults
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.Damage);
        var totalSobNpcExperienceDamage = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                updatedContext,
                updatedSession,
                playerSpell,
                spell,
                totalMonsterDamage + totalSobNpcExperienceDamage,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = context with { Session = updatedSession };

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(
                updatedContext,
                updatedSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        await updatedContext.Log.WriteLineAsync(
                "WorldServer handled legacy classic Bomb magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} targets={animationTargets.Count} " +
                $"monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} " +
                $"sobNpcTargets={sobNpcResults.Count} removedXpList={removedXpList} " +
                $"totalMonsterDamage={totalMonsterDamage} " +
                $"totalSobNpcExperienceDamage={totalSobNpcExperienceDamage}; " +
                "supportedSpellIds=5010,7020,8030; " +
                "source=Bomb.cs cases 5010/7020/8030 Range.OnMonster/OnPlayer/OnNpcs slice.")
            .ConfigureAwait(false);
        return true;
    }
}
