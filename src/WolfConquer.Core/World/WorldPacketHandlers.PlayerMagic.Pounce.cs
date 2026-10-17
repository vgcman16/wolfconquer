using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyPounceSpellId = 12_770;
    private const byte LegacyPounceRadius = 5;

    private static async ValueTask<bool> HandleLegacyPlayerPounceMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (!LegacyMagicSortCapabilities.IsSupportedPounceRuntimeSpell(spell.Sort, spell.Id))
        {
            return false;
        }

        var attackerUid = session.Character.Uid;

        // Pounce.Execute evaluates CanUseSpell.Verified before checking the active shield.
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

        context = context with { Session = resourceSession };
        if (!TryGetLegacyPounceActiveShield(resourceSession, out var shieldItemId))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy Pounce after resource preparation " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort}; " +
                    "activeLeftShield=false source=matching DecompiledSource Pounce.cs.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = ShiftLegacyPounceCaster(context, resourceSession, query.X, query.Y);
        context = context with { Session = updatedSession };
        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyPounceMonsterTargets(context, updatedSession)
            : [];
        var playerTargets = SelectLegacyPouncePlayerTargets(context, updatedSession);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyPounceSobNpcTargets(context, updatedSession)
            : [];

        foreach (var target in monsterTargets)
        {
            if (!IsLegacyGuardMonsterFamily(target.Monster, target.MonsterFamily))
            {
                continue;
            }

            updatedSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                    context,
                    updatedSession,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };
            break;
        }

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(
            LegacyPlayerMonsterMagicTarget Target,
            MonsterDamageResult Result,
            uint RequestedDamage,
            ulong Progress)>();
        var playerResults = new List<(LegacyPlayerMagicDamageResult Result, uint RequestedDamage)>();
        var sobNpcResults = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            uint RequestedDamage,
            ulong Progress)>();
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
                    updatedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                requestedDamage = CalculateLegacyPounceSoulDamage(
                    requestedDamage,
                    playerSpell.UseSpellSoul);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    requestedDamage,
                    now,
                    updatedSession);
                var progress = result.AlreadyDead
                    ? 0UL
                    : CalculateLegacyPounceMonsterProgressDamage(
                        updatedSession,
                        target,
                        requestedDamage);
                monsterResults.Add((target, result, requestedDamage, progress));
                if (result.AlreadyDead)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    requestedDamage,
                    Hit: true,
                    MsgAttackEffect.None));
            }
        }

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

        foreach (var target in playerTargets)
        {
            var activeTarget = target.ActiveSession;
            if (context.Sessions?.TryGet(activeTarget.Connection, out var refreshedTarget) != true ||
                refreshedTarget is null)
            {
                continue;
            }

            activeTarget = activeTarget with { Session = refreshedTarget };
            if (!CanSeePlayer(updatedSession.Character, activeTarget.Session.Character) ||
                ReadCharacterDynamicId(updatedSession.Character) != ReadCharacterDynamicId(activeTarget.Session.Character) ||
                LegacyGetDistance(
                    updatedSession.Character.X,
                    updatedSession.Character.Y,
                    activeTarget.Session.Character.X,
                    activeTarget.Session.Character.Y) > LegacyPounceRadius ||
                activeTarget.Session.Character.MinHitPoints <= 0 ||
                HasLegacyStatusFlag(activeTarget.Session.Character, LegacyDeadFlag) ||
                HasLegacyStatusFlag(activeTarget.Session.Character, LegacyGhostFlag))
            {
                continue;
            }

            if (!CanLegacyPlayerMagicDamageTarget(
                    context,
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    out _,
                    out var failureEffect))
            {
                if (failureEffect == LegacyPlayerMagicGateFailureEffect.HellVortexCounter)
                {
                    await TrySendLegacyPlayerHellVortexCounterAsync(
                            context,
                            updatedSession,
                            activeTarget,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (context.Sessions?.TryGet(context.Connection, out var refreshedCaster) == true &&
                        refreshedCaster is not null)
                    {
                        updatedSession = refreshedCaster;
                        context = context with { Session = updatedSession };
                    }
                }

                continue;
            }

            var targetUid = activeTarget.Session.Character.Uid;
            updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                    context,
                    updatedSession,
                    [new LegacyPlayerMagicTarget(activeTarget)],
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };

            var originalAttackerSession = updatedSession;
            var statusDefended =
                HasLegacyStatusFlag(activeTarget.Session.Character, LegacyShurikenVortexFlag) ||
                HasLegacyStatusFlag(activeTarget.Session.Character, LegacyManiacDanceFlag);
            var wildPhoenixDefended = false;
            if (!statusDefended)
            {
                (activeTarget, wildPhoenixDefended) =
                    await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                            context,
                            activeTarget,
                            cancellationToken)
                        .ConfigureAwait(false);
            }

            var physicalDamage = statusDefended || wildPhoenixDefended
                ? new LegacyPlayerPlayerPhysicalSpellDamageResult(1U, MsgAttackEffect.None)
                : CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData,
                    damageDivisor: 1);
            var requestedDamage = physicalDamage.Damage;
            if (!statusDefended && !wildPhoenixDefended)
            {
                // Physical.OnPlayer halves DefensiveStance damage without skipping BackDmg.
                physicalDamage = ApplyLegacyPlayerPlayerPhysicalDefensiveStance(
                    activeTarget.Session,
                    physicalDamage);
                var receiveAttacker = updatedSession.Character;
                var backDamageEnteredAlive = receiveAttacker.MinHitPoints > 0;
                (context, activeTarget, receiveAttacker, var backDamageAbortReason) =
                    await TryApplyLegacyPlayerReceiveHeavensWonderAsync(
                            context,
                            activeTarget,
                            receiveAttacker,
                            cancellationToken)
                        .ConfigureAwait(false);
                updatedSession = context.Session ?? updatedSession;
                context = context with { Session = updatedSession };
                if (backDamageAbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
                {
                    return true;
                }
                if (backDamageAbortReason == LegacyPlayerMagicReceiveAbortReason.TargetLost)
                {
                    continue;
                }
                (context, activeTarget, receiveAttacker, backDamageAbortReason) =
                    await TryApplyLegacyPlayerReceiveRevengeTailAsync(
                            context,
                            activeTarget,
                            receiveAttacker,
                            physicalDamage.Damage,
                            cancellationToken,
                            backDamageEnteredAlive: backDamageEnteredAlive)
                        .ConfigureAwait(false);
                updatedSession = context.Session ?? updatedSession;
                context = context with { Session = updatedSession };
                if (backDamageAbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
                {
                    return true;
                }
                if (backDamageAbortReason == LegacyPlayerMagicReceiveAbortReason.TargetLost)
                {
                    continue;
                }
                updatedSession = context.Session ?? updatedSession;
                context = context with { Session = updatedSession };

                // Physical applies MudWall after BackDmg, before soul and the opening receive hooks.
                requestedDamage = ApplyLegacyPlayerReceiveMudWall(
                    activeTarget.Session,
                    context.WorldData,
                    physicalDamage.Damage);
            }

            requestedDamage = CalculateLegacyPounceSoulDamage(
                requestedDamage,
                playerSpell.UseSpellSoul);

            // Carry pre-hit state through opening ReceiveAttack.Player without consuming modifiers or HP.
            var openingReceiveResult = new LegacyPlayerMagicDamageResult(
                activeTarget,
                activeTarget.Session,
                CheckedUInt32(
                    activeTarget.Session.Character.MinHitPoints,
                    nameof(activeTarget.Session.Character.MinHitPoints)),
                DamageApplied: 0,
                Killed: false);
            var combatGearOutcome = await ExecuteLegacyPlayerReceiveCombatGearOpeningAsync(
                    context,
                    new LegacyPlayerReceiveCombatGearRequest(
                        updatedSession,
                        activeTarget,
                        attackerUid,
                        targetUid),
                    cancellationToken)
                .ConfigureAwait(false);
            if (combatGearOutcome.AttackerSession is { } currentCombatGearAttacker)
            {
                updatedSession = currentCombatGearAttacker;
                context = context with { Session = updatedSession };
            }

            if (combatGearOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return true;
            }

            if (combatGearOutcome.Target is not { } combatGearTarget)
            {
                continue;
            }

            openingReceiveResult = openingReceiveResult with
            {
                ActiveSession = combatGearTarget,
                UpdatedSession = combatGearTarget.Session,
                UpdatedAttackerSession = updatedSession,
            };
            context = context with { Session = updatedSession };

            (updatedSession, openingReceiveResult) = await TrySendLegacyReceiveAttackPlayerSpeffectAttributesAsync(
                    context,
                    updatedSession,
                    openingReceiveResult,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };

            (updatedSession, _) = await TrySendLegacyPlayerReceiveNinjaSageModeAsync(
                    context,
                    updatedSession,
                    context.WorldData,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };

            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                context,
                openingReceiveResult.ActiveSession with { Session = openingReceiveResult.UpdatedSession },
                updatedSession.Character,
                requestedDamage,
                cancellationToken,
                applyMudWall: false,
                combatGearReceipt: combatGearOutcome.Receipt)
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
            if (result.UpdatedAttackerSession is null &&
                !Equals(updatedSession, originalAttackerSession))
            {
                result = result with { UpdatedAttackerSession = updatedSession };
            }

            if (result.UpdatedAttackerSession is not null)
            {
                updatedSession = result.UpdatedAttackerSession;
                context = context with { Session = updatedSession };
            }

            await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);

            playerResults.Add((result, requestedDamage));
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                result.ReceiveDamage ?? requestedDamage,
                Hit: true,
                physicalDamage.Effect));
        }

        if (context.SobNpcStates is { } sobNpcStates)
        {
            foreach (var target in sobNpcTargets)
            {
                var requestedDamage = CalculateLegacySobNpcPhysicalSpellDamage(
                    updatedSession,
                    spell,
                    context.WorldData,
                    damageMultiplier: 1);
                requestedDamage = CalculateLegacyPounceSoulDamage(
                    requestedDamage,
                    playerSpell.UseSpellSoul);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(
                    target.SobNpc,
                    context.WorldData?.StaticStatues);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, requestedDamage, now, deathBehavior);
                var progress = result.AlreadyDead
                    ? 0UL
                    : CalculateLegacySobNpcExperienceDamage(target.SobNpc, result);
                sobNpcResults.Add((target.SobNpc, result, requestedDamage, progress));
                if (result.AlreadyDead)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                    requestedDamage,
                    Hit: true,
                    MsgAttackEffect.None));

                if (result.DamageApplied == 0)
                {
                    continue;
                }

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

        await TryApplyLegacyPlayerMagicChillingSnowAsync(
                context,
                updatedSession,
                playerResults.Select(result => result.Result).ToList(),
                cancellationToken)
            .ConfigureAwait(false);

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
        var sobNpcProgress = sobNpcResults.Aggregate(0UL, (total, result) => total + result.Progress);
        var progressDamage = monsterProgress + sobNpcProgress;
        updatedSession = await ApplyLegacyPounceIncreaseExperienceAsync(
                context,
                updatedSession,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(progressDamage),
                cancellationToken,
                activity: "Pounce magic weapon proficiency experience")
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var animation = new LegacySpellAnimationPacket(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            AttackedUid: 0,
            query.X,
            query.Y,
            LegacyPounceSpellId,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(
                context,
                updatedSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        var totalAppliedDamage =
            monsterResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            playerResults.Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            sobNpcResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Pounce magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"shieldItemId={shieldItemId} castX={query.X} castY={query.Y} " +
                $"radius={LegacyPounceRadius} inclusiveRadius=true attackStampCadenceMs={spell.CoolDown} " +
                $"coldTimeMs={spell.ColdTime} targets={animationTargets.Count} " +
                $"monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} " +
                $"sobNpcTargets={sobNpcResults.Count} monsterProgress={monsterProgress} " +
                $"sobNpcProgress={sobNpcProgress} totalAppliedDamage={totalAppliedDamage}; " +
                "source=matching DecompiledSource Pounce.cs sort 84 family 12770.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool TryGetLegacyPounceActiveShield(WorldSession session, out uint shieldItemId)
    {
        foreach (var item in LegacyEquipmentSelector.SelectCurrentEquipment(
                     session.PlayerItems,
                     session.AlternateEquipmentActive))
        {
            if (item.Position % 50 != 5 || !ItemType.IsLegacyShield(item.ItemId))
            {
                continue;
            }

            shieldItemId = item.ItemId;
            return true;
        }

        shieldItemId = 0;
        return false;
    }

    private static WorldSession ShiftLegacyPounceCaster(
        WorldPacketHandlerContext context,
        WorldSession session,
        ushort x,
        ushort y)
    {
        if (session.Character.X == x && session.Character.Y == y)
        {
            return session;
        }

        var movedSession = session with
        {
            Character = session.Character with
            {
                X = x,
                Y = y,
            },
        };
        context.Sessions?.Set(context.Connection, movedSession);
        return movedSession;
    }

    private static ulong CalculateLegacyPounceMonsterProgressDamage(
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
        return checked((ulong)Math.Max(0, adjustedDamage));
    }

    private static async ValueTask<WorldSession> ApplyLegacyPounceIncreaseExperienceAsync(
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

    private static uint CalculateLegacyPounceSoulDamage(uint damage, byte useSpellSoul)
    {
        // Matching Base.CalculateSoul returns the incoming physical damage unchanged.
        _ = useSpellSoul;
        return damage;
    }
}
