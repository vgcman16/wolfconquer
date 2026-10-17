using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly LegacyRandomFourLayTrapFacing[][] LegacyRandomFourLayTrapFacingPairs =
    [
        [LegacyRandomFourLayTrapFacing.North, LegacyRandomFourLayTrapFacing.South],
        [LegacyRandomFourLayTrapFacing.East, LegacyRandomFourLayTrapFacing.West],
        [LegacyRandomFourLayTrapFacing.NorthWest, LegacyRandomFourLayTrapFacing.SouthWest],
        [LegacyRandomFourLayTrapFacing.NorthEast, LegacyRandomFourLayTrapFacing.SouthEast],
    ];

    private static async ValueTask<bool> HandleLegacyPlayerLayTrapSourceCases12550And12570MagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Id is not (LegacyLayTrap.SourceCase12550SpellId or LegacyLayTrap.SourceCase12570SpellId))
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
        LegacyLayTrapResolvedTarget? resolvedTarget = null;
        if (spell.Id == LegacyLayTrap.SourceCase12570SpellId &&
            TryResolveLegacyLayTrap12990Target(context, resourceSession, query.OpponentUid, out var target))
        {
            resolvedTarget = new LegacyLayTrapResolvedTarget(
                target.Uid,
                target.Kind switch
                {
                    "player" => LegacyLayTrapTargetKind.Player,
                    "monster" => LegacyLayTrapTargetKind.Monster,
                    "sobNpc" => LegacyLayTrapTargetKind.SobNpc,
                    _ => LegacyLayTrapTargetKind.Monster,
                },
                target.X,
                target.Y);
        }

        var randomCoordinates = spell.Id == LegacyLayTrap.SourceCase12550SpellId
            ? CreateLegacyRandomFourLayTrapCoordinates(casterX, casterY)
            : null;
        var firstFloorUid = NextLegacyFloorItemUidRangeStart(
            spell.Id == LegacyLayTrap.SourceCase12550SpellId
                ? LegacyLayTrap.SourceCase12550MaxTraps
                : LegacyLayTrap.SourceCase12570MaxTraps);
        var projection = LegacyLayTrap.Project(new LegacyLayTrapCastInput(
            SpellId: spell.Id,
            SpellLevel: (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            SpellSoulLevel: playerSpell.UseSpellSoul,
            CasterUid: casterUid,
            GuildId: 0,
            CasterX: casterX,
            CasterY: casterY,
            TargetX: query.X,
            TargetY: query.Y,
            FirstFloorUid: firstFloorUid,
            RandomFourCoordinates: randomCoordinates,
            ResolvedTarget: resolvedTarget));

        if (!projection.Projected || projection.CastAnimation is not { } castAnimation)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.LayTrapFloorEffectRuntime(
                        spell.Id,
                        reason: projection.Reason,
                        nextSeam: "lay-trap-source-cases-12550-12570-random-or-target-resolution"))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy LayTrap magic " +
                    $"{FormatSession(context)} spellId={spell.Id} reason={projection.Status}; " +
                    "source=LayTrap.cs cases 12550/12570.")
                .ConfigureAwait(false);
            return true;
        }

        if (castAnimation.SendsSelfInteraction)
        {
            var selfInteraction = new MsgAttackPacket(new MsgAttackQuery(
                castAnimation.CasterUid,
                castAnimation.TargetUid,
                castAnimation.X,
                castAnimation.Y,
                MsgAttackType.None,
                checked((int)castAnimation.InteractionDamage),
                KilledMonster: 0,
                castAnimation.InteractionAttackType,
                MsgAttackEffect.None,
                Unknown: 0));
            await context.Connection.SendAsync(selfInteraction.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        var now = DateTimeOffset.UtcNow;
        var animationTargets = new List<LegacySpellAnimationTarget>(projection.FloorEffects.Count);
        var storedTrapCount = 0;
        foreach (var floorEffect in projection.FloorEffects)
        {
            var packet = new LegacyFloorItemPacket(
                floorEffect.FloorUid,
                floorEffect.ItemId,
                floorEffect.X,
                floorEffect.Y,
                MaxLife: 0,
                floorEffect.DropType,
                Life: 0,
                floorEffect.Color,
                floorEffect.ItemOwnerUid,
                floorEffect.GuildId,
                FlowerType: 0,
                Timer: 0,
                floorEffect.OwnerX,
                floorEffect.OwnerY,
                floorEffect.Name);

            if (context.FloorItems is { } floorItems)
            {
                var runtimeEffect = new FloorItemRuntimeEffect(
                    spell.Id,
                    casterX,
                    casterY,
                    now.AddMilliseconds(floorEffect.InvokeDelayMilliseconds),
                    Distance: floorEffect.TriggerRadius,
                    HitRadius: floorEffect.TriggerRadius,
                    RemovalActionType: projection.Definition?.RemovalActionType ?? 0,
                    RemovalVisibilityRadius: floorEffect.TriggerRadius == 0
                        ? LegacyLayTrap.SourceCases12550And12570RemovalVisibilityRadius
                        : projection.Definition?.VisibilityRadius ?? LegacyLayTrap.SourceCases12550And12570RemovalVisibilityRadius,
                    SpellLevel: castAnimation.SpellLevel,
                    LevelHu: castAnimation.SoulLevel,
                    RuntimeOwnerUid: casterUid);
                floorItems.AddVisible(FloorItemRuntimeEntry.ForEffect(character.MapId, packet, runtimeEffect), now);
                storedTrapCount++;
            }
            else
            {
                await LogParityGapAsync(
                        context,
                        LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                            FloorItemRuntimeStateResource,
                            "lay-trap-source-cases-12550-12570-floor-effect-runtime",
                            "LayTrap 12550/12570 floor effect was sent, but floor item runtime state is not loaded",
                            facts:
                            [
                                LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                                LegacyParityGapDiagnostics.Fact("floorUid", packet.Uid),
                            ],
                            dedupeFacts:
                            [
                                LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                            ]))
                    .ConfigureAwait(false);
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                floorEffect.FloorUid,
                floorEffect.AnimationTargetDamage,
                Hit: floorEffect.AnimationTargetHit != 0,
                MsgAttackEffect.None,
                floorEffect.AnimationTargetMoveX,
                floorEffect.AnimationTargetMoveY));
            await SendFloorItemVisibleAsync(context, character, packet, cancellationToken)
                .ConfigureAwait(false);
        }

        var updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                resourceSession,
                playerSpell,
                spell,
                totalDamage: 10_000,
                cancellationToken)
            .ConfigureAwait(false);

        var animation = new LegacySpellAnimationPacket(
            castAnimation.CasterUid,
            castAnimation.TargetUid,
            castAnimation.X,
            castAnimation.Y,
            castAnimation.SpellId,
            castAnimation.SpellLevel,
            NextSpell: 0,
            castAnimation.SoulLevel,
            SpellEffect: 0,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(
                context with { Session = updatedSession },
                updatedSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy LayTrap magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} targetUid={query.OpponentUid} targetX={query.X} targetY={query.Y} " +
                $"floorEffectCount={projection.FloorEffects.Count} storedTrapCount={storedTrapCount}; " +
                "source=LayTrap.cs cases 12550/12570.")
            .ConfigureAwait(false);
        return true;
    }

    internal static async ValueTask<int> SendDueLegacyLayTrapSourceCases12550And12570FloorSpellsAsync(
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

        var processedCount = 0;
        processedCount += await SendDueLegacyLayTrapSourceCaseFloorSpellsAsync(
                context,
                activeSessions,
                floorItems.ConsumeDueEffects(LegacyLayTrap.SourceCase12550SpellId, now),
                now,
                cancellationToken)
            .ConfigureAwait(false);
        processedCount += await SendDueLegacyLayTrapSourceCaseFloorSpellsAsync(
                context,
                activeSessions,
                floorItems.ConsumeDueEffects(LegacyLayTrap.SourceCase12570SpellId, now),
                now,
                cancellationToken)
            .ConfigureAwait(false);
        return processedCount;
    }

    private static async ValueTask<int> SendDueLegacyLayTrapSourceCaseFloorSpellsAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<WorldActiveSession> activeSessions,
        IReadOnlyList<FloorItemRuntimeEntry> dueEffects,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
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
            if (context.WorldData is not { } worldData ||
                !TryGetActiveSessionByUid(activeSessions, ownerUid, out var owner) ||
                owner.Session.Character.MapId != entry.MapId ||
                !TryResolveLegacyLayTrapSourceCaseRuntimeSpell(worldData, effect, out var spell))
            {
                removeViewerCount += await SendLegacyLayTrapRemoveEffectAsync(
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
            var animationTargets = new List<LegacySpellAnimationTarget>();
            var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result)>();
            var playerResults = new List<(LegacyPlayerMagicDamageResult Result, uint AnimationDamage)>();
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
                        CalculateLegacyLayTrapSourceCaseSoulDamage(result.RequestedDamage, effect.LevelHu),
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

                var animationDamage = MulDivToUInt32(
                    CalculateLegacyLayTrapSourceCaseSoulDamage(damage, effect.LevelHu),
                    LegacyLayTrap.SourceCases12550And12570PlayerAnimationDamageNumerator,
                    LegacyLayTrap.SourceCases12550And12570PlayerAnimationDamageDenominator);
                playerResults.Add((result, animationDamage));
                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                    animationDamage,
                    Hit: true,
                    MsgAttackEffect.None));
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
                continue;
            }

            if (context.SobNpcStates is { } sobNpcStates)
            {
                foreach (var target in EnumerateVisibleSobNpcMagicTargets(ownerContext, ownerSession.Character))
                {
                    if (!IsWithinLegacyLayTrapHitRadius(
                            entry,
                            CheckedUInt16(target.SobNpc.X, nameof(target.SobNpc.X)),
                            CheckedUInt16(target.SobNpc.Y, nameof(target.SobNpc.Y))))
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
                        CalculateLegacyLayTrapSourceCaseSoulDamage(damage, effect.LevelHu),
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
                ownerUid,
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

            foreach (var (result, _) in playerResults)
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
            animationTargetCount += animationTargets.Count;
        }

        if (processedCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy LayTrap source-case floor spell runtime " +
                    $"trapCount={processedCount} targetCount={animationTargetCount} removeViewerCount={removeViewerCount}; " +
                    "source=ThreadPool.cs cases 12550/12570.")
                .ConfigureAwait(false);
        }

        return processedCount;
    }

    private static bool TryResolveLegacyLayTrapSourceCaseRuntimeSpell(
        WorldDataCatalog worldData,
        FloorItemRuntimeEffect effect,
        out LegacyMagicRuntimeSpell spell)
    {
        if (LegacyMagicRuntime.TryResolveSpell(worldData, effect.SpellId, effect.SpellLevel, out spell))
        {
            return true;
        }

        return LegacyMagicRuntime.TryResolveSpell(worldData, effect.SpellId, spellLevel: 0, out spell);
    }

    private static IReadOnlyList<LegacyLayTrapCoordinate> CreateLegacyRandomFourLayTrapCoordinates(
        ushort casterX,
        ushort casterY)
    {
        var rolls = new LegacyRandomFourLayTrapRoll[LegacyLayTrap.SourceCase12550MaxTraps];
        for (var index = 0; index < rolls.Length; index++)
        {
            var pair = LegacyRandomFourLayTrapFacingPairs[index];
            var facing = pair[Random.Shared.Next(10) < 3 ? 1 : 0];
            var distance = checked((sbyte)Random.Shared.Next(3, 10));
            rolls[index] = new LegacyRandomFourLayTrapRoll(facing, distance);
        }

        return LegacyLayTrap.SelectRandomFourLayTrapCoordinates(casterX, casterY, rolls);
    }

    private static uint CalculateLegacyLayTrapSourceCaseSoulDamage(uint damage, byte useSpellSoul)
    {
        _ = useSpellSoul;
        return damage;
    }
}
