using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyManiacDanceSpellId = 12_700;
    private const int LegacyManiacDanceRange = 5;
    private const int LegacyManiacDanceBaseAttackSpeedMilliseconds = 800;
    private const int LegacyManiacDanceBaseAttackSpeedReductionMilliseconds = 100;
    private const int LegacyManiacDanceMinimumAttackSpeedMilliseconds = 300;
    private const int LegacyManiacDanceCycloneAttackSpeedReductionMilliseconds = 150;
    private static readonly TimeSpan LegacyManiacDanceDuration = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LegacyManiacDanceRecurringAttackDelay = TimeSpan.FromSeconds(1);
    private const string LegacyManiacDanceSource =
        "matching DecompiledSource AttackHandler/ManiacDance.cs plus MrRayzo/ThreadPool.cs FloorCallback";

    private static async ValueTask<bool> HandleLegacyPlayerManiacDanceMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Id != LegacyManiacDanceSpellId ||
            spell.Sort != LegacyMagicSort.ManiacDance)
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

        if (!HasLegacyStatusFlag(resourceSession.Character, LegacyRuntimeUpdateTicker.XpListFlag))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer source-backed returned legacy ManiacDance magic after resource preparation " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} " +
                    $"sort={(int)spell.Sort}; XPList status flag is not active.")
                .ConfigureAwait(false);
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        var hadManiacDance = HasLegacyStatusFlag(resourceSession.Character, LegacyManiacDanceFlag);
        var (xpListSession, openedManiacDance) = await ApplyLegacyManiacDanceXpListTransitionAsync(
                context with { Session = resourceSession },
                resourceSession,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        var initialDelayMilliseconds = CalculateLegacyManiacDanceInitialAttackDelayMilliseconds(
            xpListSession,
            context.WorldData);
        var firstAttackAt = now.AddMilliseconds(initialDelayMilliseconds);
        xpListSession = xpListSession with
        {
            Character = WithCharacterAttribute(
                xpListSession.Character,
                RuntimeLegacyManiacDanceNextAttackAtUtcAttribute,
                firstAttackAt.ToString("O", CultureInfo.InvariantCulture)),
        };
        context.Sessions?.Set(context.Connection, xpListSession);

        var casterUid = CheckedUInt32(xpListSession.Character.Uid, nameof(xpListSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(casterUid, 0, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(
                context with { Session = xpListSession },
                xpListSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy ManiacDance XPList transition " +
                $"{FormatSession(context with { Session = xpListSession })} spellId={spell.Id} " +
                $"sort={(int)spell.Sort} consumedXpList=True openedManiacDance={openedManiacDance} " +
                $"refreshedManiacDance={hadManiacDance} durationSeconds={(int)LegacyManiacDanceDuration.TotalSeconds} " +
                $"initialAttackDelayMs={initialDelayMilliseconds} firstAttackAtUtc={firstAttackAt:O}; " +
                $"source={LegacyManiacDanceSource}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<(WorldSession Session, bool OpenedManiacDance)>
        ApplyLegacyManiacDanceXpListTransitionAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            DateTimeOffset now,
            CancellationToken cancellationToken)
    {
        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var xpListRemovedSession = session with
        {
            Character = WithoutLegacyStatusFlag(
                session.Character,
                LegacyRuntimeUpdateTicker.XpListFlag),
        };
        context.Sessions?.Set(context.Connection, xpListRemovedSession);
        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(uid, value: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendPlayerStatusFlagVectorToVisibleAsync(
                context with { Session = xpListRemovedSession },
                xpListRemovedSession,
                cancellationToken)
            .ConfigureAwait(false);

        if (ReadAttributeBool(xpListRemovedSession.Character.Attributes, RuntimeAutoHuntAttribute))
        {
            return (xpListRemovedSession, false);
        }

        var xpCircleSession = xpListRemovedSession with
        {
            Character = WithCharacterAttribute(xpListRemovedSession.Character, "XPCount", 0UL),
        };
        context.Sessions?.Set(context.Connection, xpCircleSession);
        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeXPCircleTickUpdate(uid, count: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var currentXpSkillFlag = LegacyXpSkillFlags.FirstOrDefault(
            flag => HasLegacyStatusFlag(xpCircleSession.Character, flag));
        var transitionSession = xpCircleSession;
        if (currentXpSkillFlag != 0 && currentXpSkillFlag != LegacyManiacDanceFlag)
        {
            transitionSession = xpCircleSession with
            {
                Character = WithoutLegacyStatusFlag(xpCircleSession.Character, currentXpSkillFlag),
            };
            context.Sessions?.Set(context.Connection, transitionSession);
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context with { Session = transitionSession },
                    transitionSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var openedSession = transitionSession with
        {
            Character = WithLegacyOpenXpSkill(
                transitionSession.Character,
                LegacyManiacDanceFlag,
                RuntimeLegacyManiacDanceExpiresAtUtcAttribute,
                now,
                LegacyManiacDanceDuration),
        };
        context.Sessions?.Set(context.Connection, openedSession);
        if (currentXpSkillFlag != LegacyManiacDanceFlag)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context with { Session = openedSession },
                    openedSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return (openedSession, true);
    }

    internal static async ValueTask<bool> TrySendDueLegacyManiacDanceAttackAsync(
        WorldPacketHandlerContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions ||
            context.WorldData is not { } worldData ||
            !sessions.TryGet(context.Connection, out var currentSession) ||
            currentSession is null ||
            !HasLegacyStatusFlag(currentSession.Character, LegacyManiacDanceFlag) ||
            !TryReadLegacyManiacDanceTimestamp(
                currentSession.Character,
                RuntimeLegacyManiacDanceNextAttackAtUtcAttribute,
                out var nextAttackAt) ||
            now.ToUniversalTime() <= nextAttackAt)
        {
            return false;
        }

        var nextRecurringAttackAt = now.ToUniversalTime() + LegacyManiacDanceRecurringAttackDelay;
        var scheduledSession = currentSession with
        {
            Character = WithCharacterAttribute(
                currentSession.Character,
                RuntimeLegacyManiacDanceNextAttackAtUtcAttribute,
                nextRecurringAttackAt.ToString("O", CultureInfo.InvariantCulture)),
        };
        if (!sessions.TryCompareAndSet(context.Connection, currentSession, scheduledSession))
        {
            return false;
        }

        context = context with { Session = scheduledSession };
        var playerSpell = scheduledSession.PlayerSpells.Spells.FirstOrDefault(
            candidate => candidate.Id == LegacyManiacDanceSpellId);
        if (playerSpell is null ||
            !LegacyMagicRuntime.TryResolveSpell(
                worldData,
                LegacyManiacDanceSpellId,
                spellLevel: 0,
                out var damageSpell) ||
            damageSpell.Sort != LegacyMagicSort.ManiacDance)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped due legacy ManiacDance automatic attack " +
                    $"{FormatSession(context)} spellId={LegacyManiacDanceSpellId}; " +
                    "learned spell or matching level-zero magictype row is missing.")
                .ConfigureAwait(false);
            return true;
        }

        await SendLegacyManiacDanceAutomaticDamageAsync(
                context,
                scheduledSession,
                playerSpell,
                damageSpell,
                now.ToUniversalTime(),
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask SendLegacyManiacDanceAutomaticDamageAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell damageSpell,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var updatedSession = session;
        var animationCasterUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var animationX = checked((ushort)Math.Clamp(session.Character.X, ushort.MinValue, ushort.MaxValue));
        var animationY = checked((ushort)Math.Clamp(session.Character.Y, ushort.MinValue, ushort.MaxValue));
        var monsterTargets = context.MonsterStates is null
            ? []
            : SelectLegacyManiacDanceMonsterTargets(context, updatedSession);
        var playerTargets = SelectLegacyManiacDancePlayerTargets(context, updatedSession, damageSpell);
        var sobNpcTargets = context.SobNpcStates is null
            ? []
            : SelectLegacyManiacDanceSobNpcTargets(context, updatedSession);

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

        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(
            LegacyPlayerMonsterMagicTarget Target,
            MonsterDamageResult Result,
            uint RequestedDamage)>();
        var playerResults = new List<(LegacyPlayerMagicDamageResult Result, uint RequestedDamage)>();
        var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result, uint RequestedDamage)>();
        var killedTrainingSobNpcTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var eventSobNpcSideEffectTargets = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();

        if (context.MonsterStates is { } monsterStates)
        {
            foreach (var target in monsterTargets)
            {
                var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    updatedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    damageSpell);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    damage,
                    now,
                    updatedSession);
                monsterResults.Add((target, result, damage));
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
            if (context.Sessions?.TryGet(activeTarget.Connection, out var refreshedTarget) == true &&
                refreshedTarget is not null)
            {
                activeTarget = activeTarget with { Session = refreshedTarget };
            }

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

            var damageResult = statusDefended || wildPhoenixDefended
                ? new LegacyPlayerPlayerPhysicalSpellDamageResult(1, MsgAttackEffect.None)
                : CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
                    updatedSession,
                    activeTarget.Session,
                    damageSpell,
                    context.WorldData,
                    damageDivisor: 1);
            if (!statusDefended && !wildPhoenixDefended)
            {
                var breakthroughResult = ApplyLegacyPlayerPlayerPhysicalSpellBreakthroughBranch(
                    updatedSession,
                    activeTarget.Session,
                    damageSpell,
                    context.WorldData,
                    damageResult);
                damageResult = breakthroughResult.DamageResult;
                damageResult = ApplyLegacyPlayerPlayerPhysicalDefensiveStance(
                    activeTarget.Session,
                    damageResult);
                damageResult = ApplyLegacyPlayerPlayerPhysicalSpellInfinity(
                    updatedSession,
                    damageSpell,
                    damageResult);
                damageResult = ApplyLegacyPlayerPlayerPhysicalSpellNoBreakthroughBonus(
                    breakthroughResult.State,
                    damageResult);
                var ironShieldResult = await ApplyLegacyPlayerPlayerPhysicalSpellIronShieldAsync(
                        context,
                        activeTarget,
                        updatedSession,
                        damageResult.Damage,
                        cancellationToken)
                    .ConfigureAwait(false);
                activeTarget = ironShieldResult.Target;
                damageResult = damageResult with { Damage = ironShieldResult.Damage };
                var postIronShieldResult =
                    await ApplyLegacyPlayerPlayerPhysicalPostIronShieldTailAsync(
                            context,
                            updatedSession,
                            damageResult.Damage,
                            cancellationToken)
                        .ConfigureAwait(false);
                updatedSession = postIronShieldResult.Attacker;
                context = context with { Session = updatedSession };
                damageResult = damageResult with { Damage = postIronShieldResult.Damage };
                var dragonSwingResult = await ApplyLegacyPlayerPlayerPhysicalDragonSwingAsync(
                        context,
                        activeTarget,
                        damageSpell,
                        damageResult.Damage,
                        cancellationToken)
                    .ConfigureAwait(false);
                activeTarget = dragonSwingResult.Target;
                damageResult = damageResult with { Damage = dragonSwingResult.Damage };
            }

            var damage = statusDefended || wildPhoenixDefended
                ? damageResult.Damage
                : ApplyLegacyPlayerReceiveMudWall(
                    activeTarget.Session,
                    context.WorldData,
                    damageResult.Damage);
            var receiveOutcome = statusDefended || wildPhoenixDefended
                ? await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    context,
                    activeTarget,
                    updatedSession.Character,
                    damage,
                    cancellationToken,
                    applyMudWall: false)
                .ConfigureAwait(false)
                : await ApplyLegacyBackDamageEligiblePlayerDamageAsync(
                        context,
                        activeTarget,
                        updatedSession.Character,
                        damageResult.Damage,
                        cancellationToken,
                        appliedDamageOverride: damage,
                        applyMudWallInFinalReceive: false)
                    .ConfigureAwait(false);
            if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
            {
                updatedSession = currentReceiveAttacker;
                context = context with { Session = updatedSession };
            }

            if (receiveOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return;
            }

            if (receiveOutcome.Result is not { } result)
            {
                continue;
            }
            if (result.UpdatedAttackerSession is not null)
            {
                updatedSession = result.UpdatedAttackerSession;
                context = context with { Session = updatedSession };
            }

            playerResults.Add((result, damage));
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                damage,
                Hit: true,
                damageResult.Effect));
        }

        if (context.SobNpcStates is { } sobNpcStates)
        {
            foreach (var target in sobNpcTargets)
            {
                var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                    updatedSession,
                    damageSpell,
                    context.WorldData,
                    damageMultiplier: 1);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(
                    target.SobNpc,
                    context.WorldData?.StaticStatues);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                sobNpcResults.Add((target.SobNpc, result, damage));
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

        foreach (var (target, result, _) in monsterResults)
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
        }

        foreach (var (result, _) in playerResults)
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
                await LogLegacySobNpcEventSideEffectGapAsync(context, sobNpc, result, damageSpell)
                    .ConfigureAwait(false);
            }
        }

        var monsterExperience = monsterResults
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(
                0UL,
                (total, result) => total + CalculateLegacyManiacDanceMonsterReturnExperience(
                    updatedSession,
                    result.Target,
                    result.RequestedDamage));
        var sobNpcExperience = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        var returnExperience = monsterExperience + sobNpcExperience;
        updatedSession = await ApplyLegacyManiacDanceIncreaseExperienceAsync(
                context,
                updatedSession,
                returnExperience,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            animationCasterUid,
            animationX,
            animationY,
            LegacyManiacDanceSpellId,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
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
                "WorldServer handled due legacy ManiacDance automatic damage " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={LegacyManiacDanceSpellId} " +
                $"sort={(int)LegacyMagicSort.ManiacDance} range={LegacyManiacDanceRange} targets={animationTargets.Count} " +
                $"monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} " +
                $"sobNpcTargets={sobNpcResults.Count} totalAppliedDamage={totalAppliedDamage} " +
                $"monsterExperience={monsterExperience} sobNpcExperience={sobNpcExperience} " +
                $"returnExperience={returnExperience} spellExperienceApplied=False " +
                $"nextAttackAtUtc={now + LegacyManiacDanceRecurringAttackDelay:O}; source={LegacyManiacDanceSource}.")
            .ConfigureAwait(false);
    }

    private static ulong CalculateLegacyManiacDanceMonsterReturnExperience(
        WorldSession session,
        LegacyPlayerMonsterMagicTarget target,
        uint requestedDamage)
    {
        if (requestedDamage == 0 || LegacyMonsterDamageRules.IsLegacyGuardFamily(target.MonsterFamily))
        {
            return 0;
        }

        var adjustedExperience = AdjustLegacyMonsterExperience(
            checked((int)Math.Min(requestedDamage, target.Monster.MaxLife)),
            checked((int)session.Character.Level),
            target.Monster.Level);
        return checked((ulong)Math.Max(adjustedExperience, 0));
    }

    private static async ValueTask<WorldSession> ApplyLegacyManiacDanceIncreaseExperienceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        ulong returnExperience,
        CancellationToken cancellationToken)
    {
        if (returnExperience == 0)
        {
            return session;
        }

        var boundedExperience = returnExperience > uint.MaxValue
            ? uint.MaxValue
            : (uint)returnExperience;
        var experienceResult = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateLegacyExperienceGainInputs(context, session, boundedExperience));
        var updatedSession = ApplyLegacyExperienceSideEffects(
            context,
            context.Connection,
            session,
            experienceResult);
        if (experienceResult.AppliedExperienceGain != 0)
        {
            updatedSession = await ApplyLegacyFinalExperienceGainAsync(
                    context,
                    context.Connection,
                    updatedSession,
                    experienceResult.AppliedExperienceGain,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy ManiacDance combined return experience " +
                $"{FormatSession(context with { Session = updatedSession })} returnExperience={boundedExperience} " +
                $"gain={experienceResult.AppliedExperienceGain} oblivion={experienceResult.OblivionExperienceGain} " +
                $"huntingBlessing={experienceResult.HuntingBlessingGain} suppression={experienceResult.SuppressionReason}; " +
                "source=ThreadPool FloorCallback accumulated Monster.Execute/Npc.Execute returns -> " +
                "IncreaseExperience.Up.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyManiacDanceMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        return EnumerateVisibleMonsterTargets(context, session.Character)
            .Where(candidate => LegacyGetDistance(
                session.Character.X,
                session.Character.Y,
                candidate.Monster.X,
                candidate.Monster.Y) <= LegacyManiacDanceRange)
            .ToList();
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyManiacDancePlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell)
    {
        return EnumerateVisiblePlayerMagicTargets(context, session, spell)
            .Where(candidate => LegacyGetDistance(
                session.Character.X,
                session.Character.Y,
                candidate.ActiveSession.Session.Character.X,
                candidate.ActiveSession.Session.Character.Y) <= LegacyManiacDanceRange)
            .ToList();
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyManiacDanceSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        return EnumerateVisibleSobNpcMagicTargets(context, session.Character)
            .Where(candidate => LegacyGetDistance(
                session.Character.X,
                session.Character.Y,
                candidate.SobNpc.X,
                candidate.SobNpc.Y) <= LegacyManiacDanceRange)
            .ToList();
    }

    private static int CalculateLegacyManiacDanceInitialAttackDelayMilliseconds(
        WorldSession session,
        WorldDataCatalog? worldData)
    {
        var rightWeaponFrequency = 0U;
        var leftWeaponFrequency = 0U;
        var ringFrequency = 0U;
        foreach (var item in LegacyEquipmentSelector.SelectCurrentEquipment(
                     session.PlayerItems,
                     session.AlternateEquipmentActive))
        {
            if (!TryGetLegacyItemType(worldData?.ItemsById, item.ItemId, out var itemType))
            {
                continue;
            }

            var position = item.Position is > 50 and <= 100
                ? item.Position - 50
                : item.Position;
            switch (position)
            {
                case 4:
                    rightWeaponFrequency = itemType.Frequency;
                    break;
                case 5:
                    leftWeaponFrequency = itemType.Frequency;
                    break;
                case 6:
                    ringFrequency = itemType.Frequency;
                    break;
            }
        }

        var delay = LegacyManiacDanceBaseAttackSpeedMilliseconds;
        delay = SubtractLegacyManiacDanceAttackSpeed(delay, LegacyManiacDanceBaseAttackSpeedReductionMilliseconds);
        delay = SubtractLegacyManiacDanceAttackSpeed(delay, rightWeaponFrequency);
        delay = SubtractLegacyManiacDanceAttackSpeed(delay, leftWeaponFrequency);
        delay = SubtractLegacyManiacDanceAttackSpeed(delay, ringFrequency);
        delay = SubtractLegacyManiacDanceAttackSpeed(
            delay,
            checked((uint)Math.Clamp(session.Character.Agility / 2, 0, uint.MaxValue)));
        if (HasLegacyStatusFlag(session.Character, LegacyCycloneFlag))
        {
            delay = SubtractLegacyManiacDanceAttackSpeed(
                delay,
                LegacyManiacDanceCycloneAttackSpeedReductionMilliseconds);
        }

        if (TryGetLegacyEquippedRuneLevel(
                session.PlayerItems,
                worldData,
                "DeadlySight",
                out var deadlySightLevel))
        {
            delay = SubtractLegacyManiacDanceAttackSpeed(
                delay,
                checked((uint)(25 + deadlySightLevel * 10)));
        }

        return delay;
    }

    private static int SubtractLegacyManiacDanceAttackSpeed(int delay, uint reduction)
    {
        var boundedReduction = reduction > int.MaxValue ? int.MaxValue : (int)reduction;
        return Math.Max(LegacyManiacDanceMinimumAttackSpeedMilliseconds, delay - boundedReduction);
    }

    private static bool TryReadLegacyManiacDanceTimestamp(
        CharacterSnapshot character,
        string attributeName,
        out DateTimeOffset timestamp)
    {
        timestamp = default;
        return character.Attributes.TryGetValue(attributeName, out var rawTimestamp) &&
            DateTimeOffset.TryParse(
                rawTimestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out timestamp);
    }
}
