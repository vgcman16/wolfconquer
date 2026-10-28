using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyCrackingShockSpellId = 15_720;
    private const byte LegacyShockRadius = 6;

    private static async ValueTask<bool> HandleLegacyPlayerShockMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.Shock)
        {
            return false;
        }
        if (!LegacyShockCasterOwner.TryCapture(context, session, out var casterOwner)) return true;

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken,
                expectedNativeClientIdentity: casterOwner.Identity)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }
        if (!casterOwner.IsCurrent(context, resourceSession)) return true;

        if (spell.Id != LegacyCrackingShockSpellId ||
            !IsLegacyThunderstrikerClass(resourceSession.Character.Class))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer source-backed returned legacy Shock/CrackingShock magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                    $"class={resourceSession.Character.Class}; source Shock.cs returns after CanUseSpell.Verified.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = resourceSession;
        context = context with { Session = updatedSession };
        var monsterStates = context.MonsterStates;
        var sobNpcStates = context.SobNpcStates;
        var monsterTargets = monsterStates is not null
            ? SelectLegacyShockMonsterTargets(context, updatedSession, query)
            : [];
        var sobNpcTargets = sobNpcStates is not null
            ? SelectLegacyShockSobNpcTargets(context, updatedSession, query)
            : [];

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result, uint RuntimeDamage)>();
        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var killedTrainingSobNpcTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var eventSobNpcSideEffectTargets = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();
        var now = DateTimeOffset.UtcNow;

        if (monsterStates is not null)
        {
            foreach (var target in monsterTargets)
            {
                var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    updatedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                damage = CalculateLegacyShockSoulDamage(damage, playerSpell.UseSpellSoul);
                damage = MulDivToUInt32(damage, 2, 1);
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
                updatedSession = await PublishLegacyShockMonsterReceiveAsync(
                        context,
                        updatedSession,
                        target,
                        result,
                        cancellationToken)
                    .ConfigureAwait(false);
                context = context with { Session = updatedSession };
                if (!result.Killed)
                {
                    firstRetaliationTarget ??= target;
                }
            }
        }

        // Shock.cs enters the player view only after completing monster receives.
        var playerTargets = SelectLegacyShockPlayerTargets(context, updatedSession);
        foreach (var target in playerTargets)
        {
            var prepared = await TryPrepareLegacyShockPlayerReceiveAsync(
                    context,
                    updatedSession,
                    target.ActiveSession,
                    query,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedSession = prepared.Session;
            context = context with { Session = updatedSession };
            if (prepared.Target is not { } activeTarget)
            {
                continue;
            }

            var physicalDamage =
                await CalculateLegacyShockPlayerPhysicalDamageAsync(
                        context,
                        updatedSession,
                        activeTarget,
                        spell,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (physicalDamage.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return true;
            }

            updatedSession = physicalDamage.Session;
            context = context with { Session = updatedSession };
            activeTarget = physicalDamage.Target;
            if (physicalDamage.DamageResult is not { } physicalResult)
            {
                continue;
            }

            var damage = physicalResult.Damage;
            damage = CalculateLegacyShockSoulDamage(damage, playerSpell.UseSpellSoul);
            damage = MulDivToUInt32(damage, 75, 100);
            var receiveOutcome = await ApplyLegacyShockPlayerReceiveAsync(
                    context,
                    updatedSession,
                    activeTarget,
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
            context = context with { Session = updatedSession };
            playerResults.Add(result);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                result.ReceiveDamage ?? damage,
                Hit: true,
                physicalResult.Effect));
        }

        if (sobNpcStates is not null)
        {
            foreach (var target in sobNpcTargets)
            {
                var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                    updatedSession,
                    spell,
                    context.WorldData,
                    damageMultiplier: 1);
                // Physical.OnNpcs applies Infinity after its minimum-one floor.
                if (HasLegacyStatusFlag(updatedSession.Character, LegacyInfinityFlag))
                {
                    damage = MulDivToUInt32(
                        damage,
                        ReadAttributeUInt32(updatedSession.Character.Attributes, "InfinitySDamage", "InfinitySpellDamage"),
                        100);
                }

                damage = CalculateLegacyShockSoulDamage(damage, playerSpell.UseSpellSoul);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
                var result = sobNpcStates.ApplyDamage(
                    target.SobNpc,
                    damage,
                    now,
                    deathBehavior);
                sobNpcResults.Add((target.SobNpc, result));
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

        foreach (var (target, result, runtimeDamage) in monsterResults)
        {
            if (result.AlreadyDead)
            {
                continue;
            }

            updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                    context,
                    updatedSession,
                    target.Monster,
                    target.MonsterFamily,
                    runtimeDamage,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (firstRetaliationTarget is { } retaliationTarget)
        {
            var retaliationSession = await TrySendLegacyShockRetaliationAsync(
                    context, updatedSession, retaliationTarget, casterOwner, cancellationToken).ConfigureAwait(false);
            if (retaliationSession is null) return true;
            updatedSession = retaliationSession;
            context = context with { Session = updatedSession };
        }
        if (!casterOwner.TryCurrent(context, updatedSession, out updatedSession)) return true;
        context = context with { Session = updatedSession };

        if (sobNpcStates is not null)
        {
            foreach (var killed in killedTrainingSobNpcTargets)
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        context,
                        updatedSession,
                        killed.SobNpc,
                        sobNpcStates.GetVisible(killed.SobNpc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var (sobNpc, result, _) in eventSobNpcSideEffectTargets)
            {
                await LogLegacySobNpcEventSideEffectGapAsync(context, sobNpc, result, spell)
                    .ConfigureAwait(false);
            }
        }

        var totalMonsterDamage = monsterResults
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.RuntimeDamage);
        var totalSobNpcExperienceDamage = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        var totalSobNpcAppliedDamage = sobNpcResults
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        var totalAppliedDamage = monsterResults
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            playerResults.Aggregate(0UL, (total, result) => total + result.DamageApplied) +
            totalSobNpcAppliedDamage;
        var progressDamage = totalMonsterDamage + totalSobNpcExperienceDamage;
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                progressDamage,
                cancellationToken,
                casterOwner)
            .ConfigureAwait(false);
        if (!casterOwner.TryCurrent(context, updatedSession, out updatedSession)) return true;
        context = context with { Session = updatedSession };
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(totalMonsterDamage + totalSobNpcAppliedDamage),
                cancellationToken,
                activity: "Shock magic weapon proficiency experience")
            .ConfigureAwait(false);
        if (!casterOwner.TryCurrent(context, updatedSession, out updatedSession)) return true;
        context = context with { Session = updatedSession };

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Shock/CrackingShock magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={query.X} targetY={query.Y} radius={LegacyShockRadius} targets={animationTargets.Count} " +
                $"monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"totalMonsterDamage={totalMonsterDamage} totalSobNpcExperienceDamage={totalSobNpcExperienceDamage} " +
                $"totalAppliedDamage={totalAppliedDamage}; source=Shock.cs.")
            .ConfigureAwait(false);
        return true;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyShockMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(query.X, query.Y, candidate.Monster.X, candidate.Monster.Y) <= LegacyShockRadius)
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyShockPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        if (context.Sessions is not { } sessions)
        {
            return targets;
        }

        foreach (var active in sessions.Snapshot())
        {
            if (!ReferenceEquals(active.Connection, context.Connection) &&
                active.Session.Character.Uid != session.Character.Uid &&
                CanSeePlayer(session.Character, active.Session.Character))
            {
                targets.Add(new LegacyPlayerMagicTarget(active));
            }
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyShockSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(query.X, query.Y, candidate.SobNpc.X, candidate.SobNpc.Y) <= LegacyShockRadius)
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static uint CalculateLegacyShockSoulDamage(uint damage, byte useSpellSoul)
    {
        // Source Base.CalculateSoul(Damage, LevelSoul) returns Damage unchanged.
        _ = useSpellSoul;
        return damage;
    }
}
