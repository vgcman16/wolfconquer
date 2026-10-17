using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyCombatGearDirectLineSort = 104;
    private const byte LegacyCombatGearDirectLineRange = 15;
    private const byte LegacyCombatGearDragonhowlDirectLineRange = 20;
    private const byte LegacyCombatGearDirectLineWidth = 2;
    private const uint LegacyCombatGearDirectLineDefaultPlayerDamageDivisor = 1;
    private const uint LegacyCombatGearTharnCutterDirectLinePlayerDamageDivisor = 3;
    private const uint LegacyCombatGearColdStoneDirectLinePlayerAnimationDamageDivisor = 2;

    private static async ValueTask<bool> HandleLegacyCombatGearDirectLineAttackMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyCombatGearDirectLineAttackSpell(spell.Id) ||
            !HasLegacyCombatGearDirectLineSort(spell))
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

        return await ExecuteLegacyPreparedCombatGearDirectLineAttackMagicAsync(
                context, resourceSession, query, playerSpell, spell, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> ExecuteLegacyPreparedCombatGearDirectLineAttackMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var isTharnCutter = IsLegacyCombatGearTharnCutterDirectLineAttackSpell(spell.Id);
        var sendsManualInteractionFrames = IsLegacyCombatGearSpellRangeManualLineAttackSpell(spell.Id);
        var appliesProgress = !IsLegacyCombatGearAnimationOnlyLineAttackSpell(spell.Id);
        var appliesProgressBeforeAnimation = IsLegacyCombatGearProgressBeforeAnimationLineAttackSpell(spell.Id);
        var appliesPlayerCounterHitShoot = IsLegacyCombatGearPlayerCounterHitShootLineAttackSpell(spell.Id);
        var playerDamageDivisor = isTharnCutter
            ? LegacyCombatGearTharnCutterDirectLinePlayerDamageDivisor
            : LegacyCombatGearDirectLineDefaultPlayerDamageDivisor;
        var playerAnimationDamageDivisor = isTharnCutter
            ? LegacyCombatGearDirectLineDefaultPlayerDamageDivisor
            : LegacyCombatGearColdStoneDirectLinePlayerAnimationDamageDivisor;
        if (IsLegacyCombatGearDragonhowlDirectLineAttackSpell(spell.Id) ||
            sendsManualInteractionFrames ||
            !appliesProgress)
        {
            playerAnimationDamageDivisor = LegacyCombatGearDirectLineDefaultPlayerDamageDivisor;
        }

        var lineRange = GetLegacyCombatGearDirectLineRange(spell);
        var sourceCases = GetLegacyCombatGearDirectLineSourceCases(spell.Id);
        var anchoredQuery = GetLegacyCombatGearDirectLineAnchoredQuery(context, resourceSession, query);
        var updatedSession = resourceSession;
        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyCombatGearDirectLineMonsterTargets(context, updatedSession, anchoredQuery, spell, lineRange)
            : [];
        var playerTargets = SelectLegacyCombatGearDirectLinePlayerTargets(
            context,
            updatedSession,
            anchoredQuery,
            spell,
            lineRange);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyCombatGearDirectLineSobNpcTargets(context, updatedSession, anchoredQuery, spell, lineRange)
            : [];

        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result, uint Damage, ulong Progress)>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
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
                var progress = result.AlreadyDead ? 0 : damage;
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
                    playerDamageDivisor);
            if (wildPhoenixDefended && playerDamageDivisor > 1)
            {
                damage /= playerDamageDivisor;
            }

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
            if (result.DamageApplied == 0 && !isTharnCutter)
            {
                continue;
            }

            playerResults.Add(result);
            var animationDamage = playerAnimationDamageDivisor > 1
                ? result.DamageApplied / playerAnimationDamageDivisor
                : result.DamageApplied;
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                animationDamage,
                Hit: true,
                MsgAttackEffect.None));
        }

        var counterHitShoots = 0;
        if (appliesPlayerCounterHitShoot)
        {
            updatedSession = ApplyLegacyCounterHitShoot(
                context,
                updatedSession,
                playerTargets.Count,
                out counterHitShoots);
            context = context with { Session = updatedSession };
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
                if (result.AlreadyDead || result.DamageApplied == 0)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                    result.DamageApplied,
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

        if (animationTargets.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear direct line magic " +
                    $"{FormatSession(context)} spellId={spell.Id}; no source line targets.")
                .ConfigureAwait(false);
            return true;
        }

        var manualInteractionFrames = sendsManualInteractionFrames
            ? await SendLegacyCombatGearDirectLineManualInteractionsAsync(
                    context,
                    updatedSession,
                    monsterResults,
                    playerResults,
                    sobNpcResults,
                    cancellationToken)
                .ConfigureAwait(false)
            : 0;

        var monsterProgress = monsterResults.Aggregate(0UL, (total, result) => total + result.Progress);
        var sobNpcProgress = sobNpcResults.Aggregate(0UL, (total, result) => total + result.Progress);
        var progressDamage = monsterProgress + sobNpcProgress;
        if (appliesProgress && appliesProgressBeforeAnimation)
        {
            updatedSession = await ApplyLegacyCombatGearDirectLineProgressAsync(
                    context,
                    updatedSession,
                    playerSpell,
                    spell,
                    progressDamage,
                    sourceCases,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            anchoredQuery.X,
            anchoredQuery.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets,
            bombFlag: GetLegacyCombatGearDirectLineBombFlag(spell.Id, updatedSession.Character));
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

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

        if (appliesProgress && !appliesProgressBeforeAnimation)
        {
            updatedSession = await ApplyLegacyCombatGearDirectLineProgressAsync(
                    context,
                    updatedSession,
                    playerSpell,
                    spell,
                    progressDamage,
                    sourceCases,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var totalAppliedDamage =
            monsterResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            playerResults.Aggregate(0UL, (total, result) => total + result.DamageApplied) +
            sobNpcResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy CombatGear direct line magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={anchoredQuery.X} targetY={anchoredQuery.Y} range={lineRange} " +
                $"width={LegacyCombatGearDirectLineWidth} playerDamageDivisor={playerDamageDivisor} " +
                $"playerAnimationDamageDivisor={playerAnimationDamageDivisor} targets={animationTargets.Count} " +
                $"monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"manualInteractionFrames={manualInteractionFrames} progressionApplied={appliesProgress} " +
                $"progressBeforeAnimation={appliesProgressBeforeAnimation} counterHitShoots={counterHitShoots} " +
                $"monsterProgress={monsterProgress} sobNpcProgress={sobNpcProgress} totalAppliedDamage={totalAppliedDamage}; " +
                $"source={sourceCases}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyCombatGearDirectLineAttackSpell(ushort spellId)
    {
        return LegacyCombatGearColdMoonAttackSpells.Contains(spellId) ||
            LegacyCombatGearStoneCrackerAttackSpells.Contains(spellId) ||
            IsLegacyCombatGearTharnCutterDirectLineAttackSpell(spellId) ||
            IsLegacyCombatGearDragonhowlDirectLineAttackSpell(spellId) ||
            IsLegacyCombatGearSpellRangeManualLineAttackSpell(spellId) ||
            IsLegacyCombatGearAnimationOnlyLineAttackSpell(spellId);
    }

    private static bool HasLegacyCombatGearDirectLineSort(LegacyMagicRuntimeSpell spell)
    {
        return (spell.Sort == LegacyMagicSort.Rectangle &&
                IsLegacyCombatGearWindWalkerRangeLineAttackSpell(spell.Id)) ||
            ((int)spell.Sort == LegacyCombatGearDirectLineSort &&
                !IsLegacyCombatGearWindWalkerRangeLineAttackSpell(spell.Id));
    }

    private static bool IsLegacyCombatGearTharnCutterDirectLineAttackSpell(ushort spellId)
    {
        return LegacyCombatGearTharnCutterAttackSpells.Contains(spellId);
    }

    private static byte GetLegacyCombatGearDirectLineBombFlag(ushort spellId, CharacterSnapshot character)
    {
        return IsLegacyCombatGearTharnCutterDirectLineAttackSpell(spellId)
            ? GetLegacyCombatGearTharnCutterActiveBombFlag(spellId, character)
            : (byte)0;
    }

    private static bool IsLegacyCombatGearDragonhowlDirectLineAttackSpell(ushort spellId)
    {
        return spellId is 17_460 or 17_470 or 17_480;
    }

    private static bool IsLegacyCombatGearSpellRangeManualLineAttackSpell(ushort spellId)
    {
        return IsLegacyCombatGearBloodlustRangeLineAttackSpell(spellId) ||
            IsLegacyCombatGearWindWalkerRangeLineAttackSpell(spellId);
    }

    private static bool IsLegacyCombatGearBloodlustRangeLineAttackSpell(ushort spellId)
    {
        return spellId is 17_500 or 17_510 or 17_520 or 17_540 or 17_550 or 17_560;
    }

    private static bool IsLegacyCombatGearWindWalkerRangeLineAttackSpell(ushort spellId)
    {
        return spellId == 13_060;
    }

    private static bool IsLegacyCombatGearProgressBeforeAnimationLineAttackSpell(ushort spellId)
    {
        return IsLegacyCombatGearWindWalkerRangeLineAttackSpell(spellId);
    }

    private static bool IsLegacyCombatGearPlayerCounterHitShootLineAttackSpell(ushort spellId)
    {
        return IsLegacyCombatGearWindWalkerRangeLineAttackSpell(spellId);
    }

    private static bool IsLegacyCombatGearAnimationOnlyLineAttackSpell(ushort spellId)
    {
        return spellId is 17_530 or 17_570;
    }

    private static byte GetLegacyCombatGearDirectLineRange(LegacyMagicRuntimeSpell spell)
    {
        if (IsLegacyCombatGearSpellRangeManualLineAttackSpell(spell.Id))
        {
            return spell.Range;
        }

        return IsLegacyCombatGearDragonhowlDirectLineAttackSpell(spell.Id)
            ? LegacyCombatGearDragonhowlDirectLineRange
            : LegacyCombatGearDirectLineRange;
    }

    private static string GetLegacyCombatGearDirectLineSourceCases(ushort spellId)
    {
        if (IsLegacyCombatGearTharnCutterDirectLineAttackSpell(spellId))
        {
            return "CombatGearAttack.cs cases 17390/17391/17392";
        }

        if (IsLegacyCombatGearWindWalkerRangeLineAttackSpell(spellId))
        {
            return "CombatGearAttack.cs case 13060";
        }

        if (IsLegacyCombatGearBloodlustRangeLineAttackSpell(spellId))
        {
            return "CombatGearAttack.cs cases 17500/17510/17520/17540/17550/17560";
        }

        if (IsLegacyCombatGearAnimationOnlyLineAttackSpell(spellId))
        {
            return "CombatGearAttack.cs cases 17530/17570";
        }

        return IsLegacyCombatGearDragonhowlDirectLineAttackSpell(spellId)
            ? "CombatGearAttack.cs cases 17460/17470/17480"
            : "CombatGearAttack.cs cases 11950/11951/11952/17400/17401";
    }

    private static MsgAttackQuery GetLegacyCombatGearDirectLineAnchoredQuery(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        if (TryGetVisibleSessionByUid(context, session, query.OpponentUid, out var playerTarget))
        {
            return query with
            {
                X = CheckedUInt16(playerTarget.Session.Character.X, nameof(playerTarget.Session.Character.X)),
                Y = CheckedUInt16(playerTarget.Session.Character.Y, nameof(playerTarget.Session.Character.Y)),
            };
        }

        if (TryFindVisibleMonsterTargetByUid(
                context,
                session.Character,
                query.OpponentUid,
                out var monsterTarget))
        {
            return query with
            {
                X = monsterTarget.Monster.X,
                Y = monsterTarget.Monster.Y,
            };
        }

        return query;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyCombatGearDirectLineMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        byte lineRange)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        var usesSpellRangeManualLine = IsLegacyCombatGearSpellRangeManualLineAttackSpell(spell.Id);
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if ((usesSpellRangeManualLine &&
                    (IsLegacyGuardMonsterFamily(candidate.Monster, candidate.MonsterFamily) ||
                    LegacyGetDistance(
                        session.Character.X,
                        session.Character.Y,
                        candidate.Monster.X,
                        candidate.Monster.Y) > spell.Range)) ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    lineRange,
                    LegacyCombatGearDirectLineWidth) ||
                !CanLegacyPlayerAttackMonster(session.Character, candidate.Monster, candidate.MonsterFamily, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyCombatGearDirectLinePlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        byte lineRange)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        var usesSpellRangeManualLine = IsLegacyCombatGearSpellRangeManualLineAttackSpell(spell.Id);
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if ((usesSpellRangeManualLine &&
                    LegacyGetDistance(
                        session.Character.X,
                        session.Character.Y,
                        character.X,
                        character.Y) > spell.Range) ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    character.X,
                    character.Y,
                    lineRange,
                    LegacyCombatGearDirectLineWidth) ||
                !CanLegacyPlayerMagicDamageTarget(context, session, candidate.ActiveSession.Session, spell, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyCombatGearDirectLineSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        byte lineRange)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        var usesSpellRangeManualLine = IsLegacyCombatGearSpellRangeManualLineAttackSpell(spell.Id);
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if ((usesSpellRangeManualLine &&
                    LegacyGetDistance(
                        session.Character.X,
                        session.Character.Y,
                        candidate.SobNpc.X,
                        candidate.SobNpc.Y) > spell.Range) ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    lineRange,
                    LegacyCombatGearDirectLineWidth) ||
                !CanLegacySobNpcMagicDamageTarget(context, session.Character, candidate.SobNpc, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static async ValueTask<int> SendLegacyCombatGearDirectLineManualInteractionsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        IReadOnlyList<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result, uint Damage, ulong Progress)> monsterResults,
        IReadOnlyList<LegacyPlayerMagicDamageResult> playerResults,
        IReadOnlyList<(SobNpc SobNpc, SobNpcDamageResult Result, uint Damage, ulong Progress)> sobNpcResults,
        CancellationToken cancellationToken)
    {
        var sent = 0;
        foreach (var (target, result, _, _) in monsterResults)
        {
            if (result.AlreadyDead)
            {
                continue;
            }

            await SendLegacyCombatGearDirectLineManualInteractionAsync(
                    context,
                    session,
                    target.Monster.Uid,
                    target.Monster.X,
                    target.Monster.Y,
                    result.RequestedDamage,
                    MsgAttackEffect.None,
                    cancellationToken)
                .ConfigureAwait(false);
            sent++;
        }

        foreach (var result in playerResults)
        {
            var character = result.UpdatedSession.Character;
            await SendLegacyCombatGearDirectLineManualInteractionAsync(
                    context,
                    session,
                    CheckedUInt32(character.Uid, nameof(character.Uid)),
                    CheckedUInt16(character.X, nameof(character.X)),
                    CheckedUInt16(character.Y, nameof(character.Y)),
                    result.DamageApplied,
                    MsgAttackEffect.None,
                    cancellationToken)
                .ConfigureAwait(false);
            sent++;
        }

        foreach (var (sobNpc, result, _, _) in sobNpcResults)
        {
            if (result.AlreadyDead || result.DamageApplied == 0)
            {
                continue;
            }

            await SendLegacyCombatGearDirectLineManualInteractionAsync(
                    context,
                    session,
                    CheckedUInt32(sobNpc.Id, nameof(sobNpc.Id)),
                    CheckedUInt16(sobNpc.X, nameof(sobNpc.X)),
                    CheckedUInt16(sobNpc.Y, nameof(sobNpc.Y)),
                    result.DamageApplied,
                    MsgAttackEffect.None,
                    cancellationToken)
                .ConfigureAwait(false);
            sent++;
        }

        return sent;
    }

    private static async ValueTask SendLegacyCombatGearDirectLineManualInteractionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint opponentUid,
        ushort x,
        ushort y,
        uint damage,
        MsgAttackEffect effect,
        CancellationToken cancellationToken)
    {
        var interaction = new MsgAttackPacket(new MsgAttackQuery(
            Uid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            OpponentUid: opponentUid,
            X: x,
            Y: y,
            AttackType: MsgAttackType.Physical,
            Damage: unchecked((int)damage),
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: effect,
            Unknown: 0));
        await context.Connection.SendAsync(interaction.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> ApplyLegacyCombatGearDirectLineIncreaseExperienceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        ulong progressDamage,
        string sourceCases,
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
                        "WorldServer applied legacy CombatGear direct line experience side effects " +
                        $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                        $"applied=0 oblivion={experienceResult.OblivionExperienceGain} " +
                        $"huntingBlessing={experienceResult.HuntingBlessingGain} suppression={experienceResult.SuppressionReason}; " +
                        $"source={sourceCases} IncreaseExperience.Up.")
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
                "WorldServer sent legacy CombatGear direct line experience " +
                $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                $"gain={experienceResult.AppliedExperienceGain} oblivion={experienceResult.OblivionExperienceGain} " +
                $"huntingBlessing={experienceResult.HuntingBlessingGain} experience={updatedSession.Character.Experience} " +
                $"level={updatedSession.Character.Level}; " +
                $"source={sourceCases} IncreaseExperience.Up.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> ApplyLegacyCombatGearDirectLineProgressAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        ulong progressDamage,
        string sourceCases,
        CancellationToken cancellationToken)
    {
        var updatedSession = await ApplyLegacyCombatGearDirectLineIncreaseExperienceAsync(
                context,
                session,
                progressDamage,
                sourceCases,
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
        return updatedSession;
    }
}
