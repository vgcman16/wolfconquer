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
        context.Sessions?.Set(prior.ActiveSession.Connection, updated);
        return prior with
        {
            ActiveSession = prior.ActiveSession with { Session = updated },
            UpdatedSession = updated,
            AttackerTransformFinished = false,
        };
    }

}
