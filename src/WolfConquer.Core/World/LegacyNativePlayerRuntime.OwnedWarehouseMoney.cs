namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    private LegacyOrdinarySpellBinding? ownedWarehouseMoneyBinding;
    private LegacyOrdinarySpellBinding? AttachedWarehouseMoneyBinding => Volatile.Read(ref ownedWarehouseMoneyBinding) ??
        Owner.OrdinarySpellBinding ?? Owner.MySpells?.OrdinaryBinding;
    internal bool HasOwnedWarehouseMoneyAttachment => AttachedWarehouseMoneyBinding is not null;

    // Public WHMoney remains a field for compatibility. This participant has
    // no emitting setter, saver or delegate that could manufacture ownership.
    internal sealed class OwnedWarehouseMoneyPublication
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyOrdinarySpellBinding? beforeBinding;
        private readonly long before, after;
        private readonly LegacyWarehouseMoneyState.NativeValueProof? expected;
        private readonly LegacyWarehouseMoneyState.NativeValueProof proposed;

        private OwnedWarehouseMoneyPublication(LegacyOrdinarySpellBinding binding,
            LegacyWarehouseMoneyState.NativeValueProof? expected, LegacyWarehouseMoneyState.NativeValueProof proposed)
        {
            this.binding = binding; player = binding.Player;
            this.expected = expected; this.proposed = proposed;
            before = Volatile.Read(ref player.WHMoney); after = proposed.Value;
            beforeBinding = Volatile.Read(ref player.ownedWarehouseMoneyBinding);
            if (before < 0) throw new InvalidOperationException("ordinaryWarehouseMoneyPhysicalBaselineInvalid");
        }

        internal static OwnedWarehouseMoneyPublication PrepareAdmission(LegacyOrdinarySpellBinding binding,
            LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner, WorldSession proposed)
        {
            var saved = new LegacyWarehouseMoneyState.NativeValueProof(proposed.Character);
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !owner.BeforeOwnerIsCurrentPlain(binding.Client, binding.Player) ||
                owner.PreparedUid != proposed.Character.Uid || binding.Player.ownedWarehouseMoneyBinding is not null ||
                !saved.IsCurrentPlain()) throw new InvalidOperationException("ordinaryWarehouseMoneyAdmissionOwnerInvalid");
            return new(binding, null, saved);
        }

        internal static OwnedWarehouseMoneyPublication PrepareBound(LegacyOrdinarySpellBinding binding,
            WorldSession expected, WorldSession proposed)
        {
            var current = new LegacyWarehouseMoneyState.NativeValueProof(expected.Character);
            var next = new LegacyWarehouseMoneyState.NativeValueProof(proposed.Character);
            // Alias/wrapper callbacks have all completed before native baseline
            // comparison. Later catalog/map callbacks must still pass the proof.
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !binding.Store.IsBoundSpellOwnerCurrent(binding) || !binding.Store.TryGet(binding.Connection, out var session) ||
                !ReferenceEquals(session, expected) || !ReferenceEquals(binding.Player.ownedWarehouseMoneyBinding, binding) ||
                current.Value != Volatile.Read(ref binding.Player.WHMoney) || !current.IsCurrentPlain() || !next.IsCurrentPlain())
                throw new InvalidOperationException("ordinaryWarehouseMoneyPhysicalBaselineMismatch");
            return new(binding, current, next);
        }

        internal bool IsCurrentPlain() => Volatile.Read(ref player.WHMoney) == before &&
            ReferenceEquals(Volatile.Read(ref player.ownedWarehouseMoneyBinding), beforeBinding) &&
            (expected is null || expected.IsCurrentPlain()) && proposed.IsCurrentPlain();

        internal void PublishPrepared()
        {
            Volatile.Write(ref player.WHMoney, after);
            Volatile.Write(ref player.ownedWarehouseMoneyBinding, binding);
        }
    }
}
