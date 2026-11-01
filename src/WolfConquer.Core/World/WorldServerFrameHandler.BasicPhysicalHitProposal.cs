namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal bool TryReadBasicPhysicalHitProposal(WorldSessionStore store, WorldPacketHandlerContext context,
        WorldPacketHandlers.BasicPhysicalReceiveDecision decision, out LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot? before,
        out long generation, out DateTime stamp)
    {
        before = null; generation = 0; stamp = default;
        if (!ReferenceEquals(store, _sessions) || !Monitor.IsEntered(_tradeStates.SyncRoot) ||
            context.BasicPhysicalAttempt is not { } attempt || !basicPhysicalAttempts.TryGetValue(attempt, out var issued) ||
            !ReferenceEquals(issued.Slot.Attempt, attempt) || issued.CommittedGeneration >= 0 ||
            issued.Slot.Generation != issued.Generation || !IsStaminaReadinessCurrent(issued.Slot.Readiness) ||
            !WorldPacketHandlers.TryReadBasicPhysicalReceive(decision, out var actual, out _, out _, out _, out _) ||
            !ReferenceEquals(actual, attempt) || !TryGetBasicPhysicalAttemptRoles(context, attempt, out var actor, out _) ||
            !ReferenceEquals(actor, issued.Attacker) || !TryReadBasicPhysicalNative(actor, out before, out generation) ||
            before is null || generation != issued.Generation) return false;
        stamp = issued.Stamp; // Actual configured-clock capture from this attempt.
        return true;
    }
}
