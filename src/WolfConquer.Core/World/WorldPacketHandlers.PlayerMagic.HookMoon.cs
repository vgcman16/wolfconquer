using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyHookMoonSpellId = 15_930;
    private const byte LegacyHookMoonLineAlgorithmRange = 11;
    private const byte LegacyHookMoonLineWidth = 2;
    private const int LegacyHookMoonMagicLevelField = 8;
    private const int LegacyHookMoonMagicDamage3Field = 37;
    private const string RuntimeLegacyHookMoonExpiresAtUtcAttribute = "HookMoonExpiresAtUtc";
    private const string RuntimeLegacyHookMoonCasterExpiresAtUtcAttribute = "HookMoonCasterExpiresAtUtc";

    private static async ValueTask<bool> HandleLegacyPlayerHookMoonLineMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.Line ||
            spell.Id != LegacyHookMoonSpellId)
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
            ? SelectLegacyHookMoonMonsterTargets(context, updatedSession, query, spell)
            : [];
        var playerTargets = SelectLegacyHookMoonPlayerTargets(context, updatedSession, query, spell);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyHookMoonSobNpcTargets(context, updatedSession, query, spell)
            : [];

        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result, uint RequestedDamage, uint Progress)>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
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
                requestedDamage = CalculateLegacyHookMoonSoulDamage(requestedDamage, playerSpell.UseSpellSoul);
                requestedDamage = MulDivToUInt32(requestedDamage, 2, 3);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    requestedDamage,
                    now,
                    updatedSession);
                var progress = result.AlreadyDead ? 0 : requestedDamage;
                monsterResults.Add((target, result, requestedDamage, progress));
                if (result.AlreadyDead)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    LegacyCombatDamageCalculator.AddLegacySaturating(result.RequestedDamage, result.RequestedDamage),
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
            damage = CalculateLegacyHookMoonSoulDamage(damage, playerSpell.UseSpellSoul);
            damage = MulDivToUInt32(damage, 7, 16);
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

            result = ApplyLegacyHookMoonTargetStatus(
                context,
                result,
                spell,
                now,
                out var addedHookMoonTargetStatus);
            if (addedHookMoonTargetStatus)
            {
                await SendLegacyHookMoonArchiveSkillUpdateAsync(
                        result.ActiveSession.Connection,
                        result.UpdatedSession,
                        LegacyHookMoonFlag,
                        spell.Duration,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            playerResults.Add(result);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                result.DamageApplied,
                Hit: true,
                MsgAttackEffect.None));
        }

        var sobNpcProgress = 0UL;
        if (context.SobNpcStates is { } sobNpcStates)
        {
            foreach (var target in sobNpcTargets)
            {
                var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                    updatedSession,
                    spell,
                    context.WorldData,
                    damageMultiplier: 1);
                damage = CalculateLegacyHookMoonSoulDamage(damage, playerSpell.UseSpellSoul);
                damage = MulDivToUInt32(damage, 50, 100);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                sobNpcResults.Add((target.SobNpc, result));
                if (!result.AlreadyDead)
                {
                    sobNpcProgress += result.DamageApplied;
                }
                if (result.AlreadyDead || result.DamageApplied == 0)
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

        var hookMoonDamage3 = ReadLegacyHookMoonDamage3SourceEvidence(context.WorldData, playerSpell, spell);
        updatedSession = ApplyLegacyHookMoonCasterStatus(
            updatedSession,
            spell,
            hookMoonDamage3,
            now,
            out var addedHookMoonCasterStatus);
        context.Sessions?.Set(context.Connection, updatedSession);
        context = context with { Session = updatedSession };
        if (addedHookMoonCasterStatus)
        {
            await SendLegacyHookMoonArchiveSkillUpdateAsync(
                    context.Connection,
                    updatedSession,
                    LegacyHookMoonCasterFlag,
                    spell.Duration,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy HookMoon caster status " +
                    $"{FormatSession(context)} spellId={spell.Id} statusFlag={LegacyHookMoonCasterFlag} " +
                    $"duration={spell.Duration} damage3Source={hookMoonDamage3} hookMoonPower={hookMoonDamage3 % 1_000}; " +
                    "source=Line.cs case 15930 AddFlag(HookMoonCaster) and ArchiveSkill.")
                .ConfigureAwait(false);
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

        foreach (var result in playerResults)
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

        var monsterProgress = monsterResults.Aggregate(0UL, (total, result) => total + result.Progress);
        var progressDamage = monsterProgress + sobNpcProgress;
        updatedSession = await ApplyLegacyHookMoonIncreaseExperienceAsync(
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
                activity: "HookMoon Line magic weapon proficiency experience")
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
                "WorldServer handled legacy HookMoon Line magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={query.X} targetY={query.Y} range={spell.Range} algorithmRange={LegacyHookMoonLineAlgorithmRange} " +
                $"width={LegacyHookMoonLineWidth} targets={animationTargets.Count} monsterTargets={monsterResults.Count} " +
                $"playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"monsterProgress={monsterProgress} sobNpcProgress={sobNpcProgress} totalAppliedDamage={totalAppliedDamage}; " +
                "source=Line.cs case 15930.")
            .ConfigureAwait(false);
        return true;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyHookMoonMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (IsLegacyGuardMonsterFamily(candidate.Monster, candidate.MonsterFamily) ||
                LegacyGetDistance(session.Character.X, session.Character.Y, candidate.Monster.X, candidate.Monster.Y) >= spell.Range ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    LegacyHookMoonLineAlgorithmRange,
                    LegacyHookMoonLineWidth) ||
                !CanLegacyPlayerAttackMonster(session.Character, candidate.Monster, candidate.MonsterFamily, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyHookMoonPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(session.Character.X, session.Character.Y, character.X, character.Y) >= spell.Range ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    character.X,
                    character.Y,
                    LegacyHookMoonLineAlgorithmRange,
                    LegacyHookMoonLineWidth) ||
                !CanLegacyPlayerMagicDamageTarget(context, session, candidate.ActiveSession.Session, spell, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyHookMoonSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(session.Character.X, session.Character.Y, candidate.SobNpc.X, candidate.SobNpc.Y) >= spell.Range ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    LegacyHookMoonLineAlgorithmRange,
                    LegacyHookMoonLineWidth) ||
                !CanLegacySobNpcMagicDamageTarget(context, session.Character, candidate.SobNpc, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static async ValueTask<WorldSession> ApplyLegacyHookMoonIncreaseExperienceAsync(
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
                        "WorldServer applied legacy HookMoon Line experience side effects " +
                        $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                        $"applied=0 oblivion={experienceResult.OblivionExperienceGain} " +
                        $"huntingBlessing={experienceResult.HuntingBlessingGain} suppression={experienceResult.SuppressionReason}; " +
                        "source=Line.cs case 15930 IncreaseExperience.Up.")
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
                "WorldServer sent legacy HookMoon Line experience " +
                $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                $"gain={experienceResult.AppliedExperienceGain} oblivion={experienceResult.OblivionExperienceGain} " +
                $"huntingBlessing={experienceResult.HuntingBlessingGain} experience={updatedSession.Character.Experience} " +
                $"level={updatedSession.Character.Level}; source=Line.cs case 15930 IncreaseExperience.Up.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static uint ReadLegacyHookMoonDamage3SourceEvidence(
        WorldDataCatalog? worldData,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        if (worldData is null ||
            !worldData.MagicByTypeId.TryGetValue(spell.Id, out var rows))
        {
            return 0;
        }

        foreach (var row in rows)
        {
            if (row.Fields.Count <= LegacyHookMoonMagicDamage3Field)
            {
                continue;
            }

            if (row.Fields.Count > LegacyHookMoonMagicLevelField &&
                ushort.TryParse(row.Fields[LegacyHookMoonMagicLevelField], out var rowLevel) &&
                rowLevel != playerSpell.Level)
            {
                continue;
            }

            if (TryParseUInt32(row.Fields[LegacyHookMoonMagicDamage3Field], out var damage3))
            {
                return damage3;
            }
        }

        return 0;
    }

    private static LegacyPlayerMagicDamageResult ApplyLegacyHookMoonTargetStatus(
        WorldPacketHandlerContext context,
        LegacyPlayerMagicDamageResult result,
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset nowUtc,
        out bool addedStatus)
    {
        if (HasLegacyStatusFlag(result.UpdatedSession.Character, LegacyHookMoonFlag))
        {
            addedStatus = false;
            return result;
        }

        var attackedPower = CalculateLegacyHookMoonAttackedPower(spell);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(
                    WithLegacyStatusFlag(result.UpdatedSession.Character, LegacyHookMoonFlag),
                    "HookMoon",
                    "true"),
                "HookMoonAttackedPower",
                attackedPower.ToString(CultureInfo.InvariantCulture)),
            RuntimeLegacyHookMoonExpiresAtUtcAttribute,
            AddLegacyHookMoonDuration(nowUtc, spell.Duration).ToString("O", CultureInfo.InvariantCulture));
        var updatedSession = result.UpdatedSession with { Character = updatedCharacter };
        context.Sessions?.Set(result.ActiveSession.Connection, updatedSession);
        addedStatus = true;
        return result with
        {
            ActiveSession = result.ActiveSession with { Session = updatedSession },
            UpdatedSession = updatedSession,
        };
    }

    private static WorldSession ApplyLegacyHookMoonCasterStatus(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        uint damage3,
        DateTimeOffset nowUtc,
        out bool addedStatus)
    {
        if (HasLegacyStatusFlag(session.Character, LegacyHookMoonCasterFlag))
        {
            addedStatus = false;
            return session;
        }

        var hookMoonPower = damage3 % 1_000;
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(
                    WithLegacyStatusFlag(session.Character, LegacyHookMoonCasterFlag),
                    "HookMoonCaster",
                    "true"),
                "HookMoonPower",
                hookMoonPower.ToString(CultureInfo.InvariantCulture)),
            RuntimeLegacyHookMoonCasterExpiresAtUtcAttribute,
            AddLegacyHookMoonDuration(nowUtc, spell.Duration).ToString("O", CultureInfo.InvariantCulture));

        addedStatus = true;
        return session with { Character = updatedCharacter };
    }

    private static uint CalculateLegacyHookMoonAttackedPower(LegacyMagicRuntimeSpell spell)
    {
        return spell.Level >= 7 ? 150U : 170U;
    }

    private static DateTimeOffset AddLegacyHookMoonDuration(DateTimeOffset nowUtc, uint durationSeconds)
    {
        var normalizedNow = nowUtc.ToUniversalTime();
        if (durationSeconds == 0)
        {
            return normalizedNow;
        }

        var maximumDuration = DateTimeOffset.MaxValue - normalizedNow;
        return durationSeconds >= maximumDuration.TotalSeconds
            ? DateTimeOffset.MaxValue
            : normalizedNow.AddSeconds(durationSeconds);
    }

    private static ValueTask SendLegacyHookMoonArchiveSkillUpdateAsync(
        ConquerTcpConnection connection,
        WorldSession session,
        int statusFlag,
        uint duration,
        CancellationToken cancellationToken)
    {
        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.ArchiveSkill,
                    Value: 0,
                    Flag: checked((uint)statusFlag),
                    Time: duration),
            ]);
        return connection.SendAsync(packet.ToFrameBytes(), cancellationToken);
    }

    private static uint CalculateLegacyHookMoonSoulDamage(uint damage, byte useSpellSoul)
    {
        _ = useSpellSoul;
        return damage;
    }
}
