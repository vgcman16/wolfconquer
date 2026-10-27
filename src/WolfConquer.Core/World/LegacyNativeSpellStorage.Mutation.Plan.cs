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
        internal bool RemovedExisting { get; }

        internal PreparedSpellMutation(LegacyNativeSpellStorage owner, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player, SpellMutationKind kind, ushort id, LegacyNativeMsgSpell? row,
            HashSet<ushort>? retained)
        {
            this.owner = owner; this.client = client; this.player = player;
            uid = player.UID; socket = client.Socket; fake = client.Fake;
            owner.RequireTypedMutationOwner(client, player, uid);
            original = owner.ClientSpells;
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
            RequireCurrent();
        }

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
            RequireNinjaSpellRows(original, beforeRows);
        }

        internal void RequireCurrent()
        {
            RequireOriginal();
            if (changed) RequireNinjaSpellRows(candidate, afterRows);
        }

        internal void PublishMap() { if (changed) owner.ClientSpells = candidate; }
    }
}
