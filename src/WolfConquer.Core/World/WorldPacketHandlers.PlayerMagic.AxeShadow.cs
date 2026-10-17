using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const byte LegacyAxeShadowWildwindLineWidth = 2;
    private const string RuntimeAxeShadowBleedDamageAttribute = "BleedDamage";
    private const string RuntimeLegacyAxeShadowBleedDamageAttribute = "LegacyBleedDamage";
    private const uint LegacyAxeShadowBleedDamage = 3_000;

    private static async ValueTask<bool> HandleLegacyPlayerAxeShadowWildwindMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.Wildwind ||
            spell.Id != LegacyAxeShadowStatusSetterSpellId)
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

        var updatedSession = resourceSession;
        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyAxeShadowWildwindMonsterTargets(context, updatedSession, query, spell)
            : [];
        var playerTargets = SelectLegacyAxeShadowWildwindPlayerTargets(context, updatedSession, query, spell);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyAxeShadowWildwindSobNpcTargets(context, updatedSession, query, spell)
            : [];

        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result, uint RuntimeDamage)>();
        var playerResults = new List<(LegacyPlayerMagicDamageResult Result, uint RuntimeDamage)>();
        var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result, uint RuntimeDamage)>();
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
                var runtimeDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    updatedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                runtimeDamage = CalculateLegacyAxeShadowSoulDamage(runtimeDamage, playerSpell.UseSpellSoul);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    runtimeDamage,
                    now,
                    updatedSession);
                monsterResults.Add((target, result, runtimeDamage));
                if (result.AlreadyDead)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    MulDivToUInt32(runtimeDamage, 50, 100),
                    Hit: runtimeDamage > 0,
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
            var runtimeDamage = wildPhoenixDefended
                ? 1U
                : CalculateLegacyPlayerPlayerPhysicalSpellDamage(
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData,
                    damageDivisor: 1);
            runtimeDamage = MulDivToUInt32(
                CalculateLegacyAxeShadowSoulDamage(runtimeDamage, playerSpell.UseSpellSoul),
                7,
                23);
            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                context,
                activeTarget,
                updatedSession.Character,
                runtimeDamage,
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

            result = ApplyLegacyAxeShadowBleedDamage(context, result);
            playerResults.Add((result, runtimeDamage));
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                runtimeDamage,
                Hit: true,
                MsgAttackEffect.None));
        }

        if (context.SobNpcStates is { } sobNpcStates)
        {
            foreach (var target in sobNpcTargets)
            {
                var runtimeDamage = CalculateLegacySobNpcPhysicalSpellDamage(
                    updatedSession,
                    spell,
                    context.WorldData,
                    damageMultiplier: 1);
                runtimeDamage = CalculateLegacyAxeShadowSoulDamage(runtimeDamage, playerSpell.UseSpellSoul);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc, context.WorldData?.StaticStatues);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, runtimeDamage, now, deathBehavior);
                sobNpcResults.Add((target.SobNpc, result, runtimeDamage));
                if (result.AlreadyDead || result.DamageApplied == 0)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                    MulDivToUInt32(runtimeDamage, 50, 100),
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
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
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

        var monsterProgress = monsterResults
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.RuntimeDamage);
        var sobNpcProgress = sobNpcResults
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.RuntimeDamage);
        var progressDamage = monsterProgress + sobNpcProgress;
        updatedSession = await ApplyLegacyAxeShadowIncreaseExperienceAsync(
                context,
                updatedSession,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(progressDamage),
                cancellationToken,
                activity: "AxeShadow Wildwind magic weapon proficiency experience")
            .ConfigureAwait(false);

        var expiresAt = AddLegacyAxeShadowCelestialDanceDuration(DateTimeOffset.UtcNow, spell.Duration);
        updatedSession = updatedSession with
        {
            Character = WithLegacyAxeShadowStatus(updatedSession.Character, spell.Damage3, expiresAt),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        context = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(context, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCombatGearArchiveSkillUpdateAsync(
                context,
                updatedSession,
                LegacyAxeShadowFlag,
                spell.Duration,
                damage: 0,
                spellLevel: 0,
                visible: true,
                cancellationToken)
            .ConfigureAwait(false);

        var totalPlayerDamage = playerResults.Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        var totalAppliedDamage =
            monsterResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            totalPlayerDamage +
            sobNpcResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy AxeShadow Wildwind magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} targetX={query.X} targetY={query.Y} " +
                $"range={spell.Range} width={LegacyAxeShadowWildwindLineWidth} targets={animationTargets.Count} " +
                $"monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"monsterProgress={monsterProgress} sobNpcProgress={sobNpcProgress} totalAppliedDamage={totalAppliedDamage} " +
                $"playerAppliedDamage={totalPlayerDamage} statusFlag={LegacyAxeShadowFlag} axeShadowPower={spell.Damage3} " +
                $"duration={spell.Duration} expiresAtUtc={expiresAt:O}; source=Wildwind.cs case 15940.")
            .ConfigureAwait(false);
        return true;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyAxeShadowWildwindMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (!IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    spell.Range,
                    LegacyAxeShadowWildwindLineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyAxeShadowWildwindPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (!IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    character.X,
                    character.Y,
                    spell.Range,
                    LegacyAxeShadowWildwindLineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyAxeShadowWildwindSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (!IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    spell.Range,
                    LegacyAxeShadowWildwindLineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static LegacyPlayerMagicDamageResult ApplyLegacyAxeShadowBleedDamage(
        WorldPacketHandlerContext context,
        LegacyPlayerMagicDamageResult result)
    {
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                result.UpdatedSession.Character,
                RuntimeAxeShadowBleedDamageAttribute,
                LegacyAxeShadowBleedDamage),
            RuntimeLegacyAxeShadowBleedDamageAttribute,
            LegacyAxeShadowBleedDamage);
        var updatedSession = result.UpdatedSession with { Character = updatedCharacter };
        context.Sessions?.Set(result.ActiveSession.Connection, updatedSession);
        return result with
        {
            ActiveSession = result.ActiveSession with { Session = updatedSession },
            UpdatedSession = updatedSession,
        };
    }

    private static async ValueTask<WorldSession> ApplyLegacyAxeShadowIncreaseExperienceAsync(
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
        var updatedSession = ApplyLegacyExperienceSideEffects(context, context.Connection, session, experienceResult);
        if (experienceResult.AppliedExperienceGain == 0)
        {
            if (experienceResult.OblivionExperienceGain != 0 ||
                experienceResult.HuntingBlessingGain != 0)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy AxeShadow Wildwind experience side effects " +
                        $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                        $"applied=0 oblivion={experienceResult.OblivionExperienceGain} " +
                        $"huntingBlessing={experienceResult.HuntingBlessingGain} suppression={experienceResult.SuppressionReason}; " +
                        "source=Wildwind.cs case 15940 IncreaseExperience.Up.")
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
                "WorldServer sent legacy AxeShadow Wildwind experience " +
                $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                $"gain={experienceResult.AppliedExperienceGain} oblivion={experienceResult.OblivionExperienceGain} " +
                $"huntingBlessing={experienceResult.HuntingBlessingGain} experience={updatedSession.Character.Experience} " +
                $"level={updatedSession.Character.Level}; source=Wildwind.cs case 15940 IncreaseExperience.Up.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static uint CalculateLegacyAxeShadowSoulDamage(uint damage, byte useSpellSoul)
    {
        // Source Base.CalculateSoul(Damage, LevelSoul) returns Damage unchanged for this runtime slice.
        _ = useSpellSoul;
        return damage;
    }
}
