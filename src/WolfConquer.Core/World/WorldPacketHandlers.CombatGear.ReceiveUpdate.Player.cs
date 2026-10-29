using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(WorldSession AttackerSession, WorldSession DefenderSession, bool Applied, LegacyPlayerMagicReceiveAbortReason AbortReason)>
        TrySendLegacyCombatGearReceivePlayerUpdateAsync(
            WorldPacketHandlerContext context,
            WorldSession attackerSession,
            ConquerTcpConnection defenderConnection,
            WorldSession defenderSession,
            CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData)
        {
            return (attackerSession, defenderSession, false, LegacyPlayerMagicReceiveAbortReason.None);
        }

        var updatedAttackerSession = attackerSession;
        var updatedDefenderSession = defenderSession;
        var defenderX = CheckedUInt16(defenderSession.Character.X, nameof(defenderSession.Character.X));
        var defenderY = CheckedUInt16(defenderSession.Character.Y, nameof(defenderSession.Character.Y));
        var applied = false;

        if (HasLegacyCombatGearEquippedForClass(
            updatedAttackerSession.CombatGear ?? CombatGearSnapshot.Empty,
            updatedAttackerSession.Character.Class,
            LegacyCombatGearStoneCrackerItemId))
        {
            (updatedAttackerSession, updatedDefenderSession, var counterApplied, var counterAbortReason) =
                await TrySendLegacyCombatGearStoneCrackerCounterUpdateAsync(
                        context,
                        updatedAttackerSession,
                        defenderConnection,
                        updatedDefenderSession,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (counterAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
            {
                return (updatedAttackerSession, updatedDefenderSession, applied, counterAbortReason);
            }

            applied |= counterApplied;
            context = context with { Session = updatedAttackerSession };

            (updatedAttackerSession, updatedDefenderSession, var archShadowApplied) =
                await TrySendLegacyCombatGearStoneCrackerArchShadowUpdateAsync(
                        context,
                        updatedAttackerSession,
                        defenderConnection,
                        updatedDefenderSession,
                        worldData,
                        cancellationToken)
                    .ConfigureAwait(false);
            applied |= archShadowApplied;
            context = context with { Session = updatedAttackerSession };
        }

        (updatedAttackerSession, var dragonhowlApplied) =
            await TrySendLegacyCombatGearDragonhowlReceivePlayerUpdateAsync(
                    context,
                    updatedAttackerSession,
                    defenderX,
                    defenderY,
                    cancellationToken)
                .ConfigureAwait(false);
        applied |= dragonhowlApplied;
        context = context with { Session = updatedAttackerSession };

        (updatedAttackerSession, var bloodlustApplied) =
            await TrySendLegacyCombatGearBloodlustReceivePlayerUpdateAsync(
                    context,
                    updatedAttackerSession,
                    defenderX,
                    defenderY,
                    cancellationToken)
                .ConfigureAwait(false);
        applied |= bloodlustApplied;
        context = context with { Session = updatedAttackerSession };

        (updatedAttackerSession, updatedDefenderSession, var taoistApplied) =
            await TrySendLegacyCombatGearTaoistReceivePlayerUpdateAsync(
                    context,
                    updatedAttackerSession,
                    defenderConnection,
                    updatedDefenderSession,
                    cancellationToken)
                .ConfigureAwait(false);
        applied |= taoistApplied;
        context = context with { Session = updatedAttackerSession };

        (updatedAttackerSession, updatedDefenderSession, var pirateApplied) =
            await TrySendLegacyCombatGearPirateReceivePlayerUpdateAsync(
                    context,
                    updatedAttackerSession,
                    defenderConnection,
                    updatedDefenderSession,
                    cancellationToken)
                .ConfigureAwait(false);
        applied |= pirateApplied;
        context = context with { Session = updatedAttackerSession };

        return (updatedAttackerSession, updatedDefenderSession, applied, LegacyPlayerMagicReceiveAbortReason.None);
    }

    private static async ValueTask<(WorldSession AttackerSession, WorldSession DefenderSession, bool Applied)>
        TrySendLegacyCombatGearStoneCrackerArchShadowUpdateAsync(
            WorldPacketHandlerContext context,
            WorldSession attackerSession,
            ConquerTcpConnection defenderConnection,
            WorldSession defenderSession,
            WorldDataCatalog worldData,
            CancellationToken cancellationToken)
    {
        if (
            !TryResolveLegacyPlayerMagicRuntimeSpell(
                attackerSession,
                worldData,
                LegacyCombatGearArchShadowSpellId,
                out var playerSpell,
                out var spell) ||
            LegacyGetDistance(
                attackerSession.Character.X,
                attackerSession.Character.Y,
                defenderSession.Character.X,
                defenderSession.Character.Y) >= spell.Range ||
            !CanSeePlayer(attackerSession.Character, defenderSession.Character) ||
            !CanLegacyPlayerMagicDamageTarget(
                context,
                attackerSession,
                defenderSession,
                spell,
                out _) ||
            !CheckLegacyCombatGearAttackRateRoll(
                attackerSession.Character,
                LegacyCombatGearArchShadowSpellId,
                rate: 10))
        {
            return (attackerSession, defenderSession, false);
        }

        var archShadow = spell.DamageOnHuman != 0
            ? spell.DamageOnHuman
            : ReadLegacyMagicUInt32FieldOrDefault(
                worldData,
                LegacyCombatGearArchShadowSpellId,
                playerSpell.Level,
                fieldIndex: 35);
        var updatedDefenderCharacter = WithCharacterAttribute(
            WithLegacySourceAddSpellFlag(defenderSession.Character, LegacyCombatGearArchShadowFlag, removeOnDead: true),
            RuntimeLegacyCombatGearArchShadowAttribute,
            archShadow);
        var updatedDefenderSession = defenderSession with
        {
            Character = updatedDefenderCharacter,
        };
        context.Sessions?.Set(defenderConnection, updatedDefenderSession);

        var attackerContext = context with { Session = attackerSession };
        await SendPlayerStatusFlagVectorToVisibleAsync(
                attackerContext,
                defenderConnection,
                updatedDefenderSession,
                cancellationToken)
            .ConfigureAwait(false);

        var attackerUid = CheckedUInt32(attackerSession.Character.Uid, nameof(attackerSession.Character.Uid));
        var defenderUid = CheckedUInt32(updatedDefenderSession.Character.Uid, nameof(updatedDefenderSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            attackerUid,
            0,
            0,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(defenderUid, 0, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(attackerContext, attackerSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy CombatGear Update player ArchShadow " +
                $"{FormatSession(attackerContext)} defenderUid={updatedDefenderSession.Character.Uid} " +
                $"spellId={spell.Id} archshadow={archShadow} statusFlag={LegacyCombatGearArchShadowFlag}; " +
                "source=ReceiveAttack.Player.Execute -> client.CombatGear.Update(attacked) -> " +
                "MyCombatGear.Update StoneCracker -> CombatGearAttack 18980.")
            .ConfigureAwait(false);
        return (attackerSession, updatedDefenderSession, true);
    }

    private static async ValueTask<(WorldSession AttackerSession, WorldSession DefenderSession, bool Applied, LegacyPlayerMagicReceiveAbortReason AbortReason)>
        TrySendLegacyCombatGearStoneCrackerCounterUpdateAsync(
            WorldPacketHandlerContext context,
            WorldSession attackerSession,
            ConquerTcpConnection defenderConnection,
            WorldSession defenderSession,
            CancellationToken cancellationToken)
    {
        var attackerUid = attackerSession.Character.Uid;
        var defenderUid = defenderSession.Character.Uid;
        var updatedAttackerSession = attackerSession;
        var updatedDefenderSession = defenderSession;
        var applied = false;

        var arrowBlades = ReadAttributeUInt32(
            updatedAttackerSession.Character.Attributes,
            RuntimeLegacyCombatGearArrowBladesAttribute);
        if (arrowBlades <= 2)
        {
            arrowBlades++;
            updatedAttackerSession = WithUpdatedLegacyCombatGearAttackerAttribute(
                context,
                updatedAttackerSession,
                RuntimeLegacyCombatGearArrowBladesAttribute,
                arrowBlades);
            context = context with { Session = updatedAttackerSession };
            applied = true;
        }

        if (arrowBlades >= 3 &&
            !HasLegacyStatusFlag(updatedAttackerSession.Character, LegacyCombatGearWaterTop4WeeklyFlag))
        {
            updatedAttackerSession = await SendLegacyCombatGearStoneCrackerStatusAsync(
                    context,
                    updatedAttackerSession,
                    LegacyCombatGearWaterTop4WeeklyFlag,
                    LegacyMsgUpdateDataType.AppendIcon,
                    damage: 2,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedAttackerSession };
            var currentDefender = new WorldActiveSession(defenderConnection, updatedDefenderSession);
            var currentAttackerCharacter = updatedAttackerSession.Character;
            var participants = RefreshLegacyBackDamageParticipants(
                ref context, ref currentDefender, ref currentAttackerCharacter, attackerUid, defenderUid);
            if (participants != LegacyPlayerMagicReceiveAbortReason.None)
                return (context.Session ?? updatedAttackerSession, updatedDefenderSession, true, participants);
            updatedAttackerSession = context.Session ?? updatedAttackerSession;
            updatedDefenderSession = currentDefender.Session;
            applied = true;
        }

        var starFlow = ReadAttributeUInt32(
            updatedAttackerSession.Character.Attributes,
            RuntimeLegacyCombatGearStarFlowAttribute);
        if (starFlow <= 2)
        {
            starFlow++;
            if (starFlow == 3)
            {
                starFlow = 4;
                updatedAttackerSession = WithUpdatedLegacyCombatGearAttackerAttribute(
                    context,
                    updatedAttackerSession,
                    RuntimeLegacyCombatGearStarFlowAttribute,
                    starFlow);
                var starFlowOutcome = await SendLegacyCombatGearStoneCrackerStarFlowTriggerAsync(
                        context with { Session = updatedAttackerSession },
                        updatedAttackerSession,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (starFlowOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
                {
                    return (updatedAttackerSession, updatedDefenderSession, applied, starFlowOutcome.AbortReason);
                }

                updatedAttackerSession = starFlowOutcome.Session;
                context = context with { Session = updatedAttackerSession };
                var activeDefender = new WorldActiveSession(defenderConnection, updatedDefenderSession);
                var attackerCharacter = updatedAttackerSession.Character;
                var originalParticipants = RefreshLegacyBackDamageParticipants(
                    ref context, ref activeDefender, ref attackerCharacter, attackerUid, defenderUid);
                if (originalParticipants != LegacyPlayerMagicReceiveAbortReason.None)
                {
                    return (context.Session ?? updatedAttackerSession, updatedDefenderSession, applied, originalParticipants);
                }

                updatedAttackerSession = context.Session ?? updatedAttackerSession;
                updatedDefenderSession = activeDefender.Session;

                applied = true;
            }
            else
            {
                updatedAttackerSession = WithUpdatedLegacyCombatGearAttackerAttribute(
                    context,
                    updatedAttackerSession,
                    RuntimeLegacyCombatGearStarFlowAttribute,
                    starFlow);
                updatedAttackerSession = await SendLegacyCombatGearStoneCrackerStatusAsync(
                        context with { Session = updatedAttackerSession },
                        updatedAttackerSession,
                        LegacyCombatGearSageSpeedModeTop4WeeklyFlag,
                        LegacyMsgUpdateDataType.SageSpeedModeCounter,
                        damage: starFlow,
                        cancellationToken)
                    .ConfigureAwait(false);
                context = context with { Session = updatedAttackerSession };
                var currentDefender = new WorldActiveSession(defenderConnection, updatedDefenderSession);
                var currentAttackerCharacter = updatedAttackerSession.Character;
                var participants = RefreshLegacyBackDamageParticipants(
                    ref context, ref currentDefender, ref currentAttackerCharacter, attackerUid, defenderUid);
                if (participants != LegacyPlayerMagicReceiveAbortReason.None)
                    return (context.Session ?? updatedAttackerSession, updatedDefenderSession, true, participants);
                updatedAttackerSession = context.Session ?? updatedAttackerSession;
                updatedDefenderSession = currentDefender.Session;
                applied = true;
            }
        }

        starFlow = ReadAttributeUInt32(updatedAttackerSession.Character.Attributes, RuntimeLegacyCombatGearStarFlowAttribute);
        if (starFlow == 4)
        {
            starFlow = 0;
            updatedAttackerSession = WithUpdatedLegacyCombatGearAttackerAttribute(
                context,
                updatedAttackerSession,
                RuntimeLegacyCombatGearStarFlowAttribute,
                starFlow);
            context = context with { Session = updatedAttackerSession };
            applied = true;
        }

        if (applied)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy CombatGear Update player StoneCracker counters " +
                    $"{FormatSession(context)} arrowBlades={arrowBlades} starFlow={starFlow}; " +
                    "source=ReceiveAttack.Player.Execute -> client.CombatGear.Update(attacked) -> " +
                    "MyCombatGear.Update StoneCracker ArrowBlades/StarFlow.")
                .ConfigureAwait(false);
        }

        return (updatedAttackerSession, updatedDefenderSession, applied, LegacyPlayerMagicReceiveAbortReason.None);
    }

    private static async ValueTask<(WorldSession Session, bool Applied, LegacyPlayerMagicReceiveAbortReason AbortReason)>
        TrySendLegacyCombatGearStoneCrackerSobCounterUpdateAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            ushort targetX,
            ushort targetY,
            CancellationToken cancellationToken)
    {
        var ownerUid = session.Character.Uid;
        var updatedSession = session;
        var applied = false;

        var arrowBlades = ReadAttributeUInt32(
            updatedSession.Character.Attributes,
            RuntimeLegacyCombatGearArrowBladesAttribute);
        if (arrowBlades <= 2)
        {
            arrowBlades++;
            updatedSession = WithUpdatedLegacyCombatGearAttackerAttribute(
                context,
                updatedSession,
                RuntimeLegacyCombatGearArrowBladesAttribute,
                arrowBlades);
            context = context with { Session = updatedSession };
            applied = true;
        }

        if (arrowBlades >= 3 &&
            !HasLegacyStatusFlag(updatedSession.Character, LegacyCombatGearWaterTop4WeeklyFlag))
        {
            updatedSession = await SendLegacyCombatGearStoneCrackerStatusAsync(
                    context,
                    updatedSession,
                    LegacyCombatGearWaterTop4WeeklyFlag,
                    LegacyMsgUpdateDataType.AppendIcon,
                    damage: 2,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!TryRefreshLegacyPlayerMagicResourceSession(context, ownerUid, ref updatedSession))
                return (updatedSession, true, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
            context = context with { Session = updatedSession };
            applied = true;
        }

        var starFlow = ReadAttributeUInt32(
            updatedSession.Character.Attributes,
            RuntimeLegacyCombatGearStarFlowAttribute);
        if (starFlow <= 2)
        {
            starFlow++;
            if (starFlow == 3)
            {
                starFlow = 4;
                updatedSession = WithUpdatedLegacyCombatGearAttackerAttribute(
                    context,
                    updatedSession,
                    RuntimeLegacyCombatGearStarFlowAttribute,
                    starFlow);
                var starFlowOutcome = await SendLegacyCombatGearStoneCrackerStarFlowTriggerAsync(
                        context with { Session = updatedSession },
                        updatedSession,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (starFlowOutcome.AbortReason != LegacyPlayerMagicReceiveAbortReason.None)
                {
                    return (updatedSession, applied, starFlowOutcome.AbortReason);
                }

                updatedSession = starFlowOutcome.Session;
                context = context with { Session = updatedSession };
                applied = true;
            }
            else
            {
                updatedSession = WithUpdatedLegacyCombatGearAttackerAttribute(
                    context,
                    updatedSession,
                    RuntimeLegacyCombatGearStarFlowAttribute,
                    starFlow);
                updatedSession = await SendLegacyCombatGearStoneCrackerStatusAsync(
                        context with { Session = updatedSession },
                        updatedSession,
                        LegacyCombatGearSageSpeedModeTop4WeeklyFlag,
                        LegacyMsgUpdateDataType.SageSpeedModeCounter,
                        damage: starFlow,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!TryRefreshLegacyPlayerMagicResourceSession(context, ownerUid, ref updatedSession))
                    return (updatedSession, true, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
                context = context with { Session = updatedSession };
                applied = true;
            }
        }

        starFlow = ReadAttributeUInt32(updatedSession.Character.Attributes, RuntimeLegacyCombatGearStarFlowAttribute);
        if (starFlow == 4)
        {
            starFlow = 0;
            updatedSession = WithUpdatedLegacyCombatGearAttackerAttribute(
                context,
                updatedSession,
                RuntimeLegacyCombatGearStarFlowAttribute,
                starFlow);
            context = context with { Session = updatedSession };
            applied = true;
        }

        if (applied)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy CombatGear Update SOB StoneCracker counters " +
                    $"{FormatSession(context)} arrowBlades={arrowBlades} starFlow={starFlow} " +
                    $"targetX={targetX} targetY={targetY}; " +
                    "source=ReceiveAttack.Npc.Execute -> client.CombatGear.Update(attacked) -> " +
                    "MyCombatGear.Update StoneCracker ArrowBlades/StarFlow.")
                .ConfigureAwait(false);
        }

        return (updatedSession, applied, LegacyPlayerMagicReceiveAbortReason.None);
    }

    private static WorldSession WithUpdatedLegacyCombatGearAttackerAttribute(
        WorldPacketHandlerContext context,
        WorldSession session,
        string attributeName,
        uint value)
    {
        var updatedSession = session with
        {
            Character = WithCharacterAttribute(session.Character, attributeName, value),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> SendLegacyCombatGearStoneCrackerStatusAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        int statusFlag,
        LegacyMsgUpdateDataType dataType,
        uint damage,
        CancellationToken cancellationToken)
    {
        if (statusFlag == LegacyArrowBladesFlagRegistration.WaterFlag)
        {
            var uid = session.Character.Uid;
            var current = await ReplaceLegacyArrowBladesFlagAsync(
                    context, session, uid, statusFlag, cancellationToken).ConfigureAwait(false);
            if (current is null) return session;
            return await SendLegacyArrowBladesOwnerUpdateAsync(
                    context with { Session = current }, current, uid, statusFlag, dataType, damage, cancellationToken)
                .ConfigureAwait(false) ?? current;
        }
        var statusCharacter = WithLegacySourceAddSpellFlag(session.Character, statusFlag, removeOnDead: true);
        if (statusFlag == LegacyArrowBladesFlagRegistration.StarFlowFlag &&
            dataType == LegacyMsgUpdateDataType.SageSpeedModeCounter)
        {
            var registration = LegacyArrowBladesFlagRegistration.Create(
                session.Character.Uid, statusFlag, context.TimeProvider ?? TimeProvider.System);
            statusCharacter = registration.Install(statusCharacter);
        }
        var updatedSession = session with { Character = statusCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        if (statusFlag == LegacyCombatGearSageSpeedModeTop4WeeklyFlag &&
            dataType == LegacyMsgUpdateDataType.SageSpeedModeCounter)
        {
            var uid = session.Character.Uid;
            var frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                CreateLegacyRuntimeStatusFlagVector(updatedSession.Character)).ToFrameBytes();
            var current = await SendLegacyArrowBladesVisibleFrameAsync(
                    updatedContext, updatedSession, uid, frame, cancellationToken).ConfigureAwait(false);
            if (current is null) return updatedSession;
            return await SendLegacyArrowBladesOwnerUpdateAsync(
                    updatedContext with { Session = current }, current, uid, statusFlag, dataType, damage, cancellationToken)
                .ConfigureAwait(false) ?? current;
        }
        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCombatGearStoneCrackerStatusUpdateAsync(
                updatedContext,
                updatedSession,
                statusFlag,
                dataType,
                damage,
                cancellationToken)
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask SendLegacyCombatGearStoneCrackerStatusUpdateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        int statusFlag,
        LegacyMsgUpdateDataType dataType,
        uint damage,
        CancellationToken cancellationToken)
    {
        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    dataType,
                    Value: 0,
                    Flag: checked((uint)statusFlag),
                    Time: 0,
                    Damage: damage,
                    SpellLevel: 0),
            ]);
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                context.Connection,
                session,
                packet.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<(WorldSession Session, LegacyPlayerMagicReceiveAbortReason AbortReason)> SendLegacyCombatGearStoneCrackerStarFlowSpellAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        return await SendLegacyCombatGearStoneCrackerStarFlowSpellAsync(
                context,
                session,
                CheckedUInt16(session.Character.X, nameof(session.Character.X)),
                CheckedUInt16(session.Character.Y, nameof(session.Character.Y)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<(WorldSession Session, LegacyPlayerMagicReceiveAbortReason AbortReason)> SendLegacyCombatGearStoneCrackerStarFlowSpellAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        ushort originX,
        ushort originY,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                LegacyCombatGearStarFlowSpellId,
                out var playerSpell,
                out var spell))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear StoneCracker StarFlow spell " +
                    $"{FormatSession(context)} spellId={LegacyCombatGearStarFlowSpellId}; spell is not available.")
                .ConfigureAwait(false);
            return (session, LegacyPlayerMagicReceiveAbortReason.None);
        }

        var admission = await TryCommitLegacyStarFlowAdmissionAsync(
                context, session, spell, cancellationToken).ConfigureAwait(false);
        session = admission.Owner;
        context = context with { Session = session };
        if (!admission.Admitted || admission.AbortReason != LegacyPlayerMagicReceiveAbortReason.None)
            return (session, admission.AbortReason);

        try
        {
        const double starFlowRadius = 2.5;
        var now = DateTimeOffset.UtcNow;
        var updatedSession = session;
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var monsterResults = new List<(LegacyPlayerMonsterMagicTarget Target, MonsterDamageResult Result, uint Damage)>();
        var playerResults = new List<LegacyPlayerMagicDamageResult>();
        var sobNpcResults = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var killedTrainingSobNpcTargets = new List<(SobNpc SobNpc, SobNpcDamageResult Result)>();
        var eventSobNpcSideEffectTargets = new List<(
            SobNpc SobNpc,
            SobNpcDamageResult Result,
            LegacySobNpcDeathBehavior DeathBehavior)>();

        if (context.MonsterStates is { } monsterStates)
        {
            foreach (var target in EnumerateVisibleMonsterTargets(context, updatedSession.Character))
            {
                if (LegacyGetDistance(originX, originY, target.Monster.X, target.Monster.Y) > starFlowRadius)
                {
                    continue;
                }

                var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(
                    updatedSession,
                    worldData,
                    target.Monster,
                    target.MonsterFamily,
                    spell);
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
                    result.DamageApplied,
                    Hit: result.DamageApplied > 0,
                    MsgAttackEffect.None));
            }
        }

        var playerPass = await ReceiveLegacyStarFlowPlayerTargetsAsync(
                context, updatedSession, spell, worldData, originX, originY,
                playerResults, animationTargets, cancellationToken)
            .ConfigureAwait(false);
        updatedSession = playerPass.OwnerSession;
        context = context with { Session = updatedSession };
        if (playerPass.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            return (updatedSession, playerPass.AbortReason);

        if (context.SobNpcStates is { } sobNpcStates)
        {
            foreach (var target in EnumerateVisibleSobNpcMagicTargets(context, updatedSession.Character))
            {
                if (LegacyGetDistance(originX, originY, target.SobNpc.X, target.SobNpc.Y) > starFlowRadius)
                {
                    continue;
                }

                var damage = CalculateLegacySobNpcPhysicalSpellDamage(
                    updatedSession,
                    spell,
                    worldData,
                    damageMultiplier: 1);
                var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target.SobNpc, worldData.StaticStatues);
                var result = sobNpcStates.ApplyDamage(target.SobNpc, damage, now, deathBehavior);
                sobNpcResults.Add((target.SobNpc, result));
                if (result.AlreadyDead || result.DamageApplied == 0)
                {
                    continue;
                }

                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(target.SobNpc.Id, nameof(target.SobNpc.Id)),
                    result.DamageApplied,
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

        foreach (var result in playerResults)
        {
            await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                    context,
                    updatedSession,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
            originX,
            originY,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        LegacyPlayerMonsterMagicTarget? firstRetaliationTarget = null;
        foreach (var (target, result, damage) in monsterResults)
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
                context = context with { Session = updatedSession };
                await TryApplyLegacyMonsterTeamExperienceAsync(
                        context,
                        updatedSession,
                        target.Monster,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                firstRetaliationTarget ??= target;
            }

            updatedSession = await TryApplyLegacyMonsterExperienceAsync(
                    context,
                    updatedSession,
                    target.Monster,
                    target.MonsterFamily,
                    damage,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = updatedSession };
        }

        if (firstRetaliationTarget is { } retaliationTarget)
        {
            await TrySendLegacyMonsterRetaliationAsync(
                    context,
                    updatedSession,
                    retaliationTarget.Monster,
                    retaliationTarget.MonsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var result in playerResults)
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

            foreach (var eventTarget in eventSobNpcSideEffectTargets)
            {
                await LogLegacySobNpcEventSideEffectGapAsync(
                        context,
                        eventTarget.SobNpc,
                        eventTarget.Result,
                        spell)
                    .ConfigureAwait(false);
            }
        }

        var totalMonsterDamage = monsterResults
            .Where(result => !result.Result.AlreadyDead)
            .Aggregate(0UL, (total, result) => total + result.Damage);
        var totalSobNpcExperienceDamage = sobNpcResults.Aggregate(
            0UL,
            (total, result) => total + CalculateLegacySobNpcExperienceDamage(result.SobNpc, result.Result));
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                updatedSession,
                playerSpell,
                spell,
                totalMonsterDamage + totalSobNpcExperienceDamage,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy CombatGear StoneCracker StarFlow spell " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} " +
                $"spellLevel={playerSpell.Level} soulLevel={playerSpell.UseSpellSoul} " +
                $"targets={animationTargets.Count} monsterTargets={monsterResults.Count} " +
                $"playerTargets={playerResults.Count} sobNpcTargets={sobNpcResults.Count}; " +
                "source=MyCombatGear.Update StoneCracker -> CombatGearAttack 10950.")
            .ConfigureAwait(false);
        return (updatedSession, LegacyPlayerMagicReceiveAbortReason.None);
        }
        catch (WorldSessionPublicationException) { throw; }
        catch (Exception error) when (admission.Receipt is not null) { throw admission.Receipt.Failure(error); }
    }
}
