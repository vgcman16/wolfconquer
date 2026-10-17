using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacySwirlingStormSort = 91;
    private const byte LegacySwirlingStormLineWidth = 2;
    private const ushort LegacySwirlingStormStaminaBonus = 10;
    private const uint LegacySwirlingStormPlayerDamageNumerator = 9;
    private const uint LegacySwirlingStormPlayerDamageDenominator = 15;
    private const uint LegacySwirlingStormSobNpcDamageNumerator = 40;
    private const uint LegacySwirlingStormSobNpcDamageDenominator = 100;

    private static async Task<bool> HandleLegacyPlayerSwirlingStormMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if ((int)spell.Sort != LegacySwirlingStormSort ||
            spell.Id != LegacySwirlingStormSpellId)
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

        var updatedSession = await TryApplyLegacySwirlingStormOmnipotenceStaminaAsync(
                context,
                resourceSession,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacySwirlingStormMonsterTargets(context, updatedSession, query, spell)
            : [];
        var playerTargets = SelectLegacySwirlingStormPlayerTargets(
            context,
            updatedSession,
            query,
            spell);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacySwirlingStormSobNpcTargets(context, updatedSession, query, spell)
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
            uint RuntimeDamage,
            uint Progress)>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var sobNpcResults = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            uint RuntimeDamage,
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
                var rangeDamage = CalculateLegacyAttackStatus7000MonsterRangeDamage(
                    updatedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                var runtimeDamage = CalculateLegacySwirlingStormSoulDamage(
                    rangeDamage.Damage,
                    playerSpell.UseSpellSoul);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    runtimeDamage,
                    now,
                    updatedSession);
                var progress = result.AlreadyDead
                    ? 0
                    : CalculateLegacySwirlingStormMonsterProgress(updatedSession, target, runtimeDamage);
                monsterResults.Add((target, result, runtimeDamage, progress));
                if (result.AlreadyDead)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    result.RequestedDamage,
                    Hit: true,
                    rangeDamage.Effect));
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
            var damage = CalculateLegacySwirlingStormSoulDamage(
                physicalDamage.Damage,
                playerSpell.UseSpellSoul);
            damage = MulDivToUInt32(
                damage,
                LegacySwirlingStormPlayerDamageNumerator,
                LegacySwirlingStormPlayerDamageDenominator);
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

            playerResults.Add(result);
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
                Hit: false,
                physicalDamage.Effect));
        }

        if (context.SobNpcStates is { } sobNpcStates)
        {
            foreach (var target in sobNpcTargets)
            {
                var rangeDamage = CalculateLegacyAttackStatus7000SobNpcRangeDamage(
                    updatedSession,
                    spell,
                    context.WorldData);
                var damage = MulDivToUInt32(
                    rangeDamage,
                    LegacySwirlingStormSobNpcDamageNumerator,
                    LegacySwirlingStormSobNpcDamageDenominator);
                damage = CalculateLegacySwirlingStormSoulDamage(damage, playerSpell.UseSpellSoul);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
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
        updatedSession = await ApplyLegacySwirlingStormIncreaseExperienceAsync(
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
                activity: "SwirlingStorm magic weapon proficiency experience")
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

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
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
            playerResults.Aggregate(0UL, (total, result) => total + result.DamageApplied) +
            sobNpcResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy SwirlingStorm magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={query.X} targetY={query.Y} range={spell.Range} " +
                $"width={LegacySwirlingStormLineWidth} increasedRange=true " +
                $"targets={animationTargets.Count} monsterTargets={monsterResults.Count} " +
                $"playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"monsterProgress={monsterProgress} sobNpcProgress={sobNpcProgress} " +
                $"progressDamage={progressDamage} totalAppliedDamage={totalAppliedDamage}; " +
                "source=matching DecompiledSource SwirlingStorm.cs spell 12890 sort 91.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession> TryApplyLegacySwirlingStormOmnipotenceStaminaAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (!HasLegacyStatusFlag(session.Character, LegacyOmnipotenceFlag))
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

        var restoredStamina = checked((ushort)(stamina + LegacySwirlingStormStaminaBonus));
        var updatedSession = await ApplyLegacyStaminaToPlayerAsync(
                context,
                context.Connection,
                session,
                restoredStamina,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer restored legacy SwirlingStorm Omnipotence stamina " +
                $"{FormatSession(context with { Session = updatedSession })} stamina={stamina} " +
                $"restoredStamina={restoredStamina} threshold={threshold}; " +
                "source=SwirlingStorm.cs Omnipotence +10 branch.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacySwirlingStormMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (!IsLegacyMortalStrikeInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    spell.Range,
                    LegacySwirlingStormLineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacySwirlingStormPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (!IsLegacyMortalStrikeInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    character.X,
                    character.Y,
                    spell.Range,
                    LegacySwirlingStormLineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacySwirlingStormSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (!IsLegacyMortalStrikeInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    spell.Range,
                    LegacySwirlingStormLineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static uint CalculateLegacySwirlingStormMonsterProgress(
        WorldSession session,
        LegacyPlayerMonsterMagicTarget target,
        uint requestedDamage)
    {
        if (requestedDamage == 0 ||
            LegacyMonsterDamageRules.IsLegacyGuardFamily(target.MonsterFamily))
        {
            return 0;
        }

        return checked((uint)AdjustLegacyMonsterExperience(
            checked((int)Math.Min(requestedDamage, target.Monster.MaxLife)),
            CheckedInt32(session.Character.Level, nameof(session.Character.Level)),
            target.Monster.Level));
    }

    private static async ValueTask<WorldSession> ApplyLegacySwirlingStormIncreaseExperienceAsync(
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
                        "WorldServer applied legacy SwirlingStorm experience side effects " +
                        $"{FormatSession(context with { Session = updatedSession })} " +
                        $"progressDamage={boundedProgress} applied=0 " +
                        $"oblivion={experienceResult.OblivionExperienceGain} " +
                        $"huntingBlessing={experienceResult.HuntingBlessingGain} " +
                        $"suppression={experienceResult.SuppressionReason}; " +
                        "source=SwirlingStorm.cs IncreaseExperience.Up.")
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
                "WorldServer sent legacy SwirlingStorm experience " +
                $"{FormatSession(context with { Session = updatedSession })} " +
                $"progressDamage={boundedProgress} gain={experienceResult.AppliedExperienceGain} " +
                $"oblivion={experienceResult.OblivionExperienceGain} " +
                $"huntingBlessing={experienceResult.HuntingBlessingGain} " +
                $"experience={updatedSession.Character.Experience} level={updatedSession.Character.Level}; " +
                "source=SwirlingStorm.cs IncreaseExperience.Up.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static uint CalculateLegacySwirlingStormSoulDamage(uint damage, byte useSpellSoul)
    {
        // Source Base.CalculateSoul(Damage, LevelSoul) returns Damage unchanged.
        _ = useSpellSoul;
        return damage;
    }
}
