using System.Collections.Concurrent;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeProficiencyStorage
{
    // Canonical bound publication deliberately uses detached rows. A native
    // component caller, however, legitimately retains its original row objects.
    // Prepare the complete future image without touching those objects, then
    // publish their scalar progress only after the compound spell/prof fence.
    internal sealed class PreparedUnboundTraining
    {
        private readonly LegacyNativeProficiencyStorage owner;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly uint uid;
        private readonly long generation;
        private readonly PhysicalMapAnchor? priorAnchor;
        private readonly PhysicalMapAnchor nextAnchor;
        private readonly ConcurrentDictionary<uint, LegacyNativeMsgProficiency> original, candidate;
        private readonly Row[] originalRows, beforeCandidate, afterCandidate;
        private readonly bool changed;
        private IEnumerator<KeyValuePair<uint, LegacyNativeMsgProficiency>>? originalBefore, originalFinal, candidateWitness;

        internal PreparedUnboundTraining(LegacyNativeProficiencyStorage owner,
            LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player,
            PlayerProficiencySnapshot expected, PlayerProficiencySnapshot proposed)
        {
            this.owner = owner; this.client = client; this.player = player; uid = player.UID;
            RequireOwner();
            original = owner.ClientProf; generation = owner.mutationGeneration; priorAnchor = owner.ordinaryMapAnchor;
            originalRows = CaptureRows(original);
            // First advancement is AFTER all original comparer/allocation work.
            originalBefore = original.GetEnumerator(); originalFinal = original.GetEnumerator();
            var indexed = originalRows.ToDictionary(row => row.Key);
            var expectedRows = FreezeRows(expected);
            var desired = FreezeRows(proposed);
            if (expectedRows.Length != indexed.Count) throw UnavailableProficiencies();
            foreach (var row in expectedRows)
                if (!indexed.TryGetValue(row.Id, out var actual) || actual.Value != ReadValues(row))
                    throw UnavailableProficiencies();
            foreach (var row in desired)
            {
                bool raw = indexed.TryGetValue(row.Id, out var prior);
                bool found = original.TryGetValue(row.Id, out var physical);
                if (raw != found || found && !ReferenceEquals(prior.Physical, physical))
                    throw UnavailableProficiencies();
            }
            bool sameMembership = desired.Length == indexed.Count && desired.All(row => indexed.ContainsKey(row.Id));
            candidate = sameMembership ? original : new(original.Comparer);
            var values = desired.ToDictionary(row => row.Id, ReadValues);
            if (!sameMembership)
                foreach (var row in desired)
                    if (!candidate.TryAdd(row.Id, indexed.TryGetValue(row.Id, out var prior) ? prior.Physical : CreateRow(row)))
                        throw UnavailableProficiencies();
            beforeCandidate = CaptureRows(candidate);
            afterCandidate = beforeCandidate.Select(row => row with { Value = values[row.Key] }).ToArray();
            changed = !sameMembership || afterCandidate.Where((row, index) => row.Value != beforeCandidate[index].Value).Any();
            nextAnchor = new(candidate, afterCandidate);
            candidateWitness = candidate.GetEnumerator();
            RequireOriginal();
        }

        internal bool ConsumeCurrentPlain()
        {
            try
            {
                RequireOwner();
                return ReferenceEquals(owner.ClientProf, original) && owner.mutationGeneration == generation &&
                    ReferenceEquals(owner.ordinaryMapAnchor, priorAnchor) &&
                    Consume(ref originalBefore, originalRows) && Consume(ref originalFinal, originalRows) &&
                    Consume(ref candidateWitness, beforeCandidate);
            }
            catch (InvalidOperationException) { return false; }
        }

        // No lookups, callbacks, allocation or refusal after the first store.
        internal void PublishPrepared()
        {
            foreach (var row in afterCandidate)
            {
                row.Physical.Level = row.Value.Level;
                row.Physical.Experience = row.Value.Experience;
                row.Physical.NeededExperience = row.Value.Needed;
                row.Physical.PreviouseLevel = row.Value.Previous;
            }
            owner.ClientProf = candidate;
            if (changed) Interlocked.Increment(ref owner.mutationGeneration);
            owner.PublishOrdinaryAnchor(nextAnchor);
        }

        internal bool HasCurrentCommittedGraph()
        {
            try
            {
                RequireOwner();
                if (!ReferenceEquals(owner.ClientProf, candidate) ||
                    owner.mutationGeneration != unchecked(generation + (changed ? 1 : 0)) ||
                    !ReferenceEquals(owner.ordinaryMapAnchor, nextAnchor)) return false;
                RequireRows(candidate, afterCandidate);
                return true;
            }
            catch (InvalidOperationException) { return false; }
        }

        private void RequireOriginal()
        {
            RequireOwner();
            if (!ReferenceEquals(owner.ClientProf, original) || owner.mutationGeneration != generation ||
                !ReferenceEquals(owner.ordinaryMapAnchor, priorAnchor)) throw UnavailableProficiencies();
            RequireRows(original, originalRows); RequireRows(candidate, beforeCandidate);
        }
        private void RequireOwner()
        {
            if (uid == 0 || client.OrdinarySpellBinding is not null || !ReferenceEquals(owner.Owner, client) ||
                !ReferenceEquals(client.Player, player) || !ReferenceEquals(player.Owner, client) || player.UID != uid ||
                !ReferenceEquals(client.MyProfs, owner) || !Monitor.IsEntered(client.SpellAdmissionSyncRoot) ||
                !Monitor.IsEntered(player.SpellAdmissionSyncRoot) || !Monitor.IsEntered(owner.mutationGate))
                throw UnavailableProficiencies();
        }
        private static bool Consume(ref IEnumerator<KeyValuePair<uint, LegacyNativeMsgProficiency>>? slot, Row[] rows)
        {
            var iterator = slot; slot = null;
            if (iterator is null) throw UnavailableProficiencies();
            try { return ConsumeRows(iterator, rows); }
            finally { iterator.Dispose(); }
        }
    }
}
