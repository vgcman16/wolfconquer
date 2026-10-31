namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    private void SetCurrentStamina(ushort value)
    {
        if (HasOwnedVitalsAttachment)
            throw new InvalidOperationException("ordinaryStaminaRequiresOwnedStorePlan");
        // Match the joint participant's client -> player order. Reading the
        // scalar never acquires a Store gate; retirement retains attachment.
        if (Monitor.IsEntered(SpellAdmissionSyncRoot) && !Monitor.IsEntered(Owner.SpellAdmissionSyncRoot))
            throw new InvalidOperationException("nativeStaminaActorOrderInvalid");
        lock (Owner.SpellAdmissionSyncRoot)
        lock (SpellAdmissionSyncRoot)
        {
            if (HasOwnedVitalsAttachment)
                throw new InvalidOperationException("ordinaryStaminaRequiresOwnedStorePlan");
            Volatile.Write(ref _stamina, value); // Whole unbound component: numeric only, no output.
        }
    }
}
