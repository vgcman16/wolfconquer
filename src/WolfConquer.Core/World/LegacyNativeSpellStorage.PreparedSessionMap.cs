using System.Collections.Concurrent;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSpellStorage
{
    internal sealed class PreparedSessionSpellMap
    {
        private readonly LegacyNativeSpellStorage owner;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly uint uid;
        private readonly LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner? admissionOwner;
        private readonly long generation;
        private readonly PhysicalMapAnchor? originalAnchor;
        private readonly PhysicalMapAnchor candidateAnchor;
        private readonly bool detached;
        private readonly ConcurrentDictionary<ushort, LegacyNativeMsgSpell> original, candidate;
        private SpellRow[] originalRows, candidateRows;
        private readonly bool changed;
        private IEnumerator<KeyValuePair<ushort, LegacyNativeMsgSpell>>? beforeWriterWitness;
        private IEnumerator<KeyValuePair<ushort, LegacyNativeMsgSpell>>? finalWitness;
        private IEnumerator<KeyValuePair<ushort, LegacyNativeMsgSpell>>? publishedWitness;
        private IEnumerator<KeyValuePair<ushort, LegacyNativeMsgSpell>>? candidateBeforeWitness, candidateFinalWitness;
        internal PlayerSpellSnapshot FinalSnapshot { get; }

        internal PreparedSessionSpellMap(LegacyNativeSpellStorage owner, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player, PlayerSpellSnapshot expected, PlayerSpellSnapshot proposed, bool detached,
            LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner? admissionOwner = null)
        {
            this.owner = owner; this.client = client; this.player = player; this.detached = detached;
            this.admissionOwner = admissionOwner; uid = admissionOwner?.PreparedUid ?? player.UID;
            if (!detached && admissionOwner is not null) throw UnavailableNinjaSpells();
            RequireOwnerPlain(published: false);
            var expectedRows = FreezeSnapshotRows(expected);
            FinalSnapshot = FreezeSessionSnapshot(proposed);
            var proposedRows = FreezeSnapshotRows(FinalSnapshot);
            owner.RequireOrdinaryAnchorCurrent();
            original = owner.ClientSpells;
            originalAnchor = owner.ordinaryMapAnchor;
            generation = owner.MutationGenerationPlain;
            originalRows = CaptureNinjaSpellRows(original);
            var physical = originalRows.ToDictionary(row => row.Key, row => row);
            var physicalReferences = physical.ToDictionary(pair => pair.Key, pair => pair.Value.Row);
            foreach (var id in expectedRows.Select(row => row.Id).Concat(proposedRows.Select(row => row.Id)).Distinct())
                CheckSpellLookup(original, physicalReferences, id);
            RequireSnapshotMatchesPhysical(expectedRows, physical);
            RequireOriginalPlain();

            var next = new ConcurrentDictionary<ushort, LegacyNativeMsgSpell>(original.Comparer);
            var inserted = new Dictionary<ushort, LegacyNativeMsgSpell>();
            foreach (var proposedRow in proposedRows)
            {
                LegacyNativeMsgSpell row;
                if (physical.TryGetValue(proposedRow.Id, out var saved) && PersistedValuesEqual(saved.Values, proposedRow))
                    row = saved.Row;
                else if (physical.TryGetValue(proposedRow.Id, out saved)) row = CopyPersisted(saved.Values, proposedRow);
                else row = CreatePersisted(proposedRow);
                if (!next.TryAdd(proposedRow.Id, row) || !inserted.TryAdd(proposedRow.Id, row))
                    throw UnavailableNinjaSpells();
            }
            RequireExpectedSpellMap(next, inserted);
            candidateRows = CaptureNinjaSpellRows(next);
            changed = !SamePhysicalRows(originalRows, candidateRows);
            candidate = changed ? next : original;
            if (!changed) candidateRows = originalRows;
            candidateAnchor = new(candidate, candidateRows);
            RequireOriginalPlain();
        }

        internal void RequireCurrentBeforeWriter() => RequireOriginalPlain();

        internal void ArmFinalProof()
        {
            if (beforeWriterWitness is not null || finalWitness is not null || publishedWitness is not null)
                throw UnavailableNinjaSpells();
            RequireOriginalPlain();
            beforeWriterWitness = original.GetEnumerator();
            finalWitness = original.GetEnumerator();
            publishedWitness = candidate.GetEnumerator();
            candidateBeforeWitness = candidate.GetEnumerator();
            candidateFinalWitness = candidate.GetEnumerator();
        }

        internal bool ConsumeBeforeWriterPlain()
        {
            try
            {
                RequireOwnerPlain(published: false);
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
                RequireOwnerPlain(published: false);
                if (!ReferenceEquals(owner.ClientSpells, original) || owner.MutationGenerationPlain != generation ||
                    !ReferenceEquals(owner.ordinaryMapAnchor, originalAnchor))
                    return false;
                return ConsumeFinalWitness(ref finalWitness, originalRows) &&
                    ConsumeFinalWitness(ref candidateFinalWitness, candidateRows);
            }
            catch (InvalidOperationException) { return false; }
        }

        internal void PublishPreparedMap()
        {
            if (changed) owner.PublishPreparedMapPlain(candidate);
            owner.PublishOrdinaryAnchor(candidateAnchor);
        }

        internal bool MatchesPublishedPlain()
        {
            try
            {
                RequireOwnerPlain(published: true);
                if (!ReferenceEquals(owner.ClientSpells, candidate) ||
                    !ReferenceEquals(owner.ordinaryMapAnchor, candidateAnchor) ||
                    owner.MutationGenerationPlain != unchecked(generation + (changed ? 1 : 0))) return false;
                return ConsumeFinalWitness(ref publishedWitness, candidateRows);
            }
            catch (InvalidOperationException) { return false; }
        }

        internal bool HasCurrentCommittedGraph()
        {
            try
            {
                RequireOwnerPlain(published: true);
                if (!ReferenceEquals(owner.ClientSpells, candidate) ||
                    owner.MutationGenerationPlain != unchecked(generation + (changed ? 1 : 0))) return false;
                RequireNinjaSpellRows(candidate, candidateRows);
                return true;
            }
            catch (InvalidOperationException) { return false; }
        }

        private void RequireOriginalPlain()
        {
            RequireOwnerPlain(published: false);
            if (!ReferenceEquals(owner.ClientSpells, original) || owner.MutationGenerationPlain != generation ||
                    !ReferenceEquals(owner.ordinaryMapAnchor, originalAnchor))
                throw UnavailableNinjaSpells();
            owner.RequireOrdinaryAnchorCurrent();
            RequireNinjaSpellRows(original, originalRows);
            if (changed) RequireNinjaSpellRows(candidate, candidateRows);
        }

        private void RequireOwnerPlain(bool published)
        {
            if (uid == 0 || !ReferenceEquals(owner.Owner, client) || !ReferenceEquals(client.Player, player) ||
                !ReferenceEquals(player.Owner, client) ||
                (admissionOwner is null ? player.UID != uid : published
                    ? !admissionOwner.PublishedOwnerIsCurrentPlain(client, player) : !admissionOwner.BeforeOwnerIsCurrentPlain(client, player)) ||
                !Monitor.IsEntered(client.SpellAdmissionSyncRoot) ||
                !Monitor.IsEntered(player.SpellAdmissionSyncRoot) || !Monitor.IsEntered(owner.mutationGate))
                throw UnavailableNinjaSpells();
            if (detached)
            {
                if (published ? !ReferenceEquals(client.MySpells, owner) : client.MySpells is not null)
                    throw UnavailableNinjaSpells();
            }
            else if (!ReferenceEquals(client.MySpells, owner)) throw UnavailableNinjaSpells();
        }

        private static void RequireSnapshotMatchesPhysical(PlayerSpellSnapshotEntry[] expected,
            Dictionary<ushort, SpellRow> physical)
        {
            if (expected.Length != physical.Count) throw UnavailableNinjaSpells();
            foreach (var row in expected)
                if (!physical.TryGetValue(row.Id, out var saved) || !PersistedValuesEqual(saved.Values, row))
                    throw UnavailableNinjaSpells();
        }

        private static bool PersistedValuesEqual(SpellValues value, PlayerSpellSnapshotEntry row) =>
            value.Id == row.Id && value.Level == row.Level && value.Experience == row.Experience &&
            value.PreviousLevel == row.PreviousLevel && value.SoulLevel == row.SoulLevel &&
            SelectedSoul(value.UseSoul) == row.UseSpellSoul;

        internal static byte SelectedSoul(LegacyNativeMsgSpell.UseSpellSoulTyp value) => value switch
        {
            LegacyNativeMsgSpell.UseSpellSoulTyp.None => 0,
            LegacyNativeMsgSpell.UseSpellSoulTyp.One => 1,
            LegacyNativeMsgSpell.UseSpellSoulTyp.Two => 2,
            LegacyNativeMsgSpell.UseSpellSoulTyp.Three => 3,
            LegacyNativeMsgSpell.UseSpellSoulTyp.Four => 4,
            _ => throw UnavailableNinjaSpells(),
        };

        private static LegacyNativeMsgSpell CopyPersisted(SpellValues value, PlayerSpellSnapshotEntry persisted)
        {
            var row = new LegacyNativeMsgSpell
            {
                ColdTime = value.ColdTime, IsSpellWithColdTime = value.WithColdTime, Size = value.Size,
                PacketType = value.PacketType, PacketStamp = value.PacketStamp, Experience = persisted.Experience,
                ID = persisted.Id, UnKnow = value.Unknown, Level = persisted.Level, Type = value.Type,
                UseSoul = value.UseSoul, Soul = value.Soul, HashLevelSoul = value.HashLevelSoul,
                StampSpell = value.Stamp, PreviousLevel = persisted.PreviousLevel,
            };
            row.SoulLevel = persisted.SoulLevel; // Preserve prior hash, then apply the genuine setter's OR bits.
            if (persisted.SoulLevel == value.SoulLevel) row.HashLevelSoul = value.HashLevelSoul;
            row.UseSpellSoul = persisted.UseSpellSoul;
            if (row.UseSpellSoul != persisted.UseSpellSoul) throw UnavailableNinjaSpells();
            return row;
        }

        private static LegacyNativeMsgSpell CreatePersisted(PlayerSpellSnapshotEntry persisted)
        {
            var row = new LegacyNativeMsgSpell { ID = persisted.Id, Level = persisted.Level,
                Experience = persisted.Experience, PreviousLevel = persisted.PreviousLevel };
            row.SoulLevel = persisted.SoulLevel;
            row.UseSpellSoul = persisted.UseSpellSoul;
            if (row.UseSpellSoul != persisted.UseSpellSoul) throw UnavailableNinjaSpells();
            return row;
        }

        private static bool SamePhysicalRows(SpellRow[] left, SpellRow[] right)
        {
            if (left.Length != right.Length) return false;
            var indexed = left.ToDictionary(row => row.Key, row => row.Row);
            foreach (var row in right)
                if (!indexed.TryGetValue(row.Key, out var value) || !ReferenceEquals(value, row.Row)) return false;
            return true;
        }
    }
}
