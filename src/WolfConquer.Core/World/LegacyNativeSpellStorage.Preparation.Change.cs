using System.Collections.Concurrent;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSpellStorage
{
    internal sealed class PreparedNinjaSpells
    {
        private readonly LegacyNativeSpellStorage owner;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly uint uid;
        private readonly ConcurrentDictionary<ushort, LegacyNativeMsgSpell> original, candidate;
        private readonly SpellRow[] originalRows, candidateRows;
        private readonly bool changed;
        private readonly long generation;
        private readonly PhysicalMapAnchor? originalAnchor;
        private readonly PhysicalMapAnchor candidateAnchor;
        private IEnumerator<KeyValuePair<ushort, LegacyNativeMsgSpell>>? beforeWriterWitness, finalWitness;
        private IEnumerator<KeyValuePair<ushort, LegacyNativeMsgSpell>>? candidateBeforeWitness, candidateFinalWitness;
        private readonly LegacyNativeMsgSpell[] added;
        private readonly ushort[] removed;
        internal IReadOnlyList<LegacyNativeMsgSpell> Added => added;
        internal IReadOnlyList<ushort> Removed => removed;
        internal PlayerSpellSnapshot FinalSnapshot { get; }

        internal PreparedNinjaSpells(LegacyNativeSpellStorage owner, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player, uint uid, LegacyNativeSpellMutationIntent.NinjaRefresh intent)
        {
            var choices = intent.Choices.ToArray();
            var managedSkills = intent.Managed.ToArray();
            this.owner = owner; this.client = client; this.player = player; this.uid = uid;
            owner.RequireOrdinaryAnchorCurrent();
            original = owner.ClientSpells; generation = owner.MutationGenerationPlain;
            originalAnchor = owner.ordinaryMapAnchor;
            originalRows = CaptureNinjaSpellRows(original);
            var indexed = originalRows.ToDictionary(row => row.Key, row => row.Row);
            foreach (var id in choices.Select(choice => choice.Id).Concat(managedSkills).Distinct())
                CheckSpellLookup(original, indexed, id);
            RequireOriginal();
            var next = new ConcurrentDictionary<ushort, LegacyNativeMsgSpell>(originalRows.Select(row =>
                new KeyValuePair<ushort, LegacyNativeMsgSpell>(row.Key, row.Row)), original.Comparer);
            var additions = new List<LegacyNativeMsgSpell>();
            var retained = new HashSet<ushort>();
            foreach (var choice in choices)
            {
                retained.Add(choice.Id);
                var exists = CheckSpellLookup(next, indexed, choice.Id);
                if (!PermittedNinjaSkill(choice.Id) || exists) continue;
                if (choice.Level > byte.MaxValue) throw UnavailableNinjaSpells();
                var row = new LegacyNativeMsgSpell { ID = choice.Id, Level = (byte)choice.Level,
                    Experience = 0, SoulLevel = 0, PreviousLevel = 0 };
                if (!next.TryAdd(row.ID, row)) throw UnavailableNinjaSpells();
                indexed.Add(row.ID, row); additions.Add(row);
            }
            var removals = new List<ushort>();
            foreach (var id in managedSkills)
                if (!retained.Contains(id) && CheckSpellLookup(next, indexed, id))
                { RemoveExpectedSpell(next, indexed, id); removals.Add(id); }
            RequireExpectedSpellMap(next, indexed);
            added = additions.ToArray(); removed = removals.ToArray();
            changed = added.Length != 0 || removed.Length != 0;
            candidate = changed ? next : original;
            candidateRows = changed ? CaptureNinjaSpellRows(candidate) : originalRows;
            FinalSnapshot = SnapshotPhysicalRows(candidateRows);
            candidateAnchor = new(candidate, candidateRows);
            RequireCurrent();
        }

        internal void RequireCurrent()
        {
            RequireOriginal();
            if (changed) RequireNinjaSpellRows(candidate, candidateRows);
        }

        internal void ArmFinalProof()
        {
            if (beforeWriterWitness is not null || finalWitness is not null) throw UnavailableNinjaSpells();
            RequireCurrent();
            beforeWriterWitness = original.GetEnumerator();
            finalWitness = original.GetEnumerator();
            candidateBeforeWitness = candidate.GetEnumerator();
            candidateFinalWitness = candidate.GetEnumerator();
        }

        internal bool ConsumeBeforeWriterPlain()
        {
            try
            {
                owner.RequireNinjaSpellOwner(client, player, uid);
                if (!ReferenceEquals(owner.ClientSpells, original) || owner.MutationGenerationPlain != generation ||
                    !ReferenceEquals(owner.ordinaryMapAnchor, originalAnchor))
                    return false;
                return ConsumeFinalWitness(ref beforeWriterWitness, originalRows) &&
                    ConsumeFinalWitness(ref candidateBeforeWitness, candidateRows);
            }
            catch (InvalidOperationException) { return false; }
        }

        internal bool ConsumeCurrentPlain()
        {
            try
            {
                owner.RequireNinjaSpellOwner(client, player, uid);
                if (!ReferenceEquals(owner.ClientSpells, original) || owner.MutationGenerationPlain != generation ||
                    !ReferenceEquals(owner.ordinaryMapAnchor, originalAnchor)) return false;
                return ConsumeFinalWitness(ref finalWitness, originalRows) &&
                    ConsumeFinalWitness(ref candidateFinalWitness, candidateRows);
            }
            catch (InvalidOperationException) { return false; }
        }

        private void RequireOriginal()
        {
            owner.RequireNinjaSpellOwner(client, player, uid);
            if (!ReferenceEquals(owner.ClientSpells, original)) throw UnavailableNinjaSpells();
            owner.RequireOrdinaryAnchorCurrent();
            RequireNinjaSpellRows(original, originalRows);
        }

        internal void PublishPreparedMap()
        {
            if (changed) owner.PublishPreparedMapPlain(candidate);
            owner.PublishOrdinaryAnchor(candidateAnchor);
        }

        internal bool HasCurrentCommittedGraph()
        {
            if (!owner.IsOwnedBy(client) || !ReferenceEquals(client.Player, player) ||
                !ReferenceEquals(player.Owner, client) || player.UID != uid ||
                !ReferenceEquals(owner.ClientSpells, candidate)) return false;
            try { RequireNinjaSpellRows(candidate, candidateRows); return true; }
            catch (InvalidOperationException) { return false; }
        }
    }
}
