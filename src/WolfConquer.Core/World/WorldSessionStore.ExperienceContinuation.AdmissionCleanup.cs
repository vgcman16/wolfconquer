namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // A committed plan may retire only its exact initially published holder
    // when origin capture fails. This cannot issue a query or credit owner.
    internal bool TryRetireUnprovenPendingAdmission(LegacyOrdinaryAdmissionPlan plan, WorldSession expected,
        LegacyOrdinarySpellBinding binding)
    {
        if (framePersistenceOwner is not { } frame || !plan.CoversPendingAdmissionCleanup(this, expected, binding)) return false;
        RefuseBoundSpellReentry(binding.Connection);
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            if (!_connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                using var actors = new SpellActorScope(binding);
                if (!plan.CoversPendingAdmissionCleanup(this, expected, binding) ||
                    !_sessions.TryGetValue(binding.Connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                    !ReferenceEquals(holder.SpellBinding, binding) || !ReferenceEquals(holder.NativeClientIdentity, binding.NativeIdentity) ||
                    !ReferenceEquals(binding.Connection.NativeGameClient, binding.Client) ||
                    !ReferenceEquals(binding.Client.OrdinarySpellBinding, binding)) return false;
                holder.Clear();
                return true;
            }
        }
    }
}
