namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static LegacyPlayerMagicDamageResult CommitLegacyPreparedPlayerReceive(
        WorldPacketHandlerContext context,
        LegacyPlayerMagicReceivePreparation prepared,
        WorldSession targetSession)
    {
        var prior = prepared.Result;
        if (prior.Killed)
            throw new InvalidOperationException("Normal lethal receive requires its asynchronous source death phases.");
        var updated = targetSession with
        {
            Character = targetSession.Character with { MinHitPoints = prior.PreviousHitpoints - prior.DamageApplied },
        };
        if (context.BasicPhysicalReceiveDecision is { } basic)
        {
            if (context.Sessions is not { } actualStore ||
                !actualStore.TryCommitBasicPhysicalReceive(context, basic,
                    prior.ActiveSession with { Session = prior.UpdatedSession }, out var committed) || committed is null)
                throw new LegacyNativeHitpointsRefusalException("basicPhysicalReceiveContinuationChanged");
            prior.CapriceReceipt?.ObserveCompletedTargetContinuation(committed);
            updated = committed;
        }
        else
            updated = PublishLegacyReceiveTargetContinuation(context, prior.ActiveSession.Connection,
                updated, prior.CapriceReceipt);
        return prior with
        {
            ActiveSession = prior.ActiveSession with { Session = updated },
            UpdatedSession = updated,
            AttackerTransformFinished = false,
        };
    }

}
