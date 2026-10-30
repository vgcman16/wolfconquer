namespace WolfConquer.Core.World;

internal sealed partial class LegacyOrdinarySpellBinding
{
    private LegacyNativeBeastsRuntime? admittedBeasts;
    internal LegacyNativeBeastsRuntime AdmittedBeasts => admittedBeasts ?? throw new InvalidOperationException("ordinaryRuneBeastWasNotAdmitted");

    internal void InitializePreparedBeasts(LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner)
    {
        if (admittedBeasts is not null || IsRetired || Client.OrdinarySpellBinding is not null)
            throw new InvalidOperationException("ordinaryRuneBeastAdmissionChanged");
        admittedBeasts = owner.PreparedBeastsForBinding(Client, Player);
    }
}
