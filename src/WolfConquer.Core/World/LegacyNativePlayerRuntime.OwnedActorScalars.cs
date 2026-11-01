namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    private LegacyNativeActorScalarState.Values ReadActorScalarsPlain() => new(_reborn, _agility, xx, yy, _mmmap, _dynamicId);
    private LegacyNativeAttributeState ReadAttributeStatePlain() => new(_strength, _agility, _vitality, _spirit, _atributes);
    internal bool AttributeStateMatches(WorldSession session)
    {
        try { return session.Character.Experience >= 0 && session.Character.Level == _level &&
                (ulong)session.Character.Experience == experience &&
                LegacyNativeAttributeState.From(session.Character) == ReadAttributeStatePlain(); }
        catch (InvalidOperationException) { return false; }
    }

    internal sealed class OwnedActorScalarsPublication
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyOrdinarySpellBinding? beforeBinding;
        private readonly LegacyNativeActorScalarState.Values before;
        private readonly LegacyNativeAttributeState beforeAttributes, proposedAttributes;
        private readonly ulong beforeExperience;
        private readonly ulong? admissionExperience;
        private readonly WorldPacketHandlers.OwnedCombatExperience? combatExperience;
        private readonly ushort beforeLevel, proposedLevel;
        private readonly ulong proposedExperience;
        private readonly WorldPacketHandlers.OwnedFullAccountClassSpellRetirement? classRetirement;
        private readonly WorldSessionStore.OwnedAttributeChange? attributeChange;
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
            LegacyPreparedTeleportDestinationWrite.CharacterPublication? teleport = null,
            LegacyNativeAttributeState proposedAttributes = default,
            WorldPacketHandlers.OwnedFullAccountClassSpellRetirement? classRetirement = null,
            WorldSessionStore.OwnedAttributeChange? attributeChange = null,
            ulong? admissionExperience = null, WorldPacketHandlers.OwnedCombatExperience? combatExperience = null,
            ushort proposedLevel = 0, ulong proposedExperience = 0)
        {
            this.binding = binding; player = binding.Player;
            beforeBinding = Volatile.Read(ref player.ownedActorScalarsBinding);
            before = player.ReadActorScalarsPlain();
            beforeAttributes = player.ReadAttributeStatePlain();
            beforeExperience = player.Experience;
            this.admissionExperience = admissionExperience;
            this.combatExperience = combatExperience; beforeLevel = player._level;
            this.proposedLevel = proposedLevel; this.proposedExperience = proposedExperience;
            this.proposedAttributes = proposedAttributes;
            this.classRetirement = classRetirement;
            this.attributeChange = attributeChange;
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
            LegacyPreparedTeleportDestinationWrite.CharacterPublication? teleport = null,
            WorldPacketHandlers.OwnedFullAccountClassSpellRetirement? classRetirement = null,
            WorldSessionStore.OwnedAttributeChange? attributeChange = null,
            WorldPacketHandlers.OwnedCombatExperience? combatExperience = null)
        {
            var current = new LegacyNativeActorScalarState.NativeValueProof(expected.Character);
            var next = new LegacyNativeActorScalarState.NativeValueProof(proposed.Character);
            var currentAttributes = LegacyNativeAttributeState.From(expected.Character);
            var nextAttributes = LegacyNativeAttributeState.From(proposed.Character);
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !binding.Store.IsBoundSpellOwnerCurrent(binding) || !binding.Store.TryGet(binding.Connection, out var actual) ||
                !ReferenceEquals(actual, expected) || !ReferenceEquals(binding.Player.ownedActorScalarsBinding, binding) ||
                current.Value != binding.Player.ReadActorScalarsPlain() || currentAttributes != binding.Player.ReadAttributeStatePlain() ||
                !current.IsCurrentPlain() || !next.IsCurrentPlain())
                throw new InvalidOperationException("ordinaryActorScalarPhysicalBaselineMismatch");
            if (currentAttributes != nextAttributes)
            {
                if (!(classRetirement?.CoversAttributes(binding, expected, proposed) ?? false) &&
                    !(attributeChange?.Covers(binding, expected, proposed) ?? false) &&
                    !(combatExperience?.CoversAttributes(binding, expected, proposed) ?? false))
                    throw new InvalidOperationException("ordinaryAttributesRequireOwnedPurpose");
            }
            bool levelExperienceChanged = expected.Character.Level != proposed.Character.Level ||
                expected.Character.Experience != proposed.Character.Experience;
            if (levelExperienceChanged && !(combatExperience?.CoversAttributes(binding, expected, proposed) ?? false) &&
                !(classRetirement?.CoversLevelExperience(binding, expected, proposed) ?? false))
                throw new InvalidOperationException("ordinaryLevelExperienceRequireOwnedPurpose");
            if (combatExperience is not null && (!combatExperience.CoversAttributes(binding, expected, proposed) ||
                expected.Character.Level != binding.Player._level || expected.Character.Experience < 0 ||
                (ulong)expected.Character.Experience != binding.Player.Experience ||
                proposed.Character.Level is < 1 or > ushort.MaxValue || proposed.Character.Experience < 0))
                throw new InvalidOperationException("combatExperienceNativeBaselineInvalid");
            pairMovement?.RequireCurrent(binding.Store, binding.Connection, expected, proposed);
            movement?.RequireCurrent(binding.Store, binding.Connection, expected, proposed);
            teleport?.RequireCurrent(binding.Store, binding.Connection, expected, proposed);
            return new(binding, current, next, pairMovement, movement, teleport, nextAttributes, classRetirement, attributeChange,
                combatExperience: combatExperience, proposedLevel: checked((ushort)proposed.Character.Level),
                proposedExperience: checked((ulong)proposed.Character.Experience));
        }

        internal static OwnedActorScalarsPublication PrepareAdmission(LegacyOrdinarySpellBinding binding,
            LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner, WorldSession proposed)
        {
            var next = new LegacyNativeActorScalarState.NativeValueProof(proposed.Character);
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !owner.BeforeOwnerIsCurrentPlain(binding.Client, binding.Player) || owner.PreparedUid != proposed.Character.Uid ||
                binding.Player.ownedActorScalarsBinding is not null || !next.IsCurrentPlain())
                throw new InvalidOperationException("ordinaryActorScalarAdmissionOwnerInvalid");
            if (proposed.Character.Experience < 0) throw new InvalidOperationException("ordinaryExperienceAdmissionInvalid");
            return new(binding, null, next, proposedAttributes: LegacyNativeAttributeState.From(proposed.Character),
                admissionExperience: checked((ulong)proposed.Character.Experience));
        }

        internal bool IsCurrentPlain() => player.ReadActorScalarsPlain() == before && player.ReadAttributeStatePlain() == beforeAttributes &&
            player._level == beforeLevel && player.experience == beforeExperience &&
            (combatExperience is null || player._level == beforeLevel && player.Experience == beforeExperience && combatExperience.IsCurrentPlain()) &&
            (classRetirement is null || classRetirement.IsCurrentPlain()) &&
            (attributeChange is null || attributeChange.IsCurrentPlain()) && player.Px == beforePx && player.Py == beforePy &&
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
            player._strength = proposedAttributes.Strength; player._vitality = proposedAttributes.Vitality;
            player._spirit = proposedAttributes.Spirit; player._atributes = proposedAttributes.Atributes;
            if (admissionExperience is { } admittedExperience) player.experience = admittedExperience;
            if (expected is not null) { player._level = proposedLevel; player.experience = proposedExperience; }
            player.xx = next.X; player.yy = next.Y;
            player._mmmap = next.Map; player._dynamicId = next.DynamicID;
            Volatile.Write(ref player.ownedActorScalarsBinding, binding);
        }
    }
}
