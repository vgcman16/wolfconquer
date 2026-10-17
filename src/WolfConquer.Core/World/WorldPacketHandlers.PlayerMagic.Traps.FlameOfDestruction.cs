using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerFlameOfDestructionLayTrapMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (!LegacyLayTrap.IsFlameOfDestructionSpellId(spell.Id))
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

        var character = resourceSession.Character;
        var casterUid = CheckedUInt32(character.Uid, nameof(character.Uid));
        var casterX = CheckedUInt16(character.X, nameof(character.X));
        var casterY = CheckedUInt16(character.Y, nameof(character.Y));
        var spellLevel = (byte)Math.Min(playerSpell.Level, byte.MaxValue);
        var floorPacket = new LegacyFloorItemPacket(
            NextLegacyFloorItemUid(),
            LegacyLayTrap.FlameOfDestructionFloorItemId,
            query.X,
            query.Y,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            LegacyLayTrap.FlameOfDestructionFloorColor,
            ItemOwnerUid: 0,
            ReadAttributeUInt32(character.Attributes, "GuildID", "GuildId"),
            FlowerType: 0,
            Timer: 0,
            casterX,
            casterY,
            Name: "");

        var now = DateTimeOffset.UtcNow;
        var storedFloorEffectCount = 0;
        if (context.FloorItems is { } floorItems)
        {
            var effect = new FloorItemRuntimeEffect(
                spell.Id,
                casterX,
                casterY,
                now.AddMilliseconds(LegacyLayTrap.FlameOfDestructionInvokeMilliseconds),
                Distance: spell.MaxTargets,
                HitRadius: spell.MaxTargets,
                RemovalVisibilityRadius: LegacyLayTrap.FlameOfDestructionRemovalVisibilityRadius,
                RepeatMilliseconds: LegacyLayTrap.FlameOfDestructionRepeatMilliseconds,
                SpellLevel: spellLevel,
                LevelHu: playerSpell.UseSpellSoul,
                RuntimeOwnerUid: casterUid,
                NextRepeatAt: now);
            floorItems.AddVisible(FloorItemRuntimeEntry.ForEffect(character.MapId, floorPacket, effect), now);
            storedFloorEffectCount = 1;
        }
        else
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        FloorItemRuntimeStateResource,
                        "flameofdestruction-floor-effect-runtime",
                        "FlameofDestruction floor effect was sent, but floor item runtime state is not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                            LegacyParityGapDiagnostics.Fact("floorUid", floorPacket.Uid),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                        ]))
                .ConfigureAwait(false);
        }

        await SendFloorItemVisibleAsync(context, character, floorPacket, cancellationToken)
            .ConfigureAwait(false);

        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: 0,
            query.X,
            query.Y,
            spell.Id,
            spellLevel,
            NextSpell: 0,
            playerSpell.UseSpellSoul,
            SpellEffect: 0,
            Targets: []);
        await SendPlayerSpellAnimationToVisibleAsync(
                context with { Session = resourceSession },
                resourceSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy FlameofDestruction LayTrap magic " +
                $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={spellLevel} soulLevel={playerSpell.UseSpellSoul} casterX={casterX} casterY={casterY} " +
                $"targetX={query.X} targetY={query.Y} floorItemId={floorPacket.ItemId} " +
                $"storedFloorEffectCount={storedFloorEffectCount}; source=LayTrap.cs cases 16540/16550.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<int> SendDueLegacyFlameOfDestructionLayTrapFloorSpellsAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<WorldActiveSession> activeSessions,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (context.FloorItems is not { } floorItems)
        {
            return 0;
        }

        var processedCount = 0;
        var finalProcessedCount = 0;
        var repeatProcessedCount = 0;
        var animationTargetCount = 0;
        var removeViewerCount = 0;
        foreach (var spellId in new[] { LegacyLayTrap.FlameOfDestructionSpellId, LegacyLayTrap.FlameOfDestructionPassiveSpellId })
        {
            var repeatPulses = floorItems.ConsumeDueRepeatEffects(spellId, now);
            var repeatResult = await SendDueLegacyFlameOfDestructionRepeatPulsesAsync(
                    context,
                    activeSessions,
                    repeatPulses,
                    now,
                    cancellationToken)
                .ConfigureAwait(false);
            processedCount += repeatResult.ProcessedCount;
            repeatProcessedCount += repeatResult.ProcessedCount;
            animationTargetCount += repeatResult.AnimationTargetCount;

            var dueEffects = floorItems.ConsumeDueEffects(spellId, now);
            foreach (var entry in dueEffects)
            {
                if (entry.Effect is not { } effect)
                {
                    continue;
                }

                removeViewerCount += await SendLegacyLayTrapRemoveEffectAsync(
                        entry,
                        effect,
                        activeSessions,
                        cancellationToken,
                        visibilityCenterX: effect.OriginX,
                        visibilityCenterY: effect.OriginY)
                    .ConfigureAwait(false);
                processedCount++;
                finalProcessedCount++;
            }
        }

        if (repeatProcessedCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy FlameofDestruction LayTrap repeat pulse runtime " +
                    $"pulseCount={repeatProcessedCount} targetCount={animationTargetCount}; " +
                    "source=ThreadPool.cs cases 16540/16550 CheckElseInvocke bomb=1; " +
                    "FlameSigilRapid equipment bonus is applied to monster damage before Execute.")
                .ConfigureAwait(false);
        }

        if (finalProcessedCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy FlameofDestruction LayTrap final floor spell removal " +
                    $"trapCount={finalProcessedCount} removeViewerCount={removeViewerCount}; " +
                    "source=ThreadPool.cs cases 16540/16550 CheckInvocke sends RemoveEffect only.")
                .ConfigureAwait(false);
        }

        return processedCount;
    }

    private static async ValueTask<(int ProcessedCount, int AnimationTargetCount)> SendDueLegacyFlameOfDestructionRepeatPulsesAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<WorldActiveSession> activeSessions,
        IReadOnlyList<FloorItemRuntimeEntry> repeatPulses,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (repeatPulses.Count == 0)
        {
            return (0, 0);
        }

        var processedCount = 0;
        var animationTargetCount = 0;
        foreach (var entry in repeatPulses)
        {
            if (entry.Effect is not { } effect)
            {
                continue;
            }

            var ownerUid = effect.RuntimeOwnerUid != 0 ? effect.RuntimeOwnerUid : entry.Packet.ItemOwnerUid;
            if (ownerUid == 0 ||
                context.WorldData is not { } worldData ||
                !TryResolveLegacyLayTrapSourceCaseRuntimeSpell(worldData, effect, out var spell) ||
                !TryGetActiveSessionByUid(activeSessions, ownerUid, out var owner) ||
                owner.Session.Character.MapId != entry.MapId)
            {
                continue;
            }

            var ownerContext = context with
            {
                Connection = owner.Connection,
                Session = owner.Session,
            };
            var ownerSession = owner.Session;
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
                    if (!IsWithinLegacyLayTrapHitRadius(entry, target.Monster.X, target.Monster.Y) ||
                        !CanLegacyPlayerAttackMonster(ownerSession.Character, target.Monster, target.MonsterFamily, out _))
                    {
                        continue;
                    }

                    var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                        ownerSession,
                        worldData,
                        target.Monster,
                        target.MonsterFamily,
                        spell);
                    damage = AddLegacyFlameSigilRapidMonsterDamage(ownerSession, worldData, damage);
                    var result = ApplyLegacyPlayerMonsterDamage(
                        monsterStates,
                        target.Monster,
                        damage,
                        now,
                        ownerSession);
                    monsterResults.Add((target, result));
                    if (result.AlreadyDead || result.DamageApplied == 0)
                    {
                        continue;
                    }

                    animationTargets.Add(new LegacySpellAnimationTarget(
                        target.Monster.Uid,
                        MulDivToUInt32(CalculateLegacyFlameOfDestructionSoulDamage(damage, effect.LevelHu), 200, 100),
                        Hit: true,
                        MsgAttackEffect.None));
                }
            }

            var ownerLostDuringReceive = false;
            foreach (var target in EnumerateVisiblePlayerMagicTargets(ownerContext, ownerSession, spell))
            {
                var targetCharacter = target.ActiveSession.Session.Character;
                if (!IsWithinLegacyLayTrapHitRadius(
                        entry,
                        CheckedUInt16(targetCharacter.X, nameof(targetCharacter.X)),
                        CheckedUInt16(targetCharacter.Y, nameof(targetCharacter.Y))) ||
                    !CanLegacyPlayerMagicDamageTarget(ownerContext, ownerSession, target.ActiveSession.Session, spell, out _))
                {
                    continue;
                }

                var damage = CalculateLegacyPlayerPlayerPhysicalSpellDamage(
                    ownerSession,
                    target.ActiveSession.Session,
                    spell,
                    worldData,
                    damageDivisor: 1);
                var receiveOutcome = await ApplyLegacyBackDamageEligiblePlayerDamageAsync(
                        ownerContext,
                        target.ActiveSession,
                        ownerSession.Character,
                        damage,
                        cancellationToken,
                        postBackPhysicalSpellId: spell.Id,
                        postBackFlameDamageOnHuman: spell.DamageOnHuman)
                    .ConfigureAwait(false);
                if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
                {
                    ownerSession = currentReceiveAttacker;
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

                playerResults.Add(result);
                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                    CalculateLegacyFlameOfDestructionSoulDamage(result.DamageApplied, effect.LevelHu),
                    Hit: true,
                    MsgAttackEffect.None));
            }

            if (ownerLostDuringReceive)
            {
                // Repeat cadence is already consumed; final removal remains in the outer expiry pass.
                processedCount++;
                continue;
            }

            if (context.SobNpcStates is { } sobNpcStates)
            {
                foreach (var target in EnumerateVisibleSobNpcMagicTargets(ownerContext, ownerSession.Character))
                {
                    if (!IsWithinLegacyLayTrapHitRadius(
                            entry,
                            CheckedUInt16(target.SobNpc.X, nameof(target.SobNpc.X)),
                            CheckedUInt16(target.SobNpc.Y, nameof(target.SobNpc.Y))) ||
                        !CanLegacySobNpcMagicDamageTarget(ownerContext, ownerSession.Character, target.SobNpc, out _))
                    {
                        continue;
                    }

                    var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                        ownerSession,
                        spell,
                        worldData,
                        damageMultiplier: 1);
                    damage = MulDivToUInt32(damage, 10, 100);
                    var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc, worldData.StaticStatues);
                    var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                    if (result.AlreadyDead || result.DamageApplied == 0)
                    {
                        continue;
                    }

                    animationTargets.Add(new LegacySpellAnimationTarget(
                        CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                        CalculateLegacyFlameOfDestructionSoulDamage(damage, effect.LevelHu),
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
                entry.Packet.Uid,
                entry.Packet.X,
                entry.Packet.Y,
                effect.SpellId,
                effect.SpellLevel,
                effect.LevelHu,
                animationTargets,
                bombFlag: 1);
            await SendPlayerSpellAnimationToVisibleAsync(
                    ownerContext,
                    ownerSession,
                    animation,
                    cancellationToken)
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
                        Uid: ownerUid,
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
                        spell)
                    .ConfigureAwait(false);
            }

            processedCount++;
            animationTargetCount += animationTargets.Count;
        }

        return (processedCount, animationTargetCount);
    }

    private static uint ClampLegacyFlameOfDestructionPlayerDamage(uint damage, uint damageOnHuman)
    {
        if (damage >= damageOnHuman)
        {
            return AddLegacySaturating(damageOnHuman, 10_000);
        }

        if (damageOnHuman > damage)
        {
            damage = damageOnHuman;
        }

        if (damageOnHuman / 2 > damage)
        {
            damage = damageOnHuman / 2;
        }

        return damage;
    }

    private static uint CalculateLegacyFlameOfDestructionSoulDamage(uint damage, byte useSpellSoul)
    {
        _ = useSpellSoul;
        return damage;
    }
}
