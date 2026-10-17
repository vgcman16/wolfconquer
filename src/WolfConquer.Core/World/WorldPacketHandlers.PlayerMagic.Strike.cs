using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyStrike12600SpellId = 12_600;
    private const ushort LegacyGunfireStrikeSpellId = 14_590;

    private static async ValueTask<bool> HandleLegacyPlayerStrikeMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.Strike ||
            spell.Id is not (LegacyStrike12600SpellId or LegacyGunfireStrikeSpellId))
        {
            return false;
        }

        var isSourceStrike12600 = spell.Id == LegacyStrike12600SpellId;

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
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        (LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result, uint RequestedDamage)? monsterResult = null;
        (SobNpc SobNpc, SobNpcDamageResult Result, LegacySobNpcDeathBehavior DeathBehavior, uint RequestedDamage)? sobNpcResult = null;
        var selectedTargetKind = "none";
        var monsterProgressDamage = 0UL;
        var sobNpcProgressDamage = 0UL;
        var now = DateTimeOffset.UtcNow;

        if (TryFindVisibleMonsterTargetByUid(
                context,
                updatedSession.Character,
                query.OpponentUid,
                out var monsterTarget))
        {
            selectedTargetKind = "monster";
            if (CanLegacyPlayerAttackMonster(
                    updatedSession.Character,
                    monsterTarget.Monster,
                    monsterTarget.MonsterFamily,
                    out _) &&
                context.MonsterStates is { } monsterStates)
            {
                if (IsLegacyGuardMonsterFamily(monsterTarget.Monster, monsterTarget.MonsterFamily))
                {
                    updatedSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                            context,
                            updatedSession,
                            cancellationToken)
                        .ConfigureAwait(false);
                    context = context with { Session = updatedSession };
                }

                var requestedDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    updatedSession,
                    context.WorldData,
                    monsterTarget.Monster,
                    monsterTarget.MonsterFamily,
                    spell);
                requestedDamage = CalculateLegacyStrikeSoulDamage(
                    requestedDamage,
                    playerSpell.UseSpellSoul);
                if (isSourceStrike12600)
                {
                    requestedDamage = unchecked(requestedDamage * 2U);
                }
                var result = ApplyLegacyPlayerMonsterDamage(
                    monsterStates,
                    monsterTarget.Monster,
                    requestedDamage,
                    now,
                    updatedSession);
                if (!result.AlreadyDead)
                {
                    monsterResult = (monsterTarget, result, requestedDamage);
                    monsterProgressDamage = CalculateLegacyStrikeMonsterProgressDamage(
                        updatedSession,
                        monsterTarget,
                        requestedDamage);
                    animationTargets.Add(new LegacySpellAnimationTarget(
                        monsterTarget.Monster.Uid,
                        requestedDamage,
                        Hit: requestedDamage > 0,
                        MsgAttackEffect.None));
                }
            }
        }
        else if (TryGetVisibleSessionByUid(
                     context,
                     updatedSession,
                     query.OpponentUid,
                     out var playerTarget))
        {
            selectedTargetKind = "player";
            if (!CanLegacyPlayerMagicDamageTarget(
                    context,
                    updatedSession,
                    playerTarget.Session,
                    spell,
                    out _,
                    out var failureEffect))
            {
                if (failureEffect == LegacyPlayerMagicGateFailureEffect.ForbiddenPirate)
                {
                    await SendScalarUpdateAsync(
                            context,
                            updatedSession.Character,
                            LegacyMsgUpdateDataType.ForbiddenPirate,
                            1,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (failureEffect == LegacyPlayerMagicGateFailureEffect.HellVortexCounter)
                {
                    await TrySendLegacyPlayerHellVortexCounterAsync(
                            context,
                            updatedSession,
                            playerTarget,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                        context,
                        updatedSession,
                        [new LegacyPlayerMagicTarget(playerTarget)],
                        cancellationToken)
                    .ConfigureAwait(false);
                context = context with { Session = updatedSession };

                var activeTarget = playerTarget;
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
                var requestedDamage = CalculateLegacyStrikeSoulDamage(
                    physicalDamage.Damage,
                    playerSpell.UseSpellSoul);
                requestedDamage = MulDivToUInt32(requestedDamage, 2, 3);
                var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
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
                    goto PlayerReceiveComplete;
                }
                if (result.UpdatedAttackerSession is not null)
                {
                    updatedSession = result.UpdatedAttackerSession;
                    context = context with { Session = updatedSession };
                }

                playerResults.Add(result);
                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                    requestedDamage,
                    Hit: true,
                    physicalDamage.Effect));
                PlayerReceiveComplete:
                    ;
            }
        }
        else if (TryFindVisibleSobNpcByUid(
                     context,
                     updatedSession.Character,
                     query.OpponentUid,
                     out var sobNpcTarget))
        {
            selectedTargetKind = "sob-npc";
            if (!CanLegacySobNpcMagicDamageTarget(
                    context,
                    updatedSession.Character,
                    sobNpcTarget,
                    out var failureDiagnostic))
            {
                if (TryCreateLegacySobNpcMagicGateDialog(
                        updatedSession.Character,
                        sobNpcTarget,
                        failureDiagnostic,
                        out var gateDialog))
                {
                    await context.Connection.SendAsync(gateDialog.ToFrameBytes(), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            else if (context.SobNpcStates is { } sobNpcStates)
            {
                var requestedDamage = CalculateLegacySobNpcPhysicalSpellDamage(
                    updatedSession,
                    spell,
                    context.WorldData,
                    damageMultiplier: 1);
                requestedDamage = CalculateLegacyStrikeSoulDamage(
                    requestedDamage,
                    playerSpell.UseSpellSoul);
                if (isSourceStrike12600)
                {
                    requestedDamage = unchecked(requestedDamage * 4U);
                }
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(sobNpcTarget);
                var result = sobNpcStates.ApplyDamage(
                    sobNpcTarget,
                    requestedDamage,
                    now,
                    deathBehavior);
                if (!result.AlreadyDead)
                {
                    sobNpcResult = (sobNpcTarget, result, deathBehavior, requestedDamage);
                    sobNpcProgressDamage = CalculateLegacySobNpcExperienceDamage(sobNpcTarget, result);
                    animationTargets.Add(new LegacySpellAnimationTarget(
                        CheckedUInt32(sobNpcTarget.Id, nameof(sobNpcTarget.Id)),
                        requestedDamage,
                        Hit: requestedDamage > 0,
                        MsgAttackEffect.None));
                }
            }
        }

        if (monsterResult is { } monsterHit)
        {
            if (monsterHit.Result.Killed)
            {
                var death = new MsgAttackPacket(new MsgAttackQuery(
                    Uid: CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                    OpponentUid: monsterHit.Target.Monster.Uid,
                    X: monsterHit.Target.Monster.X,
                    Y: monsterHit.Target.Monster.Y,
                    AttackType: MsgAttackType.Death,
                    Damage: 0,
                    KilledMonster: 1,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0));
                await SendMonsterCombatFrameToVisibleAsync(
                        context,
                        updatedSession,
                        monsterHit.Target.Monster,
                        death,
                        cancellationToken)
                    .ConfigureAwait(false);
                await TrySendLegacyMonsterDeathDropAsync(
                        context,
                        updatedSession,
                        monsterHit.Target.Monster,
                        monsterHit.Target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                updatedSession = await TryApplyLegacyBossDeathRewardsAsync(
                        context,
                        updatedSession,
                        monsterHit.Target.Monster,
                        monsterHit.Target.MonsterFamily,
                        cancellationToken)
                    .ConfigureAwait(false);
                context = context with { Session = updatedSession };
                await TryApplyLegacyMonsterTeamExperienceAsync(
                        context,
                        updatedSession,
                        monsterHit.Target.Monster,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                if (LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(monsterHit.Target.Monster))
                {
                    var damagedMonster = monsterHit.Target.Monster with
                    {
                        Hitpoints = monsterHit.Result.RemainingHitpoints,
                    };
                    foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                    {
                        await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                if (monsterHit.Result.DamageApplied > 0)
                {
                    await TrySendLegacyMonsterRetaliationAsync(
                            context,
                            updatedSession,
                            monsterHit.Target.Monster,
                            monsterHit.Target.MonsterFamily,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        foreach (var result in playerResults)
        {
            await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (sobNpcResult is { } sobNpcHit)
        {
            if (sobNpcHit.Result.Killed && LegacySobNpcEventRules.IsTrainingSob(sobNpcHit.SobNpc))
            {
                await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                        context,
                        updatedSession,
                        sobNpcHit.SobNpc,
                        context.SobNpcStates!.GetVisible(sobNpcHit.SobNpc),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (!LegacySobNpcEventRules.IsTrainingSob(sobNpcHit.SobNpc))
            {
                await LogLegacySobNpcEventSideEffectGapAsync(
                        context,
                        sobNpcHit.SobNpc,
                        sobNpcHit.Result,
                        spell)
                    .ConfigureAwait(false);
            }
        }

        var progressDamage = monsterProgressDamage + sobNpcProgressDamage;
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

        var casterUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        foreach (var result in playerResults)
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
                playerResults,
                cancellationToken)
            .ConfigureAwait(false);

        var totalAppliedDamage =
            (monsterResult?.Result.DamageApplied ?? 0UL) +
            playerResults.Aggregate(0UL, (total, result) => total + result.DamageApplied) +
            (sobNpcResult?.Result.DamageApplied ?? 0UL);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Strike/Gunfire magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetKind={selectedTargetKind} targets={animationTargets.Count} " +
                $"monsterProgress={monsterProgressDamage} sobNpcProgress={sobNpcProgressDamage} " +
                $"progressDamage={progressDamage} totalAppliedDamage={totalAppliedDamage}; " +
                $"source=Strike.cs case {spell.Id}.")
            .ConfigureAwait(false);
        return true;
    }

    private static ulong CalculateLegacyStrikeMonsterProgressDamage(
        WorldSession session,
        LegacyPlayerMonsterMagicTarget target,
        uint requestedDamage)
    {
        if (requestedDamage == 0 ||
            LegacyMonsterDamageRules.IsLegacyGuardFamily(target.MonsterFamily))
        {
            return 0;
        }

        var adjustedDamage = AdjustLegacyMonsterExperience(
            checked((int)Math.Min(requestedDamage, target.Monster.MaxLife)),
            CheckedInt32(session.Character.Level, nameof(session.Character.Level)),
            target.Monster.Level);
        return checked((ulong)Math.Max(0, adjustedDamage));
    }

    private static async ValueTask<WorldSession> ApplyLegacyStrikeIncreaseExperienceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        ulong progressDamage,
        CancellationToken cancellationToken)
    {
        if (progressDamage == 0)
        {
            return session;
        }

        var boundedProgress = progressDamage > uint.MaxValue ? uint.MaxValue : (uint)progressDamage;
        var experienceResult = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateLegacyExperienceGainInputs(context, session, boundedProgress));
        var updatedSession = ApplyLegacyExperienceSideEffects(
            context,
            context.Connection,
            session,
            experienceResult);
        if (experienceResult.AppliedExperienceGain == 0)
        {
            return updatedSession;
        }

        return await ApplyLegacyFinalExperienceGainAsync(
                context,
                context.Connection,
                updatedSession,
                experienceResult.AppliedExperienceGain,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static uint CalculateLegacyStrikeSoulDamage(uint damage, byte useSpellSoul)
    {
        _ = useSpellSoul;
        return damage;
    }
}
