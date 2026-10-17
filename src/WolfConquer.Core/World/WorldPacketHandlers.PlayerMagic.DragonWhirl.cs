using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyDragonWhirlSpellId = 10_315;
    private const byte LegacyVortexLineWidth = 4;

    private static async ValueTask<bool> HandleLegacyPlayerDragonWhirlMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (GetLegacyVortexRuntimeProfile(spell.Sort) is not { } runtimeProfile)
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

        if (spell.Id != runtimeProfile.SpellId)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedMagic(
                        spell.Id,
                        sort: (int)spell.Sort,
                        reason:
                            $"{runtimeProfile.Name} handling is currently modeled only for spell " +
                            $"{runtimeProfile.SpellId} after the source resource/cooldown gate."))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped legacy {runtimeProfile.Name} magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort}; " +
                    $"unsupported {runtimeProfile.Name} spell id after CanUseSpell.Verified.")
                .ConfigureAwait(false);
            return true;
        }

        var guildWarEndpointCheck = CheckLegacyMoveLineGuildWarEndpoint(context, resourceSession, query.X, query.Y);
        if (guildWarEndpointCheck.Applies)
        {
            if (!guildWarEndpointCheck.Allowed)
            {
                await SendLegacyGuildWarPullbackAsync(
                        context,
                        context.Connection,
                        resourceSession,
                        guildWarEndpointCheck,
                        sourceName: runtimeProfile.Name,
                        cancellationToken)
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

        var castPointDistance = LegacyGetDistance(
            resourceSession.Character.X,
            resourceSession.Character.Y,
            query.X,
            query.Y);
        if (castPointDistance >= spell.Range)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped legacy {runtimeProfile.Name} magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} " +
                    $"castPointDistance={castPointDistance} range={spell.Range}; " +
                    $"source {runtimeProfile.SourceFileName} returns after CanUseSpell.Verified when distance >= Spell.Range.")
                .ConfigureAwait(false);
            return true;
        }

        var lineRange = checked((byte)Math.Min(castPointDistance, byte.MaxValue));
        var updatedSession = resourceSession;
        var monsterStates = context.MonsterStates;
        var targets = monsterStates is not null
            ? SelectLegacyDragonWhirlMonsterTargets(context, updatedSession, query, spell, lineRange)
            : [];
        var playerTargets = SelectLegacyDragonWhirlPlayerTargets(context, updatedSession, query, spell, lineRange);
        var sobNpcTargets = SelectLegacyDragonWhirlSobNpcTargets(context, updatedSession, query, spell, lineRange);

        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var results = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result, uint RequestedDamage)>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var killedTrainingSobNpcTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var eventSobNpcSideEffectTargets = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();
        var now = DateTimeOffset.UtcNow;
        var hasAssassinRune = HasLegacyAssassinAutoHuntRune(updatedSession, context.WorldData);
        var onAutoHunt = ReadAttributeBool(updatedSession.Character.Attributes, RuntimeAutoHuntAttribute);

        foreach (var target in targets)
        {
            var requestedDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                updatedSession,
                context.WorldData,
                target.Monster,
                target.MonsterFamily,
                spell);
            var kylinResult = ApplyLegacyKylinSigilPhysicalSpellDamage(
                context,
                updatedSession,
                spell.Id,
                requestedDamage);
            updatedSession = kylinResult.Session;
            requestedDamage = kylinResult.Damage;
            requestedDamage = LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceAssassinAutoHuntRuneBonus(
                requestedDamage,
                hasAssassinRune,
                onAutoHunt);
            var result = ApplyLegacyPlayerMonsterDamage(
                monsterStates!,
                target.Monster,
                requestedDamage,
                now,
                updatedSession);
            results.Add((target, result, requestedDamage));
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
            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    context,
                    activeTarget,
                    updatedSession.Character,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false);
            if (receiveOutcome.AttackerSession is { } receiveAttacker)
            {
                updatedSession = receiveAttacker;
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
                    updatedSession,
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

        var totalDamage = results
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.RequestedDamage);
        var totalSobNpcExperienceDamage = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        var proficiencyExperienceDamage = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + result.Result.DamageApplied);
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                totalDamage + totalSobNpcExperienceDamage,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(proficiencyExperienceDamage),
                cancellationToken,
                activity: $"{runtimeProfile.Name} SOB magic weapon proficiency experience")
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        await SendLegacyDragonWhirlInteractionMarkerAsync(
                context,
                updatedSession,
                query,
                spell.Id,
                cancellationToken)
            .ConfigureAwait(false);

        await SendLegacyPhysicalSpellsAnimationAsync(
                context,
                updatedSession,
                query,
                playerSpell,
                spell,
                animationTargets,
                spellEffect: 0,
                cancellationToken)
            .ConfigureAwait(false);

        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
        foreach (var (target, result, requestedDamage) in results)
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

            updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                    context,
                    updatedSession,
                    target.Monster,
                    target.MonsterFamily,
                    requestedDamage,
                    cancellationToken)
                .ConfigureAwait(false);
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

        foreach (var (sobNpc, _) in killedTrainingSobNpcTargets)
        {
            if (context.SobNpcStates is { } currentSobNpcStates)
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        context,
                        updatedSession,
                        sobNpc,
                        currentSobNpcStates.GetVisible(sobNpc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        foreach (var (sobNpc, result, _) in eventSobNpcSideEffectTargets)
        {
            await LogLegacySobNpcEventSideEffectGapAsync(context, sobNpc, result, spell)
                .ConfigureAwait(false);
        }

        var totalAppliedDamage = results
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);

        var landing = CalculateLegacyDragonWhirlLanding(updatedSession, query, lineRange);
        updatedSession = await ApplyLegacyDragonWhirlMovementAsync(
                context,
                updatedSession,
                query,
                landing,
                spell.Id,
                runtimeProfile,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                $"WorldServer handled legacy {runtimeProfile.Name} magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} lineRange={lineRange} lineWidth={LegacyVortexLineWidth} " +
                $"landingX={landing.X} landingY={landing.Y} targets={animationTargets.Count} " +
                $"monsterTargets={results.Count} playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"totalDamage={totalDamage} totalSobNpcExperienceDamage={totalSobNpcExperienceDamage} " +
                $"totalAppliedDamage={totalAppliedDamage}; source={runtimeProfile.SourceFileName} bounded runtime slice.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask SendLegacyDragonWhirlInteractionMarkerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        ushort activeSpellId,
        CancellationToken cancellationToken)
    {
        var marker = new MsgAttackPacket(new MsgAttackQuery(
            Uid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            OpponentUid: query.OpponentUid,
            X: query.X,
            Y: query.Y,
            AttackType: MsgAttackType.InMoveSpell,
            Damage: 0,
            KilledMonster: activeSpellId,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0));
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                context.Connection,
                session,
                marker.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static LegacyMoveLineCoordinate CalculateLegacyDragonWhirlLanding(
        WorldSession session,
        MsgAttackQuery query,
        byte lineRange)
    {
        return LegacyMoveLineGeometry.SelectCoordinates(
                CheckedUInt16(session.Character.X, nameof(session.Character.X)),
                CheckedUInt16(session.Character.Y, nameof(session.Character.Y)),
                query.X,
                query.Y,
                distance: lineRange)
            .Landing;
    }

    private static async ValueTask<WorldSession> ApplyLegacyDragonWhirlMovementAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMoveLineCoordinate landing,
        ushort activeSpellId,
        LegacyVortexRuntimeProfile runtimeProfile,
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
            KilledMonster: activeSpellId,
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

        await SendLegacyDragonWhirlViewRoleRefreshAsync(
                context with { Session = movedSession },
                movedSession,
                runtimeProfile,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                $"WorldServer applied legacy {runtimeProfile.Name} movement " +
                $"{FormatSession(context with { Session = movedSession })} source={runtimeProfile.SourceFileName} " +
                $"fromX={session.Character.X} fromY={session.Character.Y} landingX={landing.X} landingY={landing.Y}.")
            .ConfigureAwait(false);
        return movedSession;
    }

    private static async ValueTask SendLegacyDragonWhirlViewRoleRefreshAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyVortexRuntimeProfile runtimeProfile,
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
                    $"WorldServer refreshed LegacyPlayerSpawn {runtimeProfile.Name} view-role " +
                    $"{FormatSession(context)} map={character.MapId} toCasterCount={toCasterCount} toPeerCount={toPeerCount}; " +
                    $"source={runtimeProfile.SourceFileName} Player.View.Role(clear:false, InteractionCreate).")
                .ConfigureAwait(false);
        }
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyDragonWhirlMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        byte lineRange)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y) >= spell.Range ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    lineRange,
                    LegacyVortexLineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyDragonWhirlPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        byte lineRange)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.ActiveSession.Session.Character.X,
                    candidate.ActiveSession.Session.Character.Y) >= spell.Range ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.ActiveSession.Session.Character.X,
                    candidate.ActiveSession.Session.Character.Y,
                    lineRange,
                    LegacyVortexLineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyDragonWhirlSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        byte lineRange)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y) >= spell.Range ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    lineRange,
                    LegacyVortexLineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static LegacyVortexRuntimeProfile? GetLegacyVortexRuntimeProfile(LegacyMagicSort sort)
    {
        return sort switch
        {
            LegacyMagicSort.DragonWhirl => new LegacyVortexRuntimeProfile(
                LegacyDragonWhirlSpellId,
                "DragonWhirl",
                "DragonWhirl.cs"),
            LegacyMagicSort.ChargingVortex => new LegacyVortexRuntimeProfile(
                LegacyChargingVortexSpellId,
                "ChargingVortex",
                "ChargingVortex.cs"),
            _ => null,
        };
    }

    private readonly record struct LegacyVortexRuntimeProfile(
        ushort SpellId,
        string Name,
        string SourceFileName);
}
