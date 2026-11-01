namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // A read-only bridge from this proc's actual final-clear/query completion.
    // It cannot issue a query, start an award, find a replacement owner, or turn
    // matching future DATA into native publication authority.
    internal bool IsDragonhowlQuerySuccessor(OwnedDragonhowlOwner purpose,
        LegacyOrdinarySpellBinding binding, WorldSession current,
        WorldPacketHandlers.FutureEquipmentReceiveData? expected = null)
    {
        if (!dragonhowlOwners.TryGetValue(purpose, out var owner) || owner.Plan is null || owner.Failed ||
            !owner.Committed || !DragonhowlHeldCurrent(owner) ||
            !owner.Bindings.TryGetValue(binding.Connection, out var original) || !ReferenceEquals(original, binding) ||
            !owner.Current.TryGetValue(binding.Connection, out var retained) || !ReferenceEquals(retained, current) ||
            !owner.CompletedExperience.TryGetValue(binding.Connection, out var award) || !award.Complete ||
            award.Terminal is null || award.Published is not { } terminal ||
            award.Data.Levels?.FinalReceiveData is not { } future ||
            expected is not null && !ReferenceEquals(expected, future) ||
            current.Character.Class != terminal.Character.Class || current.Character.Level != terminal.Character.Level ||
            current.Character.Reborn != terminal.Character.Reborn ||
            current.Character.Strength != terminal.Character.Strength || current.Character.Agility != terminal.Character.Agility ||
            current.Character.Vitality != terminal.Character.Vitality || current.Character.Spirit != terminal.Character.Spirit ||
            current.Character.AttributePoints != terminal.Character.AttributePoints ||
            !ReferenceEquals(current.PlayerItems, terminal.PlayerItems)) return false;
        return award.Terminal.HasCurrentQueryMaximums(binding, current, purpose);
    }

    internal bool IsDragonhowlWornQuerySuccessor(OwnedDragonhowlOwner purpose,
        LegacyOrdinarySpellBinding binding, LegacyNativeRuneItemStorage.BasicPhysicalWornSnapshot snapshot,
        WorldSession original, WorldSession current) =>
        dragonhowlOwners.TryGetValue(purpose, out var owner) && owner.Plan is { } plan &&
        plan.ContainsWornSnapshot(binding, snapshot, original) && IsDragonhowlQuerySuccessor(purpose, binding, current);

    internal bool TryReadDragonhowlPerfectionSuccessor(OwnedDragonhowlOwner purpose,
        LegacyOrdinarySpellBinding binding, object inputs, out WorldSession current)
    {
        current = null!;
        if (!dragonhowlOwners.TryGetValue(purpose, out var owner) || owner.Plan is not { } plan ||
            !plan.ContainsPerfectionInputs(binding, inputs) || !owner.Current.TryGetValue(binding.Connection, out var actual) ||
            !IsDragonhowlQuerySuccessor(purpose, binding, actual)) return false;
        current = actual; return true;
    }

    internal bool CoversDragonhowlQueryCache(OwnedDragonhowlOwner purpose, LegacyOrdinarySpellBinding binding,
        WorldSession current, LegacyNativeMagicQueryClient.CompletedMaximums query, WorldSession completedQuery)
    {
        if (!dragonhowlOwners.TryGetValue(purpose, out var owner) || owner.Plan is null || owner.Failed ||
            !owner.Committed || !DragonhowlHeldCurrent(owner) ||
            !owner.Bindings.TryGetValue(binding.Connection, out var bound) || !ReferenceEquals(bound, binding) ||
            !owner.Current.TryGetValue(binding.Connection, out var retained) || !ReferenceEquals(retained, current) ||
            !ReferenceEquals(current.EquipmentHitpointsStatus, completedQuery.EquipmentHitpointsStatus) ||
            current.PhysicalStatusOverlay is not { } overlay || completedQuery.PhysicalStatusOverlay is not { } original) return false;
        if (owner.CompletedExperience.TryGetValue(binding.Connection, out var award))
        {
            if (!award.Complete || award.Terminal is null || award.Published is not { } terminal ||
                !award.Terminal.OwnsRetainedQueryMaximums(binding, query) ||
                !ReferenceEquals(terminal.PhysicalStatusOverlay, completedQuery.PhysicalStatusOverlay) ||
                !ReferenceEquals(terminal.EquipmentHitpointsStatus, completedQuery.EquipmentHitpointsStatus)) return false;
        }
        else if (!owner.ScalarQueries.TryGetValue(binding.Connection, out var scalar) ||
            !ReferenceEquals(scalar.Binding, binding) || !ReferenceEquals(scalar.Query, query) ||
            !owner.CompletedAwards.TryGetValue(binding.Connection, out var scalarAward) || !scalarAward.Complete ||
            scalarAward.CreditPublished || scalarAward.ScalarReceipt is null ||
            !ReferenceEquals(scalarAward.ScalarReceipt.Published, scalarAward.Published) ||
            !ScalarQueryShapeUnchanged(current, scalar.Session) ||
            !ReferenceEquals(scalar.Session.PhysicalStatusOverlay, completedQuery.PhysicalStatusOverlay) ||
            !ReferenceEquals(scalar.Session.EquipmentHitpointsStatus, completedQuery.EquipmentHitpointsStatus)) return false;
        // These are the only physical-cache fields the real receive phases
        // replace. HP/mana, item effects and perfection cache remain the exact
        // completed query's values and native identity.
        return (overlay with { Breakthrough = original.Breakthrough, Counteraction = original.Counteraction,
            FrontBreakExpiresAtUtc = original.FrontBreakExpiresAtUtc }) == original;
    }
}
