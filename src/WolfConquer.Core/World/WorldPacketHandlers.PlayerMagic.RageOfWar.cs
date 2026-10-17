using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyRageOfWarSpellId = 12_930;
    private const byte LegacyRageOfWarTargetDistance = 11;
    private const byte LegacyRageOfWarMonsterSobLineWidth = 1;
    private const byte LegacyRageOfWarPlayerLineWidth = 0;
    private const uint LegacyRageOfWarFloorItemId = 1_500;
    private const int LegacyRageOfWarFloorDurationMilliseconds = 2_000;
    private const double LegacyRageOfWarFloorHitRadius = 3.0;
    private const int LegacyRageOfWarFloorPlayerDamagePercent = 60;
    private const int LegacyRageOfWarFloorRemovalVisibilityRadius = 18;
    private const byte LegacyRageOfWarFloorBombFlag = 1;

    private static async ValueTask<bool> HandleLegacyPlayerRageOfWarLineMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.Line ||
            spell.Id != LegacyRageOfWarSpellId)
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
        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyRageOfWarMonsterTargets(context, updatedSession, query, spell)
            : [];
        var playerTargets = SelectLegacyRageOfWarPlayerTargets(context, updatedSession, query, spell);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyRageOfWarSobNpcTargets(context, updatedSession, query, spell)
            : [];

        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result, uint RequestedDamage, uint Progress)>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var killedTrainingSobNpcTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var eventSobNpcSideEffectTargets = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();
        var now = DateTimeOffset.UtcNow;
        var casterX = updatedSession.Character.X;
        var casterY = updatedSession.Character.Y;
        (ushort X, ushort Y)? nearestHitTarget = null;

        void TrackNearestHitTarget(long x, long y)
        {
            if (nearestHitTarget is not { } current ||
                LegacyGetDistance(casterX, casterY, x, y) < LegacyGetDistance(casterX, casterY, current.X, current.Y))
            {
                nearestHitTarget = (CheckedUInt16(x, "targetX"), CheckedUInt16(y, "targetY"));
            }
        }

        if (context.MonsterStates is { } monsterStates)
        {
            foreach (var target in monsterTargets)
            {
                var requestedDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    updatedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                requestedDamage = CalculateLegacyRageOfWarSoulDamage(requestedDamage, playerSpell.UseSpellSoul);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    requestedDamage,
                    now,
                    updatedSession);
                var progress = result.AlreadyDead ? 0 : requestedDamage;
                monsterResults.Add((target, result, requestedDamage, progress));
                if (result.AlreadyDead)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    result.RequestedDamage,
                    Hit: result.RequestedDamage > 0,
                    MsgAttackEffect.None));
                if (result.RequestedDamage > 0)
                {
                    TrackNearestHitTarget(target.Monster.X, target.Monster.Y);
                }
            }
        }

        foreach (var target in playerTargets)
        {
            var activeTarget = target.ActiveSession;
            (activeTarget, var wildPhoenixDefended) =
                await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                        context,
                        activeTarget,
                        cancellationToken)
                    .ConfigureAwait(false);
            var damage = wildPhoenixDefended
                ? 1U
                : CalculateLegacyPlayerPlayerPhysicalSpellDamage(
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData,
                    damageDivisor: 1);
            damage = MulDivToUInt32(CalculateLegacyRageOfWarSoulDamage(damage, playerSpell.UseSpellSoul), 10, 13);
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
            // Line.cs enqueues every accepted player, including integer-scaled zero hits.
            playerResults.Add(result);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                damage,
                Hit: true,
                MsgAttackEffect.None));
            TrackNearestHitTarget(
                result.UpdatedSession.Character.X,
                result.UpdatedSession.Character.Y);
        }

        updatedSession = ApplyLegacyCounterHitShoot(
            context,
            updatedSession,
            playerTargets.Count,
            out var counterHitShoots);
        context = context with { Session = updatedSession };

        var sobNpcProgress = 0UL;
        if (context.SobNpcStates is { } sobNpcStates)
        {
            foreach (var target in sobNpcTargets)
            {
                var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                    updatedSession,
                    spell,
                    context.WorldData,
                    damageMultiplier: 1);
                damage = CalculateLegacyRageOfWarSoulDamage(damage, playerSpell.UseSpellSoul);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                sobNpcResults.Add((target.SobNpc, result));
                if (!result.AlreadyDead)
                {
                    sobNpcProgress += result.DamageApplied;
                }
                if (result.AlreadyDead || result.DamageApplied == 0)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                    damage,
                    Hit: true,
                    MsgAttackEffect.None));
                TrackNearestHitTarget(target.SobNpc.X, target.SobNpc.Y);

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

        var rageOfWarFloorEffect = CreateLegacyRageOfWarFloorEffect(
            updatedSession,
            nearestHitTarget);
        if (rageOfWarFloorEffect is { } floorEffect)
        {
            animationTargets.Add(floorEffect.AnimationTarget);
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        if (rageOfWarFloorEffect is { } floorEffectToSend)
        {
            await SendLegacyRageOfWarFloorEffectAsync(
                    context,
                    updatedSession,
                    spell,
                    floorEffectToSend.Packet,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
        foreach (var (target, result, _, _) in monsterResults)
        {
            if (result.AlreadyDead)
            {
                continue;
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
                await SendMonsterCombatFrameToVisibleAsync(context, updatedSession, target.Monster, death, cancellationToken)
                    .ConfigureAwait(false);
                await TrySendLegacyMonsterDeathDropAsync(
                        context,
                        updatedSession,
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
            else
            {
                if (LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
                {
                    var damagedMonster = target.Monster with { Hitpoints = result.RemainingHitpoints };
                    foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                    {
                        await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                    }
                }

                firstRetaliationTarget ??= target;
            }
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

        foreach (var result in playerResults)
        {
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (context.SobNpcStates is { } currentSobNpcStates)
        {
            foreach (var killed in killedTrainingSobNpcTargets)
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        context,
                        updatedSession,
                        killed.SobNpc,
                        currentSobNpcStates.GetVisible(killed.SobNpc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var (sobNpc, result, _) in eventSobNpcSideEffectTargets)
            {
                await LogLegacySobNpcEventSideEffectGapAsync(context, sobNpc, result, spell)
                    .ConfigureAwait(false);
            }
        }

        var monsterProgress = monsterResults.Aggregate(0UL, (total, result) => total + result.Progress);
        var progressDamage = monsterProgress + sobNpcProgress;
        updatedSession = await ApplyLegacyRageOfWarIncreaseExperienceAsync(
                context,
                updatedSession,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(progressDamage),
                cancellationToken,
                activity: "RageOfWar Line magic weapon proficiency experience")
            .ConfigureAwait(false);

        var totalAppliedDamage =
            monsterResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            playerResults.Aggregate(0UL, (total, result) => total + result.DamageApplied) +
            sobNpcResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy RageOfWar Line magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={query.X} targetY={query.Y} range={spell.Range} targetDistanceGate={LegacyRageOfWarTargetDistance} " +
                $"monsterSobWidth={LegacyRageOfWarMonsterSobLineWidth} playerWidth={LegacyRageOfWarPlayerLineWidth} " +
                $"targets={animationTargets.Count} monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} " +
                $"sobNpcTargets={sobNpcResults.Count} monsterProgress={monsterProgress} sobNpcProgress={sobNpcProgress} " +
                $"totalAppliedDamage={totalAppliedDamage} counterHitShoots={counterHitShoots}; source=Line.cs case 12930.")
            .ConfigureAwait(false);
        return true;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyRageOfWarMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (IsLegacyGuardMonsterFamily(candidate.Monster, candidate.MonsterFamily) ||
                LegacyGetDistance(session.Character.X, session.Character.Y, candidate.Monster.X, candidate.Monster.Y) >= LegacyRageOfWarTargetDistance ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    spell.Range,
                    LegacyRageOfWarMonsterSobLineWidth) ||
                !CanLegacyPlayerAttackMonster(session.Character, candidate.Monster, candidate.MonsterFamily, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyRageOfWarPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(session.Character.X, session.Character.Y, character.X, character.Y) >= LegacyRageOfWarTargetDistance ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    character.X,
                    character.Y,
                    spell.Range,
                    LegacyRageOfWarPlayerLineWidth) ||
                !CanLegacyPlayerMagicDamageTarget(context, session, candidate.ActiveSession.Session, spell, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyRageOfWarSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(session.Character.X, session.Character.Y, candidate.SobNpc.X, candidate.SobNpc.Y) >= LegacyRageOfWarTargetDistance ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    spell.Range,
                    LegacyRageOfWarMonsterSobLineWidth) ||
                !CanLegacySobNpcMagicDamageTarget(context, session.Character, candidate.SobNpc, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static async ValueTask<WorldSession> ApplyLegacyRageOfWarIncreaseExperienceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        ulong progressDamage,
        CancellationToken cancellationToken)
    {
        if (progressDamage == 0)
        {
            return session;
        }

        var boundedProgress = progressDamage > uint.MaxValue ? uint.MaxValue : (uint)progressDamage;
        var experienceResult = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateLegacyExperienceGainInputs(context, session, boundedProgress));
        var updatedSession = ApplyLegacyExperienceSideEffects(context, context.Connection, session, experienceResult);
        if (experienceResult.AppliedExperienceGain == 0)
        {
            if (experienceResult.OblivionExperienceGain != 0 ||
                experienceResult.HuntingBlessingGain != 0)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy RageOfWar Line experience side effects " +
                        $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                        $"applied=0 oblivion={experienceResult.OblivionExperienceGain} " +
                        $"huntingBlessing={experienceResult.HuntingBlessingGain} suppression={experienceResult.SuppressionReason}; " +
                        "source=Line.cs case 12930 IncreaseExperience.Up.")
                    .ConfigureAwait(false);
            }

            return updatedSession;
        }

        updatedSession = await ApplyLegacyFinalExperienceGainAsync(
                context,
                context.Connection,
                updatedSession,
                experienceResult.AppliedExperienceGain,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent legacy RageOfWar Line experience " +
                $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                $"gain={experienceResult.AppliedExperienceGain} oblivion={experienceResult.OblivionExperienceGain} " +
                $"huntingBlessing={experienceResult.HuntingBlessingGain} experience={updatedSession.Character.Experience} " +
                $"level={updatedSession.Character.Level}; source=Line.cs case 12930 IncreaseExperience.Up.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static LegacyRageOfWarFloorEffect? CreateLegacyRageOfWarFloorEffect(
        WorldSession session,
        (ushort X, ushort Y)? nearestHitTarget)
    {
        if (nearestHitTarget is not { } target)
        {
            return null;
        }

        var character = session.Character;
        var packet = new LegacyFloorItemPacket(
            NextLegacyFloorItemUid(),
            LegacyRageOfWarFloorItemId,
            target.X,
            target.Y,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            PlusOrColor: 14,
            CheckedUInt32(character.Uid, nameof(character.Uid)),
            ReadAttributeUInt32(character.Attributes, "GuildID", "GuildId"),
            FlowerType: 0,
            Timer: 0,
            OwnerX: CheckedUInt16(character.X, nameof(character.X)),
            OwnerY: CheckedUInt16(character.Y, nameof(character.Y)),
            Name: "trap");
        var animationTarget = new LegacySpellAnimationTarget(
            packet.Uid,
            Damage: 1,
            Hit: true,
            MsgAttackEffect.None,
            NewX: packet.OwnerX,
            NewY: packet.OwnerY);
        return new LegacyRageOfWarFloorEffect(packet, animationTarget);
    }

    private static async ValueTask SendLegacyRageOfWarFloorEffectAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        LegacyFloorItemPacket packet,
        CancellationToken cancellationToken)
    {
        var character = session.Character;
        var now = DateTimeOffset.UtcNow;
        var dueAt = now.AddMilliseconds(LegacyRageOfWarFloorDurationMilliseconds);
        if (context.FloorItems is { } floorItems)
        {
            var effect = new FloorItemRuntimeEffect(
                spell.Id,
                packet.OwnerX,
                packet.OwnerY,
                dueAt,
                HitRadius: LegacyRageOfWarFloorHitRadius,
                PlayerDamagePercent: LegacyRageOfWarFloorPlayerDamagePercent,
                RemovalVisibilityRadius: LegacyRageOfWarFloorRemovalVisibilityRadius);
            floorItems.AddVisible(FloorItemRuntimeEntry.ForEffect(character.MapId, packet, effect), now);
        }
        else
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        FloorItemRuntimeStateResource,
                        "rageofwar-floor-effect-runtime",
                        "RageOfWar floor effect was sent, but floor item runtime state is not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                            LegacyParityGapDiagnostics.Fact("floorUid", packet.Uid),
                            LegacyParityGapDiagnostics.Fact("floorItemId", packet.ItemId),
                            LegacyParityGapDiagnostics.Fact("durationMs", LegacyRageOfWarFloorDurationMilliseconds),
                        ]))
                .ConfigureAwait(false);
        }

        await SendFloorItemVisibleAsync(context, character, packet, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent legacy RageOfWar floor effect " +
                $"{FormatSession(context with { Session = session })} spellId={spell.Id} floorUid={packet.Uid} " +
                $"floorItemId={packet.ItemId} nearestX={packet.X} nearestY={packet.Y} " +
                $"ownerX={packet.OwnerX} ownerY={packet.OwnerY} durationMs={LegacyRageOfWarFloorDurationMilliseconds}; " +
                "source=Line.cs case 12930 FloorSpell(1500, nearestX, nearestY, color 14, duration 2000).")
            .ConfigureAwait(false);
    }

    internal static async ValueTask<int> SendDueLegacyRageOfWarFloorSpellsAsync(
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

        var dueEffects = floorItems.ConsumeDueEffects(LegacyRageOfWarSpellId, now);
        if (dueEffects.Count == 0)
        {
            return 0;
        }

        var processedCount = 0;
        var animationTargetCount = 0;
        var monsterProgress = 0UL;
        var sobNpcProgress = 0UL;
        var playerTargetCount = 0;
        var removeViewerCount = 0;
        foreach (var entry in dueEffects)
        {
            if (entry.Effect is not { } effect)
            {
                continue;
            }

            if (context.WorldData is not { } worldData ||
                !TryGetActiveSessionByUid(activeSessions, entry.Packet.ItemOwnerUid, out var owner) ||
                owner.Session.Character.MapId != entry.MapId ||
                !TryResolveLegacyRageOfWarFloorRuntimeSpell(owner.Session, worldData, out var playerSpell, out var spell))
            {
                removeViewerCount += await SendLegacyRageOfWarRemoveEffectAsync(
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
            var entrySobNpcProgress = 0UL;

            if (context.MonsterStates is { } monsterStates)
            {
                foreach (var target in EnumerateVisibleMonsterTargets(ownerContext, ownerSession.Character))
                {
                    if (!IsWithinLegacyRageOfWarFloorHitRadius(entry, target.Monster.X, target.Monster.Y) ||
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
                    if (result.AlreadyDead)
                    {
                        continue;
                    }

                    monsterProgress += result.DamageApplied;
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
                var activeTarget = target.ActiveSession;
                var targetCharacter = activeTarget.Session.Character;
                if (!IsWithinLegacyRageOfWarFloorHitRadius(entry, targetCharacter.X, targetCharacter.Y) ||
                    !CanLegacyPlayerMagicDamageTarget(ownerContext, ownerSession, activeTarget.Session, spell, out _))
                {
                    continue;
                }

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
                damage = MulDivToUInt32(damage, checked((uint)effect.PlayerDamagePercent), 100);
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
                // ThreadPool's delayed 60% hit is still visible when rounding yields zero.
                playerResults.Add(result);
                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                    damage,
                    Hit: true,
                    MsgAttackEffect.None));
            }

            if (ownerLostDuringReceive)
            {
                removeViewerCount += await SendLegacyRageOfWarRemoveEffectAsync(
                        entry, effect, activeSessions, cancellationToken)
                    .ConfigureAwait(false);
                processedCount++;
                continue;
            }

            if (context.SobNpcStates is { } sobNpcStates)
            {
                foreach (var target in EnumerateVisibleSobNpcMagicTargets(ownerContext, ownerSession.Character))
                {
                    if (!IsWithinLegacyRageOfWarFloorHitRadius(entry, target.SobNpc.X, target.SobNpc.Y) ||
                        !CanLegacySobNpcMagicDamageTarget(ownerContext, ownerSession.Character, target.SobNpc, out _))
                    {
                        continue;
                    }

                    var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                        ownerSession,
                        spell,
                        worldData,
                        damageMultiplier: 1);
                    var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
                    var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                    if (!result.AlreadyDead)
                    {
                        entrySobNpcProgress += result.DamageApplied;
                        sobNpcProgress += result.DamageApplied;
                    }

                    if (result.AlreadyDead || result.DamageApplied == 0)
                    {
                        continue;
                    }

                    animationTargets.Add(new LegacySpellAnimationTarget(
                        CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                        damage,
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
                LegacyRageOfWarSpellId,
                (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                playerSpell.UseSpellSoul,
                animationTargets,
                bombFlag: LegacyRageOfWarFloorBombFlag);
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

                if (!result.Killed)
                {
                    continue;
                }

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

            var progressDamage = monsterResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
                entrySobNpcProgress;
            updatedOwnerSession = await ApplyLegacyRageOfWarIncreaseExperienceAsync(
                    ownerContext,
                    updatedOwnerSession,
                    progressDamage,
                    cancellationToken)
                .ConfigureAwait(false);
            ownerContext = ownerContext with { Session = updatedOwnerSession };

            removeViewerCount += await SendLegacyRageOfWarRemoveEffectAsync(
                    entry,
                    effect,
                    activeSessions,
                    cancellationToken)
                .ConfigureAwait(false);
            processedCount++;
            animationTargetCount += animationTargets.Count;
            playerTargetCount += playerResults.Count;
        }

        if (processedCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy RageOfWar floor spell runtime " +
                    $"trapCount={processedCount} targetCount={animationTargetCount} " +
                    $"monsterProgress={monsterProgress} sobNpcProgress={sobNpcProgress} " +
                    $"playerTargets={playerTargetCount} removeViewerCount={removeViewerCount}; " +
                    "source=ThreadPool.cs case 12930.")
                .ConfigureAwait(false);
        }

        return processedCount;
    }

    private static bool TryResolveLegacyRageOfWarFloorRuntimeSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell)
    {
        if (TryResolveLegacyPlayerMagicRuntimeSpell(session, worldData, LegacyRageOfWarSpellId, out playerSpell, out spell))
        {
            return true;
        }

        if (LegacyMagicRuntime.TryResolveSpell(worldData, LegacyRageOfWarSpellId, spellLevel: 0, out spell))
        {
            playerSpell = new PlayerSpellSnapshotEntry(
                LegacyRageOfWarSpellId,
                Level: spell.Level,
                Experience: 0,
                PreviousLevel: 0,
                SoulLevel: 0,
                UseSpellSoul: 0);
            return true;
        }

        playerSpell = default!;
        return false;
    }

    private static bool IsWithinLegacyRageOfWarFloorHitRadius(
        FloorItemRuntimeEntry entry,
        long x,
        long y)
    {
        return entry.Effect is { } effect &&
            LegacyGetDistance(entry.Packet.X, entry.Packet.Y, x, y) <= effect.HitRadius;
    }

    private static async ValueTask<int> SendLegacyRageOfWarRemoveEffectAsync(
        FloorItemRuntimeEntry entry,
        FloorItemRuntimeEffect effect,
        IReadOnlyList<WorldActiveSession> activeSessions,
        CancellationToken cancellationToken)
    {
        if (entry.MapId == 0)
        {
            return 0;
        }

        var removeFrame = (entry.Packet with { DropType = LegacyFloorItemDropType.RemoveEffect }).ToFrameBytes();
        var sentCount = 0;
        foreach (var active in activeSessions)
        {
            if (active.Session.Character.MapId != entry.MapId ||
                active.Session.Character.MapId == LegacyAlienWorldMapId ||
                LegacyGetDistance(
                    active.Session.Character.X,
                    active.Session.Character.Y,
                    entry.Packet.X,
                    entry.Packet.Y) > effect.RemovalVisibilityRadius)
            {
                continue;
            }

            await active.Connection.SendAsync(removeFrame, cancellationToken).ConfigureAwait(false);
            sentCount++;
        }

        return sentCount;
    }

    private sealed record LegacyRageOfWarFloorEffect(
        LegacyFloorItemPacket Packet,
        LegacySpellAnimationTarget AnimationTarget);

    private static uint CalculateLegacyRageOfWarSoulDamage(uint damage, byte useSpellSoul)
    {
        _ = useSpellSoul;
        return damage;
    }
}
