namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    private LegacyNativeActorScalarState.Values ReadActorScalarsPlain() => new(_reborn, _agility, xx, yy, _mmmap, _dynamicId);

    internal sealed class OwnedActorScalarsPublication
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyOrdinarySpellBinding? beforeBinding;
        private readonly LegacyNativeActorScalarState.Values before;
        private readonly ushort beforePx, beforePy;
        private readonly LegacyNativeActorScalarState.NativeValueProof? expected;
        private readonly LegacyNativeActorScalarState.NativeValueProof proposed;
        private readonly LegacyPreparedPairMovementWrite.CharacterPublication? pairMovement;
        private readonly LegacyPreparedMovementWrite.CharacterPublication? movement;
        private readonly LegacyPreparedTeleportDestinationWrite.CharacterPublication? teleport;

        private OwnedActorScalarsPublication(LegacyOrdinarySpellBinding binding,
            LegacyNativeActorScalarState.NativeValueProof? expected, LegacyNativeActorScalarState.NativeValueProof proposed,
            LegacyPreparedPairMovementWrite.CharacterPublication? pairMovement = null,
            LegacyPreparedMovementWrite.CharacterPublication? movement = null,
            LegacyPreparedTeleportDestinationWrite.CharacterPublication? teleport = null)
        {
            this.binding = binding; player = binding.Player;
            beforeBinding = Volatile.Read(ref player.ownedActorScalarsBinding);
            before = player.ReadActorScalarsPlain();
            beforePx = player.Px; beforePy = player.Py;
            this.expected = expected; this.proposed = proposed;
            this.pairMovement = pairMovement;
            this.movement = movement;
            this.teleport = teleport;
        }

        internal static OwnedActorScalarsPublication PrepareBound(LegacyOrdinarySpellBinding binding,
            WorldSession expected, WorldSession proposed,
            LegacyPreparedPairMovementWrite.CharacterPublication? pairMovement = null,
            LegacyPreparedMovementWrite.CharacterPublication? movement = null,
            LegacyPreparedTeleportDestinationWrite.CharacterPublication? teleport = null)
        {
            var current = new LegacyNativeActorScalarState.NativeValueProof(expected.Character);
            var next = new LegacyNativeActorScalarState.NativeValueProof(proposed.Character);
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !binding.Store.IsBoundSpellOwnerCurrent(binding) || !binding.Store.TryGet(binding.Connection, out var actual) ||
                !ReferenceEquals(actual, expected) || !ReferenceEquals(binding.Player.ownedActorScalarsBinding, binding) ||
                current.Value != binding.Player.ReadActorScalarsPlain() || !current.IsCurrentPlain() || !next.IsCurrentPlain())
                throw new InvalidOperationException("ordinaryActorScalarPhysicalBaselineMismatch");
            pairMovement?.RequireCurrent(binding.Store, binding.Connection, expected, proposed);
            movement?.RequireCurrent(binding.Store, binding.Connection, expected, proposed);
            teleport?.RequireCurrent(binding.Store, binding.Connection, expected, proposed);
            return new(binding, current, next, pairMovement, movement, teleport);
        }

        internal static OwnedActorScalarsPublication PrepareAdmission(LegacyOrdinarySpellBinding binding,
            LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner, WorldSession proposed)
        {
            var next = new LegacyNativeActorScalarState.NativeValueProof(proposed.Character);
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !owner.BeforeOwnerIsCurrentPlain(binding.Client, binding.Player) || owner.PreparedUid != proposed.Character.Uid ||
                binding.Player.ownedActorScalarsBinding is not null || !next.IsCurrentPlain())
                throw new InvalidOperationException("ordinaryActorScalarAdmissionOwnerInvalid");
            return new(binding, null, next);
        }

        internal bool IsCurrentPlain() => player.ReadActorScalarsPlain() == before && player.Px == beforePx && player.Py == beforePy &&
            ReferenceEquals(Volatile.Read(ref player.ownedActorScalarsBinding), beforeBinding) &&
            (expected is null || expected.IsCurrentPlain()) && proposed.IsCurrentPlain();

        // Actual Store final proofs precede these plain assignments. Admission
        // retains constructor cursors. Generic movement updates both old cursors
        // only when coordinates change; equal-coordinate movement needs its own
        // closed movement-purpose capability, not an inferred Action value.
        internal void PublishPrepared()
        {
            var next = proposed.Value;
            if (expected is not null && (pairMovement is not null || movement is not null || teleport is not null || before.X != next.X || before.Y != next.Y))
            {
                player.Px = before.X; player.Py = before.Y;
            }
            player._reborn = next.Reborn;
            player._agility = next.Agility;
            player.xx = next.X; player.yy = next.Y;
            player._mmmap = next.Map; player._dynamicId = next.DynamicID;
            Volatile.Write(ref player.ownedActorScalarsBinding, binding);
        }
    }
}
