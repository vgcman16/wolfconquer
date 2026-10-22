using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.MsgAttack)]
    private static async ValueTask HandleMsgAttackAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!MsgAttackPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed MsgAttack packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        var query = packet.Query;
        LegacyCounterKillCanUseOwner? counterKillOwner = null;
        if (query.AttackType == MsgAttackType.CounterKillSwitch &&
            !TryCaptureLegacyCounterKillCanUseOwner(context, out counterKillOwner))
            return; // No registered original owner; never reinterpret this as no-Store.
        var focusedAttackPrepared = false;
        try
        {
        context = await CancelLegacyMsgAttackIntensifyArmAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (counterKillOwner is not null && !IsLegacyCounterKillCanUseOwnerCurrent(context, counterKillOwner)) return;
        var focusedAttackState = await PrepareLegacyMsgAttackFocusedAsync(context, cancellationToken)
            .ConfigureAwait(false);
        context = focusedAttackState.Context;
        focusedAttackPrepared = focusedAttackState.Prepared;
        if (counterKillOwner is not null && !IsLegacyCounterKillCanUseOwnerCurrent(context, counterKillOwner)) return;

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAttack " +
                $"{FormatSession(context)} " +
                $"uid={query.Uid} opponentUid={query.OpponentUid} x={query.X} y={query.Y} " +
                $"type={query.AttackType} damage={query.Damage} killedMonster={query.KilledMonster} " +
                $"responseDamage={query.ResponseDamage} effect={query.Effect} unknown={query.Unknown} " +
                $"combatGear={query.CombatGear} spellLevel={query.SpellLevel} spellId={query.SpellId} " +
                $"from {context.Connection.RemoteEndPoint}.")
            .ConfigureAwait(false);

        if (counterKillOwner is not null && !IsLegacyCounterKillCanUseOwnerCurrent(context, counterKillOwner)) return;
        if (await HandleLegacyCounterKillSwitchAsync(context, packet, counterKillOwner, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (await TryRejectLegacyArcherDirectTargetOutOfRangeAsync(
                context,
                packet,
                cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (context.Session is { } combatGearSession &&
            await TryHandleLegacyCombatGearPlayerAttackReplacementAsync(
                    context,
                    packet,
                    combatGearSession,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        if (context.Session is { } archerSession &&
            await TryHandleLegacyArcherPreKineticPlayerProcAttackAsync(
                    context,
                    packet,
                    archerSession,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        if (context.Session is { } session &&
            await TryHandleLegacyKineticSparkPassivePlayerAttackAsync(
                    context,
                    packet,
                    session,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        if (await TryHandleLegacyDirectPlayerArcherAttackAsync(
                context,
                packet,
                cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (await TryHandleLegacyDirectPlayerPhysicalAttackAsync(context, packet, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (await HandleMonsterCombatDamageAsync(context, packet, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await HandleTeamArenaCombatDamageAsync(context, packet, cancellationToken).ConfigureAwait(false);
        await HandleArenaCombatDamageAsync(context, packet, cancellationToken).ConfigureAwait(false);
        await HandleInteractionAttackStateAsync(context, packet, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (focusedAttackPrepared)
            {
                if (counterKillOwner is not null)
                    await ClearLegacyCounterKillCanUseFocusedAsync(context, counterKillOwner, cancellationToken).ConfigureAwait(false);
                else
                    await ClearLegacyMsgAttackFocusedAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask<WorldPacketHandlerContext> CancelLegacyMsgAttackIntensifyArmAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session ||
            !TryCancelLegacyIntensifyArm(session.Character.Attributes, out var updatedAttributes))
        {
            return context;
        }

        var updatedSession = session with
        {
            Character = session.Character with { Attributes = updatedAttributes },
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendLegacyFocusedCancelIfNeededAsync(
                updatedContext,
                updatedSession,
                removedFocusedFlag: true,
                packetName: "MsgAttack",
                cancellationToken)
            .ConfigureAwait(false);

        return updatedContext;
    }

    private static async ValueTask<(WorldPacketHandlerContext Context, bool Prepared)> PrepareLegacyMsgAttackFocusedAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session ||
            !HasLegacyStatusFlag(session.Character, LegacyFocusedFlag))
        {
            return (context, false);
        }

        var focusedCharacter = WithCharacterAttribute(
            WithoutLegacyStatusFlag(session.Character, LegacyFocusedFlag),
            "doFocus",
            "true");
        var updatedSession = session with { Character = focusedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer consumed legacy Focused attack " +
                $"{FormatSession(context)} statusFlag={LegacyFocusedFlag}; " +
                "source=MsgAttackPacket.cs RemoveFlag(Focused) one-shot focus attack.")
            .ConfigureAwait(false);

        return (updatedContext, true);
    }

    private static async ValueTask ClearLegacyMsgAttackFocusedAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            return;
        }

        if (context.Sessions is { } sessions &&
            sessions.TryGet(context.Connection, out var storedSession) &&
            storedSession is not null)
        {
            session = storedSession;
        }

        if (!ReadAttributeBool(session.Character.Attributes, "doFocus", "DoFocus"))
        {
            return;
        }

        var updatedSession = session with
        {
            Character = WithoutCharacterAttribute(
                WithoutCharacterAttribute(session.Character, "doFocus"),
                "DoFocus"),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        await context.Log.WriteLineAsync(
                "WorldServer cleared legacy Focused attack marker " +
                $"{FormatSession(context)} source=MsgAttackPacket.cs doFocus=false.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> HandleMonsterCombatDamageAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        CancellationToken cancellationToken)
    {
        var query = packet.Query;
        if (query.AttackType == MsgAttackType.Magic &&
            await HandleLegacyPlayerMonsterMagicAttackAsync(context, packet, cancellationToken, sourceFirstFamilyRootEntry: true).ConfigureAwait(false))
        {
            return true;
        }

        var shouldLogMonsterSkip = query.OpponentUid >= 400_000;
        if (!IsArenaCombatAttackType(query.AttackType))
        {
            if (shouldLogMonsterSkip)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped monster combat " +
                        $"{FormatSession(context)} monsterUid={query.OpponentUid}; attackType={query.AttackType} is not combat.")
                    .ConfigureAwait(false);
            }

            return false;
        }

        if (context.Session is not { } session)
        {
            if (shouldLogMonsterSkip)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped monster combat " +
                        $"{FormatSession(context)} monsterUid={query.OpponentUid}; no character session is available.")
                    .ConfigureAwait(false);
            }

            return false;
        }

        if (await TryHandleLegacyCombatGearSobNpcAttackReplacementAsync(
                context,
                packet,
                session,
                cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyArcherPreKineticSobNpcProcAsync(
                context,
                packet,
                session,
                cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyKineticSparkPassiveSobNpcAttackAsync(
                context,
                packet,
                session,
                cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyDirectSobNpcAttackAsync(
                context,
                packet,
                session,
                cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (context.MonsterStates is not { } monsterStates)
        {
            if (shouldLogMonsterSkip)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped monster combat " +
                        $"{FormatSession(context)} monsterUid={query.OpponentUid}; monster runtime state store is not available.")
                    .ConfigureAwait(false);
            }

            return false;
        }

        if (!TryFindMonsterInstanceByUid(context, session.Character, query.OpponentUid, out var monster, out var monsterFamily))
        {
            if (shouldLogMonsterSkip)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped monster combat " +
                        $"{FormatSession(context)} monsterUid={query.OpponentUid}; monster instance was not found on map={session.Character.MapId}.")
                    .ConfigureAwait(false);
            }

            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var hasVisibleInstance = monsterStates.TryGetVisibleInstance(monster, now, out var visibleMonster);
        if (hasVisibleInstance)
        {
            if (!CanSeeMonster(session.Character, visibleMonster))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped monster combat " +
                        $"{FormatSession(context)} monsterUid={monster.Uid}; monster is outside visible range.")
                    .ConfigureAwait(false);
                return false;
            }

            monster = visibleMonster;
        }

        if (!CanLegacyPlayerAttackMonster(
                session.Character,
                monster,
                monsterFamily,
                out var attackRejectReason,
                enforceD13UidRange: context.ProtocolProfile == ConquerProtocolProfile.VisibleDx8D13))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped monster combat " +
                    $"{FormatSession(context)} monsterUid={monster.Uid}; {attackRejectReason}.")
                .ConfigureAwait(false);
            return true;
        }

        if (IsLegacyGuardMonsterFamily(monster, monsterFamily))
        {
            session = await TryApplyLegacyGuardAttackFlashingNameAsync(
                    context,
                    session,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = session };
        }

        if (await TryHandleLegacyCombatGearMonsterAttackReplacementAsync(
                context,
                packet,
                session,
                monster,
                monsterFamily,
                cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleVisibleDx8D13ClientSelectedWeaponProcMonsterAttackAsync(
                context,
                packet,
                session,
                monster,
                cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyWeaponProcMonsterAttackAsync(
                context,
                packet,
                session,
                monster,
                cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyArcherPreKineticMonsterProcAsync(
                context,
                packet,
                session,
                monster,
                cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyKineticSparkPassiveMonsterAttackAsync(
                context,
                packet,
                session,
                cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        var requestedDamage = CalculateLegacyPlayerMonsterDamage(context, session, monster, monsterFamily, query);
        var result = ApplyLegacyPlayerMonsterDamage(
            monsterStates,
            monster,
            requestedDamage,
            now,
            session);
        if (!hasVisibleInstance && !result.AlreadyDead)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped monster combat " +
                    $"{FormatSession(context)} monsterUid={monster.Uid}; monster has not been spawned to this runtime view.")
                .ConfigureAwait(false);
            return false;
        }

        if (result.AlreadyDead)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped monster combat " +
                    $"{FormatSession(context)} monsterUid={monster.Uid}; monster is waiting to respawn.")
                .ConfigureAwait(false);
            return true;
        }

        var response = new MsgAttackPacket(packet.Query with
        {
            Damage = unchecked((int)result.RequestedDamage),
            KilledMonster = 0,
            ResponseDamage = query.ResponseDamage,
            X = monster.X,
            Y = monster.Y,
        });
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);

        if (!result.Killed && LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(monster))
        {
            var updatedMonster = monster with { Hitpoints = result.RemainingHitpoints };
            foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(updatedMonster))
            {
                await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            }
        }

        if (result.Killed)
        {
            var death = new MsgAttackPacket(new MsgAttackQuery(
                Uid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Death,
                Damage: 0,
                KilledMonster: 1,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0));
            await SendMonsterCombatFrameToVisibleAsync(context, session, monster, death, cancellationToken)
                .ConfigureAwait(false);
            await TrySendLegacyMonsterDeathDropAsync(context, session, monster, monsterFamily, cancellationToken)
                .ConfigureAwait(false);
            session = await TryApplyLegacyBossDeathRewardsAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            await TryApplyLegacyMonsterTeamExperienceAsync(context, session, monster, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            session = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                    context,
                    session,
                    requestedDamage,
                    cancellationToken)
                .ConfigureAwait(false);

            session = await TryApplyLegacyMonsterExperienceAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    result.DamageApplied,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (result.Killed)
        {
            session = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                    context,
                    session,
                    requestedDamage,
                    cancellationToken)
                .ConfigureAwait(false);

            session = await TryApplyLegacyMonsterExperienceAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    requestedDamage,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!result.Killed &&
            await TryHandleLegacyPostPhysicalLearnedMonsterProcsAsync(
                    context with { Session = session },
                    packet,
                    session,
                    monster,
                    cancellationToken)
                .ConfigureAwait(false) &&
            context.Sessions is { } sessionsAfterProc &&
            sessionsAfterProc.TryGet(context.Connection, out var sessionAfterProc) &&
            sessionAfterProc is not null)
        {
            session = sessionAfterProc;
            context = context with { Session = session };
        }

        if (HasLegacyStatusFlag(session.Character, LegacyFocusedFlag))
        {
            var focusedCharacter = WithoutCharacterAttribute(
                WithoutCharacterAttribute(
                    WithoutLegacyStatusFlag(session.Character, LegacyFocusedFlag),
                    "doFocus"),
                "DoFocus");
            session = session with { Character = focusedCharacter };
            context = context with { Session = session };
            context.Sessions?.Set(context.Connection, session);
            await SendPlayerStatusFlagVectorToVisibleAsync(context, session, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer consumed legacy Focused attack " +
                    $"{FormatSession(context)} statusFlag={LegacyFocusedFlag}; " +
                    "source=MsgAttackPacket.cs RemoveFlag(Focused) one-shot focus attack.")
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled monster combat " +
                $"{FormatSession(context)} monsterUid={monster.Uid} " +
                FormatLegacyPlayerMonsterPhysicalDamageAudit(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    requestedDamage,
                    result))
            .ConfigureAwait(false);
        return true;
    }

    private static string FormatLegacyPlayerMonsterPhysicalDamageAudit(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint requestedDamage,
        MonsterDamageResult result)
    {
        var fields = CalculateLegacyRuntimeStatusFields(session, context.WorldData);
        var minAttack = fields[LegacyPlayerStatusPacket.MinAttackIndex];
        var maxAttack = fields[LegacyPlayerStatusPacket.MaxAttackIndex];
        var physicalPercent = fields[LegacyPlayerStatusPacket.PhysicalPercentIndex];
        var physicalDamageIncrease = fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex];
        var monsterDefense = ReadMonsterDefense(monsterFamily);
        var monsterDefense2 = ReadAttributeUInt32(
            monsterFamily.Attributes,
            "defense2",
            "defence2",
            "Defense2",
            "Defence2");

        return $"requestedDamage={requestedDamage} damageApplied={result.DamageApplied} " +
            $"remainingHitpoints={result.RemainingHitpoints} killed={result.Killed} " +
            $"playerLevel={session.Character.Level} monsterLevel={monster.Level} " +
            $"minAttack={minAttack} maxAttack={maxAttack} physicalPercent={physicalPercent} " +
            $"physicalDamageIncrease={physicalDamageIncrease} monsterDefense={monsterDefense} " +
            $"monsterDefense2={monsterDefense2}; source=Physical.OnMonster damage audit.";
    }

}
