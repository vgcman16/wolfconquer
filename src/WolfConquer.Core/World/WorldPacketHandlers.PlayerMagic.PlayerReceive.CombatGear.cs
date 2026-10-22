namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<LegacyPlayerReceiveCombatGearOutcome>
        ExecuteLegacyPlayerReceiveCombatGearOpeningAsync(
            WorldPacketHandlerContext context,
            LegacyPlayerReceiveCombatGearRequest request,
            CancellationToken cancellationToken)
    {
        var attacker = request.AttackerSession;
        var target = request.Target;
        LegacyPlayerReceiveCombatGearOutcome Lost(LegacyPlayerMagicReceiveAbortReason reason) =>
            new(reason == LegacyPlayerMagicReceiveAbortReason.TargetLost ? attacker : null,
                null, null, reason);
        // Capture both ORIGINAL native clients before the first opening await.
        // Same-client record copies remain current; a retired holder cannot be recaptured.
        var entryReason = TryCaptureLegacyCombatGearReceiveParticipants(
            context, request, out var originalParticipants);
        if (entryReason != LegacyPlayerMagicReceiveAbortReason.None)
            return Lost(entryReason);
        context = context with { CombatGearReceiveParticipants = originalParticipants };
        LegacyPlayerMagicReceiveAbortReason Refresh() =>
            RefreshLegacyPlayerReceiveCombatGearParticipants(context, request, ref attacker, ref target);
        var reason = Refresh();
        if (reason != LegacyPlayerMagicReceiveAbortReason.None)
            return Lost(reason);

        var applied = 0;
        if (context.WorldData is not null)
        {
            // MyCombatGear.Update invokes the target pass once; after the
            // incoming attacker's update, Receive.Execute invokes it again.
            for (var attempt = 1; attempt <= LegacyCombatGearPlayerReceiveUpdateTargetAttempts; attempt++)
            {
                var defenderContext = context with
                    { Connection = target.Connection, Session = target.Session };
                var (defender, currentAttacker, attemptApplied, attemptReason) =
                    await TrySendLegacyCombatGearUpdateTargetPlayerAttemptAsync(
                            defenderContext, context.Connection, attacker, target.Session,
                            attempt, cancellationToken)
                        .ConfigureAwait(false);
                attacker = currentAttacker;
                target = target with { Session = defender };
                reason = Refresh();
                if (reason != LegacyPlayerMagicReceiveAbortReason.None)
                    return Lost(reason);
                if (attemptReason != LegacyPlayerMagicReceiveAbortReason.None)
                    return Lost(attemptReason);
                applied += attemptApplied;

                if (attempt == 1)
                {
                    var (receiveAttacker, receiveDefender, receiveApplied, receiveReason) =
                        await TrySendLegacyCombatGearReceivePlayerUpdateAsync(
                                context with { Session = attacker }, attacker,
                                target.Connection, target.Session, cancellationToken)
                            .ConfigureAwait(false);
                    attacker = receiveAttacker;
                    target = target with { Session = receiveDefender };
                    reason = Refresh();
                    if (reason != LegacyPlayerMagicReceiveAbortReason.None)
                        return Lost(reason);
                    if (receiveReason != LegacyPlayerMagicReceiveAbortReason.None)
                        return Lost(receiveReason);
                    if (receiveApplied) applied++;
                }
            }
        }

        if (applied > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy CombatGear UpdateTarget player defender passives " +
                    $"{FormatSession(context with { Connection = target.Connection, Session = target.Session })} " +
                    $"attackerUid={attacker.Character.Uid} attempts={LegacyCombatGearPlayerReceiveUpdateTargetAttempts} " +
                    $"applied={applied}; source=ReceiveAttack.Player.Execute opening before receive modifiers/HP.")
                .ConfigureAwait(false);
            reason = Refresh();
            if (reason != LegacyPlayerMagicReceiveAbortReason.None)
                return Lost(reason);
        }

        return new(attacker, target,
            new LegacyPlayerReceiveCombatGearReceipt(context.Connection, request.AttackerUid,
                request.Target.Connection, request.TargetUid, originalParticipants));
    }
}
