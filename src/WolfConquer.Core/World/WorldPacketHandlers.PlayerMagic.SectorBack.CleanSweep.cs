using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyCleanSweepSpellId = 15_870;
    private const byte LegacyCleanSweepLineWidth = 2;
    private const int LegacyCleanSweepFlag = 0xFE;
    private const string RuntimeCleanSweepPowerAttribute = "CleanSweepPower";
    private const string RuntimeCleanSweepExpiresAtUtcAttribute = "CleanSweepExpiresAtUtc";
    private const string RuntimeLegacyCleanSweepPowerAttribute = "LegacyCleanSweepPower";
    private const string RuntimeLegacyCleanSweepExpiresAtUtcAttribute = "LegacyCleanSweepExpiresAtUtc";

    private static async ValueTask<bool> HandleLegacyPlayerCleanSweepSectorBackMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.SectorBack ||
            spell.Id != LegacyCleanSweepSpellId)
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
            ? SelectLegacyCleanSweepMonsterTargets(context, updatedSession, query, spell)
            : [];
        var playerTargets = SelectLegacyCleanSweepPlayerTargets(context, updatedSession, query, spell);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyCleanSweepSobNpcTargets(context, updatedSession, query, spell)
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
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
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
                var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    updatedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                damage = CalculateLegacyCleanSweepSoulDamage(damage, playerSpell.UseSpellSoul) / 2;
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
            damage = MulDivToUInt32(
                CalculateLegacyCleanSweepSoulDamage(damage, playerSpell.UseSpellSoul),
                5,
                12);
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
            if (result.DamageApplied == 0 && !wildPhoenixDefended)
            {
                continue;
            }

            playerResults.Add(result);
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                result.DamageApplied,
                Hit: false,
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
                damage = CalculateLegacyCleanSweepSoulDamage(damage, playerSpell.UseSpellSoul);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                sobNpcResults.Add((target.SobNpc, result, damage));
                if (result.AlreadyDead || result.DamageApplied == 0)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                    MulDivToUInt32(damage, 50, 100),
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

        var monsterProgress = monsterResults
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.RuntimeDamage);
        var sobNpcProgress = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        var proficiencyExperienceDamage = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + result.Result.DamageApplied);
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                monsterProgress + sobNpcProgress,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(proficiencyExperienceDamage),
                cancellationToken,
                activity: "CleanSweep SectorBack SOB magic weapon proficiency experience")
            .ConfigureAwait(false);

        var statusExpiresAt = AddLegacyCleanSweepDuration(DateTimeOffset.UtcNow, spell.Duration);
        updatedSession = ApplyLegacyCleanSweepStatus(updatedSession, spell, statusExpiresAt);
        context.Sessions?.Set(context.Connection, updatedSession);
        context = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(context, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCombatGearArchiveSkillUpdateAsync(
                context,
                updatedSession,
                LegacyCleanSweepFlag,
                spell.Duration,
                spell.Damage3,
                spell.Damage3,
                visible: true,
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
                "WorldServer handled legacy CleanSweep SectorBack magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} targetX={query.X} targetY={query.Y} " +
                $"range={spell.Range} width={LegacyCleanSweepLineWidth} targets={animationTargets.Count} " +
                $"monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"monsterProgress={monsterProgress} sobNpcProgress={sobNpcProgress} totalAppliedDamage={totalAppliedDamage} " +
                $"statusFlag={LegacyCleanSweepFlag} cleanSweepPower={spell.Damage3} duration={spell.Duration} " +
                $"expiresAtUtc={statusExpiresAt:O}; source=SectorBack.cs case 15870.")
            .ConfigureAwait(false);
        return true;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyCleanSweepMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        return SelectLegacyNebulousHuntMonsterTargets(context, session, query, spell);
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyCleanSweepPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        return SelectLegacyNebulousHuntPlayerTargets(context, session, query, spell);
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyCleanSweepSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        return SelectLegacyNebulousHuntSobNpcTargets(context, session, query, spell);
    }

    private static WorldSession ApplyLegacyCleanSweepStatus(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset expiresAtUtc)
    {
        var formattedExpiresAt = expiresAtUtc.ToString("O", CultureInfo.InvariantCulture);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(
                    WithCharacterAttribute(
                        WithLegacyStatusFlag(session.Character, LegacyCleanSweepFlag),
                        RuntimeCleanSweepPowerAttribute,
                        spell.Damage3),
                    RuntimeCleanSweepExpiresAtUtcAttribute,
                    formattedExpiresAt),
                RuntimeLegacyCleanSweepPowerAttribute,
                spell.Damage3),
            RuntimeLegacyCleanSweepExpiresAtUtcAttribute,
            formattedExpiresAt);
        return session with { Character = updatedCharacter };
    }

    private static DateTimeOffset AddLegacyCleanSweepDuration(DateTimeOffset nowUtc, uint durationSeconds)
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

    private static uint CalculateLegacyCleanSweepSoulDamage(uint damage, byte useSpellSoul)
    {
        // Source Base.CalculateSoul(Damage, LevelSoul) returns Damage unchanged.
        _ = useSpellSoul;
        return damage;
    }
}
