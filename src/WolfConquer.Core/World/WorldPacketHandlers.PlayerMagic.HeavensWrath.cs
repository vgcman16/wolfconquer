using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyHeavensWrathExpiresAtUtcAttribute =
        "LegacyHeavensWrathExpiresAtUtc";
    internal const string RuntimeLegacyHeavensWrathRemoveOnDeadAttribute =
        "LegacyHeavensWrathRemoveOnDead";

    private const byte LegacyHeavensWrathRadius = 7;

    private static async Task<bool> HandleLegacyPlayerHeavensWrathMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.HeavensWrath)
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

        var resourceContext = context with { Session = resourceSession };
        if (spell.Id != LegacyHeavensWrathSpellId ||
            !IsLegacyThunderstrikerClass(resourceSession.Character.Class))
        {
            var reason = spell.Id != LegacyHeavensWrathSpellId
                ? $"spellId={spell.Id}; expected={LegacyHeavensWrathSpellId}"
                : $"class={resourceSession.Character.Class}; AtributesStatus.IsThunderStriker is false";
            await context.Log.WriteLineAsync(
                    "WorldServer source-backed returned legacy HeavensWrath magic " +
                    $"{FormatSession(resourceContext)} spellId={spell.Id} sort={(int)spell.Sort} {reason}; " +
                    "source HeavensWrath.cs returns after CanUseSpell.Verified without Ride removal or animation.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = resourceSession;
        var removedRide = HasLegacyStatusFlag(updatedSession.Character, LegacyRideFlag);
        if (removedRide)
        {
            updatedSession = updatedSession with
            {
                Character = ClearLegacyMountRideStatus(updatedSession.Character),
            };
            context.Sessions?.Set(context.Connection, updatedSession);
            resourceContext = context with { Session = updatedSession };
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    resourceContext,
                    updatedSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!HasLegacyStatusFlag(updatedSession.Character, LegacyHeavensWrathFlag))
        {
            return await HandleLegacyPlayerHeavensWrathFirstCastAsync(
                    resourceContext,
                    updatedSession,
                    playerSpell,
                    spell,
                    removedRide,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await HandleLegacyPlayerHeavensWrathRecastAsync(
                resourceContext,
                updatedSession,
                query,
                playerSpell,
                spell,
                removedRide,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<bool> HandleLegacyPlayerHeavensWrathFirstCastAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        bool removedRide,
        CancellationToken cancellationToken)
    {
        var casterUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: casterUid,
            X: 0,
            Y: 0,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            [new LegacySpellAnimationTarget(
                casterUid,
                Damage: 0,
                Hit: false,
                MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(
                context,
                session,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        var nowUtc = DateTimeOffset.UtcNow;
        var heavensWrathSession = ApplyLegacyHeavensWrathStatus(session, spell, nowUtc);
        context.Sessions?.Set(context.Connection, heavensWrathSession);
        var heavensWrathContext = context with { Session = heavensWrathSession };
        await SendPlayerStatusFlagVectorToVisibleAsync(
                heavensWrathContext,
                heavensWrathSession,
                cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = ApplyLegacyFlyStatus(heavensWrathSession, spell, nowUtc);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendPlayerStatusFlagVectorToVisibleAsync(
                updatedContext,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy HeavensWrath first cast " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} removedRide={removedRide} firstCast=True " +
                $"heavensWrathFlag={LegacyHeavensWrathFlag} flyFlag={LegacyFlyFlag} duration={spell.Duration} " +
                "animationX=0 animationY=0 attackedUid=self targetHit=False; source=HeavensWrath.cs case 15710 lines 28-42.")
            .ConfigureAwait(false);
        return true;
    }

    private static async Task<bool> HandleLegacyPlayerHeavensWrathRecastAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        bool removedRide,
        CancellationToken cancellationToken)
    {
        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyHeavensWrathMonsterTargets(context, session, query)
            : [];
        var playerTargets = SelectLegacyHeavensWrathPlayerTargets(context, session, query, spell);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyHeavensWrathSobNpcTargets(context, session, query)
            : [];

        var updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                session,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(
            LegacyPlayerMonsterMagicTarget Target,
            MonsterDamageResult Result,
            uint Progress)>();
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
                var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    updatedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                damage = CalculateLegacyHeavensWrathSoulDamage(damage, playerSpell.UseSpellSoul);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    damage,
                    now,
                    updatedSession);
                var progress = CalculateLegacyHeavensWrathMonsterProgress(
                    updatedSession,
                    target.Monster,
                    target.MonsterFamily,
                    result);
                monsterResults.Add((target, result, progress));
                if (result.AlreadyDead)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    result.RequestedDamage,
                    Hit: true,
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
            var physicalDamage = wildPhoenixDefended
                ? new LegacyPlayerPlayerPhysicalSpellDamageResult(1U, MsgAttackEffect.None)
                : CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData,
                    damageDivisor: 1);
            var damage = CalculateLegacyHeavensWrathSoulDamage(
                physicalDamage.Damage,
                playerSpell.UseSpellSoul);
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
            if (result.UpdatedAttackerSession is not null)
            {
                updatedSession = result.UpdatedAttackerSession;
                context = context with { Session = updatedSession };
            }

            context = context with { Session = updatedSession };

            (updatedSession, result) = await TrySendLegacyReceiveAttackPlayerSpeffectAttributesAsync(
                    context,
                    updatedSession,
                    result,
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

            playerResults.Add((result, damage));
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
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                damage,
                Hit: true,
                physicalDamage.Effect));
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
                damage = CalculateLegacyHeavensWrathSoulDamage(damage, playerSpell.UseSpellSoul);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(
                    target.SobNpc,
                    context.WorldData?.StaticStatues);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                var progress = result.AlreadyDead
                    ? 0UL
                    : CalculateLegacySobNpcExperienceDamage(target.SobNpc, result);
                sobNpcResults.Add((target.SobNpc, result, damage, progress));
                if (result.AlreadyDead)
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

        foreach (var (target, result, _) in monsterResults)
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
                    await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await TryApplyLegacyPlayerMagicChillingSnowAsync(
                context,
                updatedSession,
                playerResults.Select(result => result.Result).ToArray(),
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
        updatedSession = await ApplyLegacyHeavensWrathIncreaseExperienceAsync(
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
                activity: "HeavensWrath first UpdateSpell weapon proficiency experience")
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
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(progressDamage),
                cancellationToken,
                activity: "HeavensWrath magic-type UpdateSpell weapon proficiency experience")
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var casterUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: 0,
            CheckedUInt16(updatedSession.Character.X, nameof(updatedSession.Character.X)),
            CheckedUInt16(updatedSession.Character.Y, nameof(updatedSession.Character.Y)),
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 1,
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
                "WorldServer handled legacy HeavensWrath active recast " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} removedRide={removedRide} " +
                $"centerX={query.X} centerY={query.Y} animationX={updatedSession.Character.X} " +
                $"animationY={updatedSession.Character.Y} radius={LegacyHeavensWrathRadius} spellEffect=1 " +
                $"targets={animationTargets.Count} monsterTargets={monsterResults.Count} " +
                $"playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"monsterProgress={monsterProgress} sobNpcProgress={sobNpcProgress} " +
                $"progressDamage={progressDamage} totalAppliedDamage={totalAppliedDamage}; " +
                "source=HeavensWrath.cs case 15710 lines 45-88.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyHeavensWrathStatus(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithLegacyStatusFlag(session.Character, LegacyHeavensWrathFlag);
        if (spell.Duration > 0)
        {
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyHeavensWrathExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(spell.Duration)
                    .ToString("O", CultureInfo.InvariantCulture));
        }

        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyHeavensWrathRemoveOnDeadAttribute,
            "true");
        return session with { Character = updatedCharacter };
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyHeavensWrathMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(query.X, query.Y, candidate.Monster.X, candidate.Monster.Y) <=
                    LegacyHeavensWrathRadius &&
                CanLegacyPlayerAttackMonster(
                    session.Character,
                    candidate.Monster,
                    candidate.MonsterFamily,
                    out _))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyHeavensWrathPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(query.X, query.Y, character.X, character.Y) <=
                    LegacyHeavensWrathRadius &&
                CanLegacyPlayerMagicDamageTarget(
                    context,
                    session,
                    candidate.ActiveSession.Session,
                    spell,
                    out _))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyHeavensWrathSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(query.X, query.Y, candidate.SobNpc.X, candidate.SobNpc.Y) <=
                    LegacyHeavensWrathRadius &&
                CanLegacySobNpcMagicDamageTarget(
                    context,
                    session.Character,
                    candidate.SobNpc,
                    out _))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static uint CalculateLegacyHeavensWrathMonsterProgress(
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        MonsterDamageResult result)
    {
        if (result.AlreadyDead || LegacyMonsterDamageRules.IsLegacyGuardFamily(monsterFamily))
        {
            return 0;
        }

        return checked((uint)AdjustLegacyMonsterExperience(
            checked((int)Math.Min(result.RequestedDamage, monster.MaxLife)),
            CheckedInt32(session.Character.Level, nameof(session.Character.Level)),
            monster.Level));
    }

    private static async ValueTask<WorldSession> ApplyLegacyHeavensWrathIncreaseExperienceAsync(
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
            if (experienceResult.OblivionExperienceGain != 0 ||
                experienceResult.HuntingBlessingGain != 0)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy HeavensWrath experience side effects " +
                        $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                        $"applied=0 oblivion={experienceResult.OblivionExperienceGain} " +
                        $"huntingBlessing={experienceResult.HuntingBlessingGain} " +
                        $"suppression={experienceResult.SuppressionReason}; " +
                        "source=HeavensWrath.cs IncreaseExperience.Up.")
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
                "WorldServer sent legacy HeavensWrath experience " +
                $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                $"gain={experienceResult.AppliedExperienceGain} oblivion={experienceResult.OblivionExperienceGain} " +
                $"huntingBlessing={experienceResult.HuntingBlessingGain} " +
                $"experience={updatedSession.Character.Experience} level={updatedSession.Character.Level}; " +
                "source=HeavensWrath.cs IncreaseExperience.Up.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static uint CalculateLegacyHeavensWrathSoulDamage(uint damage, byte useSpellSoul)
    {
        // Base.CalculateSoul returns the incoming damage unchanged in the matching source.
        _ = useSpellSoul;
        return damage;
    }
}
