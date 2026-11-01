namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed class OwnedDragonhowlFlashingProjection : IDisposable
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldSession before;
        private readonly WorldPacketHandlers.OwnedPhysicalDeathPreparation? death;
        private readonly LegacyNativeStatusFlagsBigVector32.OwnedDragonhowlFlashingLease.ExpiryPublication? expiry;
        private readonly LegacyNativeStatusFlagsBigVector32 vector;
        private bool held;
        private OwnedDragonhowlFlashingProjection(LegacyOrdinarySpellBinding binding, WorldSession before,
            WorldSession after, WorldPacketHandlers.OwnedPhysicalDeathPreparation? death,
            LegacyNativeStatusFlagsBigVector32.OwnedDragonhowlFlashingLease.ExpiryPublication? expiry)
        {
            this.binding = binding; this.before = before; this.death = death; this.expiry = expiry;
            vector = binding.Player.BitVector;
            Monitor.Enter(vector.SyncRoot, ref held);
            try { RequireOwnedDragonhowlFlashingTransition(binding, before, after, death, expiry); }
            catch { Dispose(); throw; }
        }
        internal static OwnedDragonhowlFlashingProjection? Prepare(LegacyOrdinarySpellBinding binding,
            WorldSession before, WorldSession after, WorldPacketHandlers.OwnedPhysicalDeathPreparation? death,
            LegacyNativeStatusFlagsBigVector32.OwnedDragonhowlFlashingLease.ExpiryPublication? expiry)
        {
            if (!LegacyNativeStatusFlagsBigVector32.HasOwnedDragonhowlFlashing(binding))
            { if (expiry is not null) throw InvalidSpellPublication(); return null; }
            return new(binding, before, after, death, expiry);
        }
        internal bool IsCurrentPlain(WorldSession final)
        {
            if (!held || !ReferenceEquals(binding.Player.BitVector, vector)) return false;
            RequireOwnedDragonhowlFlashingTransition(binding, before, final, death, expiry); return true;
        }
        public void Dispose() { if (held) { held = false; Monitor.Exit(vector.SyncRoot); } }
    }

    private static void RequireOwnedDragonhowlFlashingTransition(LegacyOrdinarySpellBinding binding,
        WorldSession before, WorldSession after, WorldPacketHandlers.OwnedPhysicalDeathPreparation? death,
        LegacyNativeStatusFlagsBigVector32.OwnedDragonhowlFlashingLease.ExpiryPublication? expiry)
    {
        if (!LegacyNativeStatusFlagsBigVector32.HasOwnedDragonhowlFlashing(binding))
        {
            if (expiry is not null) throw InvalidSpellPublication();
            return; // The existing managed-only producer has no invented native lease.
        }
        if (!LegacyNativeStatusFlagsBigVector32.TryCaptureOwnedDragonhowlFlashing(binding, before, out var lease) || lease is null)
            throw InvalidSpellPublication();
        if (expiry is not null)
        {
            if (death is not null || !expiry.IsPreparedFor(binding, before, after)) throw InvalidSpellPublication();
            return;
        }
        if (death is not null && death.TryReadNativeFlashingDeathProposal(binding, after, out var removed) &&
            ReferenceEquals(removed, lease)) return;
        if (!ReferenceEquals(after.CanAttackFlashingRegistration, lease.Registration) || !lease.IsCurrentPlain(after))
            throw InvalidSpellPublication();
    }
}
