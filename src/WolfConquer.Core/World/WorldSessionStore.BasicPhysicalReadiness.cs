namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryCaptureBasicPhysicalReadiness(LegacyOwnedStaminaRuntime.Readiness readiness,
        out LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot? snapshot)
    {
        snapshot = null;
        var binding = readiness.Binding;
        if (!ReferenceEquals(framePersistenceOwner, readiness.Frame) || !ReferenceEquals(readiness.Store, this) ||
            !Monitor.IsEntered(readiness.LifecycleGate) || !readiness.Frame.IsStaminaReadinessCurrent(readiness)) return false;
        using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, PeekMailboxSession(binding.Connection));
        if (!_connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
        lock (gate)
        {
            using var nativeGate = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(binding.Connection, out var holder) || holder.Read() is not { } current ||
                !ReferenceEquals(holder.SpellBinding, binding) || !BindingIsCurrentPlain(binding, holder, current)) return false;
            using var actors = new SpellActorScope(binding);
            snapshot = LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot.Capture(binding);
            return readiness.Frame.IsStaminaReadinessCurrent(readiness) && snapshot.IsCurrentPlain(binding);
        }
    }
}
