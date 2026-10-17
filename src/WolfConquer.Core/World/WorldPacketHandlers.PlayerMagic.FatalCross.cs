using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyFatalCrossSpellId = 11_980;
    private const byte LegacyFatalCrossRange = 13;
    private const byte LegacyFatalCrossLineWidth = 2;
    private const uint LegacyFatalCrossMonsterDamageMultiplier = 3;
    private const ushort LegacyFatalCrossSuperCycloneStaminaBonus = 20;
    private const ushort LegacyFatalCrossNormalStaminaThreshold = 100;
    private const ushort LegacyFatalCrossHeavenBlessingStaminaThreshold = 150;

    private static async ValueTask<bool> HandleLegacyPlayerFatalCrossMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.FatalCross ||
            spell.Id != LegacyFatalCrossSpellId)
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

        var castPointDistance = LegacyGetDistance(
            resourceSession.Character.X,
            resourceSession.Character.Y,
            query.X,
            query.Y);
        if (castPointDistance > LegacyFatalCrossRange)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy FatalCross magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} " +
                    $"castPointDistance={castPointDistance} range={LegacyFatalCrossRange}; " +
                    "source FatalCross.cs returns after CanUseSpell.Verified.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = await TryApplyLegacyFatalCrossSuperCycloneStaminaRestoreAsync(
                context,
                resourceSession,
                cancellationToken)
            .ConfigureAwait(false);

        var monsterStates = context.MonsterStates;
        var targets = monsterStates is not null
            ? SelectLegacyFatalCrossMonsterTargets(context, updatedSession, query)
            : [];
        var playerTargets = SelectLegacyFatalCrossPlayerTargets(context, updatedSession, query, spell);
        var sobNpcTargets = SelectLegacyFatalCrossSobNpcTargets(context, updatedSession, query);
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
            requestedDamage = MulDivToUInt32(
                requestedDamage,
                LegacyFatalCrossMonsterDamageMultiplier,
                1);
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
            damage = MulDivToUInt32(damage, 22, 28);
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
            if (result.DamageApplied == 0 && !wildPhoenixDefended)
            {
                continue;
            }

            playerResults.Add(result);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                damage,
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
                    MulDivToUInt32(result.DamageApplied, 3, 1),
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

        foreach (var (sobNpc, result) in killedTrainingSobNpcTargets)
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

        var totalDamage = results
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.RequestedDamage);
        var totalSobNpcExperienceDamage = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        var proficiencyExperienceDamage = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + result.Result.DamageApplied);
        var totalAppliedDamage = results
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
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
                activity: "FatalCross SOB magic weapon proficiency experience")
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy FatalCross magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targets={animationTargets.Count} monsterTargets={results.Count} playerTargets={playerResults.Count} " +
                $"sobNpcTargets={sobNpcResults.Count} totalDamage={totalDamage} totalSobNpcExperienceDamage={totalSobNpcExperienceDamage} " +
                $"totalAppliedDamage={totalAppliedDamage}; " +
                "source=FatalCross.cs monster/player/SOB/SuperCyclone-stamina slice.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyFatalCrossSuperCycloneStaminaRestoreAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (!HasLegacyStatusFlag(session.Character, LegacySuperCycloneFlag))
        {
            return session;
        }

        var stamina = ReadLegacyRuntimeStamina(session.Character);
        var threshold = HasLegacyFatalCrossHeavenBlessing(session.Character)
            ? LegacyFatalCrossHeavenBlessingStaminaThreshold
            : LegacyFatalCrossNormalStaminaThreshold;
        if (stamina >= threshold)
        {
            return session;
        }

        var restoredStamina = (ushort)Math.Min(
            ushort.MaxValue,
            stamina + LegacyFatalCrossSuperCycloneStaminaBonus);
        var updatedSession = await ApplyLegacyStaminaToPlayerAsync(
                context,
                context.Connection,
                session,
                restoredStamina,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer restored legacy FatalCross SuperCyclone stamina " +
                $"{FormatSession(context with { Session = updatedSession })} " +
                $"stamina={stamina} restoredStamina={restoredStamina} threshold={threshold}; " +
                "source=FatalCross.cs SuperCyclone stamina branch.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool HasLegacyFatalCrossHeavenBlessing(CharacterSnapshot character)
    {
        if (HasLegacyStatusFlag(character, LegacyHeavenBlessingFlag))
        {
            return true;
        }

        return TryReadUInt64(character.Attributes, out var heavenBlessing, "HeavenBlessing", "HeavensBlessing") &&
            heavenBlessing > 0;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyFatalCrossMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y) > LegacyFatalCrossRange ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    LegacyFatalCrossRange,
                    LegacyFatalCrossLineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyFatalCrossPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.ActiveSession.Session.Character.X,
                    candidate.ActiveSession.Session.Character.Y) > LegacyFatalCrossRange ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.ActiveSession.Session.Character.X,
                    candidate.ActiveSession.Session.Character.Y,
                    LegacyFatalCrossRange,
                    LegacyFatalCrossLineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyFatalCrossSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y) > LegacyFatalCrossRange ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    LegacyFatalCrossRange,
                    LegacyFatalCrossLineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }
}
