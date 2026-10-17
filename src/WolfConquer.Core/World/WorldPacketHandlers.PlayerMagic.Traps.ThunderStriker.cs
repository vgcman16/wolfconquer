using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyThunderStrikerLayTrapSpellId = 15_770;
    private const byte LegacyThunderStrikerLayTrapRadius = 4;
    private const uint LegacyThunderStrikerLayTrapEnergyCost = 100;
    private const string LegacyThunderStrikerEnergyAttribute = "ThunderStrikerEnergy";

    private static async ValueTask<bool> HandleLegacyPlayerThunderStrikerLayTrapMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.LayTrap ||
            spell.Id != LegacyThunderStrikerLayTrapSpellId)
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

        var energy = ReadAttributeUInt32(
            resourceSession.Character.Attributes,
            LegacyThunderStrikerEnergyAttribute,
            "ThunderstrikerEnergy");
        if (!IsLegacyThunderstrikerClass(resourceSession.Character.Class) ||
            energy < LegacyThunderStrikerLayTrapEnergyCost)
        {
            var reason = !IsLegacyThunderstrikerClass(resourceSession.Character.Class)
                ? $"class={resourceSession.Character.Class}; AtributesStatus.IsThunderStriker is false"
                : $"attribute-backed {LegacyThunderStrikerEnergyAttribute}={energy} required={LegacyThunderStrikerLayTrapEnergyCost}";
            await context.Log.WriteLineAsync(
                    "WorldServer source-backed returned legacy ThunderStriker LayTrap magic " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                    $"{reason}; source LayTrap.cs case 15770 breaks after CanUseSpell.Verified without animation.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = SpendLegacyThunderStrikerLayTrapEnergy(resourceSession, energy);
        context.Sessions?.Set(context.Connection, updatedSession);
        context = context with { Session = updatedSession };

        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyThunderStrikerLayTrapMonsterTargets(context, updatedSession, query)
            : [];
        var playerTargets = SelectLegacyThunderStrikerLayTrapPlayerTargets(context, updatedSession, query, spell);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyThunderStrikerLayTrapSobNpcTargets(context, updatedSession, query)
            : [];

        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result, uint RequestedDamage)>();
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
                var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    updatedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                damage = CalculateLegacyThunderStrikerLayTrapSoulDamage(damage, playerSpell.UseSpellSoul);
                damage = MulDivToUInt32(damage, 5, 1);
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
            var damage = CalculateLegacyPlayerPlayerPhysicalSpellDamage(
                updatedSession,
                target.ActiveSession.Session,
                spell,
                context.WorldData,
                damageDivisor: 1);
            damage = CalculateLegacyThunderStrikerLayTrapSoulDamage(damage, playerSpell.UseSpellSoul);
            damage = MulDivToUInt32(damage, 18, 28);
            var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                context,
                target.ActiveSession,
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
            if (result.DamageApplied == 0)
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
                damage = CalculateLegacyThunderStrikerLayTrapSoulDamage(damage, playerSpell.UseSpellSoul);
                damage = MulDivToUInt32(damage, 3, 1);
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
            animationTargets,
            spellEffect: 1);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        foreach (var (target, result, _) in monsterResults)
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
            }
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

        const ulong sourceSpellExperienceDamage = 10_000UL;
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                sourceSpellExperienceDamage,
                cancellationToken)
            .ConfigureAwait(false);

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
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy ThunderStriker LayTrap magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={query.X} targetY={query.Y} radius={LegacyThunderStrikerLayTrapRadius} " +
                $"targets={animationTargets.Count} monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} " +
                $"sobNpcTargets={sobNpcResults.Count} totalRequestedDamage={totalRequestedDamage} " +
                $"totalAppliedDamage={totalAppliedDamage} spellExperienceDamage={sourceSpellExperienceDamage} " +
                $"attribute={LegacyThunderStrikerEnergyAttribute}; source=LayTrap.cs case 15770.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession SpendLegacyThunderStrikerLayTrapEnergy(WorldSession session, uint currentEnergy)
    {
        var remaining = currentEnergy - LegacyThunderStrikerLayTrapEnergyCost;
        return session with
        {
            Character = WithCharacterAttribute(session.Character, LegacyThunderStrikerEnergyAttribute, remaining),
        };
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyThunderStrikerLayTrapMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(query.X, query.Y, candidate.Monster.X, candidate.Monster.Y) <= LegacyThunderStrikerLayTrapRadius &&
                CanLegacyPlayerAttackMonster(session.Character, candidate.Monster, candidate.MonsterFamily, out _))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyThunderStrikerLayTrapPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(query.X, query.Y, character.X, character.Y) <= LegacyThunderStrikerLayTrapRadius &&
                CanLegacyPlayerMagicDamageTarget(context, session, candidate.ActiveSession.Session, spell, out _))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyThunderStrikerLayTrapSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(query.X, query.Y, candidate.SobNpc.X, candidate.SobNpc.Y) <= LegacyThunderStrikerLayTrapRadius &&
                CanLegacySobNpcMagicDamageTarget(context, session.Character, candidate.SobNpc, out _))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static uint CalculateLegacyThunderStrikerLayTrapSoulDamage(uint damage, byte useSpellSoul)
    {
        _ = useSpellSoul;
        return damage;
    }
}
