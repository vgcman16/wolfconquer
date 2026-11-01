namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal bool TryReadBasicPhysicalNative(LegacyOrdinarySpellBinding binding,
        out LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot? snapshot, out long generation)
    {
        snapshot = null; generation = 0;
        if (!Monitor.IsEntered(_tradeStates.SyncRoot) || !ReferenceEquals(binding.Store, _sessions) ||
            !basicPhysicalSlots.TryGetValue(binding.Connection, out var slot) ||
            !ReferenceEquals(slot.Readiness.Binding, binding) || !IsStaminaReadinessCurrent(slot.Readiness)) return false;
        snapshot = slot.Native; generation = Volatile.Read(ref slot.Generation);
        return snapshot.IsCurrentPlain(binding);
    }

    internal bool IsBasicPhysicalNativeSnapshotCurrent(LegacyOrdinarySpellBinding binding,
        LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot snapshot, long generation) =>
        Monitor.IsEntered(_tradeStates.SyncRoot) && basicPhysicalSlots.TryGetValue(binding.Connection, out var slot) &&
        ReferenceEquals(slot.Readiness.Binding, binding) && ReferenceEquals(slot.Native, snapshot) &&
        Volatile.Read(ref slot.Generation) == generation && IsStaminaReadinessCurrent(slot.Readiness);

    internal bool IsActualBasicPhysicalRetirementSnapshot(LegacyOrdinarySpellBinding binding,
        LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot snapshot) =>
        ReferenceEquals(binding.Store, _sessions) && basicPhysicalSlots.TryGetValue(binding.Connection, out var slot) &&
        ReferenceEquals(slot.Readiness.Binding, binding) && ReferenceEquals(slot.Native, snapshot);

    internal void RetireBasicPhysicalGeneration(LegacyOrdinarySpellBinding binding)
    {
        if (!ReferenceEquals(binding.Store, _sessions) || !basicPhysicalSlots.TryGetValue(binding.Connection, out var slot) ||
            !ReferenceEquals(slot.Readiness.Binding, binding)) return;
        Interlocked.Increment(ref slot.Generation);
        Volatile.Write(ref slot.Attempt, null);
        slot.Committed = null;
        slot.Repeat = null;
    }

    internal bool TryGetBasicPhysicalRetirementSnapshot(LegacyOrdinarySpellBinding binding,
        out LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot? snapshot)
    {
        snapshot = null;
        if (!ReferenceEquals(binding.Store, _sessions) || !basicPhysicalSlots.TryGetValue(binding.Connection, out var slot) ||
            !ReferenceEquals(slot.Readiness.Binding, binding)) return false;
        snapshot = slot.Native;
        return true;
    }

}
