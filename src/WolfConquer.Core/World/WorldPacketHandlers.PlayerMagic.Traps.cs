using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerLayTrapMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.LayTrap)
        {
            return false;
        }

        if (await HandleLegacyPlayerThunderStrikerLayTrapMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (spell.Id == LegacyBloomOfDeathSpellId)
        {
            var bloomResourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                    context,
                    session,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
            if (bloomResourceSession is null)
            {
                return true;
            }

            await ExecuteLegacyBloomOfDeathLayTrapRadiusAsync(
                    context with { Session = bloomResourceSession },
                    bloomResourceSession,
                    query,
                    playerSpell,
                    spell,
                    "LayTrap magic",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (await HandleLegacyPlayerLayTrap12990MagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerLayTrapSourceCases12550And12570MagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerDaggerStormLayTrapMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerSpaceLeapLayTrapMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerLotusLayTrapMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerWaterShockwaveLayTrapMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerFlameOfDestructionLayTrapMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerSickleWindLayTrapMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerDustDetachmentLayTrapMagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (spell.Id != LegacyLayTrap.TwilightDanceSpellId)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedMagic(
                        spell.Id,
                        sort: (int)spell.Sort,
                        reason:
                            "LayTrap handling is currently wired only for source case 12070 TwilightDance floor effects, 11600 DaggerStorm floor-effect creation, 12370/12380 AuroraLotus/FlameLotus visual floor-effect creation, 12550/12570 delayed source-case floor traps, 12990 target floor-effect creation, 14720 BloomofDeath radius damage, 15620 SpaceLeap floor-effect creation, 15770 SkyFall/ThunderStriker radius damage, 16310/16320 WaterShockwave floor-effect placement plus due final line damage/pushback with fenced repeat pulse runtime, 16460/16470 DustDetachment floor-effect placement plus cast-time SOB and repeat monster/player runtime, 16500/16510 SickleWind floor-effect placement plus cast-time SOB, repeat monster/player runtime, and remove-only final invoke, and 16540/16550 FlameofDestruction floor-effect placement plus repeat pulse runtime and remove-only final invoke."))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy LayTrap magic " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort}; unsupported LayTrap spell id.")
                .ConfigureAwait(false);
            return true;
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
        var firstFloorUid = NextLegacyFloorItemUidRangeStart(LegacyLayTrap.TwilightDanceMaxTraps);
        var projection = LegacyLayTrap.Project(new LegacyLayTrapCastInput(
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            CheckedUInt32(character.Uid, nameof(character.Uid)),
            ReadAttributeUInt32(character.Attributes, "GuildID", "GuildId"),
            CheckedUInt16(character.X, nameof(character.X)),
            CheckedUInt16(character.Y, nameof(character.Y)),
            query.X,
            query.Y,
            firstFloorUid));

        if (!projection.Projected || projection.CastAnimation is not { } castAnimation)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.LayTrapFloorEffectRuntime(
                        spell.Id,
                        reason: projection.Reason,
                        nextSeam: "lay-trap-same-tile-cast-capture"))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy LayTrap magic " +
                    $"{FormatSession(context)} spellId={spell.Id} reason={projection.Status}.")
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
            var runtimeEffect = new FloorItemRuntimeEffect(
                spell.Id,
                floorEffect.OwnerX,
                floorEffect.OwnerY,
                now.AddMilliseconds(floorEffect.InvokeDelayMilliseconds),
                floorEffect.TriggerRadius,
                floorEffect.TriggerRadius,
                floorEffect.PlayerDamagePercent,
                floorEffect.PlayerAttackBonus,
                LegacyLayTrap.TwilightDanceRuntime.RemovalActionType,
                LegacyLayTrap.TwilightDanceRuntime.RemovalVisibilityRadius);
            if (context.FloorItems is { } floorItems)
            {
                floorItems.AddVisible(
                    FloorItemRuntimeEntry.ForEffect(character.MapId, packet, runtimeEffect),
                    now);
                storedTrapCount++;
            }
            else
            {
                await LogParityGapAsync(
                        context,
                        LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                            FloorItemRuntimeStateResource,
                            "lay-trap-floor-effect-runtime",
                            "TwilightDance floor effect was sent, but floor item runtime state is not loaded",
                            facts:
                            [
                                LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                                LegacyParityGapDiagnostics.Fact("floorUid", packet.Uid),
                            ]))
                    .ConfigureAwait(false);
            }

            await SendFloorItemVisibleAsync(context, character, packet, cancellationToken)
                .ConfigureAwait(false);
        }

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
            Targets: []);
        await SendPlayerSpellAnimationToVisibleAsync(context, resourceSession, animation, cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                resourceSession,
                playerSpell,
                spell,
                totalDamage: 10_000,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy LayTrap magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} targetX={query.X} targetY={query.Y} " +
                $"floorEffectCount={projection.FloorEffects.Count} storedTrapCount={storedTrapCount}.")
            .ConfigureAwait(false);
        _ = updatedSession;
        return true;
    }

    internal static async ValueTask<int> SendDueLegacyLayTrapFloorSpellsAsync(
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

        var daggerStormDueEffects = floorItems.ConsumeDueEffects(LegacyLayTrap.DaggerStormSpellId, now);
        var daggerStormProcessedCount = 0;
        var daggerStormRemoveViewerCount = 0;
        foreach (var entry in daggerStormDueEffects)
        {
            if (entry.Effect is not { } effect)
            {
                continue;
            }

            var removalEntry = entry.Packet.ItemId is 41 or 42
                ? entry with { Packet = entry.Packet with { ItemId = 50 } }
                : entry;
            daggerStormRemoveViewerCount += await SendLegacyLayTrapRemoveEffectAsync(
                    removalEntry,
                    effect,
                    activeSessions,
                    cancellationToken)
                .ConfigureAwait(false);
            daggerStormProcessedCount++;
        }

        if (daggerStormProcessedCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy DaggerStorm LayTrap floor effect expiry " +
                    $"trapCount={daggerStormProcessedCount} removeViewerCount={daggerStormRemoveViewerCount}; " +
                    "source=MsgItem specialSecound=4 from LayTrap.cs case 11600.")
                .ConfigureAwait(false);
        }

        var spaceLeapDueEffects = floorItems.ConsumeDueEffects(LegacyLayTrap.SpaceLeapSpellId, now);
        var spaceLeapProcessedCount = 0;
        var spaceLeapRemoveViewerCount = 0;
        foreach (var entry in spaceLeapDueEffects)
        {
            if (entry.Effect is not { } effect)
            {
                continue;
            }

            spaceLeapRemoveViewerCount += await SendLegacyLayTrapRemoveEffectAsync(
                    entry,
                    effect,
                    activeSessions,
                    cancellationToken)
                .ConfigureAwait(false);
            spaceLeapProcessedCount++;
        }

        if (spaceLeapProcessedCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy SpaceLeap LayTrap floor effect expiry " +
                    $"trapCount={spaceLeapProcessedCount} removeViewerCount={spaceLeapRemoveViewerCount}; " +
                    "source=FloorSpell lifetime 17000ms from LayTrap.cs case 15620.")
                .ConfigureAwait(false);
        }

        var lotusProcessedCount = 0;
        var lotusRemoveViewerCount = 0;
        foreach (var spellId in new[] { LegacyLayTrap.AuroraLotusSpellId, LegacyLayTrap.FlameLotusSpellId })
        {
            var lotusDueEffects = floorItems.ConsumeDueEffects(spellId, now);
            foreach (var entry in lotusDueEffects)
            {
                if (entry.Effect is not { } effect)
                {
                    continue;
                }

                lotusRemoveViewerCount += await SendLegacyLayTrapRemoveEffectAsync(
                        entry,
                        effect,
                        activeSessions,
                        cancellationToken)
                    .ConfigureAwait(false);
                lotusProcessedCount++;
            }
        }

        if (lotusProcessedCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy Lotus LayTrap floor effect expiry " +
                    $"trapCount={lotusProcessedCount} removeViewerCount={lotusRemoveViewerCount}; " +
                    "source=AddFloor Timer=TqTimer(DateTime.Now.AddSeconds(7)) from LayTrap.cs cases 12370/12380.")
                .ConfigureAwait(false);
        }

        var waterShockwaveProcessedCount = await SendDueLegacyWaterShockwaveLayTrapFloorSpellsAsync(
                context,
                activeSessions,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        var flameOfDestructionProcessedCount = await SendDueLegacyFlameOfDestructionLayTrapFloorSpellsAsync(
                context,
                activeSessions,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        var sickleWindProcessedCount = await SendDueLegacySickleWindLayTrapFloorSpellsAsync(
                context,
                activeSessions,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        var dustDetachmentProcessedCount = await SendDueLegacyDustDetachmentLayTrapFloorSpellsAsync(
                context,
                activeSessions,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        var seaBurialProcessedCount = await SendDueLegacySeaBurialWildwindFloorSpellsAsync(
                context,
                activeSessions,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        var wildFireballProcessedCount = await SendDueLegacyWildFireballWildwindFloorSpellsAsync(
                context,
                activeSessions,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        var dueEffects = floorItems.ConsumeDueEffects(LegacyLayTrap.TwilightDanceSpellId, now);
        if (dueEffects.Count == 0)
        {
            return daggerStormProcessedCount + spaceLeapProcessedCount + lotusProcessedCount +
                waterShockwaveProcessedCount + flameOfDestructionProcessedCount + sickleWindProcessedCount +
                dustDetachmentProcessedCount + seaBurialProcessedCount + wildFireballProcessedCount;
        }

        LegacyMagicRuntimeSpell? resolvedTrapSpell = null;
        if (context.WorldData is { } worldData &&
            LegacyMagicRuntime.TryResolveSpell(worldData, LegacyLayTrap.TwilightDanceSpellId, spellLevel: 0, out var trapSpell))
        {
            resolvedTrapSpell = trapSpell;
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

            if (resolvedTrapSpell is not { } spell ||
                !TryGetActiveSessionByUid(activeSessions, entry.Packet.ItemOwnerUid, out var owner) ||
                owner.Session.Character.MapId != entry.MapId)
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
                    if (!IsWithinLegacyLayTrapHitRadius(entry, target.Monster.X, target.Monster.Y))
                    {
                        continue;
                    }

                    var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                        ownerSession,
                        context.WorldData,
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
            foreach (var target in EnumerateVisiblePlayerMagicTargets(ownerContext, ownerSession, spell))
            {
                if (!IsWithinLegacyLayTrapHitRadius(
                        entry,
                        CheckedUInt16(target.ActiveSession.Session.Character.X, nameof(target.ActiveSession.Session.Character.X)),
                        CheckedUInt16(target.ActiveSession.Session.Character.Y, nameof(target.ActiveSession.Session.Character.Y))))
                {
                    continue;
                }

                var damage = CalculateLegacyPlayerPlayerPhysicalSpellDamage(
                    ownerSession,
                    target.ActiveSession.Session,
                    spell,
                    context.WorldData,
                    damageDivisor: 1);
                damage = MulDivToUInt32(damage, checked((uint)effect.PlayerDamagePercent), 100);
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
                entry.Packet.X,
                entry.Packet.Y,
                LegacyLayTrap.TwilightDanceSpellId,
                spellLevel: 0,
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
                    "WorldServer handled legacy LayTrap floor spell runtime " +
                    $"trapCount={processedCount} targetCount={animationTargetCount} removeViewerCount={removeViewerCount}.")
                .ConfigureAwait(false);
        }

        return processedCount + daggerStormProcessedCount + spaceLeapProcessedCount + lotusProcessedCount +
            waterShockwaveProcessedCount + flameOfDestructionProcessedCount + sickleWindProcessedCount +
            dustDetachmentProcessedCount + seaBurialProcessedCount + wildFireballProcessedCount;
    }

    private static bool TryGetActiveSessionByUid(
        IReadOnlyList<WorldActiveSession> activeSessions,
        uint uid,
        out WorldActiveSession activeSession)
    {
        foreach (var active in activeSessions)
        {
            if (active.Session.Character.Uid == uid)
            {
                activeSession = active;
                return true;
            }
        }

        activeSession = default!;
        return false;
    }

    private static bool HasLegacyTideTrapEquipped(WorldSession session, WorldDataCatalog? worldData)
    {
        if (!HasLearnedLegacySpell(session.PlayerSpells, LegacyTideTrapSpellId))
        {
            return false;
        }

        foreach (var rune in session.PlayerItems.RuneItems)
        {
            if (!IsLegacyEquippedRunePosition(rune.Position))
            {
                continue;
            }

            if (worldData?.ItemsById.TryGetValue((int)rune.ItemId, out var itemTypes) == true)
            {
                foreach (var itemType in itemTypes)
                {
                    if (itemType.RuneSpellId == LegacyTideTrapSpellId ||
                        string.Equals(itemType.Name, "TideTrap", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool HasLearnedLegacySpell(PlayerSpellSnapshot playerSpells, ushort spellId)
    {
        foreach (var spell in playerSpells.Spells)
        {
            if (spell.Id == spellId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLegacyEquippedRunePosition(ushort position)
    {
        return position is >= 101 and <= 115 or >= 121 and <= 135;
    }
}
