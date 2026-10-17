using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerPirateSpecialMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort is not (
            LegacyMagicSort.ScurvyBomb or
            LegacyMagicSort.CannonBarrage or
            LegacyMagicSort.BombPirte or
            LegacyMagicSort.PirateXpSkill))
        {
            return false;
        }

        var skillName = GetLegacyPirateBombSkillName(spell.Sort);
        if (spell.Sort == LegacyMagicSort.PirateXpSkill)
        {
            var pirateXpResourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                    context,
                    session,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
            if (pirateXpResourceSession is null)
            {
                return true;
            }

            await HandleLegacyPlayerPirateXpSkillDamageAsync(
                    context,
                    pirateXpResourceSession,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (context.MonsterStates is not { } monsterStates)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedMagic(
                        spell.Id,
                        sort: (int)spell.Sort,
                        skillName: skillName,
                        targetKind: "monster",
                        resource: MonsterRuntimeStateResource,
                        reason:
                            $"{skillName} monster runtime state is not loaded, so the legacy pirate damage branch cannot run."))
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

        if (spell.Sort == LegacyMagicSort.CannonBarrage)
        {
            await HandleLegacyPlayerCannonBarrageDamageAsync(
                    context,
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    monsterStates,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var targets = SelectLegacyPirateBombMonsterTargets(context, resourceSession, query.X, query.Y, playerSpell.Level);
        var remainingTargetBudget = Math.Max(0, GetLegacyPirateBombTargetBudget(playerSpell.Level) - targets.Count);
        var supportsPlayerTargets = spell.Sort is LegacyMagicSort.ScurvyBomb or LegacyMagicSort.BombPirte;
        var supportsSobNpcTargets = spell.Sort is LegacyMagicSort.ScurvyBomb or LegacyMagicSort.BombPirte;
        var playerTargets = supportsPlayerTargets && remainingTargetBudget > 0
            ? SelectLegacyPirateBombPlayerTargetsWithRuneAvoidance(
                context,
                resourceSession,
                spell,
                query.X,
                query.Y,
                remainingTargetBudget)
            : [];
        var remainingSobNpcTargetBudget = Math.Max(0, remainingTargetBudget - playerTargets.Count);
        var sobNpcStates = context.SobNpcStates;
        var sobNpcTargets = supportsSobNpcTargets &&
            remainingSobNpcTargetBudget > 0 &&
            sobNpcStates is not null
                ? SelectLegacyPirateBombSobNpcTargets(
                    context,
                    resourceSession,
                    query.X,
                    query.Y,
                    remainingSobNpcTargetBudget)
                : [];
        var pendingSobNpcTargets = supportsSobNpcTargets && sobNpcStates is null
            ? CountLegacyPirateBombPendingSobNpcTargets(context, resourceSession, query.X, query.Y)
            : 0;
        if (pendingSobNpcTargets > 0)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedMagic(
                        spell.Id,
                        sort: (int)spell.Sort,
                        skillName: skillName,
                        targetKind: "sob-npc",
                        resource: SobNpcRuntimeStateResource,
                        reason:
                            $"{skillName} monster and player damage are modeled, but SOB runtime state is not loaded; " +
                            $"visibleSobNpcTargets={pendingSobNpcTargets}."))
                .ConfigureAwait(false);
        }

        var results = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result)>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var killedTrainingSobNpcTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var eventSobNpcSideEffectTargets = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();
        var scurvyStatusTargets = new List<(WorldActiveSession ActiveSession, WorldSession UpdatedSession)>();
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var now = DateTimeOffset.UtcNow;

        foreach (var target in targets)
        {
            if (IsLegacyGuardMonsterFamily(target.Monster, target.MonsterFamily))
            {
                resourceSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                        context,
                        resourceSession,
                        cancellationToken)
                    .ConfigureAwait(false);
                context = context with { Session = resourceSession };
            }

            var requestedDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                resourceSession,
                context.WorldData,
                target.Monster,
                target.MonsterFamily,
                spell);
            var result = ApplyLegacyPlayerMonsterDamage(
                monsterStates,
                target.Monster,
                requestedDamage,
                now,
                resourceSession);
            results.Add((target, result));
            if (result.AlreadyDead)
            {
                continue;
            }

            ushort monsterNewX = 0;
            ushort monsterNewY = 0;
            if (spell.Sort == LegacyMagicSort.BombPirte &&
                TryCalculateLegacyMonsterPushbackPosition(
                    context,
                    target.Monster,
                    ReadAttributeUInt32(resourceSession.Character.Attributes, "Angle", "Facing"),
                    distance: 5,
                    out var pushedX,
                    out var pushedY))
            {
                monsterNewX = pushedX;
                monsterNewY = pushedY;
                if (monsterStates.TryMoveTo(
                        target.Monster,
                        pushedX,
                        pushedY,
                        now,
                        out var moved))
                {
                    monsterNewX = moved.X;
                    monsterNewY = moved.Y;
                }
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                target.Monster.Uid,
                result.RequestedDamage,
                Hit: result.RequestedDamage > 0,
                MsgAttackEffect.None,
                NewX: monsterNewX,
                NewY: monsterNewY));
        }

        foreach (var target in playerTargets)
        {
            var activeTarget = target.ActiveSession;
            var scurvyStatusApplied = false;
            if (spell.Sort == LegacyMagicSort.ScurvyBomb)
            {
                var updatedTargetCharacter = WithCharacterAttribute(
                    WithLegacyStatusFlag(activeTarget.Session.Character, LegacyScurvyBombFlag),
                    "UseStamina",
                    LegacyScurvyBombFatigueValue);
                var updatedTargetSession = activeTarget.Session with
                {
                    Character = updatedTargetCharacter,
                };
                context.Sessions?.Set(activeTarget.Connection, updatedTargetSession);
                activeTarget = activeTarget with { Session = updatedTargetSession };
                scurvyStatusApplied = true;
            }

            (activeTarget, var wildPhoenixDefended) =
                await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                        context,
                        activeTarget,
                        cancellationToken)
                    .ConfigureAwait(false);
            var damage = wildPhoenixDefended
                ? 1U
                : CalculateLegacyPlayerPlayerPhysicalSpellDamage(
                    resourceSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData,
                    damageDivisor: 1);
            if (spell.Sort == LegacyMagicSort.ScurvyBomb)
            {
                damage = MulDivToUInt32(damage, 2, 1);
            }
            else if (spell.Sort == LegacyMagicSort.BombPirte)
            {
                damage /= 2;
            }

            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                context,
                activeTarget,
                resourceSession.Character,
                damage,
                cancellationToken)
            .ConfigureAwait(false);
            if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
            {
                resourceSession = currentReceiveAttacker;
                context = context with { Session = resourceSession };
            }

            if (receiveOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return true;
            }

            if (receiveOutcome.Result is not { } result)
            {
                continue;
            }

            ushort playerNewX = 0;
            ushort playerNewY = 0;
            var playerAnimationBlockedByGuildWar = false;
            if (spell.Sort == LegacyMagicSort.BombPirte)
            {
                if (TryCalculateLegacyPlayerPushbackPosition(
                    context,
                    result.UpdatedSession,
                    ReadAttributeUInt32(resourceSession.Character.Attributes, "Angle", "Facing"),
                    distance: 5,
                    out var pushedX,
                    out var pushedY,
                    out var guildWarEndpointCheck))
                {
                    playerNewX = pushedX;
                    playerNewY = pushedY;
                    var pushedTargetSession = result.UpdatedSession with
                    {
                        Character = WithLegacyPlayerAttackProtection(
                            result.UpdatedSession.Character with
                            {
                                X = pushedX,
                                Y = pushedY,
                            },
                            now,
                            TimeSpan.FromSeconds(2)),
                    };
                    context.Sessions?.Set(activeTarget.Connection, pushedTargetSession);
                    result = result with
                    {
                        ActiveSession = result.ActiveSession with { Session = pushedTargetSession },
                        UpdatedSession = pushedTargetSession,
                    };
                }
                else
                {
                    playerAnimationBlockedByGuildWar =
                        guildWarEndpointCheck.Applies &&
                        !guildWarEndpointCheck.Allowed;
                    if (playerAnimationBlockedByGuildWar)
                    {
                        await SendLegacyGuildWarPullbackAsync(
                                context,
                                activeTarget.Connection,
                                result.UpdatedSession,
                                guildWarEndpointCheck,
                                "BombPirte/GaleBomb",
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }

            playerResults.Add(result);
            if (scurvyStatusApplied)
            {
                scurvyStatusTargets.Add((result.ActiveSession, result.UpdatedSession));
            }

            if (!playerAnimationBlockedByGuildWar)
            {
                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                    result.DamageApplied,
                    Hit: true,
                    MsgAttackEffect.None,
                    NewX: playerNewX,
                    NewY: playerNewY));
            }
        }

        if (sobNpcStates is not null)
        {
            foreach (var target in sobNpcTargets)
            {
                if (spell.Sort == LegacyMagicSort.BombPirte &&
                    resourceSession.Character.MapId is 1038 or 3868)
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer source-backed returned legacy BombPirte/GaleBomb before SOB damage " +
                            $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                            $"map={resourceSession.Character.MapId}; source returns on map 1038/3868 SOB branch.")
                        .ConfigureAwait(false);
                    return true;
                }

                var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                    resourceSession,
                    spell,
                    context.WorldData,
                    damageMultiplier: 1);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
                var result = sobNpcStates.ApplyDamage(
                    target.SobNpc,
                    damage,
                    now,
                    deathBehavior);
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
            CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, resourceSession, animation, cancellationToken)
            .ConfigureAwait(false);

        foreach (var (activeSession, updatedTargetSession) in scurvyStatusTargets)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context,
                    activeSession.Connection,
                    updatedTargetSession,
                    cancellationToken)
                .ConfigureAwait(false);

            var fatigue = new LegacyMsgUpdatePacket(
                CheckedUInt32(updatedTargetSession.Character.Uid, nameof(updatedTargetSession.Character.Uid)),
                [new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.Fatigue,
                    LegacyScurvyBombFatigueValue,
                    Flag: LegacyScurvyBombFatigueFlag,
                    SpellLevel: spell.Level)]);
            await SendFrameToPlayerAndVisiblePeersAsync(
                    context,
                    activeSession.Connection,
                    updatedTargetSession,
                    fatigue.ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var result in playerResults)
        {
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    resourceSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (sobNpcStates is not null)
        {
            foreach (var killed in killedTrainingSobNpcTargets)
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        context,
                        resourceSession,
                        killed.SobNpc,
                        sobNpcStates.GetVisible(killed.SobNpc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var eventTarget in eventSobNpcSideEffectTargets)
            {
                if (eventTarget.Result.Killed &&
                    eventTarget.DeathBehavior == LegacySobNpcDeathBehavior.GateDemolition)
                {
                    await SendLegacyGuildWarGateDemolitionSobNpcUpdateAsync(
                            context,
                            resourceSession,
                            eventTarget.SobNpc,
                            sobNpcStates.GetVisible(eventTarget.SobNpc),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (eventTarget.Result.Killed &&
                    LegacySobNpcEventRules.TryGetGuildWarOwnershipChangeDwParam(eventTarget.SobNpc, out var ownershipDwParam) &&
                    TryGetLegacyGuildWarCaptureGuild(resourceSession, out var captureGuildId, out var captureGuildName))
                {
                    sobNpcStates.ResetHitpointsToMax(eventTarget.SobNpc, captureGuildName, captureGuildId);
                    await SendLegacyGuildWarCapturePillarSobNpcUpdateAsync(
                            context,
                            resourceSession,
                            eventTarget.SobNpc,
                            sobNpcStates.GetVisible(eventTarget.SobNpc),
                            captureGuildId,
                            ownershipDwParam,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                await LogLegacySobNpcEventSideEffectGapAsync(
                        context,
                        eventTarget.SobNpc,
                        eventTarget.Result,
                        spell)
                    .ConfigureAwait(false);
            }
        }

        var updatedSession = resourceSession;
        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
        foreach (var (target, result) in results)
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

        var totalDamage = results
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            sobNpcResults.Aggregate(
                0UL,
                (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                totalDamage,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                $"WorldServer handled legacy {skillName} monster/player/SOB magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} targets={animationTargets.Count} " +
                $"playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} totalDamage={totalDamage}.")
            .ConfigureAwait(false);
        _ = updatedSession;
        return true;
    }

    private static bool TryCalculateLegacyPlayerPushbackPosition(
        WorldPacketHandlerContext context,
        WorldSession targetSession,
        uint angle,
        int distance,
        out ushort x,
        out ushort y,
        out LegacyMoveLineGuildWarEndpointCheck guildWarEndpointCheck)
    {
        x = CheckedUInt16(targetSession.Character.X, nameof(targetSession.Character.X));
        y = CheckedUInt16(targetSession.Character.Y, nameof(targetSession.Character.Y));
        guildWarEndpointCheck = new LegacyMoveLineGuildWarEndpointCheck(
            Applies: false,
            Allowed: true,
            CurrentTerrainMask: 0,
            NextTerrainMask: 0,
            FloorType: 0,
            LeftGateOpen: false,
            RightGateOpen: false,
            X: x,
            Y: y);
        if (distance <= 0)
        {
            return false;
        }

        var (deltaX, deltaY) = GetLegacyWalkDelta(angle % 8);
        if (deltaX == 0 && deltaY == 0)
        {
            return false;
        }

        for (var step = 0; step < distance; step++)
        {
            var nextX = x + deltaX;
            var nextY = y + deltaY;
            if (nextX is < 0 or > ushort.MaxValue ||
                nextY is < 0 or > ushort.MaxValue ||
                !TryValidateStaticTerrainLocation(context, targetSession.Character.MapId, nextX, nextY, out _))
            {
                break;
            }

            x = checked((ushort)nextX);
            y = checked((ushort)nextY);
        }

        guildWarEndpointCheck = CheckLegacyMoveLineGuildWarEndpoint(context, targetSession, x, y);
        return guildWarEndpointCheck.Allowed;
    }

}
