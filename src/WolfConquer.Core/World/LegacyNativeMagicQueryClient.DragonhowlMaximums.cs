namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMagicQueryClient
{
    internal sealed partial class CompletedMaximums
    {
        // The same native query/cache identity may survive this proc's real
        // mutable BT/CA publication. Only the registered phase lineage may
        // account for that overlay replacement; ordinary callers stay strict.
        internal bool IsCurrentForDragonhowl(LegacyOrdinarySpellBinding binding, WorldSession current,
            WorldSessionStore.OwnedDragonhowlOwner purpose) =>
            MaximumCompletions.TryGetValue(this, out var completed) && completed.Published &&
            ReferenceEquals(completed.Binding, binding) && DerivedQueryOwnerIsCurrentPlain(binding) &&
            CurrentMaximumCompletions.TryGetValue(completed.Entry, out var retained) && ReferenceEquals(retained, this) &&
            ReferenceEquals(completed.Entry.Epoch, completed.Epoch) &&
            completed.Source.Matches(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity, completed.Entry) &&
            completed.Source.InputsMatch(current) && completed.Entry.Fields == completed.Fields &&
            completed.Entry.JiangHu == completed.JiangHu &&
            ReferenceEquals(LegacyNativePerfectionClient.Read(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity), completed.Perfection) &&
            LegacyNativePerfectionClient.Prestige(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity) == completed.Prestige &&
            binding.Store.CoversDragonhowlQueryCache(purpose, binding, current, this, completed.Session) &&
            binding.Client.Status is { } status && ReferenceEquals(status, completed.Status) &&
            current.PhysicalStatusOverlay is { MaxMana: { } mana } && current.EquipmentHitpointsStatus is { } hp &&
            status.MaxMana == mana && status.MaxHitpoints == hp.MaximumHitpoints;
    }
}
