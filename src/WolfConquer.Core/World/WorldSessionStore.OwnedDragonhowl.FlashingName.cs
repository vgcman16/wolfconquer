namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool IsActualDragonhowlFlashingPhase(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        int index, LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession proposed,
        LegacyNativeStatusFlagsBigVector32.DragonhowlFlashingPreparation native)
    {
        if (!dragonhowlOwners.TryGetValue(plan.Owner, out var state) || !ReferenceEquals(state.Plan, plan) ||
            state.Failed || state.Completed || !state.Started || state.NextPhase != index ||
            index <= 0 || index >= plan.Phases.Count || !DragonhowlHeldCurrent(state) ||
            !ReferenceEquals(binding, state.Bindings[state.Context.Connection]) ||
            !ReferenceEquals(expected, state.Current[binding.Connection])) return false;
        var phase = plan.Phases[index];
        return ReferenceEquals(phase.Connection, binding.Connection) && phase.Death is null && phase.NonPlayer is null &&
            phase.Flashing is { } flashing && ReferenceEquals(flashing.Native, native) && flashing.IsCurrentPlain() &&
            ReferenceEquals(proposed.Character, phase.After.Character) &&
            ReferenceEquals(proposed.CanAttackFlashingRegistration, phase.After.CanAttackFlashingRegistration) &&
            ReferenceEquals(proposed.CanAttackFlashingRegistration, flashing.After.CanAttackFlashingRegistration);
    }
}
