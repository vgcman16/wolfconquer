using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const byte LegacyDragonCyclonePathHitRadius = 2;
    private const byte LegacyDragonCyclonePlayerAndSobRadius = 6;

    private static async ValueTask<bool> TryHandleLegacyDragonCycloneActiveMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.DragonCyclone ||
            spell.Id != LegacyDragonCycloneSpellId)
        {
            return false;
        }

        if (!HasLegacyStatusFlag(resourceSession.Character, LegacyDragonCycloneFlag))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy DragonCyclone active branch " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort}; " +
                    "XPList and DragonCyclone status flags are not active, matching source no-op.")
                .ConfigureAwait(false);
            return true;
        }

        var castPointDistance = LegacyGetDistance(
            resourceSession.Character.X,
            resourceSession.Character.Y,
            query.X,
            query.Y);
        var pathDistance = (int)Math.Min(spell.Range, castPointDistance);
        var path = SelectLegacyDragonCyclonePath(context, resourceSession, query, pathDistance);
        if (path.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy DragonCyclone active branch " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                    $"targetX={query.X} targetY={query.Y} pathDistance={pathDistance}; " +
                    "source DragonCyclone.cs returns when CheckBladeTeampsCoords has no coordinates.")
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
                        sourceName: "DragonCyclone",
                        cancellationToken)
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer stopped legacy DragonCyclone active branch after resource prep " +
                        $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                        $"landingX={landing.X} landingY={landing.Y}; source=DragonCyclone.cs CheckGuildWar rejection.")
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

        var movedSession = await ApplyLegacyDragonCycloneMovementAsync(
                context,
                resourceSession,
                query,
                landing,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = movedSession };

        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyDragonCycloneMonsterTargets(context, movedSession, query, spell, path)
            : [];
        var playerTargets = SelectLegacyDragonCyclonePlayerTargets(context, movedSession, query, spell, path);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyDragonCycloneSobNpcTargets(context, movedSession, query, path)
            : [];

        movedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                movedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = movedSession };

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(
            LegacyPlayerMonsterMagicTarget Target,
            MonsterDamageResult Result,
            uint RequestedDamage,
            uint Progress)>();
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
            foreach (var target in monsterTargets)
            {
                var requestedDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    movedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    requestedDamage,
                    now,
                    movedSession);
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
                    movedSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData,
                    damageDivisor: 1);
            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    context,
                    activeTarget,
                    movedSession.Character,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false);
            if (receiveOutcome.AttackerSession is { } receiveAttacker)
            {
                movedSession = receiveAttacker;
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

        if (context.SobNpcStates is { } sobNpcStates)
        {
            foreach (var target in sobNpcTargets)
            {
                var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                    movedSession,
                    spell,
                    context.WorldData,
                    damageMultiplier: 1);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
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
            CheckedUInt32(movedSession.Character.Uid, nameof(movedSession.Character.Uid)),
            landing.X,
            landing.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, movedSession, animation, cancellationToken)
            .ConfigureAwait(false);

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
                    Uid: CheckedUInt32(movedSession.Character.Uid, nameof(movedSession.Character.Uid)),
                    OpponentUid: target.Monster.Uid,
                    X: target.Monster.X,
                    Y: target.Monster.Y,
                    AttackType: MsgAttackType.Death,
                    Damage: 0,
                    KilledMonster: 1,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0));
                await SendMonsterCombatFrameToVisibleAsync(context, movedSession, target.Monster, death, cancellationToken)
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

            movedSession = await TryApplyLegacyMonsterExperienceAsync(
                    context,
                    movedSession,
                    target.Monster,
                    target.MonsterFamily,
                    result.DamageApplied,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = movedSession };
        }

        if (firstRetaliationTarget is { } retaliationTarget)
        {
            await TrySendLegacyMonsterRetaliationAsync(
                    context,
                    movedSession,
                    retaliationTarget.Monster,
                    retaliationTarget.MonsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var result in playerResults)
        {
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    movedSession,
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
                        movedSession,
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
        var sobNpcProgress = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        var progressDamage = monsterProgress + sobNpcProgress;
        movedSession = await ApplyLegacyDragonCycloneIncreaseExperienceAsync(
                context,
                movedSession,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        movedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                movedSession,
                playerSpell,
                spell,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        movedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                movedSession,
                ClampLegacyProficiencyRawExperience(progressDamage),
                cancellationToken,
                activity: "DragonCyclone active magic weapon proficiency experience")
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
                "WorldServer handled legacy DragonCyclone active branch " +
                $"{FormatSession(context with { Session = movedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} targetX={query.X} targetY={query.Y} pathCells={path.Count} " +
                $"pathDistance={pathDistance} landingX={landing.X} landingY={landing.Y} " +
                $"targets={animationTargets.Count} monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} " +
                $"sobNpcTargets={sobNpcResults.Count} monsterProgress={monsterProgress} sobNpcProgress={sobNpcProgress} " +
                $"totalAppliedDamage={totalAppliedDamage}; source=DragonCyclone.cs active non-XPList branch.")
            .ConfigureAwait(false);
        return true;
    }

    private static IReadOnlyList<LegacyMoveLineCoordinate> SelectLegacyDragonCyclonePath(
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

    private static async ValueTask<WorldSession> ApplyLegacyDragonCycloneMovementAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMoveLineCoordinate landing,
        CancellationToken cancellationToken)
    {
        if (session.Character.X == landing.X &&
            session.Character.Y == landing.Y)
        {
            return session;
        }

        var movement = new MsgAttackPacket(new MsgAttackQuery(
            Uid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            OpponentUid: query.OpponentUid,
            X: landing.X,
            Y: landing.Y,
            AttackType: MsgAttackType.InMoveSpell,
            Damage: 0,
            KilledMonster: LegacyDragonCycloneSpellId,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0));
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                context.Connection,
                session,
                movement.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var movedSession = session with
        {
            Character = session.Character with
            {
                X = landing.X,
                Y = landing.Y,
            },
        };
        context.Sessions?.Set(context.Connection, movedSession);

        await SendLegacyDragonCycloneViewRoleRefreshAsync(
                context with { Session = movedSession },
                movedSession,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy DragonCyclone movement " +
                $"{FormatSession(context with { Session = movedSession })} source=DragonCyclone.cs/user.Shift " +
                $"fromX={session.Character.X} fromY={session.Character.Y} landingX={landing.X} landingY={landing.Y}.")
            .ConfigureAwait(false);
        return movedSession;
    }

    private static async ValueTask SendLegacyDragonCycloneViewRoleRefreshAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            return;
        }

        var character = session.Character;
        var casterSpawn = CreatePlayerSpawnPacket(session, context.WorldData).ToFrameBytes();
        var toCasterCount = 0;
        var toPeerCount = 0;
        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, context.Connection) ||
                active.Session.Character.Uid == character.Uid)
            {
                continue;
            }

            if (CanSeePlayer(character, active.Session.Character))
            {
                var peerSpawn = CreatePlayerSpawnPacket(active.Session, context.WorldData).ToFrameBytes();
                await context.Connection.SendAsync(peerSpawn, cancellationToken).ConfigureAwait(false);
                toCasterCount++;
            }

            if (CanSeePlayer(active.Session.Character, character))
            {
                await active.Connection.SendAsync(casterSpawn, cancellationToken).ConfigureAwait(false);
                toPeerCount++;
            }
        }

        if (toCasterCount > 0 || toPeerCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer refreshed LegacyPlayerSpawn DragonCyclone view-role " +
                    $"{FormatSession(context)} map={character.MapId} toCasterCount={toCasterCount} toPeerCount={toPeerCount}; " +
                    "source=DragonCyclone.cs user.Shift after CheckGuildWar.")
                .ConfigureAwait(false);
        }
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyDragonCycloneMonsterTargets(
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
                LegacyMoveLineGeometry.IsWithinHitRadius(
                    path,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    LegacyDragonCyclonePathHitRadius))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyDragonCyclonePlayerTargets(
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
            if (LegacyGetDistance(query.X, query.Y, character.X, character.Y) <= LegacyDragonCyclonePlayerAndSobRadius &&
                LegacyMoveLineGeometry.IsWithinHitRadius(
                    path,
                    character.X,
                    character.Y,
                    LegacyDragonCyclonePathHitRadius))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyDragonCycloneSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        IReadOnlyList<LegacyMoveLineCoordinate> path)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(query.X, query.Y, candidate.SobNpc.X, candidate.SobNpc.Y) <= LegacyDragonCyclonePlayerAndSobRadius &&
                LegacyMoveLineGeometry.IsWithinHitRadius(
                    path,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    LegacyDragonCyclonePathHitRadius))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static async ValueTask<WorldSession> ApplyLegacyDragonCycloneIncreaseExperienceAsync(
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
                        "WorldServer applied legacy DragonCyclone experience side effects " +
                        $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                        $"applied=0 oblivion={experienceResult.OblivionExperienceGain} " +
                        $"huntingBlessing={experienceResult.HuntingBlessingGain} suppression={experienceResult.SuppressionReason}; " +
                        "source=DragonCyclone.cs IncreaseExperience.Up.")
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
                "WorldServer sent legacy DragonCyclone experience " +
                $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                $"gain={experienceResult.AppliedExperienceGain} oblivion={experienceResult.OblivionExperienceGain} " +
                $"huntingBlessing={experienceResult.HuntingBlessingGain} experience={updatedSession.Character.Experience} " +
                $"level={updatedSession.Character.Level}; source=DragonCyclone.cs IncreaseExperience.Up.")
            .ConfigureAwait(false);
        return updatedSession;
    }
}
