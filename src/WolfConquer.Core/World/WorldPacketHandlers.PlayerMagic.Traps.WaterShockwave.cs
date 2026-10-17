using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerWaterShockwaveLayTrapMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (!LegacyLayTrap.IsWaterShockwaveSpellId(spell.Id))
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
        var floorPacket = new LegacyFloorItemPacket(
            NextLegacyFloorItemUid(),
            LegacyLayTrap.WaterShockwaveFloorItemId,
            casterX,
            casterY,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            LegacyLayTrap.WaterShockwaveFloorColor,
            casterUid,
            ReadAttributeUInt32(character.Attributes, "GuildID", "GuildId"),
            FlowerType: 0,
            Timer: 0,
            OwnerX: query.X,
            OwnerY: query.Y,
            LegacyLayTrap.WaterShockwaveFloorName);

        var now = DateTimeOffset.UtcNow;
        var storedFloorEffectCount = 0;
        if (context.FloorItems is { } floorItems)
        {
            var effect = new FloorItemRuntimeEffect(
                spell.Id,
                query.X,
                query.Y,
                now.AddMilliseconds(LegacyLayTrap.WaterShockwaveInvokeMilliseconds),
                Distance: LegacyLayTrap.WaterShockwaveLineRange,
                HitRadius: LegacyLayTrap.WaterShockwaveLineWidth,
                RemovalVisibilityRadius: LegacyLayTrap.WaterShockwaveRemovalVisibilityRadius,
                RepeatMilliseconds: LegacyLayTrap.WaterShockwaveRepeatMilliseconds,
                SpellLevel: (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                LevelHu: playerSpell.UseSpellSoul,
                RuntimeOwnerUid: casterUid,
                NextRepeatAt: now);
            floorItems.AddVisible(
                FloorItemRuntimeEntry.ForEffect(character.MapId, floorPacket, effect),
                now);
            storedFloorEffectCount = 1;
        }
        else
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        FloorItemRuntimeStateResource,
                        "watershockwave-floor-effect-runtime",
                        "WaterShockwave floor effect was sent, but floor item runtime state is not loaded",
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
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
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
                "WorldServer handled legacy WaterShockwave LayTrap magic " +
                $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} soulLevel={playerSpell.UseSpellSoul} casterX={casterX} casterY={casterY} " +
                $"targetX={query.X} targetY={query.Y} floorItemId={floorPacket.ItemId} " +
                $"storedFloorEffectCount={storedFloorEffectCount}; source=LayTrap.cs cases 16310/16320.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<int> SendDueLegacyWaterShockwaveLayTrapFloorSpellsAsync(
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
        var animationTargetCount = 0;
        var removeViewerCount = 0;
        foreach (var spellId in new[] { LegacyLayTrap.WaterShockwaveSpellId, LegacyLayTrap.WaterShockwavePassiveSpellId })
        {
            var repeatPulses = floorItems.ConsumeDueRepeatEffects(spellId, now);
            var repeatProcessedCount = await SendDueLegacyWaterShockwaveRepeatPulsesAsync(
                    context,
                    activeSessions,
                    repeatPulses,
                    now,
                    cancellationToken)
                .ConfigureAwait(false);
            processedCount += repeatProcessedCount;

            var dueEffects = floorItems.ConsumeDueEffects(spellId, now);
            foreach (var entry in dueEffects)
            {
                if (entry.Effect is not { } effect)
                {
                    continue;
                }

                var ownerUid = effect.RuntimeOwnerUid != 0 ? effect.RuntimeOwnerUid : entry.Packet.ItemOwnerUid;
                if (ownerUid == 0 ||
                    context.WorldData is not { } worldData ||
                    !LegacyMagicRuntime.TryResolveSpell(worldData, effect.SpellId, effect.SpellLevel, out var spell) ||
                    !TryGetActiveSessionByUid(activeSessions, ownerUid, out var owner) ||
                    owner.Session.Character.MapId != entry.MapId)
                {
                    removeViewerCount += await SendLegacyLayTrapRemoveEffectAsync(
                            entry,
                            effect,
                            activeSessions,
                            cancellationToken)
                        .ConfigureAwait(false);
                    processedCount++;
                    finalProcessedCount++;
                    continue;
                }

                var ownerContext = context with
                {
                    Connection = owner.Connection,
                    Session = owner.Session,
                };
                var ownerSession = owner.Session;
                var line = LegacyMoveLineGeometry.SelectCoordinates(
                    entry.Packet.X,
                    entry.Packet.Y,
                    entry.Packet.OwnerX,
                    entry.Packet.OwnerY,
                    isValidLocation: null,
                    distance: effect.Distance);
                var pushbackAngle = ReadAttributeUInt32(ownerSession.Character.Attributes, "Angle", "Facing");

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
                                effect.HitRadius) ||
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

                        ushort monsterNewX = 0;
                        ushort monsterNewY = 0;
                        if (TryCalculateLegacyMonsterPushbackPosition(
                                ownerContext,
                                target.Monster,
                                pushbackAngle,
                                distance: 2,
                                out var pushedX,
                                out var pushedY))
                        {
                            monsterNewX = pushedX;
                            monsterNewY = pushedY;
                            if (monsterStates.TryMoveTo(target.Monster, pushedX, pushedY, now, out var moved))
                            {
                                monsterNewX = moved.X;
                                monsterNewY = moved.Y;
                            }
                        }

                        animationTargets.Add(new LegacySpellAnimationTarget(
                            target.Monster.Uid,
                            result.RequestedDamage,
                            Hit: true,
                            MsgAttackEffect.None,
                            NewX: monsterNewX,
                            NewY: monsterNewY));
                    }
                }

                var ownerLostDuringReceive = false;
                foreach (var target in EnumerateVisiblePlayerMagicTargets(ownerContext, ownerSession, spell))
                {
                    var targetCharacter = target.ActiveSession.Session.Character;
                    if (!LegacyMoveLineGeometry.IsWithinHitRadius(
                            line.Coordinates,
                            targetCharacter.X,
                            targetCharacter.Y,
                            effect.HitRadius) ||
                        !CanLegacyPlayerMagicDamageTarget(ownerContext, ownerSession, target.ActiveSession.Session, spell, out _))
                    {
                        continue;
                    }

                    var damage = CalculateLegacyWaterShockwavePlayerDamage(
                        ownerSession,
                        target.ActiveSession.Session,
                        spell,
                        worldData);
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

                    ushort playerNewX = 0;
                    ushort playerNewY = 0;
                    var sendPlayerAnimationTarget = true;
                    if (TryCalculateLegacyPlayerPushbackPosition(
                            ownerContext,
                            result.UpdatedSession,
                            pushbackAngle,
                            distance: 2,
                            out var pushedX,
                            out var pushedY,
                            out var guildWarEndpointCheck))
                    {
                        playerNewX = pushedX;
                        playerNewY = pushedY;
                        var pushedTargetSession = result.UpdatedSession with
                        {
                            Character = result.UpdatedSession.Character with
                            {
                                X = pushedX,
                                Y = pushedY,
                            },
                        };
                        context.Sessions?.Set(target.ActiveSession.Connection, pushedTargetSession);
                        result = result with
                        {
                            ActiveSession = result.ActiveSession with { Session = pushedTargetSession },
                            UpdatedSession = pushedTargetSession,
                        };
                    }
                    else if (guildWarEndpointCheck.Applies && !guildWarEndpointCheck.Allowed)
                    {
                        sendPlayerAnimationTarget = false;
                        await SendLegacyGuildWarPullbackAsync(
                                ownerContext,
                                target.ActiveSession.Connection,
                                result.UpdatedSession,
                                guildWarEndpointCheck,
                                "WaterShockwave",
                                cancellationToken)
                            .ConfigureAwait(false);
                    }

                    playerResults.Add(result);
                    if (sendPlayerAnimationTarget)
                    {
                        animationTargets.Add(new LegacySpellAnimationTarget(
                            CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                            result.DamageApplied,
                            Hit: true,
                            MsgAttackEffect.None,
                            NewX: playerNewX,
                            NewY: playerNewY));
                    }
                }

                if (ownerLostDuringReceive)
                {
                    // The due floor entry was already consumed; keep its authoritative removal.
                    removeViewerCount += await SendLegacyLayTrapRemoveEffectAsync(
                            entry,
                            effect,
                            activeSessions,
                            cancellationToken)
                        .ConfigureAwait(false);
                    processedCount++;
                    finalProcessedCount++;
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
                                effect.HitRadius) ||
                            !CanLegacySobNpcMagicDamageTarget(ownerContext, ownerSession.Character, target.SobNpc, out _))
                        {
                            continue;
                        }

                        var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                            ownerSession,
                            spell,
                            worldData,
                            damageMultiplier: 1);
                        var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc, worldData.StaticStatues);
                        var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                        if (result.AlreadyDead || result.DamageApplied == 0)
                        {
                            continue;
                        }

                        animationTargets.Add(new LegacySpellAnimationTarget(
                            CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                            MulDivToUInt32(damage, 45, 100),
                            Hit: true,
                            MsgAttackEffect.None,
                            NewX: CheckedUInt16(target.SobNpc.X, nameof(target.SobNpc.X)),
                            NewY: CheckedUInt16(target.SobNpc.Y, nameof(target.SobNpc.Y))));

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
                    entry.Packet.OwnerX,
                    entry.Packet.OwnerY,
                    effect.SpellId,
                    effect.SpellLevel,
                    effect.LevelHu,
                    animationTargets,
                    bombFlag: 2);
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

                removeViewerCount += await SendLegacyLayTrapRemoveEffectAsync(
                        entry,
                        effect,
                        activeSessions,
                        cancellationToken)
                    .ConfigureAwait(false);
                processedCount++;
                finalProcessedCount++;
                animationTargetCount += animationTargets.Count;
            }
        }

        if (finalProcessedCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy WaterShockwave LayTrap final floor spell runtime " +
                    $"trapCount={finalProcessedCount} targetCount={animationTargetCount} removeViewerCount={removeViewerCount}; " +
                    "source=ThreadPool.cs cases 16310/16320 bomb=1 repeat pulses and bomb=2 final invoke.")
                .ConfigureAwait(false);
        }

        return processedCount;
    }

    private static async ValueTask<int> SendDueLegacyWaterShockwaveRepeatPulsesAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<WorldActiveSession> activeSessions,
        IReadOnlyList<FloorItemRuntimeEntry> repeatPulses,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (repeatPulses.Count == 0)
        {
            return 0;
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
                !LegacyMagicRuntime.TryResolveSpell(worldData, effect.SpellId, effect.SpellLevel, out var spell) ||
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
                    if (LegacyGetDistance(entry.Packet.X, entry.Packet.Y, target.Monster.X, target.Monster.Y) > 7 ||
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
                        result.RequestedDamage,
                        Hit: true,
                        MsgAttackEffect.None));
                }
            }

            var ownerLostDuringReceive = false;
            foreach (var target in EnumerateVisiblePlayerMagicTargets(ownerContext, ownerSession, spell))
            {
                var targetCharacter = target.ActiveSession.Session.Character;
                if (LegacyGetDistance(entry.Packet.X, entry.Packet.Y, targetCharacter.X, targetCharacter.Y) > 6 ||
                    !CanLegacyPlayerMagicDamageTarget(ownerContext, ownerSession, target.ActiveSession.Session, spell, out _))
                {
                    continue;
                }

                var damage = spell.DamageOnHuman;
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
                // Abort only this consumed pulse; repeating/final cleanup remains with its owner.
                processedCount++;
                continue;
            }

            if (context.SobNpcStates is { } sobNpcStates)
            {
                foreach (var target in EnumerateVisibleSobNpcMagicTargets(ownerContext, ownerSession.Character))
                {
                    if (LegacyGetDistance(entry.Packet.X, entry.Packet.Y, target.SobNpc.X, target.SobNpc.Y) > 6 ||
                        !CanLegacySobNpcMagicDamageTarget(ownerContext, ownerSession.Character, target.SobNpc, out _))
                    {
                        continue;
                    }

                    var damage = spell.Damage2;
                    var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc, worldData.StaticStatues);
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

        if (processedCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy WaterShockwave LayTrap repeat pulse runtime " +
                    $"pulseCount={processedCount} targetCount={animationTargetCount}; " +
                    "source=ThreadPool.cs cases 16310/16320 CheckElseInvocke bomb=1.")
                .ConfigureAwait(false);
        }

        return processedCount;
    }

    private static uint CalculateLegacyWaterShockwavePlayerDamage(
        WorldSession attacker,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog? worldData)
    {
        var attackerFields = CalculateLegacyRuntimeStatusFields(attacker, worldData);
        var targetFields = CalculateLegacyRuntimeStatusFields(target, worldData);
        var damage = GetLegacyDeterministicAttack(
            attackerFields[LegacyPlayerStatusPacket.MaxAttackIndex],
            attackerFields[LegacyPlayerStatusPacket.MinAttackIndex]);
        damage = AddLegacyPercentBonus(damage, attackerFields[LegacyPlayerStatusPacket.PhysicalPercentIndex]);

        var attributeDefense = ReadAttributeUInt32(
            target.Character.Attributes,
            "AjustDefense",
            "AdjustDefense",
            "Defense",
            "Defence");
        var defense = Math.Max(attributeDefense, targetFields[LegacyPlayerStatusPacket.DefenceIndex]);
        damage = damage <= defense ? 1 : damage - defense;

        damage = MulDivToUInt32(damage, spell.Damage3 % 1_000, 180);
        damage = DecreaseLegacyBless(damage, targetFields[LegacyPlayerStatusPacket.ItemBlessIndex]);
        damage = ApplyLegacyPlayerPlayerPhysicalExtraAttack(
            damage,
            attackerFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex],
            targetFields[LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex]);

        return Math.Max(1, damage);
    }
}
