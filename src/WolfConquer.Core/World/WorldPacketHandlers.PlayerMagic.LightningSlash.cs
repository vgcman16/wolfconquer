using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerLightningSlashWildwindMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.Wildwind ||
            spell.Id is not (LegacyLightningSlashSpellId or LegacyLightningSlashPassiveSpellId))
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
        var lineWidth = spell.MaxTargets;
        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyLightningSlashWildwindMonsterTargets(context, updatedSession, query, spell, lineWidth)
            : [];
        var playerTargets = SelectLegacyLightningSlashWildwindPlayerTargets(context, updatedSession, query, spell, lineWidth);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyLightningSlashWildwindSobNpcTargets(context, updatedSession, query, spell, lineWidth)
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
                damage = CalculateLegacyLightningSlashMonsterDamage(spell.Id, damage, playerSpell.UseSpellSoul);
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

        var totalPlayerRangeIndex = 0UL;
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
            var targetCharacter = activeTarget.Session.Character;
            var rangeIndex = GetLegacyLightningSlashLineRangeIndex(
                updatedSession.Character.X,
                updatedSession.Character.Y,
                query.X,
                query.Y,
                targetCharacter.X,
                targetCharacter.Y,
                spell.Range,
                lineWidth);
            totalPlayerRangeIndex += checked((ulong)rangeIndex);
            damage = CalculateLegacyLightningSlashPlayerDamage(spell.Id, damage, playerSpell.UseSpellSoul, rangeIndex);
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
                damage = CalculateLegacyLightningSlashSobNpcDamage(spell.Id, damage, playerSpell.UseSpellSoul);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc, context.WorldData?.StaticStatues);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                sobNpcResults.Add((target.SobNpc, result));
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
                activity: "LightningSlash Wildwind magic weapon proficiency experience")
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
                "WorldServer handled legacy LightningSlash Wildwind magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={query.X} targetY={query.Y} range={spell.Range} width={lineWidth} " +
                $"targets={animationTargets.Count} monsterTargets={monsterResults.Count} playerTargets={playerResults.Count} " +
                $"sobNpcTargets={sobNpcResults.Count} totalPlayerRangeIndex={totalPlayerRangeIndex} " +
                $"progressDamage={progressDamage} totalAppliedDamage={totalAppliedDamage} " +
                $"playerAppliedDamage={totalPlayerDamage}; source=Wildwind.cs case {spell.Id}.")
            .ConfigureAwait(false);
        return true;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyLightningSlashWildwindMonsterTargets(
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
                    lineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyLightningSlashWildwindPlayerTargets(
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
                    lineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyLightningSlashWildwindSobNpcTargets(
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
                    lineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static uint CalculateLegacyLightningSlashMonsterDamage(ushort spellId, uint damage, byte useSpellSoul)
    {
        damage = CalculateLegacyLightningSlashSoulDamage(damage, useSpellSoul);
        return spellId == LegacyLightningSlashPassiveSpellId
            ? damage / 2
            : damage;
    }

    private static uint CalculateLegacyLightningSlashPlayerDamage(
        ushort spellId,
        uint damage,
        byte useSpellSoul,
        int rangeIndex)
    {
        damage = CalculateLegacyLightningSlashSoulDamage(damage, useSpellSoul);
        damage = spellId == LegacyLightningSlashPassiveSpellId
            ? MulDivToUInt32(damage, 15, 55)
            : MulDivToUInt32(damage, 20, 30);
        var denominator = spellId == LegacyLightningSlashPassiveSpellId ? 160 : 150;
        var bonus = ((long)damage * (18 - (rangeIndex * 7))) / denominator;
        return unchecked(damage + (uint)(int)bonus);
    }

    private static uint CalculateLegacyLightningSlashSobNpcDamage(ushort spellId, uint damage, byte useSpellSoul)
    {
        damage = CalculateLegacyLightningSlashSoulDamage(damage, useSpellSoul);
        return spellId == LegacyLightningSlashPassiveSpellId
            ? MulDivToUInt32(damage, 22, 100)
            : damage;
    }

    private static int GetLegacyLightningSlashLineRangeIndex(
        long casterX,
        long casterY,
        long originX,
        long originY,
        long targetX,
        long targetY,
        byte range,
        byte width)
    {
        var deltaX = originX - casterX;
        var deltaY = originY - casterY;
        var divisor = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));
        if (divisor == 0)
        {
            return 0;
        }

        var stepX = (double)deltaX / divisor;
        var stepY = (double)deltaY / divisor;
        var x = (double)casterX;
        var y = (double)casterY;
        for (var index = 0; index <= range; index++)
        {
            if (LegacyGetDistance((long)Math.Round(x), (long)Math.Round(y), targetX, targetY) <= width)
            {
                return index;
            }

            x += stepX;
            y += stepY;
        }

        return range + 1;
    }

    private static uint CalculateLegacyLightningSlashSoulDamage(uint damage, byte useSpellSoul)
    {
        // Source Base.CalculateSoul(Damage, LevelSoul) returns Damage unchanged for this runtime slice.
        _ = useSpellSoul;
        return damage;
    }
}
