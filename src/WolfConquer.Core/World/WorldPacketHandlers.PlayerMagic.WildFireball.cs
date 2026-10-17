using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyWildFireballFloorItemId = 18_456;
    private const byte LegacyWildFireballFloorColor = 20;
    private const byte LegacyWildFireballTrapMaxDistance = 15;
    private const int LegacyWildFireballTrapDelayStepMilliseconds = 500;
    private const int LegacyWildFireballPlayerRadius = 6;
    private const int LegacyWildFireballFloorRuntimeHitRadius = 5;
    private const int LegacyWildFireballRemovalVisibilityRadius = 18;
    private const string LegacyWildFireballFloorName = "trap";

    private static async ValueTask<bool> HandleLegacyPlayerWildFireballWildwindMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.Wildwind ||
            spell.Id != LegacyWildFireballSpellId)
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
        var character = updatedSession.Character;
        var casterUid = CheckedUInt32(character.Uid, nameof(character.Uid));
        var casterX = CheckedUInt16(character.X, nameof(character.X));
        var casterY = CheckedUInt16(character.Y, nameof(character.Y));
        var guildId = ReadAttributeUInt32(character.Attributes, "GuildID", "GuildId");
        var spellLevel = (byte)Math.Min(playerSpell.Level, byte.MaxValue);
        var trapCoordinates = LegacyLayTrap.SelectTwilightDanceCoordinates(
            casterX,
            casterY,
            query.X,
            query.Y,
            LegacyWildFireballTrapMaxDistance);

        var now = DateTimeOffset.UtcNow;
        var storedTrapCount = 0;
        var sentTrapCount = 0;
        if (trapCoordinates.Count > 0)
        {
            var firstFloorUid = NextLegacyFloorItemUidRangeStart(trapCoordinates.Count);
            for (var index = 0; index < trapCoordinates.Count; index++)
            {
                var coordinate = trapCoordinates[index];
                var floorPacket = new LegacyFloorItemPacket(
                    unchecked(firstFloorUid + (uint)index),
                    LegacyWildFireballFloorItemId,
                    coordinate.X,
                    coordinate.Y,
                    MaxLife: 0,
                    LegacyFloorItemDropType.Effect,
                    Life: 0,
                    LegacyWildFireballFloorColor,
                    casterUid,
                    guildId,
                    FlowerType: 0,
                    Timer: 0,
                    OwnerX: coordinate.X,
                    OwnerY: coordinate.Y,
                    LegacyWildFireballFloorName);

                if (context.FloorItems is { } floorItems)
                {
                    var effect = new FloorItemRuntimeEffect(
                        spell.Id,
                        query.X,
                        query.Y,
                        now.AddMilliseconds(index * LegacyWildFireballTrapDelayStepMilliseconds),
                        Distance: LegacyWildFireballTrapMaxDistance,
                        HitRadius: LegacyWildFireballFloorRuntimeHitRadius,
                        RemovalVisibilityRadius: LegacyWildFireballRemovalVisibilityRadius,
                        SpellLevel: spellLevel,
                        LevelHu: playerSpell.UseSpellSoul,
                        RuntimeOwnerUid: casterUid);
                    floorItems.AddVisible(FloorItemRuntimeEntry.ForEffect(character.MapId, floorPacket, effect), now);
                    storedTrapCount++;
                }
                else
                {
                    await LogParityGapAsync(
                            context,
                            LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                                FloorItemRuntimeStateResource,
                                "wildfireball-wildwind-floor-runtime",
                                "WildFireball floor effect was sent, but floor item runtime state is not loaded",
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

                if (index == 0)
                {
                    await SendFloorItemVisibleAsync(context, character, floorPacket, cancellationToken)
                        .ConfigureAwait(false);
                    sentTrapCount++;
                }
            }
        }

        var playerTargets = SelectLegacyWildFireballPlayerTargets(context, updatedSession, spell);
        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var results = new List<LegacyPlayerMagicDamageResult>();
        var animationTargets = new List<LegacySpellAnimationTarget>();
        foreach (var target in playerTargets)
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
            var damage = physicalDamage.Damage;
            damage = MulDivToUInt32(
                CalculateLegacyWildFireballSoulDamage(damage, playerSpell.UseSpellSoul),
                20,
                35);

            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                context,
                activeTarget,
                updatedSession.Character,
                damage,
                cancellationToken)
            .ConfigureAwait(false);
            if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
            {
                updatedSession = currentReceiveAttacker;
                context = context with { Session = updatedSession };
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
                context = context with { Session = updatedSession };
            }

            results.Add(result);
            if (result.DamageApplied == 0)
            {
                continue;
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                result.DamageApplied,
                Hit: true,
                physicalDamage.Effect));
        }

        foreach (var result in results)
        {
            await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            casterX,
            casterY,
            spell.Id,
            spellLevel,
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        foreach (var result in results)
        {
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var totalPlayerDamage = results.Aggregate(0UL, (total, result) => total + result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy WildFireball Wildwind magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={spellLevel} soulLevel={playerSpell.UseSpellSoul} casterX={casterX} casterY={casterY} " +
                $"targetX={query.X} targetY={query.Y} floorTrapCount={trapCoordinates.Count} " +
                $"storedFloorTrapCount={storedTrapCount} sentFloorTrapCount={sentTrapCount} " +
                $"playerTargets={playerTargets.Count} totalPlayerDamage={totalPlayerDamage} playerDamageScale=20/35; " +
                "source=Wildwind.cs case 16520.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<int> SendDueLegacyWildFireballWildwindFloorSpellsAsync(
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
        var animationTargetCount = 0;
        var removeViewerCount = 0;
        foreach (var spellId in new[] { LegacyWildFireballSpellId, LegacyWildFireballPassiveSpellId })
        {
            var dueEffects = floorItems.ConsumeDueEffects(spellId, now);
            if (dueEffects.Count == 0)
            {
                continue;
            }

            var remainingSpellCount = dueEffects.Count +
                floorItems
                    .SnapshotVisible(now)
                    .Count(entry => entry.Effect?.SpellId == spellId);
            foreach (var entry in dueEffects)
            {
                if (entry.Effect is not { } effect)
                {
                    remainingSpellCount = Math.Max(0, remainingSpellCount - 1);
                    continue;
                }

                var ownerUid = effect.RuntimeOwnerUid != 0 ? effect.RuntimeOwnerUid : entry.Packet.ItemOwnerUid;
                if (ownerUid == 0 ||
                    context.WorldData is not { } worldData ||
                    !TryResolveLegacyLayTrapSourceCaseRuntimeSpell(worldData, effect, out var spell) ||
                    !TryGetActiveSessionByUid(activeSessions, ownerUid, out var owner) ||
                    owner.Session.Character.MapId != entry.MapId)
                {
                    removeViewerCount += await SendLegacyLayTrapRemoveEffectAsync(
                            entry,
                            effect,
                            activeSessions,
                            cancellationToken,
                            visibilityCenterX: effect.OriginX,
                            visibilityCenterY: effect.OriginY)
                        .ConfigureAwait(false);
                    processedCount++;
                    remainingSpellCount = Math.Max(0, remainingSpellCount - 1);
                    continue;
                }

                var ownerContext = context with
                {
                    Connection = owner.Connection,
                    Session = owner.Session,
                };
                var ownerSession = owner.Session;
                var animationTargets = new List<LegacySpellAnimationTarget>();
                var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result, uint RuntimeDamage)>();
                var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result, uint RuntimeDamage)>();
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

                        var runtimeDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                            ownerSession,
                            worldData,
                            target.Monster,
                            target.MonsterFamily,
                            spell);
                        var result = ApplyLegacyPlayerMonsterDamage(
                            monsterStates,
                            target.Monster,
                            runtimeDamage,
                            now,
                            ownerSession);
                        monsterResults.Add((target, result, runtimeDamage));
                        if (result.AlreadyDead || result.DamageApplied == 0)
                        {
                            continue;
                        }

                        var displayDamage = CalculateLegacyWildFireballFloorMonsterDisplayDamage(
                            runtimeDamage,
                            effect.LevelHu,
                            remainingSpellCount);
                        animationTargets.Add(new LegacySpellAnimationTarget(
                            target.Monster.Uid,
                            displayDamage,
                            Hit: true,
                            MsgAttackEffect.None));
                    }
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

                        var runtimeDamage = CalculateLegacySobNpcPhysicalSpellDamage(
                            ownerSession,
                            spell,
                            worldData,
                            damageMultiplier: 1);
                        var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc, worldData.StaticStatues);
                        var result = sobNpcStates.ApplyDamage(target.SobNpc, runtimeDamage, now, deathBehavior);
                        sobNpcResults.Add((target.SobNpc, result, runtimeDamage));
                        if (result.AlreadyDead || result.DamageApplied == 0)
                        {
                            continue;
                        }

                        var displayDamage = CalculateLegacyWildFireballFloorSobNpcDisplayDamage(
                            runtimeDamage,
                            effect.LevelHu,
                            remainingSpellCount);
                        animationTargets.Add(new LegacySpellAnimationTarget(
                            CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                            displayDamage,
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
                await SendPlayerSpellAnimationToVisibleAsync(ownerContext, ownerSession, animation, cancellationToken)
                    .ConfigureAwait(false);

                var updatedOwnerSession = ownerSession;
                foreach (var (target, result, _) in monsterResults)
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

                if (context.SobNpcStates is { } currentSobNpcStates)
                {
                    foreach (var killed in killedTrainingSobNpcTargets)
                    {
                        await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                                ownerContext,
                                updatedOwnerSession,
                                killed.SobNpc,
                                currentSobNpcStates.GetVisible(killed.SobNpc),
                                cancellationToken)
                            .ConfigureAwait(false);
                    }

                    foreach (var (sobNpc, result, _) in eventSobNpcSideEffectTargets)
                    {
                        await LogLegacySobNpcEventSideEffectGapAsync(ownerContext, sobNpc, result, spell)
                            .ConfigureAwait(false);
                    }
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
                animationTargetCount += animationTargets.Count;
                remainingSpellCount = Math.Max(0, remainingSpellCount - 1);
            }
        }

        if (processedCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy WildFireball Wildwind floor spell runtime " +
                    $"trapCount={processedCount} targetCount={animationTargetCount} removeViewerCount={removeViewerCount}; " +
                    "source=ThreadPool.cs cases 16520/16530.")
                .ConfigureAwait(false);
        }

        return processedCount;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyWildFireballPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var targetSession = candidate.ActiveSession.Session;
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    targetSession.Character.X,
                    targetSession.Character.Y) > LegacyWildFireballPlayerRadius ||
                !CanLegacyPlayerMagicDamageTarget(context, session, targetSession, spell, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static uint CalculateLegacyWildFireballFloorMonsterDisplayDamage(
        uint damage,
        byte useSpellSoul,
        int remainingSpellCount)
    {
        damage = CalculateLegacyWildFireballSoulDamage(damage, useSpellSoul) / 2;
        var bonusPercent = checked((uint)Math.Max(0, 50 - (remainingSpellCount * 10)));
        return damage + MulDivToUInt32(damage, bonusPercent, 100);
    }

    private static uint CalculateLegacyWildFireballFloorSobNpcDisplayDamage(
        uint damage,
        byte useSpellSoul,
        int remainingSpellCount)
    {
        damage = CalculateLegacyWildFireballSoulDamage(damage, useSpellSoul) / 2;
        var bonusNumerator = checked((uint)Math.Max(0, 50 - (remainingSpellCount * 5)));
        return damage + MulDivToUInt32(damage, bonusNumerator, 300);
    }

    private static uint CalculateLegacyWildFireballSoulDamage(uint damage, byte useSpellSoul)
    {
        // Source Base.CalculateSoul(Damage, LevelSoul) returns Damage unchanged for this bounded slice.
        _ = useSpellSoul;
        return damage;
    }
}
