namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeEquipItemStorage
{
    // Issued by the real prepared Store publication while its actor gates hold.
    internal sealed class OwnedAlternatePublication
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativeEquipItemStorage equipment;
        private readonly bool before, after;

        private OwnedAlternatePublication(LegacyOrdinarySpellBinding binding,
            WorldSession expected, WorldSession proposed)
        {
            this.binding = binding; equipment = binding.Items.Equipment;
            before = expected.AlternateEquipmentActive; after = proposed.AlternateEquipmentActive;
            if (!ReferenceEquals(expected.NativeGameClient, binding.Client) ||
                !ReferenceEquals(proposed.NativeGameClient, binding.Client) ||
                expected.Character.Uid != binding.Uid || proposed.Character.Uid != binding.Uid ||
                !IsCurrentPlain(proposed.AlternateEquipmentActive)) throw new InvalidOperationException("ordinaryAlternateEquipmentOwnerMismatch");
        }

        internal static OwnedAlternatePublication PrepareBound(LegacyOrdinarySpellBinding binding,
            WorldSession expected, WorldSession proposed) => new(binding, expected, proposed);

        internal bool IsCurrentPlain(bool proposed) => proposed == after && !binding.IsRetired && !binding.Recovery.IsBlocked &&
            ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) &&
            ReferenceEquals(binding.Client.Equipment, equipment) &&
            ReferenceEquals(binding.Items.Equipment, equipment) && equipment.IsOwnedBy(binding.Client) &&
            equipment.Alternante == before;

        // The enclosing native/holder publication performs no callbacks here.
        internal void PublishPrepared() => equipment.Alternante = after;
    }
}
