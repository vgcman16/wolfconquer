namespace WolfConquer.Core.World;

internal partial class LegacyNativeSpellStorage
{
    private LegacyOrdinarySpellBinding? ordinaryBinding;

    internal LegacyOrdinarySpellBinding? OrdinaryBinding => Volatile.Read(ref ordinaryBinding);

    internal bool CanAttachOrdinaryBinding(LegacyOrdinarySpellBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return Monitor.IsEntered(mutationGate) && ReferenceEquals(binding.Client, Owner) &&
            ReferenceEquals(binding.Storage, this) && ordinaryBinding is null &&
            Owner.CanAttachOrdinarySpellBinding(binding);
    }

    // Store calls this only after CanAttachOrdinaryBinding at the final plain
    // admission fence. No validation, comparer or allocation is permitted here.
    internal void AttachOrdinaryBinding(LegacyOrdinarySpellBinding binding)
    {
        Owner.AttachOrdinarySpellBinding(binding);
        Volatile.Write(ref ordinaryBinding, binding);
    }

    internal static LegacyOrdinarySpellBinding? GetJoinedBindingForClientEntry(LegacyNativeGameClientRuntime client)
    {
        ArgumentNullException.ThrowIfNull(client);
        var binding = client.OrdinarySpellBinding;
        if (binding is null) return client.MySpells?.GetJoinedBindingForEntry();
        if (binding.IsRetired || !ReferenceEquals(client.MySpells, binding.Storage) ||
            !ReferenceEquals(binding.Storage.OrdinaryBinding, binding))
            throw new InvalidOperationException("The ordinary spell owner was retired or replaced.");
        return binding;
    }

    internal LegacyOrdinarySpellBinding? GetJoinedBindingForEntry()
    {
        var binding = OrdinaryBinding;
        if (binding is null)
        {
            if (Owner.OrdinarySpellBinding is not null)
                throw new InvalidOperationException("The ordinary spell storage was replaced.");
            return null;
        }
        if (binding.IsRetired || !ReferenceEquals(Owner.OrdinarySpellBinding, binding) ||
            !ReferenceEquals(Owner.MySpells, this))
            throw new InvalidOperationException("The ordinary spell owner was retired or replaced.");
        return binding;
    }
}
