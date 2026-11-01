namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Temporary fail-closed hold while a genuine pending-startup query owner is
    // unavailable. Retire only this exact newly admitted binding; do not touch
    // its saved Character marker or a replacement session.
    internal bool TryHoldPendingExperienceAdmission(LegacyOrdinaryAdmissionPlan.ExperienceAdmissionOrigin origin)
    {
        var binding = origin.Binding; var expected = origin.Admitted;
        RefuseBoundSpellReentry(binding.Connection);
        using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
        if (!_connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
        lock (gate)
        {
            using var native = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(binding.Connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                !ReferenceEquals(holder.SpellBinding, binding) || !ReferenceEquals(holder.NativeClientIdentity, binding.NativeIdentity) ||
                !origin.IsCurrentPlain()) return false;
            if (!ReferenceEquals(holder.Read(), expected) || !ReferenceEquals(holder.SpellBinding, binding)) return false;
            holder.Clear(); _sessions.Remove(binding.Connection);
            return true;
        }
    }
}
