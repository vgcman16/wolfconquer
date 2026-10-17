using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyAirKickSpellId = 12_320;
    private const ushort LegacyAirKickNextSpellId = 12_330;
    private const byte LegacyAirKickPathHitRange = 1;

    private static async ValueTask<bool> HandleLegacyPlayerAirKickMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AirKick)
        {
            return false;
        }

        // AirKick.Execute calls CanUseSpell.Verified before its 12320 ID gate.
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

        if (spell.Id != LegacyAirKickSpellId)
        {
            return true;
        }

        var castPointDistance = LegacyGetDistance(
            resourceSession.Character.X,
            resourceSession.Character.Y,
            query.X,
            query.Y);
        var pathDistance = (int)Math.Min(spell.Range, castPointDistance);
        var path = SelectLegacyAirKickPath(context, resourceSession, query, pathDistance);
        if (path.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer stopped legacy AirKick magic after resource prep " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                    $"targetX={query.X} targetY={query.Y} pathDistance={pathDistance}; " +
                    "source AirKick.cs returns when CheckBladeTeampsCoords has no coordinates.")
                .ConfigureAwait(false);
            return true;
        }

        var landing = path[path.Count - 1];
        var guildWarEndpointCheck = CheckLegacyMoveLineGuildWarEndpoint(
            context,
            resourceSession,
            landing.X,
            landing.Y);
        if (guildWarEndpointCheck.Applies)
        {
            if (!guildWarEndpointCheck.Allowed)
            {
                await SendLegacyGuildWarPullbackAsync(
                        context,
                        context.Connection,
                        resourceSession,
                        guildWarEndpointCheck,
                        sourceName: "AirKick",
                        cancellationToken)
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer stopped legacy AirKick magic after resource prep " +
                        $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                        $"landingX={landing.X} landingY={landing.Y}; source=AirKick.cs CheckGuildWar rejection.")
                    .ConfigureAwait(false);
                return true;
            }

            if (guildWarEndpointCheck.NextTerrainMask != guildWarEndpointCheck.CurrentTerrainMask)
            {
                resourceSession = UpdateLegacyGuildWarTerrainMask(
                    context,
                    resourceSession,
                    guildWarEndpointCheck.NextTerrainMask);
            }
        }

        var movedSession = MoveLegacyAirKickCasterToLanding(context, resourceSession, landing);
        context = context with { Session = movedSession };

        var monsterTargets = context.MonsterStates is null
            ? []
            : SelectLegacyAirKickMonsterTargets(context, movedSession, query, spell, path);
        var playerTargets = SelectLegacyAirKickPlayerTargets(context, movedSession, query, spell, path);
        var sobNpcTargets = context.SobNpcStates is null
            ? []
            : SelectLegacyAirKickSobNpcTargets(context, movedSession, query, spell, path);

        foreach (var target in monsterTargets)
        {
            if (!IsLegacyGuardMonsterFamily(target.Monster, target.MonsterFamily))
            {
                continue;
            }

            movedSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                    context,
                    movedSession,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = movedSession };
            break;
        }

        movedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                movedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = movedSession };

        var casterUid = CheckedUInt32(movedSession.Character.Uid, nameof(movedSession.Character.Uid));
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(
            LegacyPlayerMonsterMagicTarget Target,
            MonsterDamageResult Result,
            uint Damage,
            uint Progress)>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var sobNpcResults = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            uint Damage,
            uint Progress)>();
        var progressDamage = 0U;
        var now = DateTimeOffset.UtcNow;

        if (context.MonsterStates is { } monsterStates)
        {
            foreach (var target in monsterTargets)
            {
                var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    movedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                damage = CalculateLegacyAirKickSoulDamage(damage, playerSpell.UseSpellSoul);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    damage,
                    now,
                    movedSession);
                if (result.AlreadyDead)
                {
                    continue;
                }

                var progress = CalculateLegacyAirKickMonsterProgressDamage(
                    movedSession,
                    target,
                    damage);
                progressDamage = unchecked(progressDamage + progress);
                monsterResults.Add((target, result, damage, progress));
                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    damage,
                    Hit: damage > 0,
                    MsgAttackEffect.None));

                if (result.Killed)
                {
                    var death = new MsgAttackPacket(new MsgAttackQuery(
                        Uid: casterUid,
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
                            movedSession,
                            target.Monster,
                            death,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await TrySendLegacyMonsterDeathDropAsync(
                            context,
                            movedSession,
                            target.Monster,
                            target.MonsterFamily,
                            cancellationToken)
                        .ConfigureAwait(false);
                    movedSession = await TryApplyLegacyBossDeathRewardsAsync(
                            context,
                            movedSession,
                            target.Monster,
                            target.MonsterFamily,
                            cancellationToken)
                        .ConfigureAwait(false);
                    context = context with { Session = movedSession };
                    await TryApplyLegacyMonsterTeamExperienceAsync(
                            context,
                            movedSession,
                            target.Monster,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
                {
                    var damagedMonster = target.Monster with { Hitpoints = result.RemainingHitpoints };
                    foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                    {
                        await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                            .ConfigureAwait(false);
                    }
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
            var physicalDamage = wildPhoenixDefended
                ? new LegacyPlayerPlayerPhysicalSpellDamageResult(1U, MsgAttackEffect.None)
                : CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
                    movedSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData,
                    damageDivisor: 1);
            var damage = MulDivToUInt32(
                CalculateLegacyAirKickSoulDamage(
                    physicalDamage.Damage,
                    playerSpell.UseSpellSoul),
                13,
                5);
            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                context,
                activeTarget,
                movedSession.Character,
                damage,
                cancellationToken)
            .ConfigureAwait(false);
            if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
            {
                movedSession = currentReceiveAttacker;
                context = context with { Session = movedSession };
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
                movedSession = result.UpdatedAttackerSession;
                context = context with { Session = movedSession };
            }

            context = context with { Session = movedSession };

            (movedSession, result) = await TrySendLegacyReceiveAttackPlayerSpeffectAttributesAsync(
                    context,
                    movedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = movedSession };

            (movedSession, _) = await TrySendLegacyPlayerReceiveNinjaSageModeAsync(
                    context,
                    movedSession,
                    context.WorldData,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = movedSession };

            playerResults.Add(result);
            await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                    context,
                    movedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    movedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                damage,
                Hit: true,
                physicalDamage.Effect));
        }

        await TryApplyLegacyPlayerMagicChillingSnowAsync(
                context,
                movedSession,
                playerResults,
                cancellationToken)
            .ConfigureAwait(false);

        if (context.SobNpcStates is { } sobNpcStates)
        {
            foreach (var target in sobNpcTargets)
            {
                var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                    movedSession,
                    spell,
                    context.WorldData,
                    damageMultiplier: 1);
                damage = CalculateLegacyAirKickSoulDamage(damage, playerSpell.UseSpellSoul);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(
                    target.SobNpc,
                    context.WorldData?.StaticStatues);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                if (result.AlreadyDead)
                {
                    continue;
                }

                var progress = checked((uint)Math.Min(
                    CalculateLegacySobNpcExperienceDamage(target.SobNpc, result),
                    uint.MaxValue));
                progressDamage = AccumulateLegacyAirKickSobNpcProgress(progressDamage, progress);
                sobNpcResults.Add((target.SobNpc, result, damage, progress));
                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                    damage,
                    Hit: damage > 0,
                    MsgAttackEffect.None));

                if (result.Killed && LegacySobNpcEventRules.IsTrainingSob(target.SobNpc))
                {
                    await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                            context,
                            movedSession,
                            target.SobNpc,
                            sobNpcStates.GetVisible(target.SobNpc),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (!LegacySobNpcEventRules.IsTrainingSob(target.SobNpc))
                {
                    await LogLegacySobNpcEventSideEffectGapAsync(context, target.SobNpc, result, spell)
                        .ConfigureAwait(false);
                }
            }
        }

        movedSession = await ApplyLegacyAirKickIncreaseExperienceAsync(
                context,
                movedSession,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = movedSession };
        movedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                movedSession,
                ClampLegacyProficiencyRawExperience(progressDamage),
                cancellationToken,
                activity: "AirKick magic weapon proficiency experience")
            .ConfigureAwait(false);
        context = context with { Session = movedSession };
        movedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                movedSession,
                playerSpell,
                spell,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = movedSession };

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            landing.X,
            landing.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets,
            nextSpell: LegacyAirKickNextSpellId);
        await SendPlayerSpellAnimationToVisibleAsync(context, movedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        var monsterProgress = monsterResults.Aggregate(0UL, (total, result) => total + result.Progress);
        var sobNpcProgress = sobNpcResults.Aggregate(0UL, (total, result) => total + result.Progress);
        var playerAppliedDamage = playerResults.Aggregate(0UL, (total, result) => total + result.DamageApplied);
        var totalAppliedDamage =
            monsterResults.Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            playerAppliedDamage +
            sobNpcResults.Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy AirKick magic " +
                $"{FormatSession(context with { Session = movedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} targetX={query.X} targetY={query.Y} pathCells={path.Count} " +
                $"pathDistance={pathDistance} landingX={landing.X} landingY={landing.Y} " +
                $"pathHitRange={LegacyAirKickPathHitRange} nextSpell={LegacyAirKickNextSpellId} " +
                $"targets={animationTargets.Count} monsterTargets={monsterResults.Count} " +
                $"playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"monsterProgress={monsterProgress} sobNpcProgress={sobNpcProgress} " +
                $"sourceProgress={progressDamage} playerAppliedDamage={playerAppliedDamage} " +
                $"totalAppliedDamage={totalAppliedDamage}; source=AirKick.cs spell 12320.")
            .ConfigureAwait(false);
        return true;
    }

    private static IReadOnlyList<LegacyMoveLineCoordinate> SelectLegacyAirKickPath(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        int pathDistance)
    {
        var isValidLocation = CreateLegacyMoveLineLocationValidator(context, session);
        return LegacyMoveLineGeometry.SelectCoordinates(
                CheckedUInt16(session.Character.X, nameof(session.Character.X)),
                CheckedUInt16(session.Character.Y, nameof(session.Character.Y)),
                query.X,
                query.Y,
                isValidLocation,
                pathDistance)
            .Coordinates;
    }

    private static WorldSession MoveLegacyAirKickCasterToLanding(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMoveLineCoordinate landing)
    {
        if (session.Character.X == landing.X &&
            session.Character.Y == landing.Y)
        {
            return session;
        }

        var movedSession = session with
        {
            Character = session.Character with
            {
                X = landing.X,
                Y = landing.Y,
            },
        };
        context.Sessions?.Set(context.Connection, movedSession);
        return movedSession;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyAirKickMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        IReadOnlyList<LegacyMoveLineCoordinate> path)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(query.X, query.Y, candidate.Monster.X, candidate.Monster.Y) <= spell.Range &&
                IsWithinLegacyAirKickPath(path, candidate.Monster.X, candidate.Monster.Y))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyAirKickPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        IReadOnlyList<LegacyMoveLineCoordinate> path)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(query.X, query.Y, character.X, character.Y) <= spell.Range &&
                IsWithinLegacyAirKickPath(path, character.X, character.Y))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyAirKickSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        IReadOnlyList<LegacyMoveLineCoordinate> path)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(query.X, query.Y, candidate.SobNpc.X, candidate.SobNpc.Y) <= spell.Range &&
                IsWithinLegacyAirKickPath(path, candidate.SobNpc.X, candidate.SobNpc.Y))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static bool IsWithinLegacyAirKickPath(
        IReadOnlyList<LegacyMoveLineCoordinate> path,
        long targetX,
        long targetY)
    {
        ArgumentNullException.ThrowIfNull(path);
        foreach (var coordinate in path)
        {
            if (LegacyGetDistance(coordinate.X, coordinate.Y, targetX, targetY) <= LegacyAirKickPathHitRange)
            {
                return true;
            }
        }

        return false;
    }

    private static uint CalculateLegacyAirKickMonsterProgressDamage(
        WorldSession session,
        LegacyPlayerMonsterMagicTarget target,
        uint requestedDamage)
    {
        if (requestedDamage == 0 ||
            LegacyMonsterDamageRules.IsLegacyGuardFamily(target.MonsterFamily))
        {
            return 0;
        }

        var adjustedDamage = AdjustLegacyMonsterExperience(
            checked((int)Math.Min(requestedDamage, target.Monster.MaxLife)),
            CheckedInt32(session.Character.Level, nameof(session.Character.Level)),
            target.Monster.Level);
        return checked((uint)Math.Max(0, adjustedDamage));
    }

    private static uint AccumulateLegacyAirKickSobNpcProgress(uint current, uint sobNpcProgress)
    {
        // Preserve the source expression: num2 += (num2 += Npc.Execute(...)).
        return unchecked(current + current + sobNpcProgress);
    }

    private static async ValueTask<WorldSession> ApplyLegacyAirKickIncreaseExperienceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint progressDamage,
        CancellationToken cancellationToken)
    {
        if (progressDamage == 0)
        {
            return session;
        }

        var experienceResult = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateLegacyExperienceGainInputs(context, session, progressDamage));
        var updatedSession = ApplyLegacyExperienceSideEffects(
            context,
            context.Connection,
            session,
            experienceResult);
        if (experienceResult.AppliedExperienceGain == 0)
        {
            return updatedSession;
        }

        return await ApplyLegacyFinalExperienceGainAsync(
                context,
                context.Connection,
                updatedSession,
                experienceResult.AppliedExperienceGain,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static uint CalculateLegacyAirKickSoulDamage(uint damage, byte useSpellSoul)
    {
        // Source Base.CalculateSoul returns the physical damage unchanged for this runtime slice.
        _ = useSpellSoul;
        return damage;
    }
}
