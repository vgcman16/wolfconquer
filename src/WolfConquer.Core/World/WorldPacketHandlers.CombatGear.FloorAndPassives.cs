using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyCombatGearPirateFloorPassiveAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyCombatGearPirateFloorOnlyPassiveEffect(
                spell.Id,
                out var itemId,
                out var color,
                out var durationMilliseconds,
                out var invokeDelayMilliseconds,
                out var hitRadiusOverride))
        {
            return false;
        }

        var floorUid = await SendLegacyCombatGearPirateFloorPassiveEffectAsync(
                context,
                session,
                query,
                spell,
                itemId,
                color,
                durationMilliseconds,
                invokeDelayMilliseconds,
                cancellationToken,
                hitRadiusOverride,
                capturedPlayerSpell: playerSpell)
            .ConfigureAwait(false);

        await SendLegacyCombatGearFloorSpellPlacementAnimationAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy CombatGear pirate floor passive " +
                $"{FormatSession(context with { Session = session })} spellId={spell.Id} floorUid={floorUid} " +
                $"itemId={itemId} targetX={query.X} targetY={query.Y} durationMs={durationMilliseconds} " +
                $"invokeDelayMs={invokeDelayMilliseconds}; source=CombatGearAttack floor spell branch.")
            .ConfigureAwait(false);
        return true;
    }


    private static async ValueTask<uint> SendLegacyCombatGearPirateFloorPassiveEffectAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        uint itemId,
        byte color,
        int durationMilliseconds,
        int invokeDelayMilliseconds,
        CancellationToken cancellationToken,
        double? hitRadiusOverride = null,
        PlayerSpellSnapshotEntry? capturedPlayerSpell = null)
    {
        var capturesFloorState = spell.Id is 21_580 or 22_070 or 22_100;
        if (capturesFloorState && capturedPlayerSpell is null)
        {
            throw new ArgumentNullException(nameof(capturedPlayerSpell));
        }

        var character = session.Character;
        var floorUid = NextLegacyFloorItemUid();
        var packet = new LegacyFloorItemPacket(
            floorUid,
            itemId,
            query.X,
            query.Y,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            color,
            CheckedUInt32(character.Uid, nameof(character.Uid)),
            ReadAttributeUInt32(character.Attributes, "GuildID", "GuildId"),
            FlowerType: 0,
            Timer: 0,
            OwnerX: CheckedUInt16(character.X, nameof(character.X)),
            OwnerY: CheckedUInt16(character.Y, nameof(character.Y)),
            Name: "STR_TRAP_ID_@@" + floorUid.ToString(CultureInfo.InvariantCulture));

        var now = DateTimeOffset.UtcNow;
        if (context.FloorItems is { } floorItems)
        {
            var effect = new FloorItemRuntimeEffect(
                spell.Id,
                packet.OwnerX,
                packet.OwnerY,
                now.AddMilliseconds(invokeDelayMilliseconds),
                Math.Max(1, (int)spell.Range),
                hitRadiusOverride ?? Math.Max(1, (int)spell.MaxTargets),
                ExpiresAt: now.AddMilliseconds(durationMilliseconds),
                RepeatMilliseconds: invokeDelayMilliseconds,
                SpellLevel: capturesFloorState ? spell.Level : (byte)0,
                LevelHu: capturesFloorState ? capturedPlayerSpell!.SoulLevel : (byte)0,
                CapturedSpell: capturesFloorState ? spell : null,
                AnimationOriginX: capturesFloorState ? packet.OwnerX : null,
                AnimationOriginY: capturesFloorState ? packet.OwnerY : null);
            floorItems.AddVisible(FloorItemRuntimeEntry.ForEffect(character.MapId, packet, effect), now);
        }
        else
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        FloorItemRuntimeStateResource,
                        "combatgear-pirate-floor-passive-runtime",
                        "Pirate CombatGear floor passive was sent, but floor item runtime state is not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                            LegacyParityGapDiagnostics.Fact("floorUid", floorUid),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ]))
                .ConfigureAwait(false);
        }

        await SendFloorItemVisibleAsync(context, character, packet, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent legacy CombatGear pirate floor passive effect " +
                $"{FormatSession(context with { Session = session })} spellId={spell.Id} floorUid={floorUid} " +
                $"itemId={itemId} targetX={query.X} targetY={query.Y} durationMs={durationMilliseconds} " +
                $"invokeDelayMs={invokeDelayMilliseconds}; source=CombatGearAttack floor spell branch.")
            .ConfigureAwait(false);
        return floorUid;
    }


    private static async ValueTask SendLegacyCombatGearFloorSpellPlacementAnimationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            []);
        foreach (var frame in animation.ToFrames())
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }


    private static bool TryGetLegacyCombatGearPirateFloorOnlyPassiveEffect(
        ushort spellId,
        out uint itemId,
        out byte color,
        out int durationMilliseconds,
        out int invokeDelayMilliseconds,
        out double? hitRadiusOverride)
    {
        durationMilliseconds = 3_000;
        invokeDelayMilliseconds = 900;
        switch (spellId)
        {
            case 22_070:
                itemId = 4_200;
                color = 14;
                hitRadiusOverride = 4;
                return true;
            case 22_080:
                itemId = 4_190;
                color = 14;
                hitRadiusOverride = 4;
                return true;
            case 22_100:
                itemId = 4_210;
                color = 30;
                hitRadiusOverride = null;
                return true;
            default:
                itemId = 0;
                color = 0;
                hitRadiusOverride = null;
                return false;
        }
    }


    private static bool IsLegacyCombatGearPirateFloorPassiveSpell(ushort spellId)
    {
        return TryGetLegacyCombatGearPirateFloorOnlyPassiveEffect(
                spellId,
                out _,
                out _,
                out _,
                out _,
                out _) ||
            TryGetLegacyCombatGearPirateFloorDamagePassiveEffect(spellId, out _, out _, out _, out _);
    }


    private static bool TryGetLegacyCombatGearPirateFloorDamagePassiveEffect(
        ushort spellId,
        out uint itemId,
        out byte color,
        out int durationMilliseconds,
        out int invokeDelayMilliseconds)
    {
        switch (spellId)
        {
            case 22_090:
                itemId = 4_220;
                color = 29;
                durationMilliseconds = 10_000;
                invokeDelayMilliseconds = 900;
                return true;
            default:
                itemId = 0;
                color = 0;
                durationMilliseconds = 0;
                invokeDelayMilliseconds = 0;
                return false;
        }
    }


    internal static async ValueTask<int> SendDueLegacyCombatGearFloorSpellsAsync(
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

        var totalProcessedCount = 0;
        var totalExpiredCount = 0;
        foreach (var spellId in LegacyCombatGearRuntimeFloorSpellIds)
        {
            var due = floorItems.ConsumeDueRepeatingEffects(spellId, now);
            totalExpiredCount += due.Expired.Count;

            var removeViewerCount = 0;
            foreach (var entry in due.Expired)
            {
                if (entry.Effect is { } effect)
                {
                    removeViewerCount += await SendLegacyTideTrapRemoveEffectAsync(
                            entry,
                            effect,
                            activeSessions,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            if (due.Invoked.Count == 0)
            {
                if (due.Expired.Count > 0)
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer handled legacy CombatGear floor spell runtime " +
                            $"spellId={spellId} pulseCount=0 targetCount=0 expiredCount={due.Expired.Count} " +
                            $"removeViewerCount={removeViewerCount}.")
                        .ConfigureAwait(false);
                }

                continue;
            }

            if (context.WorldData is not { } worldData)
            {
                continue;
            }

            var hasDefaultSpell = LegacyMagicRuntime.TryResolveSpell(worldData, spellId, spellLevel: 0, out var spell);
            var isCapturedFloor = spellId is 21_580 or 22_070 or 22_100;
            if (!hasDefaultSpell && !isCapturedFloor)
            {
                continue;
            }

            if (IsLegacyCombatGearColdBloodlineSourceFloorSpell(spellId))
            {
                var procCount = await SendDueLegacyCombatGearColdBloodlineFloorSpellsAsync(
                        context,
                        activeSessions,
                        due.Invoked,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (procCount > 0 || due.Expired.Count > 0)
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer handled legacy CombatGear ColdBloodline floor proc " +
                            $"spellId={spellId} procSpellId={LegacyCombatGearColdBloodlineSpellId} " +
                            $"procCount={procCount} expiredCount={due.Expired.Count} " +
                            $"removeViewerCount={removeViewerCount}; " +
                            "source=ThreadPool floor spell case 21560/22090 -> CombatGear.ProcescMagic(21550).")
                        .ConfigureAwait(false);
                }

                totalProcessedCount += procCount;
                continue;
            }

            var processedCount = 0;
            var animationTargetCount = 0;
            foreach (var entry in due.Invoked)
            {
                if (entry.Effect is not { } effect ||
                    !TryGetActiveSessionByUid(activeSessions, entry.Packet.ItemOwnerUid, out var owner) ||
                    owner.Session.Character.MapId != entry.MapId)
                {
                    continue;
                }

                var pulseSpell = isCapturedFloor ? effect.CapturedSpell ?? spell : spell;
                if (pulseSpell is null)
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
                        if (!IsLegacyCombatGearFloorSpellTarget(
                                spellId,
                                entry,
                                effect,
                                target.Monster.X,
                                target.Monster.Y))
                        {
                            continue;
                        }

                        var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                            ownerSession,
                            context.WorldData,
                            target.Monster,
                            target.MonsterFamily,
                            pulseSpell);
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
                            isCapturedFloor ? result.RequestedDamage : result.DamageApplied,
                            Hit: isCapturedFloor ? result.RequestedDamage > 0 : result.DamageApplied > 0,
                        MsgAttackEffect.None));
                    }
                }

                var ownerLostDuringReceive = false;
                foreach (var target in EnumerateVisiblePlayerMagicTargets(ownerContext, ownerSession, pulseSpell))
                {
                    var targetCharacter = target.ActiveSession.Session.Character;
                    if (!IsLegacyCombatGearFloorSpellTarget(
                            spellId,
                            entry,
                            effect,
                            targetCharacter.X,
                            targetCharacter.Y))
                    {
                        continue;
                    }

                    var damage = CalculateLegacyPlayerPlayerPhysicalSpellDamage(
                        ownerSession,
                        target.ActiveSession.Session,
                        pulseSpell,
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

                    if (result.DamageApplied == 0 && !isCapturedFloor)
                    {
                        continue;
                    }

                    // ReceiveAttack mutates SpellObj before the source's display-only Soul step.
                    // Base.CalculateSoul is identity in this native tree, including nonzero LevelHu.
                    var displayDamage = isCapturedFloor ? result.ReceiveDamage ?? damage : result.DamageApplied;
                    if (spellId == 21_580)
                    {
                        displayDamage = unchecked(displayDamage * 75u) / 100u;
                    }

                    playerResults.Add(result);
                    animationTargets.Add(new LegacySpellAnimationTarget(
                        CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                        displayDamage,
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
                        if (!IsLegacyCombatGearFloorSpellTarget(
                                spellId,
                                entry,
                                effect,
                                target.SobNpc.X,
                                target.SobNpc.Y))
                        {
                            continue;
                        }

                        var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                            ownerSession,
                            pulseSpell,
                            worldData,
                            damageMultiplier: 1);
                        var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
                        var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                        if (result.AlreadyDead || result.DamageApplied == 0 && !isCapturedFloor)
                        {
                            continue;
                        }

                        animationTargets.Add(new LegacySpellAnimationTarget(
                            CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                            isCapturedFloor ? damage : result.DamageApplied,
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
                    effect.AnimationOriginX ?? effect.OriginX,
                    effect.AnimationOriginY ?? effect.OriginY,
                    spellId,
                    spellLevel: isCapturedFloor ? effect.SpellLevel : (byte)0,
                    levelHu: isCapturedFloor ? effect.LevelHu : (byte)0,
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
                            pulseSpell)
                        .ConfigureAwait(false);
                }

                processedCount++;
                animationTargetCount += animationTargets.Count;
            }

            if (processedCount > 0 || due.Expired.Count > 0)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer handled legacy CombatGear floor spell runtime " +
                        $"spellId={spellId} pulseCount={processedCount} " +
                        $"targetCount={animationTargetCount} expiredCount={due.Expired.Count} removeViewerCount={removeViewerCount}.")
                    .ConfigureAwait(false);
            }

            totalProcessedCount += processedCount;
        }

        return totalProcessedCount + totalExpiredCount;
    }


    private static async ValueTask<int> SendDueLegacyCombatGearColdBloodlineFloorSpellsAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<WorldActiveSession> activeSessions,
        IReadOnlyList<FloorItemRuntimeEntry> invoked,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData)
        {
            return 0;
        }

        var procCount = 0;
        foreach (var entry in invoked)
        {
            if (entry.Effect is not { } effect ||
                !TryGetActiveSessionByUid(activeSessions, entry.Packet.ItemOwnerUid, out var owner) ||
                owner.Session.Character.MapId != entry.MapId ||
                LegacyGetDistance(
                    owner.Session.Character.X,
                    owner.Session.Character.Y,
                    entry.Packet.X,
                    entry.Packet.Y) > effect.HitRadius ||
                HasLegacyStatusFlag(owner.Session.Character, LegacyCombatGearColdBloodlineFlag))
            {
                continue;
            }

            var ownerContext = context with
            {
                Connection = owner.Connection,
                Session = owner.Session,
            };
            var ownerSession = owner.Session;
            if (!TryResolveLegacyPlayerMagicRuntimeSpell(
                    ownerSession,
                    worldData,
                    LegacyCombatGearColdBloodlineSpellId,
                    out var playerSpell,
                    out var coldBloodlineSpell))
            {
                if (!LegacyMagicRuntime.TryResolveSpell(
                        worldData,
                        LegacyCombatGearColdBloodlineSpellId,
                        spellLevel: 0,
                        out coldBloodlineSpell))
                {
                    continue;
                }

                playerSpell = new PlayerSpellSnapshotEntry(
                    LegacyCombatGearColdBloodlineSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0);
            }

            await SendLegacyCombatGearPassiveStatusAsync(
                    ownerContext,
                    ownerSession,
                    playerSpell,
                    coldBloodlineSpell.Id,
                    LegacyCombatGearColdBloodlineFlag,
                    duration: 5,
                    sendArchiveSkill: true,
                    archiveVisible: true,
                    archiveDamage: ReadLegacyMagicUInt32FieldOrDefault(
                        worldData,
                        coldBloodlineSpell.Id,
                        playerSpell.Level,
                        35),
                    archiveSpellLevel: 0,
                    animationTargetSelf: true,
                    cancellationToken,
                    sendEquipmentRefresh: true)
                .ConfigureAwait(false);
            procCount++;
        }

        return procCount;
    }


    private static bool IsLegacyCombatGearFloorSpellTarget(
        ushort spellId,
        FloorItemRuntimeEntry entry,
        FloorItemRuntimeEffect effect,
        long targetX,
        long targetY)
    {
        if (spellId is 21_580 or 22_100)
        {
            var range = checked((byte)Math.Clamp(effect.Distance, 0, byte.MaxValue));
            var width = checked((byte)Math.Clamp((int)Math.Ceiling(effect.HitRadius), 0, byte.MaxValue));
            return IsLegacyInLine(
                entry.Packet.X,
                entry.Packet.Y,
                effect.OriginX,
                effect.OriginY,
                targetX,
                targetY,
                range,
                width);
        }

        return LegacyGetDistance(entry.Packet.X, entry.Packet.Y, targetX, targetY) <= effect.HitRadius;
    }


    private static bool IsLegacyCombatGearColdBloodlineSourceFloorSpell(ushort spellId)
    {
        return spellId is 21_560 or LegacyCombatGearColdBloodlineSourceFloorSpellId;
    }
}
