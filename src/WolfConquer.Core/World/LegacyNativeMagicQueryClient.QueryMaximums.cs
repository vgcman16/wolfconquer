namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMagicQueryClient
{
    // Final publication of THIS genuine query only. No caller-supplied cache
    // stamp or aggregate can complete an epoch, or authorize an attribute write.
    internal static bool PublishCompletedMaximums(LegacyOrdinarySpellBinding binding,
        WorldSession expected, QueryEpoch? epoch, WorldPacketHandlers.NativeQueryCombatCommit combat,
        out CompletedMaximums? completed)
    {
        completed = null;
        if (epoch is null || !DerivedQueryOwnerIsCurrentPlain(binding) ||
            !Entries.TryGetValue(binding.NativeIdentity, out var entry) || !Queries.TryGetValue(epoch, out var source) ||
            !ReferenceEquals(entry.Epoch, epoch) ||
            !source.Matches(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity, entry) ||
            !source.InputsMatch(expected) || !binding.Store.TryGet(binding.Connection, out var current) ||
            !ReferenceEquals(current, expected) || current.PhysicalStatusOverlay is not { MaxMana: { } mana } overlay ||
            overlay.OwnerUid != binding.Uid || current.EquipmentHitpointsStatus is not { } hp || hp.OwnerUid != binding.Uid ||
            binding.Client.Status is not { } status) return false;
        // Prepare every allocation while this proof is still inert. Once the
        // associations exist, final native publication is only plain writes.
        var proof = new CompletedMaximums();
        var perfection = LegacyNativePerfectionClient.Read(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity);
        var prestige = LegacyNativePerfectionClient.Prestige(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity);
        var publication = new MaximumCompletion(binding, epoch, source, entry, status, expected, entry.Fields, entry.JiangHu,
            perfection, prestige);
        MaximumCompletions.Add(proof, publication); // Inert while Published is false.
        // Consume the real issuer before replacing any retained completion.
        // A fabricated or stale token must not disturb an existing receipt.
        if (!WorldPacketHandlers.TryConsumeNativeQueryCombatCommit(combat, binding, expected, epoch, status, out var after))
        {
            MaximumCompletions.Remove(proof);
            return false;
        }
        CurrentMaximumCompletions.Remove(entry);
        CurrentMaximumCompletions.Add(entry, proof); // All allocations remain before plain native writes.
        status.MaxMana = mana;
        status.MaxHitpoints = hp.MaximumHitpoints;
        after.Write(status);
        publication.Published = true;
        completed = proof;
        return true;
    }
}
