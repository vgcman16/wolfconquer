namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    private uint _action;
    // Retirement retains this physical player's attachment permanently.
    private LegacyOrdinarySpellBinding? ownedActionBinding;
    internal LegacyOrdinarySpellBinding? AttachedActionBinding => Volatile.Read(ref ownedActionBinding) ??
        Owner.OrdinarySpellBinding ?? Owner.MySpells?.OrdinaryBinding;
    internal bool HasOwnedActionAttachment => AttachedActionBinding is not null;

    private void SetCurrentAction(LegacyNativePlayerMapFlags.ConquerAction value)
    {
        if (HasOwnedActionAttachment) throw new InvalidOperationException("ordinaryActionRequiresOwnedStorePlan");
        if (Monitor.IsEntered(SpellAdmissionSyncRoot) && !Monitor.IsEntered(Owner.SpellAdmissionSyncRoot))
            throw new InvalidOperationException("nativeActionActorOrderInvalid");
        lock (Owner.SpellAdmissionSyncRoot)
        lock (SpellAdmissionSyncRoot)
        {
            if (HasOwnedActionAttachment) throw new InvalidOperationException("ordinaryActionRequiresOwnedStorePlan");
            Volatile.Write(ref _action, (uint)value); // Unbound component: numeric only, no output.
        }
    }

    internal sealed class OwnedActionPublication
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyOrdinarySpellBinding? beforeBinding;
        private readonly uint before;
        private readonly LegacyActionState.NativeValueProof? expected;
        private readonly LegacyActionState.NativeValueProof proposed;

        private OwnedActionPublication(LegacyOrdinarySpellBinding binding,
            LegacyActionState.NativeValueProof? expected, LegacyActionState.NativeValueProof proposed)
        {
            this.binding = binding; player = binding.Player;
            beforeBinding = Volatile.Read(ref player.ownedActionBinding);
            before = Volatile.Read(ref player._action);
            this.expected = expected; this.proposed = proposed;
        }

        internal static OwnedActionPublication PrepareBound(LegacyOrdinarySpellBinding binding,
            WorldSession expected, WorldSession proposed)
        {
            var current = new LegacyActionState.NativeValueProof(expected.Character);
            var next = new LegacyActionState.NativeValueProof(proposed.Character);
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !binding.Store.IsBoundSpellOwnerCurrent(binding) || !binding.Store.TryGet(binding.Connection, out var actual) ||
                !ReferenceEquals(actual, expected) || !ReferenceEquals(binding.Player.ownedActionBinding, binding) ||
                current.Value != Volatile.Read(ref binding.Player._action) || !current.IsCurrentPlain() || !next.IsCurrentPlain())
                throw new InvalidOperationException("ordinaryActionPhysicalBaselineMismatch");
            return new(binding, current, next);
        }

        internal static OwnedActionPublication PrepareAdmission(LegacyOrdinarySpellBinding binding,
            LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner, WorldSession proposed)
        {
            var next = new LegacyActionState.NativeValueProof(proposed.Character);
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !owner.BeforeOwnerIsCurrentPlain(binding.Client, binding.Player) || owner.PreparedUid != proposed.Character.Uid ||
                binding.Player.ownedActionBinding is not null || !next.IsCurrentPlain())
                throw new InvalidOperationException("ordinaryActionAdmissionOwnerInvalid");
            return new(binding, null, next);
        }

        internal bool IsCurrentPlain() => Volatile.Read(ref player._action) == before &&
            ReferenceEquals(Volatile.Read(ref player.ownedActionBinding), beforeBinding) &&
            (expected is null || expected.IsCurrentPlain()) && proposed.IsCurrentPlain();

        // The actual Store consumes its connection/actors/maps/file proofs
        // before this joint publication. No callback, output or lookup here.
        internal void PublishPrepared()
        {
            Volatile.Write(ref player._action, proposed.Value);
            Volatile.Write(ref player.ownedActionBinding, binding);
        }
    }
}
