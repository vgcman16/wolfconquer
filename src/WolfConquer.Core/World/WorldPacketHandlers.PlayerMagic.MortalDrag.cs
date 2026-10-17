using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyMortalDragSpellId = 11_180;

    private static async Task<bool> HandleLegacyPlayerMortalDragMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.MortalDrag)
        {
            return false;
        }

        // MortalDrag.Execute calls CanUseSpell.Verified before its 11180 ID gate.
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

        if (spell.Id != LegacyMortalDragSpellId)
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
        var monsterAppliedDamage = 0UL;
        var playerAppliedDamage = 0UL;
        var now = DateTimeOffset.UtcNow;

        if (TryFindVisibleMonsterTargetByUid(
                context,
                updatedSession.Character,
                query.OpponentUid,
                out var monsterTarget))
        {
            selectedTargetKind = "monster";
            if (LegacyGetDistance(
                    updatedSession.Character.X,
                    updatedSession.Character.Y,
                    monsterTarget.Monster.X,
                    monsterTarget.Monster.Y) < spell.Range &&
                CanLegacyPlayerAttackMonster(
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

                if (monsterStates.TryMoveTo(
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
                    var requestedDamage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                        updatedSession,
                        context.WorldData,
                        movedMonster,
                        monsterTarget.MonsterFamily,
                        spell);
                    requestedDamage = CalculateLegacyMortalDragSoulDamage(
                        requestedDamage,
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
                            MsgAttackEffect.None));

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

                    // The matching decompile returns for every equipped NatureShield after moving the target;
                    // its rate check has an empty body and the return is unconditional.
                    if (TryGetLegacyEquippedRuneLevel(
                            playerTarget.Session.PlayerItems,
                            context.WorldData,
                            "NatureShield",
                            out var natureShieldLevel))
                    {
                        await context.Log.WriteLineAsync(
                                "WorldServer applied legacy MortalDrag NatureShield early return after player pull " +
                                $"{FormatSession(context)} spellId={spell.Id} targetUid={query.OpponentUid} " +
                                $"natureShieldLevel={natureShieldLevel} earlyReturn=true; " +
                                "source=MortalDrag.cs spell 11180 unconditional return.")
                            .ConfigureAwait(false);
                        return true;
                    }

                    (playerTarget, var wildPhoenixDefended) =
                        await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                                context,
                                playerTarget,
                                cancellationToken)
                            .ConfigureAwait(false);
                    var physicalDamage = wildPhoenixDefended
                        ? new LegacyPlayerPlayerPhysicalSpellDamageResult(1U, MsgAttackEffect.None)
                        : CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
                            updatedSession,
                            playerTarget.Session,
                            spell,
                            context.WorldData,
                            damageDivisor: 1);
                    var requestedDamage = CalculateLegacyMortalDragSoulDamage(
                        physicalDamage.Damage,
                        playerSpell.UseSpellSoul);
                    var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                        context,
                        playerTarget,
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

                    playerResult = result;
                    playerAppliedDamage = result.DamageApplied;
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
                    animationTargets.Add(new LegacySpellAnimationTarget(
                        CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid)),
                        requestedDamage,
                        Hit: true,
                        physicalDamage.Effect));
                    PlayerReceiveComplete:
                        ;
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

        updatedSession = await ApplyLegacyMortalDragIncreaseExperienceAsync(
                context,
                updatedSession,
                monsterProgressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                ClampLegacyProficiencyRawExperience(monsterProgressDamage),
                cancellationToken,
                activity: "MortalDrag magic weapon proficiency experience")
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                monsterProgressDamage,
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
                "WorldServer handled legacy MortalDrag magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetKind={selectedTargetKind} targets={animationTargets.Count} " +
                $"monsterProgress={monsterProgressDamage} monsterAppliedDamage={monsterAppliedDamage} " +
                $"playerAppliedDamage={playerAppliedDamage}; source=MortalDrag.cs spell 11180.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask SendLegacyMortalDragMonsterViewRefreshAsync(
        WorldPacketHandlerContext context,
        LegacyMonsterInstance movedMonster,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var statusFlags = context.MonsterStates?.CreateStatusFlagVector(
            movedMonster,
            now,
            LegacyMonsterSpawnPacket.StatusFlagVectorLength);
        var frame = movedMonster.ToSpawnPacket(
                clientCompatibility: context.ClientCompatibility,
                statusFlags: statusFlags)
            .ToFrameBytes();
        var sentToContext = false;
        if (context.Sessions is { } sessions)
        {
            foreach (var active in sessions.Snapshot())
            {
                if (!CanSeeMonster(active.Session.Character, movedMonster))
                {
                    continue;
                }

                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                sentToContext |= ReferenceEquals(active.Connection, context.Connection);
            }
        }

        if (!sentToContext &&
            context.Session is { } currentSession &&
            CanSeeMonster(currentSession.Character, movedMonster))
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask SendLegacyMortalDragPlayerViewRoleRefreshAsync(
        WorldPacketHandlerContext context,
        WorldActiveSession pulledTarget,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            return;
        }

        var pulledCharacter = pulledTarget.Session.Character;
        var pulledSpawn = CreatePlayerSpawnPacket(pulledTarget.Session, context.WorldData).ToFrameBytes();
        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, pulledTarget.Connection) ||
                active.Session.Character.Uid == pulledCharacter.Uid)
            {
                continue;
            }

            if (CanSeePlayer(pulledCharacter, active.Session.Character))
            {
                var peerSpawn = CreatePlayerSpawnPacket(active.Session, context.WorldData).ToFrameBytes();
                await pulledTarget.Connection.SendAsync(peerSpawn, cancellationToken).ConfigureAwait(false);
            }

            if (CanSeePlayer(active.Session.Character, pulledCharacter))
            {
                await active.Connection.SendAsync(pulledSpawn, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static ulong CalculateLegacyMortalDragMonsterProgressDamage(
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

    private static async ValueTask<WorldSession> ApplyLegacyMortalDragIncreaseExperienceAsync(
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

    private static uint CalculateLegacyMortalDragSoulDamage(uint damage, byte useSpellSoul)
    {
        _ = useSpellSoul;
        return damage;
    }
}
