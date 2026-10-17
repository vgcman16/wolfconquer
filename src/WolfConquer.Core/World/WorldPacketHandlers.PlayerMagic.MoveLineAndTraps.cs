using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerMoveLineMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.MoveLine)
        {
            return false;
        }

        if (spell.Id != LegacyMoveLineSpellId)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedMagic(
                        spell.Id,
                        sort: (int)spell.Sort,
                        reason:
                            "MoveLine handling is currently modeled only for spell 11110 line-hit damage."))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy MoveLine magic " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort}; unsupported MoveLine spell id.")
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

        var isValidLocation = CreateLegacyMoveLineLocationValidator(context, resourceSession);
        var guildWarEndpointCheck = new LegacyMoveLineGuildWarEndpointCheck(
            Applies: false,
            Allowed: true,
            CurrentTerrainMask: 0,
            NextTerrainMask: 0,
            FloorType: 0,
            LeftGateOpen: false,
            RightGateOpen: false,
            X: query.X,
            Y: query.Y);
        var line = LegacyMoveLineGeometry.SelectCoordinates(
            CheckedUInt16(resourceSession.Character.X, nameof(resourceSession.Character.X)),
            CheckedUInt16(resourceSession.Character.Y, nameof(resourceSession.Character.Y)),
            query.X,
            query.Y,
            isValidLocation,
            isFinalEndpointAllowed: (x, y) =>
            {
                guildWarEndpointCheck = CheckLegacyMoveLineGuildWarEndpoint(context, resourceSession, x, y);
                return guildWarEndpointCheck.Allowed;
            });
        if (guildWarEndpointCheck.Applies)
        {
            if (!guildWarEndpointCheck.Allowed)
            {
                await SendLegacyMoveLineGuildWarPullbackAsync(
                        context,
                        resourceSession,
                        guildWarEndpointCheck,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (guildWarEndpointCheck.NextTerrainMask != guildWarEndpointCheck.CurrentTerrainMask)
            {
                resourceSession = UpdateLegacyGuildWarTerrainMask(
                    context,
                    resourceSession,
                    guildWarEndpointCheck.NextTerrainMask);
            }
        }

        var hasTideTrap = HasLegacyTideTrapEquipped(resourceSession, context.WorldData);
        var movedSession = hasTideTrap
            ? resourceSession
            : MoveLegacyMoveLineCasterToLanding(context, resourceSession, line.Landing);
        var updatedSession = movedSession;

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
        var blackSpotExpiresAt =
            context.WorldData is not null &&
            LegacyMagicRuntime.TryResolveLearnedSpell(
                resourceSession.PlayerSpells,
                context.WorldData,
                LegacyBlackSpotSpellId,
                out _,
                out var blackSpotSpell)
                ? now + TimeSpan.FromSeconds(blackSpotSpell.Duration)
                : (DateTimeOffset?)null;

        if (context.MonsterStates is { } monsterStates)
        {
            foreach (var target in EnumerateVisibleMonsterTargets(context, resourceSession.Character))
            {
                if (!LegacyMoveLineGeometry.IsWithinHitRadius(
                        line.Coordinates,
                        target.Monster.X,
                        target.Monster.Y))
                {
                    continue;
                }

                var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    resourceSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    damage,
                    now,
                    resourceSession);
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

                if (!result.Killed &&
                    blackSpotExpiresAt is { } expiresAt &&
                    monsterStates.TryMarkBlackSpotUntil(target.Monster, now, expiresAt))
                {
                    var blackSpot = LegacyBlackspotPacket.CreateActive(target.Monster.Uid);
                    await SendFrameToPlayerAndVisiblePeersAsync(
                            context,
                            context.Connection,
                            movedSession,
                            blackSpot.ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        foreach (var target in EnumerateVisiblePlayerMagicTargets(context, resourceSession, spell))
        {
            if (!LegacyMoveLineGeometry.IsWithinHitRadius(
                    line.Coordinates,
                    target.ActiveSession.Session.Character.X,
                    target.ActiveSession.Session.Character.Y))
            {
                continue;
            }

            // Receive modifiers may rewrite the attacker session. Start from the
            // moved/current caster, not the pre-move context snapshot.
            var playerContext = context with { Session = updatedSession };
            var physicalDamage =
                await CalculateLegacyMoveLinePlayerPhysicalDamageAsync(
                        playerContext,
                        updatedSession,
                        target.ActiveSession,
                        spell,
                        cancellationToken)
                    .ConfigureAwait(false);
            // Matching Base.CalculateSoul is identity before MoveLine's 12 / 16.
            var damage = MulDivToUInt32(physicalDamage.DamageResult.Damage, 12, 16);
            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                playerContext,
                physicalDamage.Target,
                updatedSession.Character,
                damage,
                cancellationToken)
            .ConfigureAwait(false);
            if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
            {
                updatedSession = currentReceiveAttacker;
                playerContext = playerContext with { Session = updatedSession };
            }

            if (receiveOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return true;
            }

            if (receiveOutcome.Result is not { } result)
            {
                continue;
            }
            updatedSession = result.UpdatedAttackerSession ?? updatedSession;

            // The source enqueues every accepted player, including ordinary
            // physical damage that becomes zero after the caller's 12 / 16.
            playerResults.Add(result);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                damage,
                Hit: true,
                physicalDamage.DamageResult.Effect));
        }

        var sobNpcStates = context.SobNpcStates;
        if (sobNpcStates is not null)
        {
            foreach (var target in EnumerateVisibleSobNpcMagicTargets(context, resourceSession.Character))
            {
                if (!LegacyMoveLineGeometry.IsWithinHitRadius(
                        line.Coordinates,
                        target.SobNpc.X,
                        target.SobNpc.Y))
                {
                    continue;
                }

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

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(movedSession.Character.Uid, nameof(movedSession.Character.Uid)),
            line.Landing.X,
            line.Landing.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets,
            bombFlag: hasTideTrap ? (byte)1 : (byte)0);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        if (hasTideTrap &&
            animationTargets.Count > 0)
        {
            await TrySendLegacyMoveLineTideTrapAsync(
                    context,
                    updatedSession,
                    line.Landing,
                    (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
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
                    Uid: CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
                    OpponentUid: target.Monster.Uid,
                    X: target.Monster.X,
                    Y: target.Monster.Y,
                    AttackType: MsgAttackType.Death,
                    Damage: 0,
                    KilledMonster: 1,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0));
                await SendMonsterCombatFrameToVisibleAsync(context, resourceSession, target.Monster, death, cancellationToken)
                    .ConfigureAwait(false);
                await TrySendLegacyMonsterDeathDropAsync(
                        context,
                        resourceSession,
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
            else if (firstRetaliationTarget is null)
            {
                firstRetaliationTarget = target;
            }

            updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                    context,
                    updatedSession,
                    target.Monster,
                    target.MonsterFamily,
                    result.DamageApplied,
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

        foreach (var killed in killedTrainingSobNpcTargets)
        {
            if (sobNpcStates is not null)
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        context,
                        resourceSession,
                        killed.SobNpc,
                        sobNpcStates.GetVisible(killed.SobNpc),
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

        var totalExperienceDamage =
            monsterResults.Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            sobNpcResults.Aggregate(
                0UL,
                (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                totalExperienceDamage,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy MoveLine magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} lineCells={line.Coordinates.Count} " +
                $"landingX={line.Landing.X} landingY={line.Landing.Y} " +
                $"targets={animationTargets.Count} totalDamage={totalExperienceDamage}.")
            .ConfigureAwait(false);
        _ = updatedSession;
        return true;
    }

    private static WorldSession MoveLegacyMoveLineCasterToLanding(
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

    private static Func<ushort, ushort, bool> CreateLegacyMoveLineLocationValidator(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        if (session.Character.MapId is < int.MinValue or > int.MaxValue ||
            context.Terrain is not { } terrain ||
            !terrain.TryGetTerrain((int)session.Character.MapId, out _))
        {
            return static (_, _) => true;
        }

        var mapId = (int)session.Character.MapId;
        return (x, y) => terrain.IsWalkable(mapId, x, y);
    }

}
