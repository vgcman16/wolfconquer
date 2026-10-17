using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyShurikenVortexSpellId = 6_010;
    private const ushort LegacyShurikenVortexDamageSpellRowId = 6_009;
    private const string LegacyShurikenVortexSource = "CombatGearAttack.cs case 6010";
    private static readonly TimeSpan LegacyShurikenVortexDuration = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan LegacyShurikenVortexRecurringAttackDelay = TimeSpan.FromSeconds(1);

    private static async ValueTask<bool> HandleLegacyPlayerShurikenVortexMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Id != LegacyShurikenVortexSpellId ||
            spell.Sort != LegacyMagicSort.ShurikenVortex)
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

        if (HasLegacyStatusFlag(resourceSession.Character, LegacyRuntimeUpdateTicker.XpListFlag))
        {
            await HandleLegacyShurikenVortexXpListActivationAsync(
                    context,
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyStatusFlag(resourceSession.Character, LegacyShurikenVortexFlag))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy ShurikenVortex magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort}; " +
                    "XPList and ShurikenVortex status flags are not active, matching source no-op.")
                .ConfigureAwait(false);
            return true;
        }

        if (context.WorldData is not { } worldData ||
            !LegacyMagicRuntime.TryResolveSpellByRowId(
                worldData,
                LegacyShurikenVortexDamageSpellRowId,
                out var damageSpell))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedMagic(
                        spell.Id,
                        sort: (int)spell.Sort,
                        skillName: "ShurikenVortex",
                        resource: WorldDataResource,
                        source: LegacyShurikenVortexSource,
                        reason:
                            "ShurikenVortex active branch depends on decompiled Pool.Magic[6009][0], " +
                            "but the 6009 damage row is not loaded."))
                .ConfigureAwait(false);
            return true;
        }

        await HandleLegacyShurikenVortexActiveDamageAsync(
                context with { Session = resourceSession },
                resourceSession,
                query,
                damageSpell,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask HandleLegacyShurikenVortexXpListActivationAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var hadShurikenVortex = HasLegacyStatusFlag(resourceSession.Character, LegacyShurikenVortexFlag);
        var now = DateTimeOffset.UtcNow;
        var (xpListSession, openedShurikenVortex, autoHunt, nextAttackAt) =
            await ApplyLegacyShurikenVortexXpListTransitionAsync(
                context with { Session = resourceSession },
                resourceSession,
                now,
                cancellationToken)
            .ConfigureAwait(false);

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

        var expiresAtUtc = xpListSession.Character.Attributes.TryGetValue(
            RuntimeLegacyShurikenVortexExpiresAtUtcAttribute,
            out var expiresAt)
            ? expiresAt
            : "none";
        var nextAttackAtUtc = nextAttackAt?.ToString("O", CultureInfo.InvariantCulture) ?? "none";
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy ShurikenVortex XPList transition " +
                $"{FormatSession(context with { Session = xpListSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"consumedXpList=True openedShurikenVortex={openedShurikenVortex} " +
                $"refreshedShurikenVortex={hadShurikenVortex && !autoHunt} autoHunt={autoHunt} " +
                $"durationSeconds={(int)LegacyShurikenVortexDuration.TotalSeconds} " +
                $"expiresAtUtc={expiresAtUtc} nextAttackAtUtc={nextAttackAtUtc}; " +
                $"source={LegacyShurikenVortexSource} XPList branch.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<(
        WorldSession Session,
        bool OpenedShurikenVortex,
        bool AutoHunt,
        DateTimeOffset? NextAttackAt)>
        ApplyLegacyShurikenVortexXpListTransitionAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            DateTimeOffset now,
            CancellationToken cancellationToken)
    {
        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        if (ReadAttributeBool(session.Character.Attributes, RuntimeAutoHuntAttribute))
        {
            var autoHuntSession = session with
            {
                Character = WithoutLegacyStatusFlag(
                    session.Character,
                    LegacyRuntimeUpdateTicker.XpListFlag),
            };
            context.Sessions?.Set(context.Connection, autoHuntSession);
            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(uid, value: 0).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context with { Session = autoHuntSession },
                    autoHuntSession,
                    cancellationToken)
                .ConfigureAwait(false);
            return (autoHuntSession, false, true, null);
        }

        var xpCircleSession = session with
        {
            Character = WithCharacterAttribute(session.Character, "XPCount", 0UL),
        };
        context.Sessions?.Set(context.Connection, xpCircleSession);
        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeXPCircleTickUpdate(uid, count: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var currentXpSkillFlag = LegacyXpSkillFlags.FirstOrDefault(
            flag => HasLegacyStatusFlag(xpCircleSession.Character, flag));
        var transitionSession = xpCircleSession;
        if (currentXpSkillFlag != 0 && currentXpSkillFlag != LegacyShurikenVortexFlag)
        {
            var transitionCharacter = xpCircleSession.Character;
            foreach (var xpSkillFlag in LegacyXpSkillFlags)
            {
                if (xpSkillFlag != LegacyShurikenVortexFlag)
                {
                    transitionCharacter = WithoutLegacyStatusFlag(transitionCharacter, xpSkillFlag);
                }
            }

            transitionSession = xpCircleSession with { Character = transitionCharacter };
            context.Sessions?.Set(context.Connection, transitionSession);
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context with { Session = transitionSession },
                    transitionSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        DateTimeOffset? preservedNextAttackAt = null;
        if (currentXpSkillFlag == LegacyShurikenVortexFlag &&
            TryReadLegacyShurikenVortexTimestamp(
                transitionSession.Character,
                RuntimeLegacyShurikenVortexNextAttackAtUtcAttribute,
                out var existingNextAttackAt))
        {
            preservedNextAttackAt = existingNextAttackAt;
        }

        var openedCharacter = transitionSession.Character;
        foreach (var expiresAtAttributeName in LegacyXpSkillExpiresAtAttributes)
        {
            openedCharacter = WithoutCharacterAttribute(openedCharacter, expiresAtAttributeName);
        }

        foreach (var xpSkillFlag in LegacyXpSkillFlags)
        {
            if (xpSkillFlag != LegacyShurikenVortexFlag)
            {
                openedCharacter = WithoutLegacyStatusFlag(openedCharacter, xpSkillFlag);
            }
        }

        var nowUtc = now.ToUniversalTime();
        var expiresAtUtc = nowUtc + LegacyShurikenVortexDuration;
        var nextAttackAtUtc = preservedNextAttackAt ??
            nowUtc + LegacyShurikenVortexRecurringAttackDelay;
        openedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithLegacyStatusFlag(openedCharacter, LegacyShurikenVortexFlag),
                RuntimeLegacyShurikenVortexExpiresAtUtcAttribute,
                expiresAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            RuntimeLegacyShurikenVortexNextAttackAtUtcAttribute,
            nextAttackAtUtc.ToString("O", CultureInfo.InvariantCulture));
        var openedSession = transitionSession with { Character = openedCharacter };
        context.Sessions?.Set(context.Connection, openedSession);
        if (currentXpSkillFlag != LegacyShurikenVortexFlag)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context with { Session = openedSession },
                    openedSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var xpListRemovedSession = openedSession with
        {
            Character = WithoutLegacyStatusFlag(
                openedSession.Character,
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

        return (
            xpListRemovedSession,
            currentXpSkillFlag != LegacyShurikenVortexFlag,
            false,
            nextAttackAtUtc);
    }

    internal static async ValueTask<bool> TrySendDueLegacyShurikenVortexAttackAsync(
        WorldPacketHandlerContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions ||
            context.WorldData is not { } worldData ||
            !sessions.TryGet(context.Connection, out var currentSession) ||
            currentSession is null ||
            !HasLegacyStatusFlag(currentSession.Character, LegacyShurikenVortexFlag))
        {
            return false;
        }

        var nowUtc = now.ToUniversalTime();
        if (TryReadLegacyShurikenVortexTimestamp(
                currentSession.Character,
                RuntimeLegacyShurikenVortexNextAttackAtUtcAttribute,
                out var nextAttackAt) &&
            nowUtc < nextAttackAt)
        {
            return false;
        }

        var nextRecurringAttackAt = nowUtc + LegacyShurikenVortexRecurringAttackDelay;
        var scheduledSession = currentSession with
        {
            Character = WithCharacterAttribute(
                currentSession.Character,
                RuntimeLegacyShurikenVortexNextAttackAtUtcAttribute,
                nextRecurringAttackAt.ToString("O", CultureInfo.InvariantCulture)),
        };
        if (!sessions.TryCompareAndSet(context.Connection, currentSession, scheduledSession))
        {
            return false;
        }

        context = context with { Session = scheduledSession };
        if (!LegacyMagicRuntime.TryResolveLearnedSpell(
                scheduledSession.PlayerSpells,
                worldData,
                LegacyShurikenVortexSpellId,
                out var playerSpell,
                out var spell) ||
            spell.Sort != LegacyMagicSort.ShurikenVortex)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped due legacy ShurikenVortex automatic attack " +
                    $"{FormatSession(context)} spellId={LegacyShurikenVortexSpellId}; " +
                    "learned spell or matching level-specific ShurikenVortex row is missing.")
                .ConfigureAwait(false);
            return true;
        }

        var casterUid = CheckedUInt32(scheduledSession.Character.Uid, nameof(scheduledSession.Character.Uid));
        var query = new MsgAttackQuery(
            Uid: casterUid,
            OpponentUid: 0,
            X: checked((ushort)Math.Clamp(scheduledSession.Character.X, ushort.MinValue, ushort.MaxValue)),
            Y: checked((ushort)Math.Clamp(scheduledSession.Character.Y, ushort.MinValue, ushort.MaxValue)),
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0);
        await HandleLegacyPlayerShurikenVortexMagicAsync(
                context,
                scheduledSession,
                query,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask HandleLegacyShurikenVortexActiveDamageAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell damageSpell,
        CancellationToken cancellationToken)
    {
        var updatedSession = resourceSession;
        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyShurikenVortexMonsterTargets(context, updatedSession, damageSpell)
            : [];
        var playerTargets = SelectLegacyShurikenVortexPlayerTargets(context, updatedSession, damageSpell);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyShurikenVortexSobNpcTargets(context, updatedSession, damageSpell)
            : [];

        foreach (var target in monsterTargets)
        {
            if (IsLegacyGuardMonsterFamily(target.Monster, target.MonsterFamily))
            {
                updatedSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                        context,
                        updatedSession,
                        cancellationToken)
                    .ConfigureAwait(false);
                context = context with { Session = updatedSession };
                break;
            }
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
        var now = DateTimeOffset.UtcNow;

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
                damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                    updatedSession,
                    damageSpell,
                    damage,
                    LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc);
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
            damage /= 6;
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
                damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                    updatedSession,
                    damageSpell,
                    damage,
                    LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc);
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
                (total, result) => total + CalculateLegacyShurikenVortexMonsterReturnExperience(
                    updatedSession,
                    result.Target,
                    result.RequestedDamage));
        var sobNpcExperience = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        var returnExperience = monsterExperience + sobNpcExperience;
        updatedSession = await ApplyLegacyShurikenVortexIncreaseExperienceAsync(
                context,
                updatedSession,
                returnExperience,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            query.X,
            query.Y,
            LegacyShurikenVortexSpellId,
            0,
            0,
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
                "WorldServer handled legacy ShurikenVortex active damage " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={LegacyShurikenVortexSpellId} " +
                $"sort={(int)LegacyMagicSort.ShurikenVortex} damageSpellRowId={LegacyShurikenVortexDamageSpellRowId} " +
                $"damageSpellTypeId={damageSpell.Id} range={damageSpell.Range} targets={animationTargets.Count} " +
                $"monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"monsterExperience={monsterExperience} sobNpcExperience={sobNpcExperience} " +
                $"returnExperience={returnExperience} totalAppliedDamage={totalAppliedDamage} " +
                $"spellExperienceApplied=False; source={LegacyShurikenVortexSource} active branch.")
            .ConfigureAwait(false);
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyShurikenVortexMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell damageSpell)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y) >= damageSpell.Range ||
                !CanLegacyPlayerAttackMonster(session.Character, candidate.Monster, candidate.MonsterFamily, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyShurikenVortexPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell damageSpell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, damageSpell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    character.X,
                    character.Y) >= damageSpell.Range)
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyShurikenVortexSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell damageSpell)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y) >= damageSpell.Range ||
                !CanLegacySobNpcMagicDamageTarget(context, session.Character, candidate.SobNpc, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static ulong CalculateLegacyShurikenVortexMonsterReturnExperience(
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

    private static async ValueTask<WorldSession> ApplyLegacyShurikenVortexIncreaseExperienceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        ulong returnExperience,
        CancellationToken cancellationToken)
    {
        if (returnExperience == 0)
        {
            return session;
        }

        var boundedExperience = returnExperience > uint.MaxValue ? uint.MaxValue : (uint)returnExperience;
        var experienceResult = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateLegacyExperienceGainInputs(context, session, boundedExperience));
        var updatedSession = ApplyLegacyExperienceSideEffects(context, context.Connection, session, experienceResult);
        if (experienceResult.AppliedExperienceGain == 0)
        {
            if (experienceResult.OblivionExperienceGain != 0 ||
                experienceResult.HuntingBlessingGain != 0)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy ShurikenVortex experience side effects " +
                        $"{FormatSession(context with { Session = updatedSession })} returnExperience={boundedExperience} " +
                        $"applied=0 oblivion={experienceResult.OblivionExperienceGain} " +
                        $"huntingBlessing={experienceResult.HuntingBlessingGain} suppression={experienceResult.SuppressionReason}; " +
                        $"source={LegacyShurikenVortexSource} IncreaseExperience.Up.")
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
                "WorldServer sent legacy ShurikenVortex experience " +
                $"{FormatSession(context with { Session = updatedSession })} returnExperience={boundedExperience} " +
                $"gain={experienceResult.AppliedExperienceGain} oblivion={experienceResult.OblivionExperienceGain} " +
                $"huntingBlessing={experienceResult.HuntingBlessingGain} experience={updatedSession.Character.Experience} " +
                $"level={updatedSession.Character.Level}; source={LegacyShurikenVortexSource} IncreaseExperience.Up.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool TryReadLegacyShurikenVortexTimestamp(
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
