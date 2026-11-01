using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class OwnedBasicPhysicalReceiveData
    {
        private readonly List<OwnedPhysicalCanonicalOutcome> canonicalOutcomes = [];

        internal bool CanonicalOutcomesMatch(WorldSession actor, WorldSession target)
        {
            if (!IsCurrentPlain() || !SameOwnedPhysicalOutcome(Actor, actor) || !SameOwnedPhysicalOutcome(Target, target)) return false;
            if (!canonicalOutcomes.Any(row => ReferenceEquals(row.Actor, actor) && ReferenceEquals(row.Target, target)))
                canonicalOutcomes.Add(new(actor, target));
            return IsCurrentPlain();
        }

        private bool CanonicalTargetsAreCurrentPlain()
        {
            foreach (var outcome in canonicalOutcomes) if (!outcome.IsCurrentPlain()) return false;
            return true;
        }
    }

    private sealed class OwnedPhysicalCanonicalOutcome
    {
        internal readonly WorldSession Actor, Target;
        private readonly LegacyNativeAttributeMapProof actorAttributes, actorPaid, targetAttributes, targetPaid;
        private readonly LegacyPerformerSpellSource actorSpells, targetSpells;
        private readonly LegacyItemSaveGraph actorItems, targetItems;
        internal OwnedPhysicalCanonicalOutcome(WorldSession actor, WorldSession target)
        {
            Actor = actor; Target = target;
            actorAttributes = new(actor.Character.Attributes); actorPaid = new(actor.Character.PaidNobility);
            targetAttributes = new(target.Character.Attributes); targetPaid = new(target.Character.PaidNobility);
            actorSpells = new(actor.PlayerSpells.Spells); targetSpells = new(target.PlayerSpells.Spells);
            if (!LegacyItemSaveGraph.TryCapture(actor.PlayerItems, out var own) || own is null ||
                !LegacyItemSaveGraph.TryCapture(target.PlayerItems, out var peer) || peer is null)
                throw new InvalidDataException("ordinaryBasicPhysicalCanonicalItemsInvalid");
            actorItems = own; targetItems = peer;
        }
        internal bool IsCurrentPlain() => actorAttributes.IsCurrentPlain() && actorPaid.IsCurrentPlain() &&
            targetAttributes.IsCurrentPlain() && targetPaid.IsCurrentPlain() && actorSpells.IsCurrent() && targetSpells.IsCurrent() &&
            actorItems.IsCurrent(Actor.PlayerItems) && targetItems.IsCurrent(Target.PlayerItems);
    }

    private static bool SameOwnedPhysicalOutcome(WorldSession expected, WorldSession actual)
    {
        if (actual is null || (expected with { Character = actual.Character, PlayerItems = actual.PlayerItems,
                PlayerSpells = actual.PlayerSpells, PlayerProficiencies = actual.PlayerProficiencies }) != actual ||
            (expected.Character with { Attributes = actual.Character.Attributes, PaidNobility = actual.Character.PaidNobility }) != actual.Character)
            return false;
        var expectedAttributes = new LegacyNativeAttributeMapProof(expected.Character.Attributes);
        var actualAttributes = new LegacyNativeAttributeMapProof(actual.Character.Attributes);
        var expectedPaid = new LegacyNativeAttributeMapProof(expected.Character.PaidNobility);
        var actualPaid = new LegacyNativeAttributeMapProof(actual.Character.PaidNobility);
        bool Same(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right) =>
            left.Count == right.Count && left.All(row => right.TryGetValue(row.Key, out var value) && value == row.Value);
        return Same(expectedAttributes.Fields, actualAttributes.Fields) && Same(expectedPaid.Fields, actualPaid.Fields) &&
            PlayerItemSnapshotLoader.ToLegacyBytes(expected.PlayerItems).AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(actual.PlayerItems)) &&
            PlayerSpellSnapshotLoader.ToLegacyBytes(expected.PlayerSpells).AsSpan().SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(actual.PlayerSpells)) &&
            PlayerProficiencySnapshotLoader.ToLegacyBytes(expected.PlayerProficiencies).AsSpan().SequenceEqual(PlayerProficiencySnapshotLoader.ToLegacyBytes(actual.PlayerProficiencies)) &&
            expectedAttributes.IsCurrentPlain() && actualAttributes.IsCurrentPlain() && expectedPaid.IsCurrentPlain() && actualPaid.IsCurrentPlain();
    }
}
