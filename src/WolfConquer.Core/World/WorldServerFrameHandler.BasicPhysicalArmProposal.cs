namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal bool TryReadBasicPhysicalArmGeneration(WorldSessionStore store, WorldPacketHandlerContext context,
        WorldPacketHandlers.BasicPhysicalReceiveDecision decision,
        LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot before, out long generation)
    {
        generation = 0;
        if (!ReferenceEquals(store, _sessions) || !Monitor.IsEntered(_tradeStates.SyncRoot) ||
            context.BasicPhysicalAttempt is not { } attempt || !basicPhysicalAttempts.TryGetValue(attempt, out var issued) ||
            !ReferenceEquals(issued.Slot.Attempt, attempt) || issued.CommittedGeneration < 0 ||
            issued.Slot.Generation != issued.CommittedGeneration || !ReferenceEquals(issued.Slot.Native, before) ||
            !before.IsCurrentPlain(issued.Attacker) || !IsStaminaReadinessCurrent(issued.Slot.Readiness) ||
            issued.Slot.Committed is not { } receipt ||
            !WorldPacketHandlers.TryReadBasicPhysicalReceive(decision, out var actual, out _, out _, out _, out _) ||
            !ReferenceEquals(actual, attempt) || !store.TryReadCommittedBasicPhysicalHit(receipt, attempt, out var actor, out var target) ||
            !ReferenceEquals(actor, issued.Attacker) || !ReferenceEquals(target, issued.Target)) return false;
        generation = issued.CommittedGeneration;
        return true;
    }

    internal void NotifyActualBasicPhysicalArm(WorldSessionStore store, LegacyOrdinarySpellBinding binding,
        LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot actual)
    {
        if (!ReferenceEquals(store, _sessions)) return;
        lock (_tradeStates.SyncRoot)
            if (!basicPhysicalSlots.TryGetValue(binding.Connection, out var slot) ||
                !ReferenceEquals(slot.Readiness.Binding, binding) || !IsStaminaReadinessCurrent(slot.Readiness) ||
                !ReferenceEquals(slot.Native, actual) || slot.Repeat is not { } repeat ||
                !ReferenceEquals(repeat.Native, actual) || !actual.OnAutoAttack) return;
        // Never start the tracked worker from inside a native/Store scope.
        NotifyBasicPhysicalRepeatArmed();
    }
}
