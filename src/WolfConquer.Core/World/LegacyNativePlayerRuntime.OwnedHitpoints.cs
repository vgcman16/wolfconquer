namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    internal sealed class OwnedHitpointsPublication
    {
        private readonly LegacyNativePlayerRuntime player;
        private readonly int before, after;
        private OwnedHitpointsPublication(LegacyNativePlayerRuntime player, int before, long after)
        {
            if (after is < int.MinValue or > int.MaxValue)
                throw new InvalidOperationException("ordinaryHitpointsSignedRangeInvalid");
            this.player = player; this.before = before; this.after = checked((int)after);
        }
        internal static OwnedHitpointsPublication PrepareBound(LegacyOrdinarySpellBinding binding,
            WorldSession expected, WorldSession proposed)
        {
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !binding.Store.IsBoundSpellOwnerCurrent(binding) || !binding.Store.TryGet(binding.Connection, out var current) ||
                !ReferenceEquals(current, expected) || expected.Character.MinHitPoints != Volatile.Read(ref binding.Player._minhitpoints))
                throw new InvalidOperationException("ordinaryHitpointsPhysicalBaselineMismatch");
            return new(binding.Player, checked((int)expected.Character.MinHitPoints), proposed.Character.MinHitPoints);
        }
        internal static OwnedHitpointsPublication PrepareAdmission(LegacyOrdinarySpellBinding binding,
            LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner, WorldSession proposed)
        {
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !owner.BeforeOwnerIsCurrentPlain(binding.Client, binding.Player) ||
                owner.PreparedUid != proposed.Character.Uid || proposed.Character.MinHitPoints < 0)
                throw new InvalidOperationException("ordinaryHitpointsAdmissionOwnerInvalid");
            return new(binding.Player, Volatile.Read(ref binding.Player._minhitpoints), proposed.Character.MinHitPoints);
        }
        internal bool IsCurrentPlain() => Volatile.Read(ref player._minhitpoints) == before;
        // The issuing Store consumes genuine owner/map/file proofs first. No
        // allocation, lookup, Team callback or notification occurs in this store.
        internal void PublishPrepared() => Volatile.Write(ref player._minhitpoints, after);
    }
}
