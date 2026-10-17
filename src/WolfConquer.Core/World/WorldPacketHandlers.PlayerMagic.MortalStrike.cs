using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyMortalStrikeSpellId = 11_990;
    private const byte LegacyMortalStrikeLineWidth = 2;
    private const byte LegacyMortalStrikeLineRange = 5;
    private const byte LegacyMortalStrikePlayerDistance = 2;
    private const uint LegacyMortalStrikePlayerDamageNumerator = 10;
    private const uint LegacyMortalStrikePlayerDamageDenominator = 16;

    private static async ValueTask<bool> HandleLegacyPlayerMortalStrikeSectorBackMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.SectorBack ||
            spell.Id != LegacyMortalStrikeSpellId)
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
            ? SelectLegacyMortalStrikeMonsterTargets(context, updatedSession, query)
            : [];
        var playerTargets = SelectLegacyMortalStrikePlayerTargets(context, updatedSession, query, spell);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyMortalStrikeSobNpcTargets(context, updatedSession, query)
            : [];

        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var monsterResults = new List<(
            LegacyPlayerMonsterMagicTarget Target,
            MonsterDamageResult Result,
            uint Damage,
            uint Progress)>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result, uint Damage)>();
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
                damage = CalculateLegacyMortalStrikeSoulDamage(damage, playerSpell.UseSpellSoul);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    damage,
                    now,
                    updatedSession);
                var progress = result.AlreadyDead
                    ? 0
                    : CalculateLegacyMortalStrikeMonsterProgress(updatedSession, target, damage);
                monsterResults.Add((target, result, damage, progress));
                if (!result.AlreadyDead)
                {
                    await SendLegacyMortalStrikeAnimationAsync(
                            context,
                            updatedSession,
                            query,
                            playerSpell,
                            spell,
                            new LegacySpellAnimationTarget(
                                target.Monster.Uid,
                                result.RequestedDamage,
                                Hit: result.RequestedDamage > 0,
                                MsgAttackEffect.None),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
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
                CalculateLegacyMortalStrikeSoulDamage(damage, playerSpell.UseSpellSoul),
                LegacyMortalStrikePlayerDamageNumerator,
                LegacyMortalStrikePlayerDamageDenominator);
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
            await SendLegacyMortalStrikeAnimationAsync(
                    context,
                    updatedSession,
                    query,
                    playerSpell,
                    spell,
                    new LegacySpellAnimationTarget(
                        CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                        result.DamageApplied,
                        Hit: false,
                        MsgAttackEffect.None),
                    cancellationToken)
                .ConfigureAwait(false);
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
                damage = CalculateLegacyMortalStrikeSoulDamage(damage, playerSpell.UseSpellSoul);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                sobNpcResults.Add((target.SobNpc, result, damage));
                if (result.AlreadyDead || result.DamageApplied == 0)
                {
                    continue;
                }

                await SendLegacyMortalStrikeAnimationAsync(
                        context,
                        updatedSession,
                        query,
                        playerSpell,
                        spell,
                        new LegacySpellAnimationTarget(
                            CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                            result.DamageApplied,
                            Hit: true,
                            MsgAttackEffect.None),
                        cancellationToken)
                    .ConfigureAwait(false);

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

        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
        foreach (var (target, result, damage, _) in monsterResults)
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
                    damage,
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
            .Aggregate(0UL, (total, result) => total + result.Progress);
        var sobNpcProgress = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        var progressDamage = monsterProgress + sobNpcProgress;
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
                activity: "MortalStrike SectorBack magic weapon proficiency experience")
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
                "WorldServer handled legacy MortalStrike SectorBack magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={query.X} targetY={query.Y} range={LegacyMortalStrikeLineRange} width={LegacyMortalStrikeLineWidth} " +
                $"targets={monsterResults.Count + playerResults.Count + sobNpcResults.Count} monsterTargets={monsterResults.Count} " +
                $"playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"monsterProgress={monsterProgress} sobNpcProgress={sobNpcProgress} " +
                $"totalAppliedDamage={totalAppliedDamage}; source=SectorBack.cs case 11990.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask SendLegacyMortalStrikeAnimationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        LegacySpellAnimationTarget target,
        CancellationToken cancellationToken)
    {
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [target]);
        await SendPlayerSpellAnimationToVisibleAsync(context, session, animation, cancellationToken)
            .ConfigureAwait(false);
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyMortalStrikeMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
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
                    LegacyMortalStrikeLineRange,
                    LegacyMortalStrikeLineWidth) ||
                !CanLegacyPlayerAttackMonster(session.Character, candidate.Monster, candidate.MonsterFamily, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyMortalStrikePlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(session.Character.X, session.Character.Y, character.X, character.Y) > LegacyMortalStrikePlayerDistance ||
                !IsLegacyMortalStrikeInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    character.X,
                    character.Y,
                    LegacyMortalStrikeLineRange,
                    LegacyMortalStrikeLineWidth) ||
                !CanLegacyPlayerMagicDamageTarget(context, session, candidate.ActiveSession.Session, spell, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyMortalStrikeSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
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
                    LegacyMortalStrikeLineRange,
                    LegacyMortalStrikeLineWidth) ||
                !CanLegacySobNpcMagicDamageTarget(context, session.Character, candidate.SobNpc, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static bool IsLegacyMortalStrikeInLine(
        long casterX,
        long casterY,
        long originX,
        long originY,
        long targetX,
        long targetY,
        byte range,
        byte width)
    {
        if (LegacyGetDistance(casterX, casterY, targetX, targetY) > range)
        {
            return false;
        }

        var deltaX = originX - casterX;
        var deltaY = originY - casterY;
        var divisor = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));
        if (divisor == 0)
        {
            return LegacyGetDistance(casterX, casterY, targetX, targetY) <= width;
        }

        var stepX = (double)deltaX / divisor;
        var stepY = (double)deltaY / divisor;
        var x = (double)casterX;
        var y = (double)casterY;
        uint expandedWidth = width;

        for (var index = 0; index <= range; index++)
        {
            if (LegacyGetDistance((long)Math.Round(x), (long)Math.Round(y), targetX, targetY) <= expandedWidth)
            {
                return true;
            }

            if (expandedWidth < 8)
            {
                expandedWidth++;
            }

            x += stepX;
            y += stepY;
        }

        return false;
    }

    private static uint CalculateLegacyMortalStrikeMonsterProgress(
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

    private static uint CalculateLegacyMortalStrikeSoulDamage(uint damage, byte useSpellSoul)
    {
        // Source Base.CalculateSoul(Damage, LevelSoul) returns Damage unchanged.
        _ = useSpellSoul;
        return damage;
    }
}
