using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacySpecialPerimeterSpell13040 = 13_040;
    private const int LegacySpecialPerimeter13040SectorSize = 140;
    private const byte LegacySpecialPerimeter13040Distance = 7;
    private const int LegacySpecialPerimeter500SectorSize = 500;
    private const byte LegacySpecialPerimeter500Distance = 5;
    private const int LegacySpecialPerimeterSourceDistanceCap = 14;

    private static async ValueTask<bool> HandleLegacyPlayerSpecialPerimeterMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        bool ignoreAttackStamp,
        CancellationToken cancellationToken)
    {
        if (!LegacyMagicSortCapabilities.IsSupportedPerimeterSpecialRuntimeSpell(spell.Sort, spell.Id))
        {
            return false;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                ignoreAttackStamp,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        var updatedSession = resourceSession;
        context = context with { Session = updatedSession };
        var anchoredQuery = GetLegacySpecialPerimeterAnchoredQuery(context, updatedSession, query);
        var monsterTargets = context.MonsterStates is null
            ? []
            : SelectLegacySpecialPerimeterMonsterTargets(context, updatedSession, anchoredQuery, spell);
        var monsterBatch = await ApplyLegacySpecialPerimeterMonsterTargetsAsync(
                context,
                updatedSession,
                spell,
                playerSpell,
                monsterTargets,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = monsterBatch.Session;
        context = context with { Session = updatedSession };

        if (spell.Id == LegacySpecialPerimeterSpell13040 &&
            updatedSession.Character.MapId == 1_505 &&
            HasLegacySpecialPerimeterVisiblePlayerEnumeration(context, updatedSession))
        {
            updatedSession = await ApplyLegacySpecialPerimeterDamageSideEffectsAsync(
                    context,
                    updatedSession,
                    spell,
                    monsterBatch.Hits,
                    Array.Empty<LegacySpecialPerimeterPlayerHit>(),
                    Array.Empty<LegacySpecialPerimeterSobNpcHit>(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy Perimeter 13040 map-1505 early return " +
                    $"{FormatSession(context with { Session = updatedSession })} targetX={anchoredQuery.X} " +
                    $"targetY={anchoredQuery.Y} monsterTargets={monsterBatch.Hits.Count} " +
                    $"manualInteractionFrames={monsterBatch.InteractionFrames} progress=0 finalAnimation=0; " +
                    "source=matching Perimeter.cs case 13040 first visible-player enumeration return.")
                .ConfigureAwait(false);
            return true;
        }

        var playerTargets = SelectLegacySpecialPerimeterPlayerTargets(
            context,
            updatedSession,
            anchoredQuery,
            spell);
        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };
        var playerBatchOutcome = await ApplyLegacySpecialPerimeterPlayerTargetsAsync(
                context,
                updatedSession,
                spell,
                playerSpell,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        if (playerBatchOutcome is not { } playerBatch)
        {
            return true;
        }
        updatedSession = playerBatch.Session;
        context = context with { Session = updatedSession };

        var sobNpcTargets = context.SobNpcStates is null
            ? []
            : SelectLegacySpecialPerimeterSobNpcTargets(context, updatedSession, anchoredQuery, spell);
        var sobNpcBatch = await ApplyLegacySpecialPerimeterSobNpcTargetsAsync(
                context,
                updatedSession,
                spell,
                playerSpell,
                sobNpcTargets,
                cancellationToken)
            .ConfigureAwait(false);

        var animationTargets = new List<LegacySpellAnimationTarget>(
            monsterBatch.AnimationTargets.Count +
            playerBatch.AnimationTargets.Count +
            sobNpcBatch.AnimationTargets.Count);
        animationTargets.AddRange(monsterBatch.AnimationTargets);
        animationTargets.AddRange(playerBatch.AnimationTargets);
        animationTargets.AddRange(sobNpcBatch.AnimationTargets);
        if (animationTargets.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy special Perimeter magic " +
                    $"{FormatSession(context)} spellId={spell.Id} targets=0; " +
                    "source=matching Perimeter.cs no-target silence.")
                .ConfigureAwait(false);
            return true;
        }

        var monsterProgress = monsterBatch.Hits.Aggregate(
            0UL,
            (total, hit) => total + hit.Progress);
        var sobNpcProgress = sobNpcBatch.Hits.Aggregate(
            0UL,
            (total, hit) => total + hit.Progress);
        var progressDamage = monsterProgress + sobNpcProgress;
        updatedSession = await ApplyLegacySpecialPerimeterProgressAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        foreach (var hit in playerBatch.Hits)
        {
            await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                    context,
                    updatedSession,
                    hit.Result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            anchoredQuery.X,
            anchoredQuery.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await ApplyLegacySpecialPerimeterDamageSideEffectsAsync(
                context,
                updatedSession,
                spell,
                monsterBatch.Hits,
                playerBatch.Hits,
                sobNpcBatch.Hits,
                cancellationToken)
            .ConfigureAwait(false);

        var totalAppliedDamage =
            monsterBatch.Hits.Aggregate(0UL, (total, hit) => total + hit.Result.DamageApplied) +
            playerBatch.Hits.Aggregate(0UL, (total, hit) => total + hit.Result.DamageApplied) +
            sobNpcBatch.Hits.Aggregate(0UL, (total, hit) => total + hit.Result.DamageApplied);
        var interactionFrames = monsterBatch.InteractionFrames +
            playerBatch.InteractionFrames +
            sobNpcBatch.InteractionFrames;
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy special Perimeter magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={anchoredQuery.X} targetY={anchoredQuery.Y} targets={animationTargets.Count} " +
                $"monsterTargets={monsterBatch.Hits.Count} playerTargets={playerBatch.Hits.Count} " +
                $"sobNpcTargets={sobNpcBatch.Hits.Count} manualInteractionFrames={interactionFrames} " +
                $"monsterProgress={monsterProgress} sobNpcProgress={sobNpcProgress} " +
                $"totalAppliedDamage={totalAppliedDamage} ignoredAttackStamp={ignoreAttackStamp}; " +
                "OnAutoAttack interaction suppression remains fenced because local runtime has no authoritative source state; " +
                $"source=matching Perimeter.cs case {spell.Id}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<LegacySpecialPerimeterMonsterBatch> ApplyLegacySpecialPerimeterMonsterTargetsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        PlayerSpellSnapshotEntry playerSpell,
        IReadOnlyList<LegacyPlayerMonsterMagicTarget> targets,
        CancellationToken cancellationToken)
    {
        var hits = new List<LegacySpecialPerimeterMonsterHit>();
        var animationTargets = new List<LegacySpellAnimationTarget>();
        if (context.MonsterStates is not { } monsterStates)
        {
            return new LegacySpecialPerimeterMonsterBatch(session, hits, animationTargets, 0);
        }

        var updatedSession = session;
        var interactionFrames = 0;
        var now = DateTimeOffset.UtcNow;
        foreach (var target in targets)
        {
            if (IsLegacyGuardMonsterFamily(target.Monster, target.MonsterFamily))
            {
                updatedSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                        context,
                        updatedSession,
                        cancellationToken)
                    .ConfigureAwait(false);
                context = context with { Session = updatedSession };
            }

            var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                updatedSession,
                context.WorldData,
                target.Monster,
                target.MonsterFamily,
                spell);
            damage = CalculateLegacySpecialPerimeterSoulDamage(damage, playerSpell.UseSpellSoul);
            damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                updatedSession,
                spell,
                damage,
                LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc);
            var result = ApplyLegacyPlayerMonsterDamage(
                monsterStates,
                target.Monster,
                damage,
                now,
                updatedSession);
            if (result.AlreadyDead)
            {
                continue;
            }

            var progress = CalculateLegacyStrikeMonsterProgressDamage(updatedSession, target, damage);
            hits.Add(new LegacySpecialPerimeterMonsterHit(target, result, progress));
            animationTargets.Add(new LegacySpellAnimationTarget(
                target.Monster.Uid,
                damage,
                Hit: damage > 0,
                MsgAttackEffect.None));
            await SendLegacySpecialPerimeterInteractionAsync(
                    context,
                    updatedSession,
                    target.Monster.Uid,
                    target.Monster.X,
                    target.Monster.Y,
                    damage,
                    MsgAttackEffect.None,
                    cancellationToken)
                .ConfigureAwait(false);
            interactionFrames++;
        }

        return new LegacySpecialPerimeterMonsterBatch(
            updatedSession,
            hits,
            animationTargets,
            interactionFrames);
    }

    private static async ValueTask<LegacySpecialPerimeterPlayerBatch?> ApplyLegacySpecialPerimeterPlayerTargetsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        PlayerSpellSnapshotEntry playerSpell,
        IReadOnlyList<LegacyPlayerMagicTarget> targets,
        CancellationToken cancellationToken)
    {
        var updatedSession = session;
        var hits = new List<LegacySpecialPerimeterPlayerHit>();
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var interactionFrames = 0;
        foreach (var target in targets)
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
            var damage = CalculateLegacySpecialPerimeterSoulDamage(
                physicalDamage.Damage,
                playerSpell.UseSpellSoul);
            damage = ScaleLegacySpecialPerimeterPlayerDamage(
                spell.Id,
                activeTarget.Session.Character.Class,
                damage);
            if (!wildPhoenixDefended)
            {
                damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                    updatedSession,
                    spell,
                    damage,
                    LegacyWildPhoenixDamageTargetKind.Player);
            }

            var receive = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    context,
                    activeTarget,
                    updatedSession.Character,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false);
            if (receive.AttackerSession is { } carriedAttacker)
            {
                updatedSession = carriedAttacker;
                context = context with { Session = updatedSession };
            }
            if (receive.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return null;
            }
            if (receive.Result is not { } result)
            {
                continue;
            }

            hits.Add(new LegacySpecialPerimeterPlayerHit(result));
            var character = result.UpdatedSession.Character;
            var playerUid = CheckedUInt32(character.Uid, nameof(character.Uid));
            animationTargets.Add(new LegacySpellAnimationTarget(
                playerUid,
                damage,
                Hit: false,
                physicalDamage.Effect));
            await SendLegacySpecialPerimeterInteractionAsync(
                    context,
                    updatedSession,
                    playerUid,
                    CheckedUInt16(character.X, nameof(character.X)),
                    CheckedUInt16(character.Y, nameof(character.Y)),
                    damage,
                    physicalDamage.Effect,
                    cancellationToken)
                .ConfigureAwait(false);
            interactionFrames++;
        }

        return new LegacySpecialPerimeterPlayerBatch(
            updatedSession,
            hits,
            animationTargets,
            interactionFrames);
    }

    private static async ValueTask<LegacySpecialPerimeterSobNpcBatch> ApplyLegacySpecialPerimeterSobNpcTargetsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        PlayerSpellSnapshotEntry playerSpell,
        IReadOnlyList<LegacyPlayerSobNpcMagicTarget> targets,
        CancellationToken cancellationToken)
    {
        var hits = new List<LegacySpecialPerimeterSobNpcHit>();
        var animationTargets = new List<LegacySpellAnimationTarget>();
        if (context.SobNpcStates is not { } sobNpcStates)
        {
            return new LegacySpecialPerimeterSobNpcBatch(hits, animationTargets, 0);
        }

        var interactionFrames = 0;
        var now = DateTimeOffset.UtcNow;
        foreach (var target in targets)
        {
            var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                session,
                spell,
                context.WorldData,
                damageMultiplier: 1);
            damage = CalculateLegacySpecialPerimeterSoulDamage(damage, playerSpell.UseSpellSoul);
            damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                session,
                spell,
                damage,
                LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc);
            var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(
                target.SobNpc,
                context.WorldData?.StaticStatues);
            var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
            if (result.AlreadyDead)
            {
                continue;
            }

            var progress = CalculateLegacySobNpcExperienceDamage(target.SobNpc, result);
            hits.Add(new LegacySpecialPerimeterSobNpcHit(
                target.SobNpc,
                result,
                progress));
            var sobNpcUid = CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id));
            animationTargets.Add(new LegacySpellAnimationTarget(
                sobNpcUid,
                damage,
                Hit: damage > 0,
                MsgAttackEffect.None));
            await SendLegacySpecialPerimeterInteractionAsync(
                    context,
                    session,
                    sobNpcUid,
                    CheckedUInt16(target.SobNpc.X, nameof(target.SobNpc.X)),
                    CheckedUInt16(target.SobNpc.Y, nameof(target.SobNpc.Y)),
                    damage,
                    MsgAttackEffect.None,
                    cancellationToken)
                .ConfigureAwait(false);
            interactionFrames++;
        }

        return new LegacySpecialPerimeterSobNpcBatch(hits, animationTargets, interactionFrames);
    }

    private static async ValueTask<WorldSession> ApplyLegacySpecialPerimeterProgressAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        ulong progressDamage,
        CancellationToken cancellationToken)
    {
        var updatedSession = await ApplyLegacyStrikeIncreaseExperienceAsync(
                context,
                session,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(progressDamage),
                cancellationToken,
                activity: "special Perimeter magic weapon proficiency experience")
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };
        return await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> ApplyLegacySpecialPerimeterDamageSideEffectsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        IReadOnlyList<LegacySpecialPerimeterMonsterHit> monsterHits,
        IReadOnlyList<LegacySpecialPerimeterPlayerHit> playerHits,
        IReadOnlyList<LegacySpecialPerimeterSobNpcHit> sobNpcHits,
        CancellationToken cancellationToken)
    {
        var updatedSession = session;
        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
        foreach (var hit in monsterHits)
        {
            var target = hit.Target;
            var result = hit.Result;
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
                await SendMonsterCombatFrameToVisibleAsync(
                        context,
                        updatedSession,
                        target.Monster,
                        death,
                        cancellationToken)
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
                context = context with { Session = updatedSession };
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
                        await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                            .ConfigureAwait(false);
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

        foreach (var hit in playerHits)
        {
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    updatedSession,
                    hit.Result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (playerHits.Count != 0)
        {
            await TryApplyLegacyPlayerMagicChillingSnowAsync(
                    context,
                    updatedSession,
                    playerHits.Select(hit => hit.Result).ToArray(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (context.SobNpcStates is { } sobNpcStates)
        {
            foreach (var hit in sobNpcHits)
            {
                if (hit.Result.DamageApplied == 0)
                {
                    continue;
                }

                if (hit.Result.Killed && LegacySobNpcEventRules.IsTrainingSob(hit.SobNpc))
                {
                    await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                            context,
                            updatedSession,
                            hit.SobNpc,
                            sobNpcStates.GetVisible(hit.SobNpc),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (!LegacySobNpcEventRules.IsTrainingSob(hit.SobNpc))
                {
                    await LogLegacySobNpcEventSideEffectGapAsync(
                            context,
                            hit.SobNpc,
                            hit.Result,
                            spell)
                        .ConfigureAwait(false);
                }
            }
        }

        return updatedSession;
    }

    private static MsgAttackQuery GetLegacySpecialPerimeterAnchoredQuery(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        if (TryGetVisibleSessionByUid(context, session, query.OpponentUid, out var playerTarget))
        {
            return query with
            {
                X = CheckedUInt16(playerTarget.Session.Character.X, nameof(playerTarget.Session.Character.X)),
                Y = CheckedUInt16(playerTarget.Session.Character.Y, nameof(playerTarget.Session.Character.Y)),
            };
        }

        if (TryFindVisibleMonsterTargetByUid(context, session.Character, query.OpponentUid, out var monsterTarget))
        {
            return query with { X = monsterTarget.Monster.X, Y = monsterTarget.Monster.Y };
        }

        return query;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacySpecialPerimeterMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (!IsInsideLegacySpecialPerimeterSector(
                    session,
                    query,
                    spell.Id,
                    candidate.Monster.X,
                    candidate.Monster.Y) ||
                !CanLegacyPlayerAttackMonster(
                    session.Character,
                    candidate.Monster,
                    candidate.MonsterFamily,
                    out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacySpecialPerimeterPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (IsInsideLegacySpecialPerimeterSector(
                    session,
                    query,
                    spell.Id,
                    character.X,
                    character.Y))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacySpecialPerimeterSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (!IsInsideLegacySpecialPerimeterSector(
                    session,
                    query,
                    spell.Id,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y) ||
                !CanLegacySobNpcMagicDamageTarget(
                    context,
                    session.Character,
                    candidate.SobNpc,
                    out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static bool HasLegacySpecialPerimeterVisiblePlayerEnumeration(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        if (context.Sessions is not { } sessions)
        {
            return false;
        }

        foreach (var active in sessions.Snapshot())
        {
            if (!ReferenceEquals(active.Connection, context.Connection) &&
                active.Session.Character.Uid != session.Character.Uid &&
                CanSeePlayer(session.Character, active.Session.Character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideLegacySpecialPerimeterSector(
        WorldSession session,
        MsgAttackQuery query,
        ushort spellId,
        long targetX,
        long targetY)
    {
        var shape = spellId switch
        {
            LegacySpecialPerimeterSpell13040 => (
                SectorSize: LegacySpecialPerimeter13040SectorSize,
                Distance: LegacySpecialPerimeter13040Distance),
            15_670 or 15_680 => (
                SectorSize: LegacySpecialPerimeter500SectorSize,
                Distance: LegacySpecialPerimeter500Distance),
            _ => (SectorSize: 0, Distance: (byte)0),
        };
        if (shape.SectorSize == 0)
        {
            return false;
        }

        return IsInsideLegacySpecialPerimeterSourceSector(
            session.Character.X,
            session.Character.Y,
            query.X,
            query.Y,
            targetX,
            targetY,
            shape.SectorSize,
            shape.Distance);
    }

    private static bool IsInsideLegacySpecialPerimeterSourceSector(
        long attackerX,
        long attackerY,
        long attackX,
        long attackY,
        long targetX,
        long targetY,
        int sectorSize,
        byte requestedDistance)
    {
        var sourceAttackerX = unchecked((ushort)attackerX);
        var sourceAttackerY = unchecked((ushort)attackerY);
        var sourceAttackX = unchecked((ushort)attackX);
        var sourceAttackY = unchecked((ushort)attackY);
        var sourceTargetX = unchecked((ushort)targetX);
        var sourceTargetY = unchecked((ushort)targetY);
        var distance = Math.Min(
            (int)requestedDistance,
            LegacySpecialPerimeterSourceDistanceCap);
        if (GetLegacySpecialPerimeterSourceDistance(
                sourceTargetX,
                sourceTargetY,
                sourceAttackerX,
                sourceAttackerY) > distance)
        {
            return false;
        }

        var degree = GetLegacySpecialPerimeterSourceDegree(
            sourceAttackerX,
            sourceAttackX,
            sourceAttackerY,
            sourceAttackY);
        var leftSide = degree - sectorSize / 2;
        if (leftSide < 0)
        {
            leftSide += 360;
        }

        var rightSide = degree + sectorSize / 2;
        var addExtra = false;
        if (leftSide < rightSide || rightSide - leftSide != sectorSize)
        {
            rightSide += 360;
            addExtra = true;
        }

        var targetDegree = GetLegacySpecialPerimeterSourceDegree(
            sourceAttackerX,
            sourceTargetX,
            sourceAttackerY,
            sourceTargetY);
        if (addExtra)
        {
            targetDegree += 360;
        }

        return targetDegree >= leftSide && targetDegree <= rightSide;
    }

    private static int GetLegacySpecialPerimeterSourceDegree(
        int sourceX,
        int targetX,
        int sourceY,
        int targetY)
    {
        var radians = Math.Atan2(targetY - sourceY, targetX - sourceX);
        if (radians < 0)
        {
            radians += Math.PI * 2;
        }

        return (int)(360 - radians * 180 / Math.PI);
    }

    private static int GetLegacySpecialPerimeterSourceDistance(
        ushort x,
        ushort y,
        ushort x2,
        ushort y2)
    {
        var deltaX = x >= x2 ? x - x2 : x2 - x;
        var deltaY = y >= y2 ? y - y2 : y2 - y;
        return Math.Max(deltaX, deltaY);
    }

    private static uint ScaleLegacySpecialPerimeterPlayerDamage(
        ushort spellId,
        long targetClass,
        uint damage)
    {
        if (spellId == LegacySpecialPerimeterSpell13040)
        {
            return damage;
        }

        return IsLegacyTrojanClass(targetClass)
            ? MulDivToUInt32(damage, 10, 7)
            : MulDivToUInt32(damage, 10, 11);
    }

    private static uint CalculateLegacySpecialPerimeterSoulDamage(uint damage, byte useSpellSoul)
    {
        _ = useSpellSoul;
        return damage;
    }

    private static async ValueTask SendLegacySpecialPerimeterInteractionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint opponentUid,
        ushort x,
        ushort y,
        uint damage,
        MsgAttackEffect effect,
        CancellationToken cancellationToken)
    {
        var interaction = new MsgAttackPacket(new MsgAttackQuery(
            Uid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            OpponentUid: opponentUid,
            X: x,
            Y: y,
            AttackType: MsgAttackType.Physical,
            Damage: unchecked((int)damage),
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: effect,
            Unknown: 0));
        await context.Connection.SendAsync(interaction.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
    }

    private readonly record struct LegacySpecialPerimeterMonsterHit(
        LegacyPlayerMonsterMagicTarget Target,
        MonsterDamageResult Result,
        ulong Progress);

    private readonly record struct LegacySpecialPerimeterPlayerHit(
        LegacyPlayerMagicDamageResult Result);

    private readonly record struct LegacySpecialPerimeterSobNpcHit(
        SobNpc SobNpc,
        SobNpcDamageResult Result,
        ulong Progress);

    private readonly record struct LegacySpecialPerimeterMonsterBatch(
        WorldSession Session,
        List<LegacySpecialPerimeterMonsterHit> Hits,
        List<LegacySpellAnimationTarget> AnimationTargets,
        int InteractionFrames);

    private readonly record struct LegacySpecialPerimeterPlayerBatch(
        WorldSession Session,
        List<LegacySpecialPerimeterPlayerHit> Hits,
        List<LegacySpellAnimationTarget> AnimationTargets,
        int InteractionFrames);

    private readonly record struct LegacySpecialPerimeterSobNpcBatch(
        List<LegacySpecialPerimeterSobNpcHit> Hits,
        List<LegacySpellAnimationTarget> AnimationTargets,
        int InteractionFrames);
}
