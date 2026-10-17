using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyWhirlwindKickSort = 54;
    private const ushort LegacyWhirlwindKickSpellId = 10_415;
    private const ushort LegacyAirSweepSpellId = 12_330;
    private const ushort LegacyAirRaidSpellId = 12_340;
    private const byte LegacyWhirlwindKickRadius = 5;
    private const uint LegacyWhirlwindKickPlayerDamageNumerator = 9;
    private const uint LegacyWhirlwindKickPlayerDamageDenominator = 14;
    private const string RuntimeLegacyWhirlwindKickAcalanathaRollAttribute =
        "LegacyWhirlwindKickAcalanathaRoll";
    private const string RuntimeLegacyWhirlwindKickNatureShieldRollAttribute =
        "LegacyWhirlwindKickNatureShieldRoll";

    private enum LegacyWhirlwindKickPlayerRuneAvoidance
    {
        None,
        NatureShield,
        Acalanatha,
    }

    private static async ValueTask<bool> HandleLegacyPlayerWhirlwindKickMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if ((int)spell.Sort != LegacyWhirlwindKickSort)
        {
            return false;
        }

        // WhirlwindKick.Execute calls CanUseSpell.Verified before its spell-ID switch.
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

        if (!IsLegacyWhirlwindKickSpell(spell.Id))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled source-silent legacy WhirlwindKick magic " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort}; " +
                    "source=matching DecompiledSource WhirlwindKick.cs post-resource spell-ID switch fallthrough.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = resourceSession;
        context = context with { Session = updatedSession };
        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyWhirlwindKickMonsterTargets(context, updatedSession)
            : [];
        var playerTargets = SelectLegacyWhirlwindKickPlayerTargets(context, updatedSession, spell);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyWhirlwindKickSobNpcTargets(context, updatedSession)
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
                requestedDamage = CalculateLegacyWhirlwindKickSoulDamage(
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
                    : CalculateLegacyMortalDragMonsterProgressDamage(
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

        foreach (var target in playerTargets)
        {
            updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                    context,
                    updatedSession,
                    [target],
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };

            var runeAvoidance = GetLegacyWhirlwindKickPlayerRuneAvoidance(
                target.ActiveSession.Session,
                context.WorldData);
            if (runeAvoidance != LegacyWhirlwindKickPlayerRuneAvoidance.None)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer stopped legacy WhirlwindKick player loop " +
                        $"{FormatSession(context)} spellId={spell.Id} targetUid={target.ActiveSession.Session.Character.Uid} " +
                        $"runeAvoidance={runeAvoidance}; source=WhirlwindKick.cs player-loop break.")
                    .ConfigureAwait(false);
                break;
            }

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
            var requestedDamage = CalculateLegacyWhirlwindKickSoulDamage(
                physicalDamage.Damage,
                playerSpell.UseSpellSoul);
            requestedDamage = MulDivToUInt32(
                requestedDamage,
                LegacyWhirlwindKickPlayerDamageNumerator,
                LegacyWhirlwindKickPlayerDamageDenominator);
            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                context,
                activeTarget,
                updatedSession.Character,
                requestedDamage,
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

            playerResults.Add((result, requestedDamage));
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                requestedDamage,
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
                requestedDamage = CalculateLegacyWhirlwindKickSoulDamage(
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

        foreach (var (result, _) in playerResults)
        {
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
        updatedSession = await ApplyLegacyMortalDragIncreaseExperienceAsync(
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
                activity: "WhirlwindKick magic weapon proficiency experience")
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

        var animationOpponentUid = SelectLegacyWhirlwindKickAnimationOpponentUid(spell);
        var animation = new LegacySpellAnimationPacket(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            animationOpponentUid,
            X: 0,
            Y: 0,
            spell.Id,
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
                "WorldServer handled legacy WhirlwindKick magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={query.X} targetY={query.Y} animationOpponentUid={animationOpponentUid} " +
                $"radius={LegacyWhirlwindKickRadius} strictRadius=true " +
                $"targets={animationTargets.Count} monsterTargets={monsterResults.Count} " +
                $"playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"monsterProgress={monsterProgress} sobNpcProgress={sobNpcProgress} " +
                $"progressDamage={progressDamage} totalAppliedDamage={totalAppliedDamage}; " +
                "source=matching DecompiledSource WhirlwindKick.cs sort 54.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyWhirlwindKickSpell(ushort spellId)
    {
        return spellId is LegacyWhirlwindKickSpellId or LegacyAirSweepSpellId or LegacyAirRaidSpellId;
    }

    private static uint SelectLegacyWhirlwindKickAnimationOpponentUid(LegacyMagicRuntimeSpell spell)
    {
        var minimum = spell.MaxTargets / 2;
        var maximum = spell.MaxTargets * 2;
        return checked((uint)(maximum > minimum
            ? Random.Shared.Next(minimum, maximum)
            : minimum));
    }

    private static uint CalculateLegacyWhirlwindKickSoulDamage(uint damage, byte useSpellSoul)
    {
        // Source Base.CalculateSoul(Damage, LevelSoul) returns Damage unchanged.
        _ = useSpellSoul;
        return damage;
    }
}

