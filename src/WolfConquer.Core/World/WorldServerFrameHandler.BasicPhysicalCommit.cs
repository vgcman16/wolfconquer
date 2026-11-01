namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal bool RecordCommittedBasicPhysicalHit(WorldSessionStore store, WorldSessionStore.BasicPhysicalHitReceipt receipt,
        BasicPhysicalAttempt attempt)
    {
        if (!ReferenceEquals(store, _sessions) || !Monitor.IsEntered(_tradeStates.SyncRoot) ||
            !basicPhysicalAttempts.TryGetValue(attempt, out var issued) ||
            !ReferenceEquals(issued.Slot.Attempt, attempt) || issued.CommittedGeneration < 0 ||
            issued.Slot.Generation != issued.CommittedGeneration || issued.Slot.AttackStamp != issued.Stamp ||
            !IsStaminaReadinessCurrent(issued.Slot.Readiness) ||
            !store.TryReadCommittedBasicPhysicalHit(receipt, attempt, out var attacker, out var target) ||
            !ReferenceEquals(attacker, issued.Attacker) || !ReferenceEquals(target, issued.Target)) return false;
        // This private clock came from the actual accepted attempt. Only a
        // real concrete receive publication can advance its cooldown stamp.
        issued.Slot.Committed = receipt;
        return true;
    }
}
