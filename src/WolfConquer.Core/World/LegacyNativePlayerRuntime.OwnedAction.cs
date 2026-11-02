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
        private readonly WorldSession? beforeSession;
        private readonly WorldSession? proposedSession;

        private OwnedActionPublication(LegacyOrdinarySpellBinding binding,
            LegacyActionState.NativeValueProof? expected, LegacyActionState.NativeValueProof proposed,
            WorldSession? beforeSession = null, WorldSession? proposedSession = null)
        {
            this.binding = binding; player = binding.Player;
            beforeBinding = Volatile.Read(ref player.ownedActionBinding);
            before = Volatile.Read(ref player._action);
            this.expected = expected; this.proposed = proposed;
            this.beforeSession = beforeSession; this.proposedSession = proposedSession;
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
            return new(binding, current, next, expected, proposed);
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

        // A future spawn may only be serialized from the exact bound Store
        // preparation, while its original holder and physical action remain current.
        internal bool TryReadPreparedSpawnAction(LegacyOrdinarySpellBinding actualBinding,
            WorldSession actualBefore, WorldSession actualProposed, out ushort value)
        {
            value = 0;
            if (!ReferenceEquals(binding, actualBinding) || !ReferenceEquals(beforeSession, actualBefore) ||
                !ReferenceEquals(proposedSession, actualProposed) || expected is null ||
                !WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(player.SpellAdmissionSyncRoot) ||
                !binding.Store.IsBoundSpellOwnerCurrent(binding) || !binding.Store.TryGet(binding.Connection, out var current) ||
                !ReferenceEquals(current, actualBefore) || !ReferenceEquals(binding.Client.Player, player) ||
                !ReferenceEquals(player.Owner, binding.Client) || !ReferenceEquals(player.AttachedActionBinding, binding) ||
                !IsCurrentPlain()) return false;
            value = unchecked((ushort)proposed.Value); return true;
        }

        // The actual Store consumes its connection/actors/maps/file proofs
        // before this joint publication. No callback, output or lookup here.
        internal void PublishPrepared()
        {
            Volatile.Write(ref player._action, proposed.Value);
            Volatile.Write(ref player.ownedActionBinding, binding);
        }
    }
}
