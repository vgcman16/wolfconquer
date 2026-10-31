namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Set only by genuine ordinary admission. Retirement never makes this
    // physical player eligible for the unbound component setter again.
    private LegacyOrdinarySpellBinding? ownedVitalsBinding;
    internal LegacyOrdinarySpellBinding? AttachedVitalsBinding => Volatile.Read(ref ownedVitalsBinding) ??
        Owner.OrdinarySpellBinding ?? Owner.MySpells?.OrdinaryBinding;
    internal bool HasOwnedVitalsAttachment => AttachedVitalsBinding is not null;

    // Historical name retained for existing writer/caller APIs. This is ONE
    // joint current-HP/current-Mana/current-Stamina participant.
    internal sealed class OwnedHitpointsPublication
    {
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyOrdinarySpellBinding? beforeBinding;
        private readonly int before, after;
        private readonly uint beforeMana, afterMana;
        private readonly ushort beforeStamina;
        private readonly LegacyStaminaState.NativeValueProof? expectedStamina;
        private readonly LegacyStaminaState.NativeValueProof proposedStamina;
        private OwnedHitpointsPublication(LegacyOrdinarySpellBinding binding, int before, long after,
            uint beforeMana, long afterMana, LegacyStaminaState.NativeValueProof? expectedStamina,
            LegacyStaminaState.NativeValueProof proposedStamina)
        {
            if (after is < int.MinValue or > int.MaxValue)
                throw new InvalidOperationException("ordinaryHitpointsSignedRangeInvalid");
            if (afterMana is < uint.MinValue or > uint.MaxValue)
                throw new InvalidOperationException("ordinaryManaUnsignedRangeInvalid");
            this.binding = binding; player = binding.Player;
            beforeBinding = Volatile.Read(ref player.ownedVitalsBinding);
            this.before = before; this.after = checked((int)after);
            this.beforeMana = beforeMana; this.afterMana = checked((uint)afterMana);
            beforeStamina = Volatile.Read(ref player._stamina);
            this.expectedStamina = expectedStamina; this.proposedStamina = proposedStamina;
        }
        internal static OwnedHitpointsPublication PrepareBound(LegacyOrdinarySpellBinding binding,
            WorldSession expected, WorldSession proposed)
        {
            var currentStamina = new LegacyStaminaState.NativeValueProof(expected.Character);
            var nextStamina = new LegacyStaminaState.NativeValueProof(proposed.Character);
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !binding.Store.IsBoundSpellOwnerCurrent(binding) || !binding.Store.TryGet(binding.Connection, out var current) ||
                !ReferenceEquals(current, expected) || !ReferenceEquals(binding.Player.ownedVitalsBinding, binding) ||
                expected.Character.MinHitPoints != Volatile.Read(ref binding.Player._minhitpoints) ||
                expected.Character.MinMana != binding.Player._mana ||
                currentStamina.Value != Volatile.Read(ref binding.Player._stamina) ||
                !currentStamina.IsCurrentPlain() || !nextStamina.IsCurrentPlain())
                throw new InvalidOperationException("ordinaryHitpointsPhysicalBaselineMismatch");
            return new(binding, checked((int)expected.Character.MinHitPoints), proposed.Character.MinHitPoints,
                binding.Player._mana, proposed.Character.MinMana, currentStamina, nextStamina);
        }
        internal static OwnedHitpointsPublication PrepareAdmission(LegacyOrdinarySpellBinding binding,
            LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner, WorldSession proposed)
        {
            var stamina = new LegacyStaminaState.NativeValueProof(proposed.Character);
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !owner.BeforeOwnerIsCurrentPlain(binding.Client, binding.Player) ||
                owner.PreparedUid != proposed.Character.Uid || proposed.Character.MinHitPoints < 0 ||
                binding.Player.ownedVitalsBinding is not null || !stamina.IsCurrentPlain())
                throw new InvalidOperationException("ordinaryHitpointsAdmissionOwnerInvalid");
            return new(binding, Volatile.Read(ref binding.Player._minhitpoints), proposed.Character.MinHitPoints,
                binding.Player._mana, proposed.Character.MinMana, null, stamina);
        }
        internal bool IsCurrentPlain() => Volatile.Read(ref player._minhitpoints) == before &&
            player._mana == beforeMana && Volatile.Read(ref player._stamina) == beforeStamina &&
            ReferenceEquals(Volatile.Read(ref player.ownedVitalsBinding), beforeBinding) &&
            (expectedStamina is null || expectedStamina.IsCurrentPlain()) && proposedStamina.IsCurrentPlain();
        // The issuing Store consumes genuine owner/map/file proofs first. No
        // allocation, lookup, Team callback or notification occurs in this store.
        internal void PublishPrepared()
        {
            Volatile.Write(ref player._minhitpoints, after);
            player._mana = afterMana;
            Volatile.Write(ref player._stamina, proposedStamina.Value);
            Volatile.Write(ref player.ownedVitalsBinding, binding);
        }
    }
}
