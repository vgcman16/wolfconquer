using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyPlayerCannonBarrageDamageAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        MonsterRuntimeStateStore monsterStates,
        CancellationToken cancellationToken)
    {
        if (HasLegacyStatusFlag(resourceSession.Character, LegacyRuntimeUpdateTicker.XpListFlag))
        {
            var hadCannonBarrage = HasLegacyStatusFlag(resourceSession.Character, LegacyCannonBarrageFlag);
            var xpSkillNow = DateTimeOffset.UtcNow;
            var xpListSession = await ApplyLegacyOpenXpSkillAsync(
                    context,
                    resourceSession,
                    LegacyCannonBarrageFlag,
                    RuntimeLegacyCannonBarrageExpiresAtUtcAttribute,
                    xpSkillNow,
                    cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy CannonBarrage XPList transition " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                    $"consumedXpList=True openedCannonBarrage=True refreshedCannonBarrage={hadCannonBarrage} " +
                    $"expiresAtUtc={xpListSession.Character.Attributes[RuntimeLegacyCannonBarrageExpiresAtUtcAttribute]}.")
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyStatusFlag(resourceSession.Character, LegacyCannonBarrageFlag))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CannonBarrage magic " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort}; CannonBarrage status flag is not active.")
                .ConfigureAwait(false);
            return;
        }

        var sobNpcStates = context.SobNpcStates;
        var pendingSobNpcTargets = sobNpcStates is null
            ? CountLegacyPirateBombPendingSobNpcTargets(context, resourceSession, query.X, query.Y)
            : 0;
        if (pendingSobNpcTargets > 0)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedMagic(
                        spell.Id,
                        sort: (int)spell.Sort,
                        targetKind: "sob-npc",
                        resource: SobNpcRuntimeStateResource,
                        reason:
                            "CannonBarrage monster and player /11 damage are modeled, but SOB runtime state is not loaded; " +
                            $"visibleSobNpcTargets={pendingSobNpcTargets}."))
                .ConfigureAwait(false);
        }

        var results = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result)>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var killedTrainingSobNpcTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var eventSobNpcSideEffectTargets = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var now = DateTimeOffset.UtcNow;
        foreach (var target in SelectLegacyCannonBarrageMonsterTargets(context, resourceSession, query.X, query.Y))
        {
            if (IsLegacyGuardMonsterFamily(target.Monster, target.MonsterFamily))
            {
                resourceSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                        context,
                        resourceSession,
                        cancellationToken)
                    .ConfigureAwait(false);
                context = context with { Session = resourceSession };
            }

            var requestedDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                resourceSession,
                context.WorldData,
                target.Monster,
                target.MonsterFamily,
                spell);
            var result = ApplyLegacyPlayerMonsterDamage(
                monsterStates,
                target.Monster,
                requestedDamage,
                now,
                resourceSession);
            results.Add((target, result));
            if (result.AlreadyDead)
            {
                continue;
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                target.Monster.Uid,
                result.RequestedDamage,
                Hit: result.RequestedDamage > 0,
                MsgAttackEffect.None));
        }

        foreach (var target in SelectLegacyCannonBarragePlayerTargets(context, resourceSession, spell, query.X, query.Y))
        {
            var activeTarget = target.ActiveSession;
            (activeTarget, var wildPhoenixDefended) =
                await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                        context,
                        activeTarget,
                        cancellationToken)
                    .ConfigureAwait(false);
            var damage = wildPhoenixDefended
                ? 1U / 11
                : CalculateLegacyPlayerPlayerPhysicalSpellDamage(
                    resourceSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData,
                    damageDivisor: 11);
            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                context,
                activeTarget,
                resourceSession.Character,
                damage,
                cancellationToken)
            .ConfigureAwait(false);
            if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
            {
                resourceSession = currentReceiveAttacker;
                context = context with { Session = resourceSession };
            }

            if (receiveOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return;
            }

            if (receiveOutcome.Result is not { } result)
            {
                continue;
            }
            playerResults.Add(result);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                damage,
                Hit: true,
                MsgAttackEffect.None));
        }

        if (sobNpcStates is not null)
        {
            foreach (var target in SelectLegacyCannonBarrageSobNpcTargets(context, resourceSession, query.X, query.Y))
            {
                var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                    resourceSession,
                    spell,
                    context.WorldData,
                    damageMultiplier: 1);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
                var result = sobNpcStates.ApplyDamage(
                    target.SobNpc,
                    damage,
                    now,
                    deathBehavior);
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

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, resourceSession, animation, cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = resourceSession;
        foreach (var result in playerResults)
        {
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    resourceSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (sobNpcStates is not null)
        {
            foreach (var killed in killedTrainingSobNpcTargets)
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        context,
                        resourceSession,
                        killed.SobNpc,
                        sobNpcStates.GetVisible(killed.SobNpc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var eventTarget in eventSobNpcSideEffectTargets)
            {
                if (eventTarget.Result.Killed &&
                    eventTarget.DeathBehavior == LegacySobNpcDeathBehavior.GateDemolition)
                {
                    await SendLegacyGuildWarGateDemolitionSobNpcUpdateAsync(
                            context,
                            resourceSession,
                            eventTarget.SobNpc,
                            sobNpcStates.GetVisible(eventTarget.SobNpc),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (eventTarget.Result.Killed &&
                    LegacySobNpcEventRules.TryGetGuildWarOwnershipChangeDwParam(eventTarget.SobNpc, out var ownershipDwParam) &&
                    TryGetLegacyGuildWarCaptureGuild(resourceSession, out var captureGuildId, out var captureGuildName))
                {
                    sobNpcStates.ResetHitpointsToMax(eventTarget.SobNpc, captureGuildName, captureGuildId);
                    await SendLegacyGuildWarCapturePillarSobNpcUpdateAsync(
                            context,
                            resourceSession,
                            eventTarget.SobNpc,
                            sobNpcStates.GetVisible(eventTarget.SobNpc),
                            captureGuildId,
                            ownershipDwParam,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                await LogLegacySobNpcEventSideEffectGapAsync(
                        context,
                        eventTarget.SobNpc,
                        eventTarget.Result,
                        spell)
                    .ConfigureAwait(false);
            }
        }

        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
        foreach (var (target, result) in results)
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
            else if (firstRetaliationTarget is null)
            {
                firstRetaliationTarget = target;
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

        if (firstRetaliationTarget is { } retaliationTarget)
        {
            await TrySendLegacyMonsterRetaliationAsync(
                    context,
                    updatedSession,
                    retaliationTarget.Monster,
                    retaliationTarget.MonsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var totalDamage = results
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            sobNpcResults.Aggregate(
                0UL,
                (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                totalDamage,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy CannonBarrage monster/player/SOB magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} targets={animationTargets.Count} " +
                $"playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} totalDamage={totalDamage}.")
            .ConfigureAwait(false);
        _ = updatedSession;
    }
}
