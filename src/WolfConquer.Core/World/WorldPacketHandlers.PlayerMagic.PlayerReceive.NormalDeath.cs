namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<LegacyPlayerMagicReceiveOutcome> CompleteLegacyNormalPlayerDeathAsync(
        WorldPacketHandlerContext context,
        LegacyPlayerMagicReceivePreparation prepared,
        WorldActiveSession target,
        LegacyNativePlayerRuntime? nativePlayerReceiver,
        WorldSession attacker,
        CancellationToken cancellationToken)
    {
        // The physical receiver is carried from ReceiveAttack entry, not re-fetched after transform awaits.
        var scope = new LegacyEquipmentQueryPublicationScope(target.Session.Character.Uid,
            RequiredParticipant: new WorldActiveSession(context.Connection, attacker));
        var death = await ExecuteLegacyNormalPlayerDeathPhasesAsync(
                context with { Connection = target.Connection, Session = target.Session }, target.Session, scope, nativePlayerReceiver,
                new LegacyNormalPlayerDeathCause(prepared.DeathX, prepared.DeathY,
                    attacker.Character.Uid, attacker.Character.Name), cancellationToken)
            .ConfigureAwait(false);
        if (!death.Completed)
            return death.RequiredParticipantSession is null
                ? new(null, null, LegacyPlayerMagicReceiveAbortReason.AttackerLost)
                : new(null, death.RequiredParticipantSession, LegacyPlayerMagicReceiveAbortReason.TargetLost);

        var result = prepared.Result with
        {
            ActiveSession = target with { Session = death.Session! },
            UpdatedSession = death.Session!,
            UpdatedAttackerSession = death.RequiredParticipantSession,
            AttackerTransformFinished = false,
            NormalDeathPhasesPublished = true,
        };
        return new(result, death.RequiredParticipantSession);
    }
}
