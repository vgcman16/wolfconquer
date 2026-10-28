using System.Collections.Concurrent;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeProficiencyStorage
{
    internal sealed class PreparedSessionProficiencyMap
    {
        private readonly LegacyNativeProficiencyStorage owner;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly uint uid;
        private readonly LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner? admissionOwner;
        private readonly bool detached, changed;
        private readonly long generation;
        private readonly PhysicalMapAnchor? originalAnchor;
        private readonly PhysicalMapAnchor candidateAnchor;
        private readonly ConcurrentDictionary<uint, LegacyNativeMsgProficiency> original, candidate;
        private readonly Row[] originalRows, candidateRows;
        private IEnumerator<KeyValuePair<uint, LegacyNativeMsgProficiency>>? beforeWitness, finalWitness, publishedWitness;
        private IEnumerator<KeyValuePair<uint, LegacyNativeMsgProficiency>>? candidateBeforeWitness, candidateFinalWitness;
        internal PlayerProficiencySnapshot FinalSnapshot { get; }

        internal PreparedSessionProficiencyMap(LegacyNativeProficiencyStorage owner, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player, PlayerProficiencySnapshot expected, PlayerProficiencySnapshot proposed, bool detached,
            LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner? admissionOwner = null)
        {
            this.owner = owner; this.client = client; this.player = player; this.detached = detached;
            this.admissionOwner = admissionOwner; uid = admissionOwner?.PreparedUid ?? player.UID;
            if (!detached && admissionOwner is not null) throw UnavailableProficiencies();
            RequireOwner(published: false);
            var expectedRows = FreezeRows(expected);
            var frozen = FreezeSessionSnapshot(proposed);
            var nextRows = FreezeRows(frozen);
            owner.RequireOrdinaryAnchorCurrent();
            original = owner.ClientProf; generation = owner.mutationGeneration;
            originalAnchor = owner.ordinaryMapAnchor;
            originalRows = CaptureRows(original);
            var indexed = originalRows.ToDictionary(row => row.Key);
            if (expectedRows.Length != indexed.Count) throw UnavailableProficiencies();
            foreach (var row in expectedRows)
                if (!indexed.TryGetValue(row.Id, out var prior) || ReadValues(row) != prior.Value)
                    throw UnavailableProficiencies();
            foreach (uint id in expectedRows.Select(row => row.Id).Concat(nextRows.Select(row => row.Id)).Distinct())
            {
                bool raw = indexed.TryGetValue(id, out var prior), present = original.TryGetValue(id, out var current);
                if (raw != present || present && !ReferenceEquals(prior.Physical, current)) throw UnavailableProficiencies();
            }
            RequireOriginal();
            var next = new ConcurrentDictionary<uint, LegacyNativeMsgProficiency>(original.Comparer);
            bool replacement = nextRows.Length != originalRows.Length;
            foreach (var row in nextRows)
            {
                bool retained = indexed.TryGetValue(row.Id, out var prior) && ReadValues(row) == prior.Value;
                var physical = retained ? prior.Physical : CreateRow(row);
                replacement |= !retained;
                if (!next.TryAdd(row.Id, physical)) throw UnavailableProficiencies();
            }
            var captured = CaptureRows(next);
            if (captured.Length != nextRows.Length) throw UnavailableProficiencies();
            var wanted = nextRows.ToDictionary(row => row.Id, row => ReadValues(row));
            foreach (var row in captured)
                if (!wanted.TryGetValue(row.Key, out var value) || value != row.Value) throw UnavailableProficiencies();
            changed = replacement; candidate = changed ? next : original;
            candidateRows = changed ? captured : originalRows;
            candidateAnchor = new(candidate, candidateRows);
            FinalSnapshot = !changed ? FreezeSessionSnapshot(expected) : frozen;
            RequireOriginal();
        }

        internal void RequireCurrentBeforeWriter() => RequireOriginal();
        internal void ArmFinalProof()
        {
            if (beforeWitness is not null || finalWitness is not null || publishedWitness is not null)
                throw UnavailableProficiencies();
            RequireOriginal();
            beforeWitness = original.GetEnumerator(); finalWitness = original.GetEnumerator();
            publishedWitness = candidate.GetEnumerator();
            candidateBeforeWitness = candidate.GetEnumerator(); candidateFinalWitness = candidate.GetEnumerator();
        }
        internal bool ConsumeBeforeWriterPlain() => ConsumeOriginal(ref beforeWitness) &&
            Consume(ref candidateBeforeWitness, candidateRows);
        internal bool ConsumeCurrentPlain() => ConsumeOriginal(ref finalWitness) &&
            Consume(ref candidateFinalWitness, candidateRows);
        private bool ConsumeOriginal(ref IEnumerator<KeyValuePair<uint, LegacyNativeMsgProficiency>>? iterator)
        {
            try
            {
                RequireOwner(published: false);
                return ReferenceEquals(owner.ClientProf, original) && owner.mutationGeneration == generation &&
                    ReferenceEquals(owner.ordinaryMapAnchor, originalAnchor) &&
                    Consume(ref iterator, originalRows);
            }
            catch (InvalidOperationException) { return false; }
        }
        private static bool Consume(ref IEnumerator<KeyValuePair<uint, LegacyNativeMsgProficiency>>? value, Row[] rows)
        {
            var iterator = value; value = null;
            if (iterator is null) throw UnavailableProficiencies();
            try { return ConsumeRows(iterator, rows); }
            finally { iterator.Dispose(); }
        }
        internal void PublishPreparedMap()
        {
            if (changed)
            {
                owner.ClientProf = candidate;
                Interlocked.Increment(ref owner.mutationGeneration);
            }
            owner.PublishOrdinaryAnchor(candidateAnchor);
        }
        internal bool MatchesPublishedPlain()
        {
            try
            {
                RequireOwner(published: true);
                return ReferenceEquals(owner.ClientProf, candidate) &&
                    ReferenceEquals(owner.ordinaryMapAnchor, candidateAnchor) &&
                    owner.mutationGeneration == unchecked(generation + (changed ? 1 : 0)) && Consume(ref publishedWitness, candidateRows);
            }
            catch (InvalidOperationException) { return false; }
        }
        internal bool HasCurrentCommittedGraph()
        {
            try
            {
                RequireOwner(published: true);
                if (!ReferenceEquals(owner.ClientProf, candidate) ||
                    owner.mutationGeneration != unchecked(generation + (changed ? 1 : 0))) return false;
                RequireRows(candidate, candidateRows);
                return true;
            }
            catch (InvalidOperationException) { return false; }
        }
        private void RequireOriginal()
        {
            RequireOwner(published: false);
            if (!ReferenceEquals(owner.ClientProf, original) || owner.mutationGeneration != generation)
                throw UnavailableProficiencies();
            if (!ReferenceEquals(owner.ordinaryMapAnchor, originalAnchor)) throw UnavailableProficiencies();
            owner.RequireOrdinaryAnchorCurrent();
            RequireRows(original, originalRows);
            if (changed) RequireRows(candidate, candidateRows);
        }
        private void RequireOwner(bool published)
        {
            if (uid == 0 || !ReferenceEquals(owner.Owner, client) || !ReferenceEquals(client.Player, player) ||
                !ReferenceEquals(player.Owner, client) ||
                (admissionOwner is null ? player.UID != uid : published
                    ? !admissionOwner.PublishedOwnerIsCurrentPlain(client, player) : !admissionOwner.BeforeOwnerIsCurrentPlain(client, player)) ||
                !Monitor.IsEntered(client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(player.SpellAdmissionSyncRoot) ||
                !Monitor.IsEntered(owner.mutationGate) ||
                (detached ? published ? !ReferenceEquals(client.MyProfs, owner) : client.MyProfs is not null
                          : !ReferenceEquals(client.MyProfs, owner))) throw UnavailableProficiencies();
        }
    }
}
