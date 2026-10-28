namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Issued only by the actual committed ordinary-admission participant. Keep
    // this identity after retirement so replacing MySpells cannot regain an
    // unbound writer path on the same physical client.
    private LegacyOrdinarySpellBinding? ordinarySpellBinding;
    internal LegacyOrdinarySpellBinding? OrdinarySpellBinding =>
        Volatile.Read(ref ordinarySpellBinding);

    internal bool CanAttachOrdinarySpellBinding(LegacyOrdinarySpellBinding binding) =>
        Monitor.IsEntered(SpellAdmissionSyncRoot) && ReferenceEquals(binding.Client, this) &&
        ordinarySpellBinding is null;

    internal void AttachOrdinarySpellBinding(LegacyOrdinarySpellBinding binding) =>
        Volatile.Write(ref ordinarySpellBinding, binding);
}
