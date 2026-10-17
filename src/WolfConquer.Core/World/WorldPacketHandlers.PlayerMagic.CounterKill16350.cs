using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyHeavensWonderSpellId = 16_350;
    private const byte LegacyHeavensWonderRadius = 6;

    private static async ValueTask<bool> HandleLegacyPlayerCounterKill16350MagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        return await HandleLegacyPlayerCounterKill16350MagicAsync(
                context,
                session,
                query,
                playerSpell,
                spell,
                ignoreAttackStamp: false,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> HandleLegacyPlayerCounterKill16350MagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        bool ignoreAttackStamp,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.CounterKill)
        {
            return false;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                ignoreAttackStamp,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        context = context with { Session = resourceSession };
        if (spell.Id != LegacyHeavensWonderSpellId)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer source-backed returned legacy CounterKill magic after resource preparation " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort}; " +
                    "source=matching DecompiledSource CounterKill.cs exact-ID fence 16350.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = resourceSession;
        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyHeavensWonderMonsterTargets(context, updatedSession)
            : [];
        var playerTargets = SelectLegacyHeavensWonderPlayerTargets(context, updatedSession, spell);

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
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var totalMonsterAppliedDamage = 0UL;
        var totalPlayerAppliedDamage = 0UL;
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
                requestedDamage = CalculateLegacyHeavensWonderSoulDamage(
                    requestedDamage,
                    playerSpell.UseSpellSoul);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    requestedDamage,
                    now,
                    updatedSession);
                if (result.AlreadyDead)
                {
                    continue;
                }

                totalMonsterAppliedDamage += result.DamageApplied;
                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    requestedDamage,
                    Hit: true,
                    MsgAttackEffect.None));

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
        }

        foreach (var target in playerTargets)
        {
            var activeTarget = target.ActiveSession;
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

            var physicalDamage = statusDefended || wildPhoenixDefended
                ? new LegacyPlayerPlayerPhysicalSpellDamageResult(1U, MsgAttackEffect.None)
                : CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData,
                    damageDivisor: 1);
            var requestedDamage = CalculateLegacyHeavensWonderSoulDamage(
                physicalDamage.Damage,
                playerSpell.UseSpellSoul);
            requestedDamage = MulDivToUInt32(requestedDamage, 70, 100);
            var receiveOutcome = statusDefended || wildPhoenixDefended
                ? await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    context,
                    activeTarget,
                    updatedSession.Character,
                    requestedDamage,
                    cancellationToken)
                .ConfigureAwait(false)
                : await ApplyLegacyBackDamageEligiblePlayerDamageAsync(
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

            playerResults.Add(result);
            totalPlayerAppliedDamage += result.DamageApplied;
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                requestedDamage,
                Hit: true,
                physicalDamage.Effect));

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
                playerResults,
                cancellationToken)
            .ConfigureAwait(false);

        var animation = new LegacySpellAnimationPacket(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            AttackedUid: 0,
            query.X,
            query.Y,
            LegacyHeavensWonderSpellId,
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

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Heaven's Wonder CounterKill magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"queryX={query.X} queryY={query.Y} radius={LegacyHeavensWonderRadius} " +
                $"monsterTargets={monsterTargets.Count} playerTargets={playerTargets.Count} " +
                $"animationTargets={animationTargets.Count} totalMonsterAppliedDamage={totalMonsterAppliedDamage} " +
                $"totalPlayerAppliedDamage={totalPlayerAppliedDamage}; " +
                "source=matching DecompiledSource CounterKill.cs.")
            .ConfigureAwait(false);
        return true;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyHeavensWonderMonsterTargets(
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
                    candidate.Monster.Y) <= LegacyHeavensWonderRadius)
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyHeavensWonderPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    character.X,
                    character.Y) <= LegacyHeavensWonderRadius)
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static uint CalculateLegacyHeavensWonderSoulDamage(uint damage, byte useSpellSoul)
    {
        // Matching Base.CalculateSoul returns the incoming physical damage unchanged.
        _ = useSpellSoul;
        return damage;
    }
}
