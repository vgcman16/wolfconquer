using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySeaBurialFloorItemId = 1_850;
    private const byte LegacySeaBurialFloorColor = 16;
    private const byte LegacySeaBurialFloorFlowerType = 3;
    private const ulong LegacySeaBurialFloorTimer = 3;
    private const int LegacySeaBurialFloorInvokeMilliseconds = 3_000;
    private const int LegacySeaBurialLineDistance = 18;
    private const double LegacySeaBurialLineWidth = 2.0;
    private const int LegacySeaBurialRemovalVisibilityRadius = 18;
    private const string LegacySeaBurialFloorName = "SeaBurialTrap";

    private static async ValueTask<bool> HandleLegacyPlayerSeaBurialWildwindMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.Wildwind ||
            spell.Id != LegacySeaBurialSpellId)
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
        var endpoint = ProjectLegacySeaBurialPushedEndpoint(casterX, casterY, query.X, query.Y);
        var line = LegacyMoveLineGeometry.SelectCoordinates(
            casterX,
            casterY,
            endpoint.X,
            endpoint.Y,
            isValidLocation: null,
            distance: LegacySeaBurialLineDistance);

        var updatedSession = resourceSession;
        var ownerContext = context with { Session = updatedSession };
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result)>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var killedTrainingSobNpcTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var eventSobNpcSideEffectTargets = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();
        var now = DateTimeOffset.UtcNow;

        if (context.MonsterStates is { } monsterStates)
        {
            foreach (var target in EnumerateVisibleMonsterTargets(ownerContext, updatedSession.Character))
            {
                if (!LegacyMoveLineGeometry.IsWithinHitRadius(
                        line.Coordinates,
                        target.Monster.X,
                        target.Monster.Y,
                        LegacySeaBurialLineWidth) ||
                    !CanLegacyPlayerAttackMonster(updatedSession.Character, target.Monster, target.MonsterFamily, out _))
                {
                    continue;
                }

                var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    updatedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    damage,
                    now,
                    updatedSession);
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

        foreach (var target in EnumerateVisiblePlayerMagicTargets(ownerContext, updatedSession, spell))
        {
            var targetSession = target.ActiveSession.Session;
            if (!LegacyMoveLineGeometry.IsWithinHitRadius(
                    line.Coordinates,
                    targetSession.Character.X,
                    targetSession.Character.Y,
                    LegacySeaBurialLineWidth) ||
                !CanLegacyPlayerMagicDamageTarget(ownerContext, updatedSession, targetSession, spell, out _))
            {
                continue;
            }

            var activeTarget = target.ActiveSession;
            (activeTarget, var wildPhoenixDefended) =
                await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                        ownerContext,
                        activeTarget,
                        cancellationToken)
                    .ConfigureAwait(false);
            var rawDamage = wildPhoenixDefended
                ? 1U
                : CalculateLegacyPlayerPlayerPhysicalSpellDamage(
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData,
                    damageDivisor: 1);
            var damage = MulDivToUInt32(rawDamage, 7, 11);
            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                ownerContext,
                activeTarget,
                updatedSession.Character,
                damage,
                cancellationToken)
            .ConfigureAwait(false);
            if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
            {
                updatedSession = currentReceiveAttacker;
                ownerContext = ownerContext with { Session = updatedSession };
            }

            if (receiveOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return true;
            }

            if (receiveOutcome.Result is not { } result)
            {
                continue;
            }
            playerResults.Add(result);
            if (result.DamageApplied == 0)
            {
                continue;
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                result.DamageApplied,
                Hit: true,
                MsgAttackEffect.None));
        }

        if (context.SobNpcStates is { } sobNpcStates &&
            context.WorldData is { } worldData)
        {
            foreach (var target in EnumerateVisibleSobNpcMagicTargets(ownerContext, updatedSession.Character))
            {
                if (!LegacyMoveLineGeometry.IsWithinHitRadius(
                        line.Coordinates,
                        target.SobNpc.X,
                        target.SobNpc.Y,
                        LegacySeaBurialLineWidth) ||
                    !CanLegacySobNpcMagicDamageTarget(ownerContext, updatedSession.Character, target.SobNpc, out _))
                {
                    continue;
                }

                var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                    updatedSession,
                    spell,
                    worldData,
                    damageMultiplier: 1);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc, worldData.StaticStatues);
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

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            endpoint.X,
            endpoint.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(ownerContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await ApplyLegacyMixedMagicDamageSideEffectsAsync(
                ownerContext,
                updatedSession,
                spell,
                monsterResults,
                playerResults,
                sobNpcResults,
                killedTrainingSobNpcTargets,
                eventSobNpcSideEffectTargets,
                cancellationToken)
            .ConfigureAwait(false);
        ownerContext = ownerContext with { Session = updatedSession };

        var floorPacket = new LegacyFloorItemPacket(
            NextLegacyFloorItemUid(),
            LegacySeaBurialFloorItemId,
            casterX,
            casterY,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            LegacySeaBurialFloorColor,
            casterUid,
            ReadAttributeUInt32(character.Attributes, "GuildID", "GuildId"),
            LegacySeaBurialFloorFlowerType,
            LegacySeaBurialFloorTimer,
            endpoint.X,
            endpoint.Y,
            LegacySeaBurialFloorName);

        var storedFloorEffectCount = 0;
        if (context.FloorItems is { } floorItems)
        {
            var effect = new FloorItemRuntimeEffect(
                spell.Id,
                endpoint.X,
                endpoint.Y,
                now.AddMilliseconds(LegacySeaBurialFloorInvokeMilliseconds),
                Distance: LegacySeaBurialLineDistance,
                HitRadius: LegacySeaBurialLineWidth,
                RemovalVisibilityRadius: LegacySeaBurialRemovalVisibilityRadius,
                SpellLevel: (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                LevelHu: playerSpell.UseSpellSoul,
                RuntimeOwnerUid: casterUid);
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
                        "seaburial-floor-effect-runtime",
                        "SeaBurial floor effect was sent, but floor item runtime state is not loaded",
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

        await SendFloorItemVisibleAsync(ownerContext, updatedSession.Character, floorPacket, cancellationToken)
            .ConfigureAwait(false);

        var progressDamage = monsterResults
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            sobNpcResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                ownerContext,
                updatedSession,
                playerSpell,
                spell,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                ownerContext,
                updatedSession,
                ClampLegacyProficiencyRawExperience(progressDamage),
                cancellationToken,
                activity: "SeaBurial Wildwind magic weapon proficiency experience")
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy SeaBurial Wildwind magic " +
                $"{FormatSession(ownerContext with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} soulLevel={playerSpell.UseSpellSoul} casterX={casterX} casterY={casterY} " +
                $"targetX={query.X} targetY={query.Y} endpointX={endpoint.X} endpointY={endpoint.Y} " +
                $"targetCount={animationTargets.Count} storedFloorEffectCount={storedFloorEffectCount}; " +
                "source=Wildwind.cs case 14680.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<int> SendDueLegacySeaBurialWildwindFloorSpellsAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<WorldActiveSession> activeSessions,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (context.FloorItems is not { } floorItems)
        {
            return 0;
        }

        var dueEffects = floorItems.ConsumeDueEffects(LegacySeaBurialSpellId, now);
        if (dueEffects.Count == 0)
        {
            return 0;
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

            var ownerUid = effect.RuntimeOwnerUid != 0 ? effect.RuntimeOwnerUid : entry.Packet.ItemOwnerUid;
            if (ownerUid == 0 ||
                context.WorldData is not { } worldData ||
                !LegacyMagicRuntime.TryResolveSpell(worldData, effect.SpellId, effect.SpellLevel, out var spell) ||
                !TryGetActiveSessionByUid(activeSessions, ownerUid, out var owner) ||
                owner.Session.Character.MapId != entry.MapId)
            {
                removeViewerCount += await SendLegacyLayTrapRemoveEffectAsync(
                        entry with { Packet = entry.Packet with { Timer = 0 } },
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
                entry.Packet.X,
                entry.Packet.Y,
                entry.Packet.OwnerX,
                entry.Packet.OwnerY,
                isValidLocation: null,
                distance: effect.Distance);

            var animationTargets = new List<LegacySpellAnimationTarget>();
            var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result)>();
            var playerResults = new List<LegacyPlayerMagicDamageResult>();
            var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
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
                var targetSession = target.ActiveSession.Session;
                if (!LegacyMoveLineGeometry.IsWithinHitRadius(
                        line.Coordinates,
                        targetSession.Character.X,
                        targetSession.Character.Y,
                        effect.HitRadius) ||
                    !CanLegacyPlayerMagicDamageTarget(ownerContext, ownerSession, targetSession, spell, out _))
                {
                    continue;
                }

                var activeTarget = target.ActiveSession;
                (activeTarget, var wildPhoenixDefended) =
                    await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                            ownerContext,
                            activeTarget,
                            cancellationToken)
                        .ConfigureAwait(false);
                var damage = wildPhoenixDefended
                    ? 1U
                    : CalculateLegacyPlayerPlayerPhysicalSpellDamage(
                        ownerSession,
                        activeTarget.Session,
                        spell,
                        worldData,
                        damageDivisor: 1);
                var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    ownerContext,
                    activeTarget,
                    ownerSession.Character,
                    damage,
                    cancellationToken)
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
                if (result.DamageApplied == 0)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                    MulDivToUInt32(result.DamageApplied, 2, 3),
                    Hit: true,
                    MsgAttackEffect.None));
            }

            if (ownerLostDuringReceive)
            {
                removeViewerCount += await SendLegacyLayTrapRemoveEffectAsync(
                        entry with { Packet = entry.Packet with { Timer = 0 } },
                        effect, activeSessions, cancellationToken)
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
                entry.Packet.Uid,
                line.Landing.X,
                line.Landing.Y,
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

            var updatedOwnerSession = await ApplyLegacyMixedMagicDamageSideEffectsAsync(
                    ownerContext,
                    ownerSession,
                    spell,
                    monsterResults,
                    playerResults,
                    sobNpcResults,
                    killedTrainingSobNpcTargets,
                    eventSobNpcSideEffectTargets,
                    cancellationToken)
                .ConfigureAwait(false);
            ownerContext = ownerContext with { Session = updatedOwnerSession };

            var removalEntry = entry with { Packet = entry.Packet with { Timer = 0 } };
            removeViewerCount += await SendLegacyLayTrapRemoveEffectAsync(
                    removalEntry,
                    effect,
                    activeSessions,
                    cancellationToken,
                    visibilityCenterX: entry.Packet.X,
                    visibilityCenterY: entry.Packet.Y)
                .ConfigureAwait(false);

            processedCount++;
            animationTargetCount += animationTargets.Count;
        }

        if (processedCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy SeaBurial Wildwind floor spell runtime " +
                    $"trapCount={processedCount} targetCount={animationTargetCount} removeViewerCount={removeViewerCount}; " +
                    "source=ThreadPool.cs case 14680.")
                .ConfigureAwait(false);
        }

        return processedCount;
    }

    private static async ValueTask<WorldSession> ApplyLegacyMixedMagicDamageSideEffectsAsync(
        WorldPacketHandlerContext context,
        WorldSession ownerSession,
        LegacyMagicRuntimeSpell spell,
        IReadOnlyList<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result)> monsterResults,
        IReadOnlyList<LegacyPlayerMagicDamageResult> playerResults,
        IReadOnlyList<(SobNpc SobNpc, SobNpcDamageResult Result)> sobNpcResults,
        IReadOnlyList<(SobNpc SobNpc, SobNpcDamageResult Result)> killedTrainingSobNpcTargets,
        IReadOnlyList<(SobNpc SobNpc, SobNpcDamageResult Result, LegacySobNpcDeathBehavior DeathBehavior)> eventSobNpcSideEffectTargets,
        CancellationToken cancellationToken)
    {
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
                    await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
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
                        context,
                        updatedOwnerSession,
                        target.Monster,
                        death,
                        cancellationToken)
                    .ConfigureAwait(false);
                await TrySendLegacyMonsterDeathDropAsync(
                        context,
                        updatedOwnerSession,
                        target.Monster,
                        target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                updatedOwnerSession = await TryApplyLegacyBossDeathRewardsAsync(
                        context,
                        updatedOwnerSession,
                        target.Monster,
                        target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                context = context with { Session = updatedOwnerSession };
                await TryApplyLegacyMonsterTeamExperienceAsync(
                        context,
                        updatedOwnerSession,
                        target.Monster,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            updatedOwnerSession = await TryApplyLegacyMonsterExperienceAsync(
                    context,
                    updatedOwnerSession,
                    target.Monster,
                    target.MonsterFamily,
                    result.DamageApplied,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedOwnerSession };
        }

        foreach (var result in playerResults)
        {
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
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
                        context,
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
                    context,
                    eventTarget.SobNpc,
                    eventTarget.Result,
                    spell)
                .ConfigureAwait(false);
        }

        return updatedOwnerSession;
    }

    private static LegacyMoveLineCoordinate ProjectLegacySeaBurialPushedEndpoint(
        ushort casterX,
        ushort casterY,
        ushort queryX,
        ushort queryY)
    {
        var deltaX = queryX - casterX;
        var deltaY = queryY - casterY;
        var steps = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));
        if (steps == 0)
        {
            return new LegacyMoveLineCoordinate(queryX, queryY);
        }

        var currentX = (double)queryX;
        var currentY = (double)queryY;
        var stepX = (double)deltaX / steps;
        var stepY = (double)deltaY / steps;
        for (var index = 0; index < LegacySeaBurialLineDistance; index++)
        {
            currentX += stepX;
            currentY += stepY;
        }

        return new LegacyMoveLineCoordinate(
            ClampLegacySeaBurialCoordinate(currentX),
            ClampLegacySeaBurialCoordinate(currentY));
    }

    private static ushort ClampLegacySeaBurialCoordinate(double value)
    {
        return checked((ushort)Math.Clamp((int)Math.Round(value), ushort.MinValue, ushort.MaxValue));
    }
}
