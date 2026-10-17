using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyCombatGearWidthLineSpellId21530 = 21_530;
    private const ushort LegacyCombatGearWidthLineSpellId21570 = 21_570;
    private const ushort LegacyCombatGearWidthLineSpellId21640 = 21_640;
    private const ushort LegacyCombatGearWidthLineSpellId21660 = 21_660;
    private const ushort LegacyCombatGearWidthLineSpellId21680 = 21_680;
    private const ushort LegacyCombatGearWidthLineSpellId21700 = 21_700;
    private const ushort LegacyCombatGearWidthLineSpellId22110 = 22_110;
    private const uint LegacyCombatGearWidthLinePlayerDamagePercent = 75;
    private const string LegacyCombatGearDurationWidthLineSourceCases = "CombatGearAttack.cs cases 21530/22110/21640";
    private const string LegacyCombatGearMaxTargetsWidthLineSourceCases = "CombatGearAttack.cs cases 21660/21700/21570/21680";

    private static async ValueTask<bool> HandleLegacyCombatGearWidthLineAttackMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyCombatGearWidthLineAttackSpell(spell.Id))
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

        return await ExecuteLegacyPreparedCombatGearWidthLineAttackMagicAsync(
                context, resourceSession, query, playerSpell, spell, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> ExecuteLegacyPreparedCombatGearWidthLineAttackMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var updatedSession = resourceSession;
        var lineWidth = GetLegacyCombatGearWidthLineWidth(spell);
        var widthSource = GetLegacyCombatGearWidthLineWidthSource(spell.Id);
        var sourceCases = GetLegacyCombatGearWidthLineSourceCases(spell.Id);
        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyCombatGearWidthLineMonsterTargets(context, updatedSession, query, spell, lineWidth)
            : [];
        var playerTargets = SelectLegacyCombatGearWidthLinePlayerTargets(
            context,
            updatedSession,
            query,
            spell,
            lineWidth);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyCombatGearWidthLineSobNpcTargets(context, updatedSession, query, spell, lineWidth)
            : [];

        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result, uint Damage)>();
        var playerResults = new List<(LegacyPlayerMagicDamageResult Result, uint Damage)>();
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
                damage = CalculateLegacyCombatGearWidthLineSoulDamage(damage, playerSpell.UseSpellSoul);
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
            damage = CalculateLegacyCombatGearWidthLineSoulDamage(damage, playerSpell.UseSpellSoul);
            damage = MulDivToUInt32(damage, LegacyCombatGearWidthLinePlayerDamagePercent, 100);
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
                damage = CalculateLegacyCombatGearWidthLineSoulDamage(damage, playerSpell.UseSpellSoul);
                damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(
                    updatedSession,
                    spell,
                    damage,
                    LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(
                    target.SobNpc,
                    context.WorldData?.StaticStatues);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                sobNpcResults.Add((target.SobNpc, result, damage));
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

        var totalAppliedDamage =
            monsterResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            playerResults.Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            sobNpcResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy CombatGear width line magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={query.X} targetY={query.Y} range={spell.Range} width={lineWidth} " +
                $"widthSource={widthSource} playerDamagePercent={LegacyCombatGearWidthLinePlayerDamagePercent} " +
                $"targets={animationTargets.Count} " +
                $"monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"spellExperienceApplied=False characterExperienceApplied=False totalAppliedDamage={totalAppliedDamage}; " +
                $"source={sourceCases}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyCombatGearWidthLineAttackSpell(ushort spellId)
    {
        return spellId is
            LegacyCombatGearWidthLineSpellId21530 or
            LegacyCombatGearWidthLineSpellId21570 or
            LegacyCombatGearWidthLineSpellId21640 or
            LegacyCombatGearWidthLineSpellId21660 or
            LegacyCombatGearWidthLineSpellId21680 or
            LegacyCombatGearWidthLineSpellId21700 or
            LegacyCombatGearWidthLineSpellId22110;
    }

    private static byte GetLegacyCombatGearWidthLineWidth(LegacyMagicRuntimeSpell spell)
    {
        return UsesLegacyCombatGearDurationWidthLine(spell.Id)
            ? unchecked((byte)spell.Duration)
            : spell.MaxTargets;
    }

    private static string GetLegacyCombatGearWidthLineWidthSource(ushort spellId)
    {
        return UsesLegacyCombatGearDurationWidthLine(spellId)
            ? "duration"
            : "maxTargets";
    }

    private static string GetLegacyCombatGearWidthLineSourceCases(ushort spellId)
    {
        return UsesLegacyCombatGearDurationWidthLine(spellId)
            ? LegacyCombatGearDurationWidthLineSourceCases
            : LegacyCombatGearMaxTargetsWidthLineSourceCases;
    }

    private static bool UsesLegacyCombatGearDurationWidthLine(ushort spellId)
    {
        return spellId is
            LegacyCombatGearWidthLineSpellId21530 or
            LegacyCombatGearWidthLineSpellId21640 or
            LegacyCombatGearWidthLineSpellId22110;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyCombatGearWidthLineMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        byte lineWidth)
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
                    lineWidth) ||
                !CanLegacyPlayerAttackMonster(session.Character, candidate.Monster, candidate.MonsterFamily, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyCombatGearWidthLinePlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        byte lineWidth)
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
                    lineWidth) ||
                !CanLegacyPlayerMagicDamageTarget(context, session, candidate.ActiveSession.Session, spell, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyCombatGearWidthLineSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        byte lineWidth)
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
                    lineWidth) ||
                !CanLegacySobNpcMagicDamageTarget(context, session.Character, candidate.SobNpc, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static uint CalculateLegacyCombatGearWidthLineSoulDamage(uint damage, byte useSpellSoul)
    {
        _ = useSpellSoul;
        return damage;
    }
}
