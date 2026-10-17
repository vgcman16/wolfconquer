using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerAttackStatusMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttackStatus)
        {
            return false;
        }

        if (spell.Id == LegacyAttackStatus7000SpellId)
        {
            return await HandleLegacyPlayerAttackStatus7000MagicAsync(
                    context,
                    session,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (spell.Id == LegacyAttackStatusVenomSpellId)
        {
            return await HandleLegacyPlayerAttackStatus20000MagicAsync(
                    context,
                    session,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (spell.Id == LegacyAttackStatus15560SpellId)
        {
            return await HandleLegacyPlayerAttackStatus15560MagicAsync(
                    context,
                    session,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (spell.Id == LegacyBonePulseSpellId)
        {
            return await HandleLegacyPlayerBonePulseAttackStatusMagicAsync(
                    context,
                    session,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (spell.Id == LegacyAttackStatus15880SpellId)
        {
            return await HandleLegacyPlayerAttackStatus15880MagicAsync(
                    context,
                    session,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (spell.Id == LegacyAttackStatus10800SpellId)
        {
            return await HandleLegacyPlayerAttackStatus10800MagicAsync(
                    context,
                    session,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsLegacyAttackStatusSpecialCaseSpell(spell.Id))
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

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var updatedSession = resourceSession;
        var totalDamage = 0UL;
        var handledTarget = false;

        if (context.MonsterStates is not null &&
            TryFindVisibleMonsterTargetByUid(context, resourceSession.Character, query.OpponentUid, out var monsterTarget))
        {
            handledTarget = true;
            (updatedSession, totalDamage) = await ExecuteLegacyAttackStatusMonsterAsync(
                    context,
                    resourceSession,
                    query,
                    spell,
                    monsterTarget,
                    animationTargets,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (TryGetVisibleSessionByUid(context, resourceSession, query.OpponentUid, out var playerTarget))
        {
            handledTarget = true;
            var playerOutcome = await ExecuteLegacyAttackStatusPlayerAsync(
                    context,
                    resourceSession,
                    playerTarget,
                    spell,
                    animationTargets,
                    cancellationToken)
                .ConfigureAwait(false);

            if (playerOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                return true;
            }

            updatedSession = playerOutcome.Session;
            totalDamage = playerOutcome.TotalDamage;
            context = context with { Session = updatedSession };
        }
        else if (context.SobNpcStates is not null &&
            TryFindVisibleSobNpcByUid(context, resourceSession.Character, query.OpponentUid, out var sobNpcTarget))
        {
            handledTarget = true;
            totalDamage = await ExecuteLegacyAttackStatusSobNpcAsync(
                    context,
                    resourceSession,
                    sobNpcTarget,
                    spell,
                    animationTargets,
                    cancellationToken)
                .ConfigureAwait(false);
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

        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                totalDamage,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy AttackStatus magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} handledTarget={handledTarget} " +
                $"targets={animationTargets.Count} totalDamage={totalDamage}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<(WorldSession Session, ulong TotalDamage)> ExecuteLegacyAttackStatusMonsterAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        LegacyPlayerMonsterMagicTarget target,
        List<LegacySpellAnimationTarget> animationTargets,
        CancellationToken cancellationToken)
    {
        if (!CanLegacyPlayerAttackMonster(session.Character, target.Monster, target.MonsterFamily, out _) ||
            LegacyGetDistance(session.Character.X, session.Character.Y, target.Monster.X, target.Monster.Y) >= spell.Range)
        {
            return (session, 0);
        }

        var requestedDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
            session,
            context.WorldData,
            target.Monster,
            target.MonsterFamily,
            spell);
        var kylinResult = ApplyLegacyKylinSigilPhysicalSpellDamage(
            context,
            session,
            spell.Id,
            requestedDamage);
        session = kylinResult.Session;
        requestedDamage = kylinResult.Damage;

        var result = context.MonsterStates!.ApplyDamage(
            target.Monster,
            requestedDamage,
            DateTimeOffset.UtcNow);
        if (result.AlreadyDead)
        {
            return (session, 0);
        }

        animationTargets.Add(new LegacySpellAnimationTarget(
            target.Monster.Uid,
            result.RequestedDamage,
            Hit: result.RequestedDamage > 0,
            MsgAttackEffect.None));

        var updatedSession = session;
        if (result.Killed)
        {
            var death = new MsgAttackPacket(new MsgAttackQuery(
                Uid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
                OpponentUid: target.Monster.Uid,
                X: target.Monster.X,
                Y: target.Monster.Y,
                AttackType: MsgAttackType.Death,
                Damage: 0,
                KilledMonster: 1,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0));
            await SendMonsterCombatFrameToVisibleAsync(context, session, target.Monster, death, cancellationToken)
                .ConfigureAwait(false);
            await TrySendLegacyMonsterDeathDropAsync(
                    context,
                    session,
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

            await TrySendLegacyMonsterRetaliationAsync(
                    context,
                    session,
                    target.Monster,
                    target.MonsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                context,
                updatedSession,
                target.Monster,
                target.MonsterFamily,
                requestedDamage,
                cancellationToken)
            .ConfigureAwait(false);
        return (updatedSession, requestedDamage);
    }

    private static async ValueTask<(WorldSession Session, ulong TotalDamage, LegacyPlayerMagicReceiveAbortReason AbortReason)> ExecuteLegacyAttackStatusPlayerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldActiveSession target,
        LegacyMagicRuntimeSpell spell,
        List<LegacySpellAnimationTarget> animationTargets,
        CancellationToken cancellationToken)
    {
        if (!CanLegacyPlayerMagicDamageTarget(context, session, target.Session, spell, out _) ||
            LegacyGetDistance(
                session.Character.X,
                session.Character.Y,
                target.Session.Character.X,
                target.Session.Character.Y) >= spell.Range)
        {
            return (session, 0, LegacyPlayerMagicReceiveAbortReason.None);
        }

        var activeTarget = target;
        (activeTarget, var wildPhoenixDefended) =
            await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                    context,
                    activeTarget,
                    cancellationToken)
                .ConfigureAwait(false);
        var physicalDamage = wildPhoenixDefended
            ? new LegacyPlayerPlayerPhysicalSpellDamageResult(1U, MsgAttackEffect.None)
            : CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
                session,
                activeTarget.Session,
                spell,
                context.WorldData,
                damageDivisor: 4);
        var damage = physicalDamage.Damage;
        if (wildPhoenixDefended)
        {
            damage /= 4U;
        }

        var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
            context,
            activeTarget,
            session.Character,
            damage,
            cancellationToken)
        .ConfigureAwait(false);
        var updatedSession = receiveOutcome.AttackerSession ?? session;
        context = context with { Session = updatedSession };
        if (receiveOutcome.Result is not { } result)
        {
            return (updatedSession, 0, receiveOutcome.AbortReason);
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

        if (result.DamageApplied == 0 && !wildPhoenixDefended)
        {
            return (updatedSession, 0, LegacyPlayerMagicReceiveAbortReason.None);
        }

        animationTargets.Add(new LegacySpellAnimationTarget(
            CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
            damage,
            Hit: true,
            physicalDamage.Effect));

        await SendLegacyPlayerMagicDamageUpdatesAsync(context, updatedSession, result, cancellationToken)
            .ConfigureAwait(false);
        await TryApplyLegacyPlayerMagicChillingSnowAsync(
                context,
                updatedSession,
                [result],
                cancellationToken)
            .ConfigureAwait(false);
        return (updatedSession, result.DamageApplied, LegacyPlayerMagicReceiveAbortReason.None);
    }

    private static async ValueTask<ulong> ExecuteLegacyAttackStatusSobNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        SobNpc sobNpc,
        LegacyMagicRuntimeSpell spell,
        List<LegacySpellAnimationTarget> animationTargets,
        CancellationToken cancellationToken)
    {
        if (!CanLegacySobNpcMagicDamageTarget(context, session.Character, sobNpc, out _) ||
            LegacyGetDistance(session.Character.X, session.Character.Y, sobNpc.X, sobNpc.Y) >= spell.Range)
        {
            return 0;
        }

        var damage = CalculateLegacySobNpcPhysicalSpellDamage(
            session,
            spell,
            context.WorldData,
            damageMultiplier: 1);
        var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(sobNpc);
        var result = context.SobNpcStates!.ApplyDamage(sobNpc, damage, DateTimeOffset.UtcNow, deathBehavior);
        if (result.AlreadyDead || result.DamageApplied == 0)
        {
            return 0;
        }

        animationTargets.Add(new LegacySpellAnimationTarget(
            CheckedUInt32(sobNpc.Id, nameof(sobNpc.Id)),
            result.DamageApplied,
            Hit: true,
            MsgAttackEffect.None));

        if (result.Killed && LegacySobNpcEventRules.IsTrainingSob(sobNpc))
        {
            await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                    context,
                    session,
                    sobNpc,
                    context.SobNpcStates.GetVisible(sobNpc),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (!LegacySobNpcEventRules.IsTrainingSob(sobNpc))
        {
            await LogLegacySobNpcEventSideEffectGapAsync(context, sobNpc, result, spell)
                .ConfigureAwait(false);
        }

        return CalculateLegacySobNpcExperienceDamage(sobNpc, result);
    }

    private static bool IsLegacyAttackStatusSpecialCaseSpell(ushort spellId)
    {
        return spellId is 7_000 or 10_800 or 15_560 or 15_880 or 16_390 or 20_000;
    }
}
