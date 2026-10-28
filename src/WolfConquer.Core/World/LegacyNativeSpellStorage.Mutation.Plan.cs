using WolfConquer.Core.Data;
using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSpellStorage
{
    private sealed partial class PreparedSpellMutation
    {
        private readonly LegacyNativeSpellStorage owner;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly uint uid;
        private readonly object? socket;
        private readonly bool fake;
        private readonly ConcurrentDictionary<ushort, LegacyNativeMsgSpell> original, candidate;
        private readonly SpellRow[] beforeRows, afterRows;
        private readonly Dictionary<ushort, LegacyNativeMsgSpell> indexed;
        private readonly SpellNotice[] notices;
        private readonly bool changed;
        private readonly long generation;
        private readonly PhysicalMapAnchor? originalAnchor;
        private readonly PhysicalMapAnchor candidateAnchor;
        private IEnumerator<KeyValuePair<ushort, LegacyNativeMsgSpell>>? beforeWriterWitness, finalWitness;
        private IEnumerator<KeyValuePair<ushort, LegacyNativeMsgSpell>>? candidateBeforeWitness, candidateFinalWitness;
        internal bool RemovedExisting { get; }
        internal PlayerSpellSnapshot FinalSnapshot { get; private set; } = PlayerSpellSnapshot.Empty;

        internal PreparedSpellMutation(LegacyNativeSpellStorage owner, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player, LegacyNativeSpellMutationIntent intent)
        {
            var (kind, id, row, retained) = PrepareIntent(intent);
            this.owner = owner; this.client = client; this.player = player;
            uid = player.UID; socket = client.Socket; fake = client.Fake;
            owner.RequireTypedMutationOwner(client, player, uid);
            owner.RequireOrdinaryAnchorCurrent();
            original = owner.ClientSpells;
            originalAnchor = owner.ordinaryMapAnchor;
            generation = owner.MutationGenerationPlain;
            beforeRows = CaptureNinjaSpellRows(original);
            indexed = beforeRows.ToDictionary(saved => saved.Key, saved => saved.Row);
            if (kind == SpellMutationKind.Clear)
                foreach (var saved in beforeRows) CheckOriginalLookup(saved.Key);
            else CheckOriginalLookup(id);
            RequireOriginal(); // Genuine comparer reads precede this plain fence.
            var next = new ConcurrentDictionary<ushort, LegacyNativeMsgSpell>(beforeRows.Select(saved =>
                new KeyValuePair<ushort, LegacyNativeMsgSpell>(saved.Key, saved.Row)), original.Comparer);
            var expected = indexed.ToDictionary(pair => pair.Key, pair => pair.Value);
            var output = new List<SpellNotice>();
            switch (kind)
            {
                case SpellMutationKind.Add:
                    var added = row ?? throw UnavailableNinjaSpells();
                    next[id] = added; expected[id] = added; output.Add(new(added, 0)); break;
                case SpellMutationKind.Remove:
                    if (expected.ContainsKey(id))
                    { RemoveExpectedSpell(next, expected, id); RemovedExisting = true; output.Add(new(null, id)); }
                    break;
                case SpellMutationKind.Reborn:
                    if (expected.ContainsKey(id))
                    {
                        RemoveExpectedSpell(next, expected, id); RemovedExisting = true;
                        PrepareReborn(next, expected, beforeRows.Single(saved => saved.Key == id), output);
                    }
                    break;
                case SpellMutationKind.Clear:
                    foreach (var saved in beforeRows)
                    {
                        RemoveExpectedSpell(next, expected, saved.Key);
                        if (retained!.Contains(saved.Key)) PrepareReborn(next, expected, saved, output);
                        else output.Add(new(null, saved.Key));
                    }
                    break;
            }
            RequireExpectedSpellMap(next, expected);
            changed = kind == SpellMutationKind.Add || RemovedExisting ||
                kind == SpellMutationKind.Clear && beforeRows.Length != 0;
            candidate = changed ? next : original;
            afterRows = changed ? CaptureNinjaSpellRows(candidate) : beforeRows;
            notices = output.ToArray();
            FinalSnapshot = SnapshotPhysicalRows(afterRows);
            candidateAnchor = new(candidate, afterRows);
            RequireCurrent();
        }

        private static (SpellMutationKind Kind, ushort Id, LegacyNativeMsgSpell? Row, HashSet<ushort>? Retained)
            PrepareIntent(LegacyNativeSpellMutationIntent intent) => intent switch
        {
            LegacyNativeSpellMutationIntent.Add add when add.Id != 0 && add.Experience >= 0 =>
                (SpellMutationKind.Add, add.Id, CreateTypedSpell(add.Id, add.Level, add.SoulLevel,
                    add.PreviousLevel, add.ClearExperience ? 0 : add.Experience), null),
            LegacyNativeSpellMutationIntent.Remove remove when remove.Id != 0 => (SpellMutationKind.Remove, remove.Id, null, null),
            LegacyNativeSpellMutationIntent.Reborn reborn when reborn.Id != 0 => (SpellMutationKind.Reborn, reborn.Id, null, null),
            LegacyNativeSpellMutationIntent.Clear clear => (SpellMutationKind.Clear, 0, null, clear.Retained.ToArray().ToHashSet()),
            _ => throw UnavailableNinjaSpells(),
        };

        private void CheckOriginalLookup(ushort id)
        {
            CheckSpellLookup(original, indexed, id);
        }

        private static void PrepareReborn(ConcurrentDictionary<ushort, LegacyNativeMsgSpell> next,
            Dictionary<ushort, LegacyNativeMsgSpell> expected, SpellRow source, List<SpellNotice> output)
        {
            if (!PermittedNinjaSkill(source.Key)) return;
            if (source.Values.Level > byte.MaxValue) throw UnavailableNinjaSpells();
            var row = CreateTypedSpell(source.Key, 0, source.Values.SoulLevel, (byte)source.Values.Level, 0);
            next[source.Key] = row;
            expected[source.Key] = row;
            output.Add(new(row, 0));
        }

        private void RequireOriginal()
        {
            owner.RequireTypedMutationOwner(client, player, uid);
            if (!ReferenceEquals(owner.ClientSpells, original)) throw UnavailableNinjaSpells();
            owner.RequireOrdinaryAnchorCurrent();
            RequireNinjaSpellRows(original, beforeRows);
        }

        internal void RequireCurrent()
        {
            RequireOriginal();
            if (changed) RequireNinjaSpellRows(candidate, afterRows);
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
                owner.RequireTypedMutationOwner(client, player, uid);
                if (!ReferenceEquals(owner.ClientSpells, original) || owner.MutationGenerationPlain != generation ||
                    !ReferenceEquals(owner.ordinaryMapAnchor, originalAnchor))
                    return false;
                return ConsumeFinalWitness(ref beforeWriterWitness, beforeRows) &&
                    ConsumeFinalWitness(ref candidateBeforeWitness, afterRows);
            }
            catch (InvalidOperationException) { return false; }
        }

        internal bool ConsumeCurrentPlain()
        {
            try
            {
                owner.RequireTypedMutationOwner(client, player, uid);
                if (!ReferenceEquals(owner.ClientSpells, original) || owner.MutationGenerationPlain != generation ||
                    !ReferenceEquals(owner.ordinaryMapAnchor, originalAnchor))
                    return false;
                return ConsumeFinalWitness(ref finalWitness, beforeRows) &&
                    ConsumeFinalWitness(ref candidateFinalWitness, afterRows);
            }
            catch (InvalidOperationException) { return false; }
        }

        internal void PublishMap()
        {
            if (changed) owner.PublishPreparedMapPlain(candidate);
            owner.PublishOrdinaryAnchor(candidateAnchor);
        }
    }
}
