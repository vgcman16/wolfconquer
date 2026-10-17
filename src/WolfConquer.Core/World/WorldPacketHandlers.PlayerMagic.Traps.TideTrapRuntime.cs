using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static async ValueTask<int> SendDueLegacyTideTrapFloorSpellsAsync(
        WorldPacketHandlerContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (context.FloorItems is not { } floorItems ||
            context.Sessions is not { } sessions)
        {
            return 0;
        }

        var activeSessions = sessions.Snapshot();
        if (activeSessions.Count == 0)
        {
            return 0;
        }

        var dueEffects = floorItems.ConsumeDueEffects(LegacyTideTrapSpellId, now);
        if (dueEffects.Count == 0)
        {
            return 0;
        }

        LegacyMagicRuntimeSpell? resolvedTrapAnimationSpell = null;
        if (context.WorldData is { } trapAnimationWorldData &&
            LegacyMagicRuntime.TryResolveSpell(trapAnimationWorldData, LegacyTideTrapSpellId, spellLevel: 0, out var trapSpell))
        {
            resolvedTrapAnimationSpell = trapSpell;
        }

        var processedCount = 0;
        var animationTargetCount = 0;
        var removeViewerCount = 0;
        foreach (var entry in dueEffects)
        {
            if (entry.Effect is not { } effect)
            {
                continue;
            }

            if (resolvedTrapAnimationSpell is not { } trapAnimationSpell ||
                context.WorldData is not { } worldData ||
                !LegacyMagicRuntime.TryResolveSpell(
                    worldData,
                    LegacyMoveLineSpellId,
                    effect.SpellLevel,
                    out var damageSpell) ||
                !TryGetActiveSessionByUid(activeSessions, entry.Packet.ItemOwnerUid, out var owner) ||
                owner.Session.Character.MapId != entry.MapId)
            {
                removeViewerCount += await SendLegacyTideTrapRemoveEffectAsync(
                        entry,
                        effect,
                        activeSessions,
                        cancellationToken)
                    .ConfigureAwait(false);
                processedCount++;
                continue;
            }

            var ownerContext = context with
            {
                Connection = owner.Connection,
                Session = owner.Session,
            };
            var ownerSession = owner.Session;
            var line = LegacyMoveLineGeometry.SelectCoordinates(
                effect.OriginX,
                effect.OriginY,
                entry.Packet.X,
                entry.Packet.Y,
                isValidLocation: null,
                distance: effect.Distance);

            var animationTargets = new List<LegacySpellAnimationTarget>();
            var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result)>();
            var playerResults = new List<LegacyPlayerMagicDamageResult>();
            var killedTrainingSobNpcTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
            var eventSobNpcSideEffectTargets = new List<(
                SobNpc SobNpc,
                SobNpcDamageResult Result,
                LegacySobNpcDeathBehavior DeathBehavior)>();

            if (context.MonsterStates is { } monsterStates)
            {
                foreach (var target in EnumerateVisibleMonsterTargets(ownerContext, ownerSession.Character))
                {
                    if (!LegacyMoveLineGeometry.IsWithinHitRadius(
                            line.Coordinates,
                            target.Monster.X,
                            target.Monster.Y,
                            effect.HitRadius))
                    {
                        continue;
                    }

                    var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                        ownerSession,
                        context.WorldData,
                        target.Monster,
                        target.MonsterFamily,
                        damageSpell);
                    var result = ApplyLegacyPlayerMonsterDamage(
                        monsterStates,
                        target.Monster,
                        damage,
                        now,
                        ownerSession);
                    monsterResults.Add((target, result));
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
            }

            var ownerLostDuringReceive = false;
            foreach (var target in EnumerateVisiblePlayerMagicTargets(ownerContext, ownerSession, damageSpell))
            {
                if (!LegacyMoveLineGeometry.IsWithinHitRadius(
                        line.Coordinates,
                        target.ActiveSession.Session.Character.X,
                        target.ActiveSession.Session.Character.Y,
                        effect.HitRadius))
                {
                    continue;
                }

                var damage = CalculateLegacyPlayerPlayerPhysicalSpellDamage(
                    ownerSession,
                    target.ActiveSession.Session,
                    damageSpell,
                    context.WorldData,
                    damageDivisor: 1);
                damage = MulDivToUInt32(damage, 60, 100);
                var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                        ownerContext,
                        target.ActiveSession,
                        ownerSession.Character,
                        damage,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (receiveOutcome.AttackerSession is { } receiveOwner)
                {
                    ownerSession = receiveOwner;
                    ownerContext = ownerContext with { Session = ownerSession };
                }

                if (receiveOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
                {
                    ownerLostDuringReceive = true;
                    break;
                }

                if (receiveOutcome.Result is not { } result)
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

            if (ownerLostDuringReceive)
            {
                // The due floor entry was already consumed; keep its authoritative removal.
                removeViewerCount += await SendLegacyTideTrapRemoveEffectAsync(
                        entry,
                        effect,
                        activeSessions,
                        cancellationToken)
                    .ConfigureAwait(false);
                processedCount++;
                continue;
            }

            if (context.SobNpcStates is { } sobNpcStates)
            {
                foreach (var target in EnumerateVisibleSobNpcMagicTargets(ownerContext, ownerSession.Character))
                {
                    if (!LegacyMoveLineGeometry.IsWithinHitRadius(
                            line.Coordinates,
                            target.SobNpc.X,
                            target.SobNpc.Y,
                            effect.HitRadius))
                    {
                        continue;
                    }

                    var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                        ownerSession,
                        damageSpell,
                        context.WorldData,
                        damageMultiplier: 1);
                    var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
                    var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
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
                CheckedUInt32(ownerSession.Character.Uid, nameof(ownerSession.Character.Uid)),
                effect.OriginX,
                effect.OriginY,
                LegacyTideTrapSpellId,
                trapAnimationSpell.Level,
                levelHu: 0,
                animationTargets);
            await SendPlayerSpellAnimationToVisibleAsync(ownerContext, ownerSession, animation, cancellationToken)
                .ConfigureAwait(false);

            var updatedOwnerSession = ownerSession;
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
                        await owner.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                    }
                }

                if (result.Killed)
                {
                    var death = new MsgAttackPacket(new MsgAttackQuery(
                        Uid: CheckedUInt32(ownerSession.Character.Uid, nameof(ownerSession.Character.Uid)),
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
                            ownerContext,
                            ownerSession,
                            target.Monster,
                            death,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await TrySendLegacyMonsterDeathDropAsync(
                            ownerContext,
                            ownerSession,
                            target.Monster,
                            target.MonsterFamily,
                            cancellationToken)
                        .ConfigureAwait(false);
                    updatedOwnerSession = await TryApplyLegacyBossDeathRewardsAsync(
                            ownerContext,
                            updatedOwnerSession,
                            target.Monster,
                            target.MonsterFamily,
                            cancellationToken)
                        .ConfigureAwait(false);
                    ownerContext = ownerContext with { Session = updatedOwnerSession };
                    await TryApplyLegacyMonsterTeamExperienceAsync(
                            ownerContext,
                            updatedOwnerSession,
                            target.Monster,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                updatedOwnerSession = await TryApplyLegacyMonsterExperienceAsync(
                        ownerContext,
                        updatedOwnerSession,
                        target.Monster,
                        target.MonsterFamily,
                        result.DamageApplied,
                        cancellationToken)
                    .ConfigureAwait(false);
                ownerContext = ownerContext with { Session = updatedOwnerSession };
            }

            foreach (var result in playerResults)
            {
                await SendLegacyPlayerMagicDamageUpdatesAsync(
                        ownerContext,
                        updatedOwnerSession,
                        result,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var killed in killedTrainingSobNpcTargets)
            {
                if (context.SobNpcStates is not null)
                {
                    await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                            ownerContext,
                            updatedOwnerSession,
                            killed.SobNpc,
                            context.SobNpcStates.GetVisible(killed.SobNpc),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            foreach (var eventTarget in eventSobNpcSideEffectTargets)
            {
                await LogLegacySobNpcEventSideEffectGapAsync(
                        ownerContext,
                        eventTarget.SobNpc,
                        eventTarget.Result,
                        damageSpell)
                    .ConfigureAwait(false);
            }

            removeViewerCount += await SendLegacyTideTrapRemoveEffectAsync(
                    entry,
                    effect,
                    activeSessions,
                    cancellationToken)
                .ConfigureAwait(false);
            processedCount++;
            animationTargetCount += animationTargets.Count;
        }

        if (processedCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy TideTrap floor spell runtime " +
                    $"trapCount={processedCount} targetCount={animationTargetCount} removeViewerCount={removeViewerCount}.")
                .ConfigureAwait(false);
        }

        return processedCount;
    }
}
