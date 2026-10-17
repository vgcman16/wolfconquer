using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyStrike13050SpellId = 13_050;
    private const byte LegacyStrike13050Radius = 5;

    // This remains separate until the parent Strike dispatcher claims the bounded source case.
    private static async ValueTask<bool> HandleLegacyPlayerStrike13050MagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.Strike || spell.Id != LegacyStrike13050SpellId)
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
        context = context with { Session = updatedSession };
        var anchoredQuery = GetLegacyStrike13050AnchoredQuery(context, updatedSession, query);
        var monsterTargets = context.MonsterStates is null
            ? []
            : SelectLegacyStrike13050MonsterTargets(context, updatedSession);
        var playerTargets = SelectLegacyStrike13050PlayerTargets(context, updatedSession, spell);
        var sobNpcTargets = context.SobNpcStates is null
            ? []
            : SelectLegacyStrike13050SobNpcTargets(context, updatedSession);

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
        var playerResults = new List<(LegacyPlayerMagicDamageResult Result, uint Damage, MsgAttackEffect Effect)>();
        var sobNpcResults = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior,
            uint Damage,
            ulong Progress)>();
        var killedTrainingSobNpcTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var eventSobNpcSideEffectTargets = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();
        var interactionFrames = 0;
        var now = DateTimeOffset.UtcNow;

        if (context.MonsterStates is { } monsterStates)
        {
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
                }

                var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    updatedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                damage = CalculateLegacyStrikeSoulDamage(damage, playerSpell.UseSpellSoul);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    damage,
                    now,
                    updatedSession);
                if (result.AlreadyDead)
                {
                    continue;
                }

                var progress = CalculateLegacyStrikeMonsterProgressDamage(updatedSession, target, damage);
                monsterResults.Add((target, result, damage, progress));
                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    damage,
                    Hit: damage > 0,
                    MsgAttackEffect.None));
                await SendLegacyStrike13050InteractionAsync(
                        context,
                        updatedSession,
                        target.Monster.Uid,
                        target.Monster.X,
                        target.Monster.Y,
                        damage,
                        MsgAttackEffect.None,
                        cancellationToken)
                    .ConfigureAwait(false);
                interactionFrames++;
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
                ? new LegacyPlayerPlayerPhysicalSpellDamageResult(1, MsgAttackEffect.None)
                : CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData,
                    damageDivisor: 1);
            var damage = CalculateLegacyStrikeSoulDamage(physicalDamage.Damage, playerSpell.UseSpellSoul);
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

            playerResults.Add((result, damage, physicalDamage.Effect));
            var character = result.UpdatedSession.Character;
            var playerUid = CheckedUInt32(character.Uid, nameof(character.Uid));
            animationTargets.Add(new LegacySpellAnimationTarget(
                playerUid,
                damage,
                Hit: true,
                physicalDamage.Effect));
            await SendLegacyStrike13050InteractionAsync(
                    context,
                    updatedSession,
                    playerUid,
                    CheckedUInt16(character.X, nameof(character.X)),
                    CheckedUInt16(character.Y, nameof(character.Y)),
                    damage,
                    physicalDamage.Effect,
                    cancellationToken)
                .ConfigureAwait(false);
            interactionFrames++;
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
                damage = CalculateLegacyStrikeSoulDamage(damage, playerSpell.UseSpellSoul);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(
                    target.SobNpc,
                    context.WorldData?.StaticStatues);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                if (result.AlreadyDead)
                {
                    continue;
                }

                var progress = CalculateLegacySobNpcExperienceDamage(target.SobNpc, result);
                sobNpcResults.Add((target.SobNpc, result, deathBehavior, damage, progress));
                var sobNpcUid = CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id));
                animationTargets.Add(new LegacySpellAnimationTarget(
                    sobNpcUid,
                    damage,
                    Hit: damage > 0,
                    MsgAttackEffect.None));
                await SendLegacyStrike13050InteractionAsync(
                        context,
                        updatedSession,
                        sobNpcUid,
                        CheckedUInt16(target.SobNpc.X, nameof(target.SobNpc.X)),
                        CheckedUInt16(target.SobNpc.Y, nameof(target.SobNpc.Y)),
                        damage,
                        MsgAttackEffect.None,
                        cancellationToken)
                    .ConfigureAwait(false);
                interactionFrames++;

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

        if (animationTargets.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy Strike 13050 magic " +
                    $"{FormatSession(context)} targets=0; source=Strike.cs case 13050 no-target silence.")
                .ConfigureAwait(false);
            return true;
        }

        var monsterProgress = monsterResults.Aggregate(0UL, (total, result) => total + result.Progress);
        var sobNpcProgress = sobNpcResults.Aggregate(0UL, (total, result) => total + result.Progress);
        var progressDamage = monsterProgress + sobNpcProgress;
        updatedSession = await ApplyLegacyStrikeIncreaseExperienceAsync(
                context,
                updatedSession,
                progressDamage,
                cancellationToken)
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

        foreach (var (result, _, _) in playerResults)
        {
            await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var animation = new LegacySpellAnimationPacket(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            AttackedUid: 0,
            anchoredQuery.X,
            anchoredQuery.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
        foreach (var (target, result, _, _) in monsterResults)
        {
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
            else
            {
                if (LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
                {
                    var damagedMonster = target.Monster with { Hitpoints = result.RemainingHitpoints };
                    foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                    {
                        await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                            .ConfigureAwait(false);
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

        foreach (var (result, _, _) in playerResults)
        {
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
            foreach (var (sobNpc, result) in killedTrainingSobNpcTargets)
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        context,
                        updatedSession,
                        sobNpc,
                        currentSobNpcStates.GetVisible(sobNpc),
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
            monsterResults.Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            playerResults.Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            sobNpcResults.Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Strike 13050 magic " +
                $"{FormatSession(context with { Session = updatedSession })} targetX={anchoredQuery.X} targetY={anchoredQuery.Y} " +
                $"radius={LegacyStrike13050Radius} targets={animationTargets.Count} monsterTargets={monsterResults.Count} " +
                $"playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"manualInteractionFrames={interactionFrames} monsterProgress={monsterProgress} " +
                $"sobNpcProgress={sobNpcProgress} totalAppliedDamage={totalAppliedDamage}; " +
                "autoAttackFanout=fenced because no proven local OnAutoAttack state is available; " +
                "source=Strike.cs case 13050.")
            .ConfigureAwait(false);
        return true;
    }

    private static MsgAttackQuery GetLegacyStrike13050AnchoredQuery(
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

        if (TryFindVisibleMonsterTargetByUid(context, session.Character, query.OpponentUid, out var monsterTarget))
        {
            return query with { X = monsterTarget.Monster.X, Y = monsterTarget.Monster.Y };
        }

        return query;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyStrike13050MonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y) > LegacyStrike13050Radius ||
                !CanLegacyPlayerAttackMonster(session.Character, candidate.Monster, candidate.MonsterFamily, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyStrike13050PlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(session.Character.X, session.Character.Y, character.X, character.Y) >
                    LegacyStrike13050Radius ||
                !CanLegacyPlayerMagicDamageTarget(
                    context,
                    session,
                    candidate.ActiveSession.Session,
                    spell,
                    out _,
                    out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyStrike13050SobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y) > LegacyStrike13050Radius ||
                !CanLegacySobNpcMagicDamageTarget(context, session.Character, candidate.SobNpc, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static async ValueTask SendLegacyStrike13050InteractionAsync(
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
}
