using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession?> ExecuteLegacyPhysicalSpellsPlayerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldActiveSession target,
        CancellationToken cancellationToken)
    {
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var results = new List<LegacyPlayerMagicDamageResult>();
        var updatedSession = session;
        var activeTarget = target;
        for (var index = 0; index < LegacyPhysicalSpellsHitCount; index++)
        {
            if (!CanLegacyPlayerMagicDamageTarget(context, updatedSession, activeTarget.Session, spell, out _))
            {
                break;
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

            var damage = statusDefended
                ? 1U / 2U
                : wildPhoenixDefended
                ? CalculateLegacyPhysicalSpellsWildPhoenixDefendedPlayerDamage()
                : CalculateLegacyPlayerPlayerPhysicalSpellDamageWithIncreaseAttack(
                    updatedSession,
                    activeTarget.Session,
                    spell,
                    context.WorldData,
                    damageDivisor: spell.Id is 16_330 or 16_340 ? 1U : 2U,
                    increaseAttack: ResolveLegacyPhysicalSpellsPlayerIncreaseAttack(
                        updatedSession,
                        activeTarget.Session,
                        spell));
            var receive = statusDefended || wildPhoenixDefended
                ? await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                        context,
                        activeTarget,
                        updatedSession.Character,
                        damage,
                        cancellationToken)
                    .ConfigureAwait(false)
                : await ApplyLegacyBackDamageEligiblePlayerDamageAsync(
                        context,
                        activeTarget,
                        updatedSession.Character,
                        damage,
                        cancellationToken,
                        postBackPhysicalSpellId: spell.Id,
                        postBackPhysicalDamageDivisor: 2)
                    .ConfigureAwait(false);
            if (receive.AttackerSession is { } carriedAttacker)
            {
                updatedSession = carriedAttacker;
                context = context with { Session = updatedSession };
            }
            if (receive.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return null;
            }
            if (receive.Result is not { } result)
            {
                break; // This target is lost; do not retry another hit against it.
            }

            context = context with { Session = updatedSession };

            (updatedSession, result) = await TrySendLegacyReceiveAttackPlayerSpeffectAttributesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };

            (updatedSession, _) = await TrySendLegacyPlayerReceiveNinjaSageModeAsync(
                    context,
                    updatedSession,
                    context.WorldData,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };

            if (result.DamageApplied == 0 &&
                !wildPhoenixDefended &&
                !statusDefended &&
                spell.Id is not (16_330 or 16_340))
            {
                break;
            }

            results.Add(result);
            activeTarget = result.ActiveSession;
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                spell.Id is 16_330 or 16_340 ? result.DamageApplied : damage,
                Hit: true,
                MsgAttackEffect.None));

            if (result.Killed)
            {
                break;
            }
        }

        await SendLegacyPhysicalSpellsAnimationAsync(
                context,
                updatedSession,
                query,
                playerSpell,
                spell,
                animationTargets,
                spellEffect: 0,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var result in results)
        {
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var totalDamage = results.Aggregate(0UL, (total, result) => total + result.DamageApplied);
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                totalDamage,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy PhysicalSpells player magic " +
                $"{FormatSession(context)} spellId={spell.Id} targetUid={target.Session.Character.Uid} " +
                $"targets={animationTargets.Count} totalDamage={totalDamage}.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static uint CalculateLegacyPhysicalSpellsWildPhoenixDefendedPlayerDamage()
    {
        return 0U;
    }

    private static int ResolveLegacyPhysicalSpellsPlayerIncreaseAttack(
        WorldSession attacker,
        WorldSession target,
        LegacyMagicRuntimeSpell spell)
    {
        if (spell.Id != LegacySuperTwofoldBladeSpellId)
        {
            return 0;
        }

        var boostedRange = (int)spell.Range - 2;
        return boostedRange >= 0 &&
            LegacyGetDistance(
                attacker.Character.X,
                attacker.Character.Y,
                target.Character.X,
                target.Character.Y) <= boostedRange
            ? 1
            : 0;
    }

    private static async ValueTask TrySendLegacyBloomOfDeathFollowUpAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                LegacyBloomOfDeathSpellId,
                out var playerSpell,
                out var spell))
        {
            return;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return;
        }

        await ExecuteLegacyBloomOfDeathLayTrapRadiusAsync(
                context with { Session = resourceSession },
                resourceSession,
                query,
                playerSpell,
                spell,
                "BloomOfDeath follow-up",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyPhysicalSpellsAnimationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        IReadOnlyList<LegacySpellAnimationTarget> targets,
        byte spellEffect,
        CancellationToken cancellationToken)
    {
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            targets,
            spellEffect: spellEffect);
        await SendPlayerSpellAnimationToVisibleAsync(context, session, animation, cancellationToken)
            .ConfigureAwait(false);
    }
}
