namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class CompletedExperienceContinuation
    {
        internal bool HasCurrentQueryMaximums(LegacyOrdinarySpellBinding binding, WorldSession current,
            WorldSessionStore.OwnedDragonhowlOwner purpose) =>
            CompletedExperienceContinuations.TryGetValue(this, out var actual) &&
            actual.HasCurrentQueryMaximums(binding, current, purpose);

        internal bool OwnsRetainedQueryMaximums(LegacyOrdinarySpellBinding binding,
            LegacyNativeMagicQueryClient.CompletedMaximums maximums) =>
            CompletedExperienceContinuations.TryGetValue(this, out var actual) &&
            actual.OwnsRetainedQueryMaximums(binding, maximums);
    }

    private sealed partial class CompletedExperienceContinuationState
    {
        internal bool OwnsRetainedQueryMaximums(LegacyOrdinarySpellBinding owner,
            LegacyNativeMagicQueryClient.CompletedMaximums query) =>
            ReferenceEquals(owner, binding) && ReferenceEquals(maximums, query) &&
            HasExactLineageContext() && attributes.IsCurrentPlain() && paid.IsCurrentPlain();

        internal bool HasCurrentQueryMaximums(LegacyOrdinarySpellBinding owner, WorldSession current,
            WorldSessionStore.OwnedDragonhowlOwner purpose) =>
            ReferenceEquals(owner, binding) && maximums is not null && HasExactLineageContext() &&
            attributes.IsCurrentPlain() && paid.IsCurrentPlain() &&
            binding.Store.IsLevelUpRebuildOwnerHeld(binding, current, operationContext) &&
            maximums.IsCurrentForDragonhowl(binding, current, purpose);
    }
}
