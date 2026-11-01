namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMagicQueryClient
{
    // Final publication of THIS genuine query only. No caller-supplied cache
    // stamp or aggregate can complete an epoch, or authorize an attribute write.
    internal static bool PublishCompletedMaximums(LegacyOrdinarySpellBinding binding,
        WorldSession expected, QueryEpoch? epoch)
    {
        if (epoch is null || !DerivedQueryOwnerIsCurrentPlain(binding) ||
            !Entries.TryGetValue(binding.NativeIdentity, out var entry) || !Queries.TryGetValue(epoch, out var source) ||
            !ReferenceEquals(entry.Epoch, epoch) ||
            !source.Matches(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity, entry) ||
            !source.InputsMatch(expected) || !binding.Store.TryGet(binding.Connection, out var current) ||
            !ReferenceEquals(current, expected) || current.PhysicalStatusOverlay is not { MaxMana: { } mana } overlay ||
            overlay.OwnerUid != binding.Uid || current.EquipmentHitpointsStatus is not { } hp || hp.OwnerUid != binding.Uid ||
            binding.Client.Status is not { } status) return false;
        status.MaxMana = mana;
        status.MaxHitpoints = hp.MaximumHitpoints;
        return true;
    }
}
