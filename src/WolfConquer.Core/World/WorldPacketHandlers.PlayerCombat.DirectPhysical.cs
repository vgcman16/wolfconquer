using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyDirectPlayerPhysicalRange = 2;

    private static async ValueTask<bool> TryHandleLegacyDirectPlayerPhysicalAttackAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        CancellationToken cancellationToken)
    {
        var query = packet.Query;
        if (query.AttackType != MsgAttackType.Physical ||
            query.Damage > 0 ||
            context.Session is not { } session ||
            !TryGetVisibleSessionByUid(context, session, query.OpponentUid, out var activeTarget))
        {
            return false;
        }

        var targetCharacter = activeTarget.Session.Character;
        if (HasLegacyStatusFlag(session.Character, LegacyNeptuneCurseFlag))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy direct player physical " +
                    $"{FormatSession(context)} targetUid={targetCharacter.Uid}; " +
                    "source=MsgAttackPacket.cs case Physical blocks attacker NeptuneCurse before player hit.")
                .ConfigureAwait(false);
            return true;
        }

        var distance = LegacyGetDistance(
            session.Character.X,
            session.Character.Y,
            targetCharacter.X,
            targetCharacter.Y);
        if (!IsLegacyDirectPlayerPhysicalTargetInRange(session, context.WorldData, distance))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy direct player physical " +
                    $"{FormatSession(context)} targetUid={targetCharacter.Uid} distance={distance}; " +
                    "source=MsgAttackPacket.cs case Physical ordinary/FuryStrike/equipped CombatGear range gate.")
                .ConfigureAwait(false);
            return true;
        }

        if (!CanLegacyDirectPlayerPhysicalDamageTarget(
                context,
                session,
                activeTarget.Session,
                out var failureDiagnostic,
                out var failureEffect))
        {
            if (failureEffect == LegacyPlayerMagicGateFailureEffect.HellVortexCounter)
            {
                await TrySendLegacyPlayerHellVortexCounterAsync(context, session, activeTarget, cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy direct player physical " +
                    $"{FormatSession(context)} targetUid={targetCharacter.Uid}; {failureDiagnostic}.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                session,
                [new LegacyPlayerMagicTarget(activeTarget)],
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        if (await TryHandleLegacyDirectPhysicalCombatGearPlayerReplacementAsync(
                context, packet, updatedSession, activeTarget,
                session.Character.Uid, targetCharacter.Uid, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        (context, activeTarget, var preparationResult, _) = await PrepareLegacyDirectPlayerPhysicalDamageAsync(
                context,
                updatedSession,
                activeTarget,
                session.Character.Uid,
                targetCharacter.Uid,
                cancellationToken)
            .ConfigureAwait(false);
        if (preparationResult is not { } damageResult)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer stopped legacy direct player physical preparation " +
                    $"{FormatSession(context)} targetUid={targetCharacter.Uid}; " +
                    "accepted participant identity is no longer registered after an awaited calculation hook.")
                .ConfigureAwait(false);
            return true;
        }

        updatedSession = context.Session ?? updatedSession;
        context = context with { Session = updatedSession };
        var requestedDamage = damageResult.Damage;
        var displayedDamage = CalculateLegacyDirectPlayerPhysicalDisplayedDamage(
            updatedSession, context.WorldData, requestedDamage);
        var response = new MsgAttackPacket(query with
        {
            Damage = CheckedInt32(displayedDamage, nameof(displayedDamage)),
            KilledMonster = 0,
            ResponseDamage = query.ResponseDamage,
            Effect = damageResult.Effect,
        });
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                context.Connection,
                updatedSession,
                response.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var receiveOutcome = await ReceiveLegacyDirectPlayerPhysicalDamageAsync(
                context,
                updatedSession,
                activeTarget,
                requestedDamage,
                session.Character.Uid,
                targetCharacter.Uid,
                cancellationToken)
            .ConfigureAwait(false);
        if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
        {
            updatedSession = currentReceiveAttacker;
        }

        context = context with { Session = updatedSession };
        if (receiveOutcome.Result is not { } result)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer stopped legacy direct player physical receive " +
                    $"{FormatSession(context)} targetUid={targetCharacter.Uid}; " +
                    "accepted participant identity is no longer registered after an awaited send.")
                .ConfigureAwait(false);
            return true;
        }

        await SendLegacyPlayerMagicPreAnimationUpdatesAsync(context, updatedSession, result, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyPlayerMagicDamageUpdatesAsync(context, updatedSession, result, cancellationToken)
            .ConfigureAwait(false);
        updatedSession = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                updatedSession,
                requestedDamage,
                cancellationToken,
                activity: "direct player physical weapon proficiency experience")
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy direct player physical " +
                $"{FormatSession(context with { Session = updatedSession })} targetUid={result.UpdatedSession.Character.Uid} " +
                $"requestedDamage={requestedDamage} displayedDamage={displayedDamage} " +
                $"damageApplied={result.DamageApplied} killed={result.Killed} effect={damageResult.Effect}; " +
                "source=MsgAttackPacket.cs case Physical -> Physical.OnPlayer DBSpell null -> ReceiveAttack.Player.Execute.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool CanLegacyDirectPlayerPhysicalDamageTarget(
        WorldPacketHandlerContext context,
        WorldSession attacker,
        WorldSession target,
        out string failureDiagnostic,
        out LegacyPlayerMagicGateFailureEffect failureEffect)
    {
        failureDiagnostic = string.Empty;
        failureEffect = LegacyPlayerMagicGateFailureEffect.None;
        if (TryGetLegacyCombatGearDamageBlockReason(target.Character, out var reason))
        {
            failureDiagnostic =
                $"target legacy CombatGear CheckDamage blocked direct physical reason={reason}; " +
                "source=CanAttackPlayer.Verified attacked.Owner.CombatGear.CheckDamage";
            return false;
        }

        if (IsLegacyPlayerMagicHellVortexCounterBlocked(context, target, out failureDiagnostic))
        {
            failureEffect = LegacyPlayerMagicGateFailureEffect.HellVortexCounter;
            return false;
        }

        if (TryGetLegacyPlayerMagicSafeZone(
                attacker.Character.MapId,
                target.Character.X,
                target.Character.Y,
                out var safeZone))
        {
            failureDiagnostic =
                $"map={attacker.Character.MapId} targetX={target.Character.X} targetY={target.Character.Y}; " +
                $"source CanAttackPlayer.Verified rejected {safeZone} safe zone: You can't PK in the safe zone";
            return false;
        }

        if (target.Character.MapId == 700 &&
            ReadCharacterDynamicId(target.Character) == 0)
        {
            failureDiagnostic = "target map=700 dynamicId=0 blocks direct physical";
            return false;
        }

        if (target.Character.MinHitPoints <= 0 ||
            HasLegacyStatusFlag(target.Character, LegacyDeadFlag) ||
            HasLegacyStatusFlag(target.Character, LegacyGhostFlag))
        {
            failureDiagnostic = "target is dead or ghosted";
            return false;
        }

        if (ReadAttributeBool(target.Character.Attributes, "Invisible"))
        {
            failureDiagnostic =
                "target Invisible attribute is set; source CanAttackPlayer.Verified rejected attacked.Invisible";
            return false;
        }

        var pkMode = ReadAttributeUInt64(attacker.Character.Attributes, RuntimePkModeAttribute, "PKMode") & byte.MaxValue;
        if (pkMode == LegacyPkModePeace)
        {
            failureDiagnostic = "attacker is in Peace PK mode";
            return false;
        }

        if (HasLegacyStatusFlag(target.Character, LegacyFlyFlag))
        {
            failureDiagnostic =
                "target is flying and direct physical uses DBSpell null; source CanAttackPlayer.Verified rejected attacked Fly";
            return false;
        }

        if (HasLegacyPlayerMagicWatchingAttribute(target.Character))
        {
            failureDiagnostic =
                "target watching attribute is set; source CanAttackPlayer.Verified rejected attacked.Owner.IsWatching()";
            return false;
        }

        if (HasLegacyPlayerMagicWatchingAttribute(attacker.Character))
        {
            failureDiagnostic =
                "attacker watching attribute is set; source CanAttackPlayer.Verified rejected client.IsWatching()";
            return false;
        }

        if (IsLegacyPlayerAttackProtected(target.Character, DateTimeOffset.UtcNow))
        {
            failureDiagnostic = "target legacy Protect window is active";
            return false;
        }

        if (IsLegacyPlayerMagicCaptureTheFlagAttackBlocked(attacker.Character, target.Character, out failureDiagnostic))
        {
            return false;
        }

        if (IsLegacyPlayerMagicSameGarmentMapBlocked(attacker, target, out failureDiagnostic))
        {
            return false;
        }

        if (IsLegacyPlayerMagicFriendlyFireBlocked(context, attacker, target, pkMode, out failureDiagnostic))
        {
            return false;
        }

        if (IsLegacyPlayerMagicBlockedMap(attacker.Character.MapId, pkMode))
        {
            failureDiagnostic = $"map={attacker.Character.MapId} blocks direct physical";
            return false;
        }

        if (IsLegacyPlayerMagicTeamQualifierSameTeamBlocked(context, attacker, target, out failureDiagnostic))
        {
            return false;
        }

        if (IsLegacyPlayerMagicOwnServerBlocked(attacker.Character, out failureDiagnostic))
        {
            return false;
        }

        if (pkMode == LegacyPkModeCapture)
        {
            if (HasLegacyStatusFlag(target.Character, LegacyFlashingNameStatusFlag) ||
                HasLegacyStatusFlag(target.Character, LegacyBlackNameStatusFlag))
            {
                return true;
            }

            failureDiagnostic = "attacker is in Capture PK mode and target is not FlashingName or BlackName";
            return false;
        }

        if (pkMode == LegacyPkModeJiang &&
            TryApplyLegacyPlayerMagicJiangPkGate(
                attacker.Character,
                target.Character,
                out var canJiangAttack,
                out failureDiagnostic))
        {
            return canJiangAttack;
        }

        if (IsLegacyPlayerMagicBlockAttackMapBlocked(attacker.Character, out failureDiagnostic))
        {
            return false;
        }

        return true;
    }
}
