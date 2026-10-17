using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyCombatGearCenteredAreaSpellId21490 = 21_490;
    private const ushort LegacyCombatGearCenteredAreaSpellId21510 = 21_510;
    private const ushort LegacyCombatGearCenteredAreaSpellId21600 = 21_600;
    private const ushort LegacyCombatGearCenteredAreaSpellId21670 = 21_670;
    private const ushort LegacyCombatGearCenteredAreaSpellId21690 = 21_690;
    private const ushort LegacyCombatGearCenteredAreaSpellId21920 = 21_920;
    private const uint LegacyCombatGearCenteredAreaPlayerDamagePercent = 75;

    private static async ValueTask<bool> HandleLegacyCombatGearCenteredAreaAttackMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyCombatGearCenteredAreaAttackSpell(spell.Id))
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

        return await ExecuteLegacyPreparedCombatGearCenteredAreaAttackMagicAsync(
                context, resourceSession, query, playerSpell, spell, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> ExecuteLegacyPreparedCombatGearCenteredAreaAttackMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var updatedSession = resourceSession;
        var radius = GetLegacyCombatGearCenteredAreaRadius(spell);
        var radiusSource = GetLegacyCombatGearCenteredAreaRadiusSource(spell.Id);
        var targetCenterX = GetLegacyCombatGearCenteredAreaTargetCenterX(updatedSession, query, spell.Id);
        var targetCenterY = GetLegacyCombatGearCenteredAreaTargetCenterY(updatedSession, query, spell.Id);
        var centerSource = GetLegacyCombatGearCenteredAreaTargetCenterSource(spell.Id);
        var sourceCase = GetLegacyCombatGearCenteredAreaSourceCase(spell.Id);
        var appliesProgress = AppliesLegacyCombatGearCenteredAreaProgress(spell.Id);
        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyCombatGearCenteredAreaMonsterTargets(
                context,
                updatedSession,
                targetCenterX,
                targetCenterY,
                radius)
            : [];
        var playerTargets = SelectLegacyCombatGearCenteredAreaPlayerTargets(
            context,
            updatedSession,
            targetCenterX,
            targetCenterY,
            spell,
            radius);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyCombatGearCenteredAreaSobNpcTargets(
                context,
                updatedSession,
                targetCenterX,
                targetCenterY,
                radius)
            : [];

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
            uint Damage,
            ulong Progress)>();
        var playerResults = new List<(LegacyPlayerMagicDamageResult Result, uint Damage)>();
        var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result, uint Damage, ulong Progress)>();
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
                damage = CalculateLegacyCombatGearCenteredAreaSoulDamage(damage, playerSpell.UseSpellSoul);
                damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                    updatedSession,
                    spell,
                    damage,
                    LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    damage,
                    now,
                    updatedSession);
                var progress = result.AlreadyDead ? 0 : (ulong)damage;
                monsterResults.Add((target, result, damage, progress));
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
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData,
                    damageDivisor: 1);
            damage = CalculateLegacyCombatGearCenteredAreaSoulDamage(damage, playerSpell.UseSpellSoul);
            damage = MulDivToUInt32(damage, LegacyCombatGearCenteredAreaPlayerDamagePercent, 100);
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
            playerResults.Add((result, damage));
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
                damage = CalculateLegacyCombatGearCenteredAreaSoulDamage(damage, playerSpell.UseSpellSoul);
                damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                    updatedSession,
                    spell,
                    damage,
                    LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(
                    target.SobNpc,
                    context.WorldData?.StaticStatues);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                var progress = result.AlreadyDead ? 0 : CalculateLegacySobNpcExperienceDamage(target.SobNpc, result);
                sobNpcResults.Add((target.SobNpc, result, damage, progress));
                if (result.AlreadyDead)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                    result.DamageApplied,
                    Hit: result.DamageApplied > 0,
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

        var monsterProgress = monsterResults.Aggregate(0UL, (total, result) => total + result.Progress);
        var sobNpcProgress = sobNpcResults.Aggregate(0UL, (total, result) => total + result.Progress);
        var progressDamage = monsterProgress + sobNpcProgress;
        if (appliesProgress)
        {
            updatedSession = await ApplyLegacyCombatGearDirectLineProgressAsync(
                    context,
                    updatedSession,
                    playerSpell,
                    spell,
                    progressDamage,
                    sourceCase,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        foreach (var frame in animation.ToFrames())
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

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
                await LogLegacySobNpcEventSideEffectGapAsync(context, sobNpc, result, spell)
                    .ConfigureAwait(false);
            }
        }

        var totalAppliedDamage =
            monsterResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            playerResults.Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            sobNpcResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy CombatGear centered area magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetCenterX={targetCenterX} targetCenterY={targetCenterY} centerSource={centerSource} " +
                $"animationX={query.X} animationY={query.Y} radius={radius} radiusSource={radiusSource} rowRange={spell.Range} " +
                $"playerDamagePercent={LegacyCombatGearCenteredAreaPlayerDamagePercent} targets={animationTargets.Count} " +
                $"monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"animationFanout=casterOnly progressApplied={appliesProgress} monsterProgress={monsterProgress} " +
                $"sobNpcProgress={sobNpcProgress} spellExperienceApplied={appliesProgress} " +
                $"characterExperienceApplied={appliesProgress} totalAppliedDamage={totalAppliedDamage}; source={sourceCase}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyCombatGearCenteredAreaAttackSpell(ushort spellId)
    {
        return spellId is
            LegacyCombatGearCenteredAreaSpellId21490 or
            LegacyCombatGearCenteredAreaSpellId21510 or
            LegacyCombatGearCenteredAreaSpellId21600 or
            LegacyCombatGearCenteredAreaSpellId21670 or
            LegacyCombatGearCenteredAreaSpellId21690 or
            LegacyCombatGearCenteredAreaSpellId21920;
    }

    private static byte GetLegacyCombatGearCenteredAreaRadius(LegacyMagicRuntimeSpell spell)
    {
        return UsesLegacyCombatGearCenteredAreaFullMaxTargetsRadius(spell.Id)
            ? spell.MaxTargets
            : (byte)(spell.MaxTargets / 2);
    }

    private static string GetLegacyCombatGearCenteredAreaRadiusSource(ushort spellId)
    {
        return UsesLegacyCombatGearCenteredAreaFullMaxTargetsRadius(spellId)
            ? "maxTargets"
            : "maxTargetsHalf";
    }

    private static ushort GetLegacyCombatGearCenteredAreaTargetCenterX(
        WorldSession session,
        MsgAttackQuery query,
        ushort spellId)
    {
        return spellId == LegacyCombatGearCenteredAreaSpellId21490
            ? CheckedUInt16(session.Character.X, nameof(session.Character.X))
            : query.X;
    }

    private static ushort GetLegacyCombatGearCenteredAreaTargetCenterY(
        WorldSession session,
        MsgAttackQuery query,
        ushort spellId)
    {
        return spellId == LegacyCombatGearCenteredAreaSpellId21490
            ? CheckedUInt16(session.Character.Y, nameof(session.Character.Y))
            : query.Y;
    }

    private static string GetLegacyCombatGearCenteredAreaTargetCenterSource(ushort spellId)
    {
        return spellId == LegacyCombatGearCenteredAreaSpellId21490 ? "caster" : "query";
    }

    private static string GetLegacyCombatGearCenteredAreaSourceCase(ushort spellId)
    {
        return spellId switch
        {
            LegacyCombatGearCenteredAreaSpellId21490 => "CombatGearAttack.cs case 21490",
            LegacyCombatGearCenteredAreaSpellId21920 => "CombatGearAttack.cs case 21920",
            LegacyCombatGearCenteredAreaSpellId21600 or LegacyCombatGearCenteredAreaSpellId21690 =>
                "CombatGearAttack.cs cases 21600/21690",
            _ => "CombatGearAttack.cs cases 21510/21670",
        };
    }

    private static bool AppliesLegacyCombatGearCenteredAreaProgress(ushort spellId)
    {
        return spellId == LegacyCombatGearCenteredAreaSpellId21490;
    }

    private static bool UsesLegacyCombatGearCenteredAreaFullMaxTargetsRadius(ushort spellId)
    {
        return spellId is LegacyCombatGearCenteredAreaSpellId21600 or LegacyCombatGearCenteredAreaSpellId21690;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyCombatGearCenteredAreaMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        ushort centerX,
        ushort centerY,
        byte radius)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(centerX, centerY, candidate.Monster.X, candidate.Monster.Y) > radius ||
                !CanLegacyPlayerAttackMonster(session.Character, candidate.Monster, candidate.MonsterFamily, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyCombatGearCenteredAreaPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        ushort centerX,
        ushort centerY,
        LegacyMagicRuntimeSpell spell,
        byte radius)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(centerX, centerY, character.X, character.Y) > radius ||
                !CanLegacyPlayerMagicDamageTarget(context, session, candidate.ActiveSession.Session, spell, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyCombatGearCenteredAreaSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        ushort centerX,
        ushort centerY,
        byte radius)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(centerX, centerY, candidate.SobNpc.X, candidate.SobNpc.Y) > radius ||
                !CanLegacySobNpcMagicDamageTarget(context, session.Character, candidate.SobNpc, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static uint CalculateLegacyCombatGearCenteredAreaSoulDamage(uint damage, byte useSpellSoul)
    {
        _ = useSpellSoul;
        return damage;
    }
}
