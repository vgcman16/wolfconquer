using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyStraightFistSpellId = 12_760;
    private const ushort LegacyStraightFistSourceSilentSpellId = 12_230;
    private const byte LegacyStraightFistRange = 5;
    private const string LegacyStraightFistCounterMissShootAttribute = "MisShoot";

    private static async ValueTask<bool> HandleLegacyPlayerStraightFistMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.StraightFist ||
            spell.Id is not (LegacyStraightFistSpellId or LegacyStraightFistSourceSilentSpellId))
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

        var updatedSession = ApplyLegacyStraightFistCounterMissShoot(
            context,
            resourceSession,
            out var counterMissShoots);
        context = context with { Session = updatedSession };

        // StraightFist.cs verifies 12230, then falls through the spell-id switch without an animation.
        if (spell.Id == LegacyStraightFistSourceSilentSpellId)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer source-backed returned legacy StraightFist magic " +
                    $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                    $"counterMissShoots={counterMissShoots}; " +
                    "source=StraightFist.cs verified 12230 then returned without animation or damage.")
                .ConfigureAwait(false);
            return true;
        }

        var lineWidth = playerSpell.UseSpellSoul > 0 ? (byte)1 : (byte)0;
        var monsterTargets = context.MonsterStates is not null
            ? SelectLegacyStraightFistMonsterTargets(context, updatedSession, query, lineWidth)
            : [];
        var playerTargets = SelectLegacyStraightFistPlayerTargets(
            context,
            updatedSession,
            query,
            spell,
            lineWidth);
        var sobNpcTargets = context.SobNpcStates is not null
            ? SelectLegacyStraightFistSobNpcTargets(context, updatedSession, query, lineWidth)
            : [];

        updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                updatedSession,
                playerTargets,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = ApplyLegacyCounterHitShoot(
            context,
            updatedSession,
            playerTargets.Count,
            out var counterHitShoots);
        context = context with { Session = updatedSession };

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(
            LegacyPlayerMonsterMagicTarget Target,
            MonsterDamageResult Result,
            uint RuntimeDamage,
            uint Progress)>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var sobNpcResults = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            ulong Progress)>();
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
                var runtimeDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    updatedSession,
                    context.WorldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
                runtimeDamage = CalculateLegacyStraightFistSoulDamage(
                    runtimeDamage,
                    playerSpell.UseSpellSoul);
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    target.Monster,
                    runtimeDamage,
                    now,
                    updatedSession);
                var progress = CalculateLegacyStraightFistMonsterProgress(
                    updatedSession,
                    target.Monster,
                    result);
                monsterResults.Add((target, result, runtimeDamage, progress));
                if (result.AlreadyDead)
                {
                    continue;
                }

                var animationDamage = CalculateLegacyStraightFistSoulDamage(
                    result.RequestedDamage,
                    playerSpell.UseSpellSoul);
                animationTargets.Add(new LegacySpellAnimationTarget(
                    target.Monster.Uid,
                    animationDamage,
                    Hit: animationDamage > 0,
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
            damage = CalculateLegacyStraightFistSoulDamage(damage, playerSpell.UseSpellSoul);
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
            if (result.UpdatedAttackerSession is { } updatedAttackerSession)
            {
                updatedSession = updatedAttackerSession;
                context = context with { Session = updatedSession };
            }

            playerResults.Add(result);
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
                damage = CalculateLegacyStraightFistSoulDamage(damage, playerSpell.UseSpellSoul);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                var progress = CalculateLegacySobNpcExperienceDamage(target.SobNpc, result);
                sobNpcResults.Add((target.SobNpc, result, progress));
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

        var postAnimation = await ApplyLegacyStraightFistPostAnimationAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                monsterResults,
                playerResults,
                sobNpcResults,
                killedTrainingSobNpcTargets,
                eventSobNpcSideEffectTargets,
                cancellationToken)
            .ConfigureAwait(false);
        updatedSession = postAnimation.Session;
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy StraightFist magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetX={query.X} targetY={query.Y} range={LegacyStraightFistRange} width={lineWidth} " +
                $"targets={animationTargets.Count} monsterTargets={monsterResults.Count} " +
                $"playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count} " +
                $"monsterProgress={postAnimation.MonsterProgress} sobNpcProgress={postAnimation.SobNpcProgress} " +
                $"totalAppliedDamage={postAnimation.TotalAppliedDamage} counterMissShoots={counterMissShoots} " +
                $"counterHitShoots={counterHitShoots}; source=StraightFist.cs case 12760.")
            .ConfigureAwait(false);
        return true;
    }

}
