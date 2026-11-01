namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryReadDragonhowlWornSuccessor(OwnedDragonhowlOwner purpose,
        LegacyOrdinarySpellBinding binding, LegacyNativeRuneItemStorage.BasicPhysicalWornSnapshot snapshot,
        WorldSession original, out WorldSession current)
    {
        current = null!;
        if (!dragonhowlOwners.TryGetValue(purpose, out var state) || state.Plan is not { } plan ||
            state.Failed || !state.Committed || state.NextPhase <= 0 || !DragonhowlHeldCurrent(state) ||
            !state.Bindings.TryGetValue(binding.Connection, out var actual) || !ReferenceEquals(actual, binding) ||
            !plan.ContainsWornSnapshot(binding, snapshot, original) ||
            !state.Current.TryGetValue(binding.Connection, out var successor)) return false;
        current = successor; return true;
    }
}
