using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyStarArrowSort = 38;
    private const ushort LegacyStarArrowSpellId = 10_313;
    private const string RuntimeLegacyStarArrowNatureShieldRollAttribute =
        "LegacyStarArrowNatureShieldRoll";

    private static async Task<bool> HandleLegacyPlayerStarArrowMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if ((int)spell.Sort != LegacyStarArrowSort)
        {
            return false;
        }

        // StarArrow.ExecuteExecute calls CanUseSpell.Verified before its 10313 ID gate.
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

        if (spell.Id != LegacyStarArrowSpellId)
        {
            return true;
        }

        var updatedSession = resourceSession;
        context = context with { Session = updatedSession };
        var casterUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        var casterX = CheckedUInt16(updatedSession.Character.X, nameof(updatedSession.Character.X));
        var casterY = CheckedUInt16(updatedSession.Character.Y, nameof(updatedSession.Character.Y));
        var animationTargets = new List<LegacySpellAnimationTarget>();
        LegacyPlayerMagicDamageResult? playerResult = null;
        var selectedTargetKind = "none";
        var monsterProgressDamage = 0UL;
        var sobNpcProgressDamage = 0UL;
        var monsterAppliedDamage = 0UL;
        var playerAppliedDamage = 0UL;
        var sobNpcAppliedDamage = 0UL;
        var now = DateTimeOffset.UtcNow;

        if (TryFindVisibleMonsterTargetByUid(
                context,
                updatedSession.Character,
                query.OpponentUid,
                out var monsterTarget))
        {
            selectedTargetKind = "monster";
            if (context.MonsterStates is { } monsterStates &&
                monsterStates.TryMoveTo(
                    monsterTarget.Monster,
                    casterX,
                    casterY,
                    now,
                    out var movedMonster))
            {
                await SendLegacyMortalDragMonsterViewRefreshAsync(
                        context,
                        movedMonster,
                        now,
                        cancellationToken)
                    .ConfigureAwait(false);

                var movedTarget = new LegacyPlayerMonsterMagicTarget(
                    movedMonster,
                    monsterTarget.MonsterFamily);
                if (LegacyGetDistance(
                        updatedSession.Character.X,
                        updatedSession.Character.Y,
                        movedMonster.X,
                        movedMonster.Y) < spell.Range &&
                    CanLegacyPlayerAttackMonster(
                        updatedSession.Character,
                        movedMonster,
                        monsterTarget.MonsterFamily,
                        out _))
                {
                    if (IsLegacyGuardMonsterFamily(movedMonster, monsterTarget.MonsterFamily))
                    {
                        updatedSession = await TryApplyLegacyGuardAttackFlashingNameAsync(
                                context,
                                updatedSession,
                                cancellationToken)
                            .ConfigureAwait(false);
                        context = context with { Session = updatedSession };
                    }

                    var rangeDamage = CalculateLegacyAttackStatus7000MonsterRangeDamage(
                        updatedSession,
                        context.WorldData,
                        movedMonster,
                        monsterTarget.MonsterFamily,
                        spell);
                    var requestedDamage = CalculateLegacyAttackStatus7000SoulDamage(
                        rangeDamage.Damage,
                        playerSpell.UseSpellSoul);
                    var result = ApplyLegacyPlayerMonsterDamage(
                        monsterStates,
                        movedMonster,
                        requestedDamage,
                        now,
                        updatedSession);
                    if (!result.AlreadyDead)
                    {
                        monsterAppliedDamage = result.DamageApplied;
                        monsterProgressDamage = CalculateLegacyMortalDragMonsterProgressDamage(
                            updatedSession,
                            movedTarget,
                            result.RequestedDamage);
                        animationTargets.Add(new LegacySpellAnimationTarget(
                            movedMonster.Uid,
                            result.RequestedDamage,
                            Hit: result.RequestedDamage > 0,
                            rangeDamage.Effect));

                        if (result.Killed)
                        {
                            var death = new MsgAttackPacket(new MsgAttackQuery(
                                Uid: casterUid,
                                OpponentUid: movedMonster.Uid,
                                X: movedMonster.X,
                                Y: movedMonster.Y,
                                AttackType: MsgAttackType.Death,
                                Damage: 0,
                                KilledMonster: 1,
                                ResponseDamage: 0,
                                Effect: MsgAttackEffect.None,
                                Unknown: 0));
                            await SendMonsterCombatFrameToVisibleAsync(
                                    context,
                                    updatedSession,
                                    movedMonster,
                                    death,
                                    cancellationToken)
                                .ConfigureAwait(false);
                            await TrySendLegacyMonsterDeathDropAsync(
                                    context,
                                    updatedSession,
                                    movedMonster,
                                    monsterTarget.MonsterFamily,
                                    cancellationToken)
                                .ConfigureAwait(false);
                            updatedSession = await TryApplyLegacyBossDeathRewardsAsync(
                                    context,
                                    updatedSession,
                                    movedMonster,
                                    monsterTarget.MonsterFamily,
                                    cancellationToken)
                                .ConfigureAwait(false);
                            context = context with { Session = updatedSession };
                            await TryApplyLegacyMonsterTeamExperienceAsync(
                                    context,
                                    updatedSession,
                                    movedMonster,
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }
                        else if (LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(movedMonster))
                        {
                            var damagedMonster = movedMonster with { Hitpoints = result.RemainingHitpoints };
                            foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                            {
                                await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                                    .ConfigureAwait(false);
                            }
                        }
                    }
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
            var pulledTargetSession = playerTarget.Session with
            {
                Character = playerTarget.Session.Character with
                {
                    X = casterX,
                    Y = casterY,
                },
            };
            context.Sessions?.Set(playerTarget.Connection, pulledTargetSession);
            playerTarget = playerTarget with { Session = pulledTargetSession };
            await SendLegacyMortalDragPlayerViewRoleRefreshAsync(
                    context,
                    playerTarget,
                    cancellationToken)
                .ConfigureAwait(false);

            if (LegacyGetDistance(
                    updatedSession.Character.X,
                    updatedSession.Character.Y,
                    playerTarget.Session.Character.X,
                    playerTarget.Session.Character.Y) < spell.Range)
            {
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

                    if (TryGetLegacyEquippedRuneLevel(
                            playerTarget.Session.PlayerItems,
                            context.WorldData,
                            "NatureShield",
                            out var natureShieldLevel) &&
                        CheckLegacyStarArrowNatureShieldRateRoll(
                            playerTarget.Session.Character,
                            natureShieldLevel))
                    {
                        await context.Log.WriteLineAsync(
                                "WorldServer applied legacy StarArrow NatureShield early return after player pull " +
                                $"{FormatSession(context)} spellId={spell.Id} targetUid={query.OpponentUid} " +
                                $"natureShieldLevel={natureShieldLevel} earlyReturn=true; " +
                                "source=StarArrow.cs spell 10313 chance return.")
                            .ConfigureAwait(false);
                        return true;
                    }

                    var activeTarget = playerTarget;
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

                    LegacyPlayerPlayerPhysicalSpellDamageResult rangeDamage;
                    if (statusDefended)
                    {
                        rangeDamage = new LegacyPlayerPlayerPhysicalSpellDamageResult(
                            CalculateLegacyPlayerRangeStatusReturnDamage(
                                updatedSession,
                                activeTarget.Session,
                                context.WorldData),
                            MsgAttackEffect.None);
                    }
                    else if (wildPhoenixDefended)
                    {
                        rangeDamage = new LegacyPlayerPlayerPhysicalSpellDamageResult(1U, MsgAttackEffect.None);
                    }
                    else
                    {
                        rangeDamage = CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
                            updatedSession,
                            activeTarget.Session,
                            spell,
                            context.WorldData,
                            damageDivisor: 1);
                    }

                    var requestedDamage = CalculateLegacyAttackStatus7000SoulDamage(
                        rangeDamage.Damage,
                        playerSpell.UseSpellSoul);
                    var rangeEffect = rangeDamage.Effect;
                    var originalAttackerSession = updatedSession;
                    if (!statusDefended && !wildPhoenixDefended)
                    {
                        if (HasLegacyStatusFlag(activeTarget.Session.Character, LegacyDefensiveStanceFlag))
                        {
                            requestedDamage = LegacyCombatDamageCalculator.DecreaseLegacyBless(
                                requestedDamage,
                                40);
                            rangeEffect = MsgAttackEffect.Block;
                        }
                        else
                        {
                            // Range.OnPlayer completes BackDmg before entering ReceiveAttack.Player.
                            var receiveAttacker = updatedSession.Character;
                            var backDamageEnteredAlive = receiveAttacker.MinHitPoints > 0;
                            (context, activeTarget, receiveAttacker, var backDamageAbortReason) =
                                await TryApplyLegacyPlayerReceiveHeavensWonderAsync(
                                        context,
                                        activeTarget,
                                        receiveAttacker,
                                        cancellationToken)
                                    .ConfigureAwait(false);
                            updatedSession = context.Session ?? updatedSession;
                            context = context with { Session = updatedSession };
                            if (backDamageAbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
                            {
                                return true;
                            }
                            if (backDamageAbortReason == LegacyPlayerMagicReceiveAbortReason.TargetLost)
                            {
                                goto PlayerReceiveComplete;
                            }
                            (context, activeTarget, receiveAttacker, backDamageAbortReason) =
                                await TryApplyLegacyPlayerReceiveRevengeTailAsync(
                                        context,
                                        activeTarget,
                                        receiveAttacker,
                                        requestedDamage,
                                        cancellationToken,
                                        backDamageEnteredAlive: backDamageEnteredAlive)
                                    .ConfigureAwait(false);
                            updatedSession = context.Session ?? updatedSession;
                            context = context with { Session = updatedSession };
                            if (backDamageAbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
                            {
                                return true;
                            }
                            if (backDamageAbortReason == LegacyPlayerMagicReceiveAbortReason.TargetLost)
                            {
                                goto PlayerReceiveComplete;
                            }
                            updatedSession = context.Session ?? updatedSession;
                            context = context with { Session = updatedSession };
                        }
                    }

                    // Carry the pre-hit sessions through the opening receive hooks without applying modifiers or HP.
                    var openingReceiveResult = new LegacyPlayerMagicDamageResult(
                        activeTarget,
                        activeTarget.Session,
                        CheckedUInt32(
                            activeTarget.Session.Character.MinHitPoints,
                            nameof(activeTarget.Session.Character.MinHitPoints)),
                        DamageApplied: 0,
                        Killed: false);
                    var combatGearOutcome = await ExecuteLegacyPlayerReceiveCombatGearOpeningAsync(
                            context,
                            new LegacyPlayerReceiveCombatGearRequest(
                                updatedSession,
                                activeTarget,
                                casterUid,
                                query.OpponentUid),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (combatGearOutcome.AttackerSession is { } currentCombatGearAttacker)
                    {
                        updatedSession = currentCombatGearAttacker;
                        context = context with { Session = updatedSession };
                    }

                    if (combatGearOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
                    {
                        return true;
                    }

                    if (combatGearOutcome.Target is not { } combatGearTarget)
                    {
                        goto PlayerReceiveComplete;
                    }

                    openingReceiveResult = openingReceiveResult with
                    {
                        ActiveSession = combatGearTarget,
                        UpdatedSession = combatGearTarget.Session,
                        UpdatedAttackerSession = updatedSession,
                    };
                    context = context with { Session = updatedSession };

                    (updatedSession, openingReceiveResult) = await TrySendLegacyReceiveAttackPlayerSpeffectAttributesAsync(
                            context,
                            updatedSession,
                            openingReceiveResult,
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

                    var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                        context,
                        openingReceiveResult.ActiveSession with { Session = openingReceiveResult.UpdatedSession },
                        updatedSession.Character,
                        requestedDamage,
                        cancellationToken,
                        combatGearReceipt: combatGearOutcome.Receipt)
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
                    if (result.UpdatedAttackerSession is null &&
                        !Equals(updatedSession, originalAttackerSession))
                    {
                        result = result with { UpdatedAttackerSession = updatedSession };
                    }

                    if (result.UpdatedAttackerSession is not null)
                    {
                        updatedSession = result.UpdatedAttackerSession;
                        context = context with { Session = updatedSession };
                    }

                    playerResult = result;
                    playerAppliedDamage = result.DamageApplied;
                    animationTargets.Add(new LegacySpellAnimationTarget(
                        CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                        result.ReceiveDamage ?? requestedDamage,
                        Hit: true,
                        rangeEffect,
                        NewX: casterX,
                        NewY: casterY));
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
                    PlayerReceiveComplete:
                        ;
                }
            }
        }
        else if (TryFindVisibleSobNpcByUid(
                     context,
                     updatedSession.Character,
                     query.OpponentUid,
                     out var sobNpcTarget))
        {
            selectedTargetKind = "sob-npc";
            if (LegacyGetDistance(
                    updatedSession.Character.X,
                    updatedSession.Character.Y,
                    sobNpcTarget.X,
                    sobNpcTarget.Y) < spell.Range)
            {
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
                    var requestedDamage = CalculateLegacyAttackStatus7000SobNpcRangeDamage(
                        updatedSession,
                        spell,
                        context.WorldData);
                    requestedDamage = CalculateLegacyAttackStatus7000SoulDamage(
                        requestedDamage,
                        playerSpell.UseSpellSoul);
                    var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(
                        sobNpcTarget,
                        context.WorldData?.StaticStatues);
                    var result = sobNpcStates.ApplyDamage(
                        sobNpcTarget,
                        requestedDamage,
                        now,
                        deathBehavior);
                    if (!result.AlreadyDead)
                    {
                        sobNpcAppliedDamage = result.DamageApplied;
                        sobNpcProgressDamage = CalculateLegacySobNpcExperienceDamage(sobNpcTarget, result);
                        animationTargets.Add(new LegacySpellAnimationTarget(
                            CheckedUInt32(sobNpcTarget.Id, nameof(sobNpcTarget.Id)),
                            requestedDamage,
                            Hit: false,
                            MsgAttackEffect.None));

                        if (result.Killed && LegacySobNpcEventRules.IsTrainingSob(sobNpcTarget))
                        {
                            await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                                    context,
                                    updatedSession,
                                    sobNpcTarget,
                                    sobNpcStates.GetVisible(sobNpcTarget),
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }
                        else if (!LegacySobNpcEventRules.IsTrainingSob(sobNpcTarget))
                        {
                            await LogLegacySobNpcEventSideEffectGapAsync(
                                    context,
                                    sobNpcTarget,
                                    result,
                                    spell)
                                .ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        if (playerResult is { } hitPlayer)
        {
            await TryApplyLegacyPlayerMagicChillingSnowAsync(
                    context,
                    updatedSession,
                    [hitPlayer],
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var progressDamage = monsterProgressDamage + sobNpcProgressDamage;
        updatedSession = await ApplyLegacyMortalDragIncreaseExperienceAsync(
                context,
                updatedSession,
                progressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(progressDamage),
                cancellationToken,
                activity: "StarArrow magic weapon proficiency experience")
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

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy StarArrow magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetKind={selectedTargetKind} targets={animationTargets.Count} " +
                $"monsterProgress={monsterProgressDamage} sobNpcProgress={sobNpcProgressDamage} " +
                $"progressDamage={progressDamage} monsterAppliedDamage={monsterAppliedDamage} " +
                $"playerAppliedDamage={playerAppliedDamage} sobNpcAppliedDamage={sobNpcAppliedDamage}; " +
                "source=matching DecompiledSource StarArrow.cs spell 10313 sort 38.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool CheckLegacyStarArrowNatureShieldRateRoll(
        CharacterSnapshot targetCharacter,
        byte itemLevel)
    {
        var rate = GetLegacyBombPirteNatureShieldAvoidanceRate(itemLevel);
        return CheckLegacyBombPirteRateRoll(
            targetCharacter,
            rate,
            RuntimeLegacyStarArrowNatureShieldRollAttribute);
    }
}
