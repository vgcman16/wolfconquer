using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyBladeFlurryClosePlayerRange = 3;
    private const uint LegacyBladeFlurryClosePlayerDamagePercent = 185;

    private enum LegacySpecialXpSkillAreaDamageKind
    {
        BladeFlurry,
        Omnipotence,
    }

    private readonly record struct LegacySpecialXpSkillAreaDamageSettings(
        LegacySpecialXpSkillAreaDamageKind Kind,
        string SkillName,
        string SourceFile,
        bool InclusiveRange);

    private static async ValueTask<bool> TryHandleLegacySpecialXpSkillNonXpAreaDamageAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        string skillName,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacySpecialXpSkillAreaDamageSettings(spell, skillName, out var settings))
        {
            return false;
        }

        var updatedSession = resourceSession;
        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacySpecialXpSkillAreaMonsterTargets(context, updatedSession, spell, settings)
            : [];
        var playerTargets = SelectLegacySpecialXpSkillAreaPlayerTargets(context, updatedSession, spell, settings);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacySpecialXpSkillAreaSobNpcTargets(context, updatedSession, spell, settings)
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
                var damage = CalculateLegacySpecialXpSkillAreaMonsterDamage(
                    updatedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell,
                    settings,
                    out var effect);
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
                    effect));
            }
        }

        foreach (var target in playerTargets)
        {
            var activeTarget = target.ActiveSession;
            if (context.Sessions?.TryGet(target.ActiveSession.Connection, out var refreshedTarget) == true &&
                refreshedTarget is not null)
            {
                activeTarget = target.ActiveSession with { Session = refreshedTarget };
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

            LegacyPlayerPlayerPhysicalSpellDamageResult rangeDamage;
            if (statusDefended)
            {
                rangeDamage = new LegacyPlayerPlayerPhysicalSpellDamageResult(
                    CalculateLegacyPlayerRangeStatusReturnDamage(
                        updatedSession,
                        activeTarget.Session,
                        context.WorldData),
                    MsgAttackEffect.None);
            }
            else if (wildPhoenixDefended)
            {
                rangeDamage = new LegacyPlayerPlayerPhysicalSpellDamageResult(1U, MsgAttackEffect.None);
            }
            else
            {
                rangeDamage = CalculateLegacySpecialXpSkillAreaPlayerDamageResult(
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    settings,
                    context.WorldData);
            }

            var damage = rangeDamage.Damage;
            damage = CalculateLegacySpecialXpSkillAreaSoulDamage(damage, playerSpell.UseSpellSoul);
            var effect = rangeDamage.Effect;
            LegacyPlayerMagicReceiveOutcome receiveOutcome;
            if (statusDefended || wildPhoenixDefended)
            {
                receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    context,
                    activeTarget,
                    updatedSession.Character,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false);
            }
            else if (HasLegacyStatusFlag(
                         activeTarget.Session.Character,
                         LegacyDefensiveStanceFlag))
            {
                damage = LegacyCombatDamageCalculator.DecreaseLegacyBless(damage, 40);
                effect = MsgAttackEffect.Block;
                receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    context,
                    activeTarget,
                    updatedSession.Character,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false);
            }
            else
            {
                receiveOutcome = await ApplyLegacyBackDamageEligiblePlayerDamageAsync(
                        context,
                        activeTarget,
                        updatedSession.Character,
                        damage,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
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

            if (result.DamageApplied == 0 && !wildPhoenixDefended)
            {
                continue;
            }

            playerResults.Add((result, damage));
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                damage,
                Hit: true,
                effect));
        }

        if (context.SobNpcStates is { } sobNpcStates)
        {
            foreach (var target in sobNpcTargets)
            {
                var damage = CalculateLegacySpecialXpSkillAreaSobNpcDamage(
                    updatedSession,
                    spell,
                    settings,
                    context.WorldData);
                damage = CalculateLegacySpecialXpSkillAreaSoulDamage(damage, playerSpell.UseSpellSoul);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                sobNpcResults.Add((target.SobNpc, result, damage));
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
        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
        foreach (var (target, result, requestedDamage) in monsterResults)
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
            else
            {
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
            context = context with { Session = updatedSession };
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

        var totalRequestedDamage =
            monsterResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.RequestedDamage) +
            playerResults.Aggregate(0UL, (total, result) => total + result.RequestedDamage) +
            sobNpcResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.RequestedDamage);
        var totalAppliedDamage =
            monsterResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            playerResults.Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            sobNpcResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        var totalSobNpcExperienceDamage = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        var progressDamage =
            monsterResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.RequestedDamage) +
            totalSobNpcExperienceDamage;
        var proficiencyDamage =
            monsterResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.RequestedDamage) +
            sobNpcResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
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
                ClampLegacyProficiencyRawExperience(proficiencyDamage),
                cancellationToken,
                activity: $"{settings.SkillName} non-XP special area damage weapon proficiency experience")
            .ConfigureAwait(false);

        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                $"WorldServer handled legacy {settings.SkillName} non-XP special area damage " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} targetX={query.X} targetY={query.Y} casterX={updatedSession.Character.X} " +
                $"casterY={updatedSession.Character.Y} range={spell.Range} targets={animationTargets.Count} " +
                $"monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"totalDamage={totalRequestedDamage} totalAppliedDamage={totalAppliedDamage} progressDamage={progressDamage}; " +
                $"source={settings.SourceFile} non-XP branch.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool TryGetLegacySpecialXpSkillAreaDamageSettings(
        LegacyMagicRuntimeSpell spell,
        string skillName,
        out LegacySpecialXpSkillAreaDamageSettings settings)
    {
        settings = spell.Sort switch
        {
            LegacyMagicSort.BladeFlurry when spell.Id == LegacyBladeFlurrySpellId =>
                new LegacySpecialXpSkillAreaDamageSettings(
                    LegacySpecialXpSkillAreaDamageKind.BladeFlurry,
                    skillName,
                    "BladeFlurry.cs",
                    InclusiveRange: true),
            LegacyMagicSort.Omnipotence when spell.Id == LegacyOmnipotenceSpellId =>
                new LegacySpecialXpSkillAreaDamageSettings(
                    LegacySpecialXpSkillAreaDamageKind.Omnipotence,
                    skillName,
                    "Omnipotence.cs",
                    InclusiveRange: false),
            _ => default,
        };

        return !string.IsNullOrEmpty(settings.SkillName);
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacySpecialXpSkillAreaMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        LegacySpecialXpSkillAreaDamageSettings settings)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (IsLegacySpecialXpSkillAreaTargetInRange(
                    session.Character.X,
                    session.Character.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    spell.Range,
                    settings))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacySpecialXpSkillAreaPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        LegacySpecialXpSkillAreaDamageSettings settings)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (IsLegacySpecialXpSkillAreaTargetInRange(
                    session.Character.X,
                    session.Character.Y,
                    character.X,
                    character.Y,
                    spell.Range,
                    settings))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacySpecialXpSkillAreaSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        LegacySpecialXpSkillAreaDamageSettings settings)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (IsLegacySpecialXpSkillAreaTargetInRange(
                    session.Character.X,
                    session.Character.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    spell.Range,
                    settings))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static bool IsLegacySpecialXpSkillAreaTargetInRange(
        long casterX,
        long casterY,
        long targetX,
        long targetY,
        byte range,
        LegacySpecialXpSkillAreaDamageSettings settings)
    {
        var distance = LegacyGetDistance(casterX, casterY, targetX, targetY);
        return settings.InclusiveRange
            ? distance <= range
            : distance < range;
    }

    private static uint CalculateLegacySpecialXpSkillAreaMonsterDamage(
        WorldSession session,
        WorldDataCatalog? worldData,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        LegacyMagicRuntimeSpell spell,
        LegacySpecialXpSkillAreaDamageSettings settings,
        out MsgAttackEffect effect)
    {
        if (settings.Kind == LegacySpecialXpSkillAreaDamageKind.BladeFlurry)
        {
            var rangeDamage = CalculateLegacyAttackStatus7000MonsterRangeDamage(
                session,
                worldData,
                monster,
                monsterFamily,
                spell);
            effect = rangeDamage.Effect;
            return rangeDamage.Damage;
        }

        effect = MsgAttackEffect.None;
        return CalculateLegacyPlayerMonsterPhysicalSpellDamage(
            session,
            worldData,
            monster,
            monsterFamily,
            spell);
    }

    private static LegacyPlayerPlayerPhysicalSpellDamageResult
        CalculateLegacySpecialXpSkillAreaPlayerDamageResult(
        WorldSession attacker,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        LegacySpecialXpSkillAreaDamageSettings settings,
        WorldDataCatalog? worldData)
    {
        if (settings.Kind == LegacySpecialXpSkillAreaDamageKind.BladeFlurry)
        {
            var rangeSpell = LegacyGetDistance(
                    attacker.Character.X,
                    attacker.Character.Y,
                    target.Character.X,
                    target.Character.Y) < LegacyBladeFlurryClosePlayerRange
                ? spell with
                {
                    Id = 0,
                    Damage = LegacyBladeFlurryClosePlayerDamagePercent,
                }
                : spell with { Id = 0 };
            return CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
                attacker,
                target,
                rangeSpell,
                worldData,
                damageDivisor: 1);
        }

        // Omnipotence calls Range.OnPlayer, whose default branch uses the row damage percent;
        // the physical helper's 12860 override belongs to Physical.OnPlayer instead.
        return CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
            attacker,
            target,
            spell with { Id = 0 },
            worldData,
            damageDivisor: 1);
    }

    private static uint CalculateLegacySpecialXpSkillAreaSobNpcDamage(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        LegacySpecialXpSkillAreaDamageSettings settings,
        WorldDataCatalog? worldData)
    {
        return settings.Kind == LegacySpecialXpSkillAreaDamageKind.BladeFlurry
            ? CalculateLegacyAttackStatus7000SobNpcRangeDamage(session, spell, worldData)
            : CalculateLegacySobNpcPhysicalSpellDamage(session, spell, worldData, damageMultiplier: 1);
    }

    private static uint CalculateLegacySpecialXpSkillAreaSoulDamage(uint damage, byte useSpellSoul)
    {
        // Source Base.CalculateSoul(Damage, LevelSoul) returns Damage unchanged for these local paths.
        _ = useSpellSoul;
        return damage;
    }
}
