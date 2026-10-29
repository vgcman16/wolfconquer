using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    private void SetCurrentMana(uint value)
    {
        var binding = AttachedVitalsBinding;
        if (binding is not null)
        {
            throw new InvalidOperationException("ordinaryManaRequiresOwnedStorePlan");
        }
        // Never acquire client after player. A late ordinary admission is
        // serialized by these same actors; do not reenter Store under either.
        if (Monitor.IsEntered(SpellAdmissionSyncRoot) && !Monitor.IsEntered(Owner.SpellAdmissionSyncRoot))
            throw new InvalidOperationException("nativeManaActorOrderInvalid");
        lock (Owner.SpellAdmissionSyncRoot)
        lock (SpellAdmissionSyncRoot)
        {
            binding = AttachedVitalsBinding;
            if (binding is null)
            {
                _mana = value;
                if (Owner.FullLoading)
                {
                    using var recycled = new LegacyNativeRecycledPacket();
                    SendUpdate(recycled.GetStream(), value, LegacyNativeMsgUpdate.DataType.Mana);
                }
                return; // Explicitly unbound component compatibility only.
            }
        }
        throw new InvalidOperationException("ordinaryManaRequiresOwnedStorePlan");
    }
}
