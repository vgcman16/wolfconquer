using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<LegacyPlayerMagicReceiveOutcome> ApplyLegacyPlayerMagicDamageToPlayerAsync(
        WorldPacketHandlerContext context,
        WorldActiveSession target,
        CharacterSnapshot attacker,
        uint damage,
        CancellationToken cancellationToken,
        bool applyMudWall = true,
        LegacyPlayerReceiveCombatGearReceipt? combatGearReceipt = null,
        object? nativeIncomingClientIdentity = null)
    {
        // Native ReceiveAttack.Execute holds its attacked Player argument through every await.
        // Carry that physical target into Dead AFTER attacker finish; never re-read client.Player there.
        var nativeDeadPlayerReceiver = target.Session.NativePlayer;
        var acceptedAttackerUid = attacker.Uid;
        var acceptedTargetUid = target.Session.Character.Uid;
        // Real runtime receives have an attacker session. Preserve the legacy
        // pure/no-owner compatibility path; never manufacture an owner/result.
        var openingAttacker = context.Session;
        if (context.Sessions is { } sessions)
        {
            if (!sessions.TryGet(context.Connection, out var registeredAttacker) ||
                registeredAttacker is null || registeredAttacker.Character.Uid != acceptedAttackerUid)
                return new(null, null, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
            openingAttacker = registeredAttacker;
        }
        if (openingAttacker is not null)
        {
            WorldSession currentOpeningAttacker = openingAttacker;
            var request = new LegacyPlayerReceiveCombatGearRequest(
                currentOpeningAttacker, target, acceptedAttackerUid, acceptedTargetUid,
                nativeIncomingClientIdentity);
            if (combatGearReceipt is not null)
            {
                var originalParticipants = combatGearReceipt.Consume(
                    context.Connection, acceptedAttackerUid, target.Connection, acceptedTargetUid);
                context = context with { CombatGearReceiveParticipants = originalParticipants };
                var reason = RefreshLegacyPlayerReceiveCombatGearParticipants(
                    context, request, ref currentOpeningAttacker, ref target);
                if (reason != LegacyPlayerMagicReceiveAbortReason.None)
                    return new(null,
                        reason == LegacyPlayerMagicReceiveAbortReason.TargetLost ? currentOpeningAttacker : null, reason);
            }
            else
            {
                var opening = await ExecuteLegacyPlayerReceiveCombatGearOpeningAsync(
                        context, request, cancellationToken)
                    .ConfigureAwait(false);
                if (opening.AbortReason != LegacyPlayerMagicReceiveAbortReason.None)
                    return new(null, opening.AttackerSession, opening.AbortReason);
                currentOpeningAttacker = opening.AttackerSession!;
                target = opening.Target!;
                var originalParticipants = opening.Receipt!.Consume(
                    context.Connection, acceptedAttackerUid, target.Connection, acceptedTargetUid);
                context = context with { CombatGearReceiveParticipants = originalParticipants };
            }
            context = context with { Session = currentOpeningAttacker };
            attacker = currentOpeningAttacker.Character;
        }
        else if (combatGearReceipt is not null)
        {
            // A completed paired opening cannot be consumed without its owner.
            return new(null, null, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
        }

        // Compatibility MudWall remains in its existing modifier location.
        // Prepared physical envelopes pass false and their completed receipt.
        var early = await ApplyLegacyPlayerReceiveEarlyModifiersAsync(
                context, target, attacker, damage, cancellationToken, applyMudWall)
            .ConfigureAwait(false);
        if (early.AbortReason != LegacyPlayerMagicReceiveAbortReason.None)
            return new(null,
                early.AbortReason == LegacyPlayerMagicReceiveAbortReason.TargetLost ? early.AttackerSession : null,
                early.AbortReason);
        context = context with { Session = early.AttackerSession ?? context.Session };
        var prepared = PrepareLegacyPlayerMagicReceive(context, early);
        prepared = prepared with { Result = prepared.Result with { EarlyModifierPhasesPublished = true } };
        if (!prepared.RequiresDamageCommit)
        {
            return CreateLegacyPlayerMagicReceiveSuccess(context, prepared.Result, attacker.Uid);
        }

        if (!prepared.Result.Killed)
        {
            return CreateLegacyPlayerMagicReceiveSuccess(context,
                CommitLegacyPreparedPlayerReceive(context, prepared, prepared.Result.UpdatedSession), attacker.Uid);
        }

        var ownerUid = attacker.Uid;
        context = context with { Session = prepared.Result.UpdatedAttackerSession ?? context.Session };
        var acceptedTarget = prepared.Result.ActiveSession with { Session = prepared.Result.UpdatedSession };
        var participants = RefreshLegacyTransformFinishParticipants(context, ownerUid, acceptedTarget);
        if (participants.Owner is null)
        {
            return new(null, null, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
        }
        if (participants.TargetLost)
        {
            return new(null, participants.Owner, LegacyPlayerMagicReceiveAbortReason.TargetLost);
        }

        // Source modifiers have already run once. Preserve them before the new
        // await, but target HP/death remains untouched until FinishTransform returns.
        context.Sessions?.Set(acceptedTarget.Connection, prepared.Result.UpdatedSession);
        participants = RefreshLegacyTransformFinishParticipants(context, ownerUid, acceptedTarget);
        if (participants.Owner is not { } currentAttacker)
        {
            return new(null, null, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
        }
        if (participants.TargetLost)
        {
            return new(null, currentAttacker, LegacyPlayerMagicReceiveAbortReason.TargetLost);
        }

        if (prepared.FinishAttackerTransform &&
            TryPrepareLegacyPlayerTransformFinish(currentAttacker, context.WorldData,
                context.LegacyClock ?? LegacySignedMillisecondClock.System, out var query))
        {
            context.Sessions?.Set(context.Connection, query.Session);
            var finish = await PublishLegacyPlayerTransformFinishAsync(
                    context with { Session = query.Session }, query, cancellationToken, acceptedTarget)
                .ConfigureAwait(false);
            if (finish.Session is null)
            {
                return new(null, null, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
            }
            if (finish.TargetLost)
            {
                return new(null, finish.Session, LegacyPlayerMagicReceiveAbortReason.TargetLost);
            }
            context = context with { Session = finish.Session };
        }

        participants = RefreshLegacyTransformFinishParticipants(context, ownerUid, acceptedTarget);
        if (participants.Owner is not { } completedAttacker)
        {
            return new(null, null, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
        }
        if (participants.TargetLost || participants.Target is not { } currentTarget)
        {
            return new(null, completedAttacker, LegacyPlayerMagicReceiveAbortReason.TargetLost);
        }

        // Player.Dead3882 performs the TARGET's FinishTransform next. Its
        // condition observes current state after the attacker's awaited finish.
        if (TryPrepareLegacyPlayerTransformFinish(currentTarget.Session, context.WorldData,
                context.LegacyClock ?? LegacySignedMillisecondClock.System, out var targetQuery))
        {
            context.Sessions?.Set(currentTarget.Connection, targetQuery.Session);
            var targetFinish = await PublishLegacyPlayerTransformFinishAsync(
                    context with { Connection = currentTarget.Connection, Session = targetQuery.Session },
                    targetQuery, cancellationToken,
                    new WorldActiveSession(context.Connection, completedAttacker))
                .ConfigureAwait(false);
            if (targetFinish.Session is { } localTarget)
            {
                acceptedTarget = acceptedTarget with { Session = localTarget };
            }
        }

        // Reclassify target-owned finish failures relative to ORIGINAL incoming
        // roles. Original attacker loss wins if both identities disappeared.
        participants = RefreshLegacyTransformFinishParticipants(context, ownerUid, acceptedTarget);
        if (participants.Owner is not { } finalAttacker)
        {
            return new(null, null, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
        }
        if (participants.TargetLost || participants.Target is not { } finalTarget)
        {
            return new(null, finalAttacker, LegacyPlayerMagicReceiveAbortReason.TargetLost);
        }

        // Entered lethal branch is fixed: no alive/range/PK/HP test or hook replay.
        // Normal Dead owns GhostStamp/talk, later DeadStamp, ClearFlags/query,
        // then HP0 setter before AddFlagDead; none is an atomic late replay.
        return await CompleteLegacyNormalPlayerDeathAsync(
                context, prepared, finalTarget, nativeDeadPlayerReceiver, finalAttacker, cancellationToken)
            .ConfigureAwait(false);
    }

    private static LegacyPlayerMagicReceiveOutcome CreateLegacyPlayerMagicReceiveSuccess(
        WorldPacketHandlerContext context,
        LegacyPlayerMagicDamageResult result,
        long ownerUid)
    {
        var carry = result.UpdatedAttackerSession ?? context.Session;
        if (carry?.Character.Uid != ownerUid)
        {
            carry = null;
        }

        return new(result, carry);
    }
}
