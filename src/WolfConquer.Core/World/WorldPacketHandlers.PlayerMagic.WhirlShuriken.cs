using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const byte LegacyWhirlShurikenWildwindLineWidth = 2;

    private static async ValueTask<bool> HandleLegacyPlayerWhirlShurikenWildwindMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.Wildwind ||
            spell.Id is not (LegacyWhirlShurikenSpellId or LegacyWhirlShurikenPassiveSpellId))
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
            ? SelectLegacyWhirlShurikenWildwindMonsterTargets(context, updatedSession, query, spell)
            : [];
        var playerTargets = SelectLegacyWhirlShurikenWildwindPlayerTargets(context, updatedSession, query, spell);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyWhirlShurikenWildwindSobNpcTargets(context, updatedSession, query, spell)
            : [];

        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result)>();
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
                var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    updatedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                damage = MulDivToUInt32(
                    CalculateLegacyWhirlShurikenSoulDamage(damage, playerSpell.UseSpellSoul),
                    50,
                    100);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    damage,
                    now,
                    updatedSession);
                monsterResults.Add((target, result));
                if (result.AlreadyDead || result.DamageApplied == 0)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    damage,
                    Hit: true,
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
            var physicalDamage = wildPhoenixDefended
                ? new LegacyPlayerPlayerPhysicalSpellDamageResult(1U, MsgAttackEffect.None)
                : CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData,
                    damageDivisor: 1);
            var damage = physicalDamage.Damage;
            damage = CalculateLegacyWhirlShurikenPlayerDamage(spell.Id, damage, playerSpell.UseSpellSoul);
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
            playerResults.Add(result);
            if (result.DamageApplied == 0)
            {
                continue;
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                damage,
                Hit: true,
                physicalDamage.Effect));
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
                damage = CalculateLegacyWhirlShurikenSoulDamage(damage, playerSpell.UseSpellSoul);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc, context.WorldData?.StaticStatues);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                sobNpcResults.Add((target.SobNpc, result));
                if (result.AlreadyDead || result.DamageApplied == 0)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                    MulDivToUInt32(damage, 20, 100),
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

        updatedSession = await ApplyLegacyMixedMagicDamageSideEffectsAsync(
                context,
                updatedSession,
                spell,
                monsterResults,
                playerResults,
                sobNpcResults,
                killedTrainingSobNpcTargets,
                eventSobNpcSideEffectTargets,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var progressDamage =
            monsterResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
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
                ClampLegacyProficiencyRawExperience(progressDamage),
                cancellationToken,
                activity: "WhirlShuriken Wildwind magic weapon proficiency experience")
            .ConfigureAwait(false);

        var totalPlayerDamage = playerResults.Aggregate(0UL, (total, result) => total + result.DamageApplied);
        var totalAppliedDamage =
            monsterResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied) +
            totalPlayerDamage +
            sobNpcResults
                .Where(result => !result.Result.AlreadyDead)
                .Aggregate(0UL, (total, result) => total + result.Result.DamageApplied);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy WhirlShuriken Wildwind magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={query.X} targetY={query.Y} range={spell.Range} width={LegacyWhirlShurikenWildwindLineWidth} " +
                $"targets={animationTargets.Count} monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} " +
                $"sobNpcTargets={sobNpcResults.Count} progressDamage={progressDamage} totalAppliedDamage={totalAppliedDamage} " +
                $"playerAppliedDamage={totalPlayerDamage}; source=Wildwind.cs case {spell.Id}.")
            .ConfigureAwait(false);
        return true;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyWhirlShurikenWildwindMonsterTargets(
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
                    LegacyWhirlShurikenWildwindLineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyWhirlShurikenWildwindPlayerTargets(
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
                    LegacyWhirlShurikenWildwindLineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyWhirlShurikenWildwindSobNpcTargets(
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
                    LegacyWhirlShurikenWildwindLineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static uint CalculateLegacyWhirlShurikenPlayerDamage(ushort spellId, uint damage, byte useSpellSoul)
    {
        damage = CalculateLegacyWhirlShurikenSoulDamage(damage, useSpellSoul);
        return spellId == LegacyWhirlShurikenPassiveSpellId
            ? MulDivToUInt32(damage, 32, 100)
            : MulDivToUInt32(damage, 12, 21);
    }

    private static uint CalculateLegacyWhirlShurikenSoulDamage(uint damage, byte useSpellSoul)
    {
        // Source Base.CalculateSoul(Damage, LevelSoul) returns Damage unchanged for this runtime slice.
        _ = useSpellSoul;
        return damage;
    }
}
