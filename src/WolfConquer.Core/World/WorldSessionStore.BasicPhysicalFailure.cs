namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private Exception FenceBasicPhysicalPersistence(BasicPhysicalPublicationData actual, string reason, Exception? cause = null)
    {
        var primary = new LegacyBasicPhysicalRecoveryRequiredException(reason, cause);
        var errors = new List<Exception> { primary };
        foreach (var binding in new[] { actual.ActorBinding, actual.TargetBinding })
        {
            try { binding.Recovery.Block(reason); } catch (Exception error) { errors.Add(error); }
            try { FenceBasicPhysicalBinding(binding); }
            catch (Exception error) { errors.Add(error); }
        }
        return errors.Count == 1 ? primary : new AggregateException("Basic physical outcome and fencing failed.", errors);
    }

    private void FenceBasicPhysicalBinding(LegacyOrdinarySpellBinding binding)
    {
        if (IsCurrentThreadInsideAnyConnectionGate)
        {
            // Commit-side callers already hold the package and both ordered
            // connection gates. Never acquire a higher gate from here.
            if (!_connectionGates.TryGetValue(binding.Connection, out var heldGate) || !Monitor.IsEntered(heldGate))
                throw new InvalidOperationException("basicPhysicalFenceConnectionGateNotHeld");
            FenceBasicPhysicalBindingHeld(binding);
            return;
        }

        // Output/observer failures arrive after all Store scopes were released.
        using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, PeekMailboxSession(binding.Connection));
        if (!_connectionGates.TryGetValue(binding.Connection, out var gate))
        {
            binding.Retire();
            return;
        }
        lock (gate)
        {
            using var nativeGate = EnterNativeConnectionGate();
            FenceBasicPhysicalBindingHeld(binding);
        }
    }

    private void FenceBasicPhysicalBindingHeld(LegacyOrdinarySpellBinding binding)
    {
        if (_sessions.TryGetValue(binding.Connection, out var holder) &&
            ReferenceEquals(holder.SpellBinding, binding) && ReferenceEquals(holder.NativeClientIdentity, binding.NativeIdentity))
            holder.Clear();
        else
            binding.Retire(); // An old binding cannot clear a replacement holder.
    }
}

internal sealed class LegacyBasicPhysicalRecoveryRequiredException(string reason, Exception? cause = null)
    : Exception(reason, cause) { }
