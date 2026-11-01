namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // This proc's applicability witness keeps the actual physical owner exact.
    // A future cache is calculation DATA until the real XP/query successor is
    // recorded by the same proc; ordinary Basic receive remains unchanged.
    private sealed class DragonhowlPerfectionInputs
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldSessionStore.OwnedDragonhowlOwner owner;
        private readonly LegacyNativePerfectionEffect physical;
        private readonly PerfectionValues before;
        private readonly LegacyNativePerfectionSnapshot cached;
        private readonly uint capturedPrestige;
        private readonly FutureEquipmentReceiveData? future;
        private LegacyNativePerfectionSnapshot Calculated => future?.Perfection ?? cached;
        internal uint Prestige => future?.Prestige ?? capturedPrestige;
        internal double LightOfStamina => Math.Max(before.LightOfStamina, Calculated.LightOfStamina);
        internal double DivineGuard => Math.Max(before.DivineGuard, Calculated.DivineGuard);
        internal double CalmWind => Math.Max(before.CalmWind, Calculated.CalmWind);
        internal double DrainingTouch => Math.Max(before.DrainingTouch, Calculated.DrainingTouch);
        internal double BloodSpawn => Math.Max(before.BloodSpawn, Calculated.BloodSpawn);
        internal double KillingFlash => Math.Max(before.KillingFlash, Calculated.KillingFlash);
        internal double MirrorOfSin => Math.Max(before.MirrorOfSin, Calculated.MirrorOfSin);

        internal DragonhowlPerfectionInputs(LegacyOrdinarySpellBinding binding,
            WorldSessionStore.OwnedDragonhowlOwner owner, FutureEquipmentReceiveData? future)
        {
            this.binding = binding; this.owner = owner; this.future = future;
            physical = binding.Client.PerfectionStatus ?? throw new InvalidDataException("ownedDragonhowlPerfectionOwnerMissing");
            before = PerfectionValues.Read(physical);
            cached = LegacyNativePerfectionClient.Read(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity);
            capturedPrestige = LegacyNativePerfectionClient.Prestige(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity);
            if (!before.IsFiniteNonnegative || !IsCurrentPlain())
                throw new InvalidDataException("ownedDragonhowlPerfectionInputInvalid");
        }

        internal bool IsCurrentPlain()
        {
            if (!ReferenceEquals(binding.Client.PerfectionStatus, physical) || PerfectionValues.Read(physical) != before) return false;
            var currentCache = LegacyNativePerfectionClient.Read(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity);
            var currentPrestige = LegacyNativePerfectionClient.Prestige(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity);
            if (ReferenceEquals(currentCache, cached) && currentPrestige == capturedPrestige) return true;
            // Between two real awards, the completed query can precede the
            // final receive projection. Its private award/query receipt owns
            // current caches; future DATA never supplies that authority. The
            // actual receive/death join separately requires its final award.
            return future is not null && binding.Store.TryReadDragonhowlPerfectionSuccessor(owner, binding, this,
                out _) && ReferenceEquals(currentCache, LegacyNativePerfectionClient.Read(
                    binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity)) &&
                currentPrestige == LegacyNativePerfectionClient.Prestige(
                    binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity);
        }
    }
}
