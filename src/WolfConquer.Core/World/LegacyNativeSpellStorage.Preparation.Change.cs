using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSpellStorage
{
    internal sealed class PreparedNinjaSpells
    {
        private readonly LegacyNativeSpellStorage owner;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly uint uid;
        private readonly ConcurrentDictionary<ushort, LegacyNativeMsgSpell> original;
        private readonly ConcurrentDictionary<ushort, LegacyNativeMsgSpell> candidate;
        private readonly SpellRow[] originalRows, candidateRows;
        private readonly bool changed;
        private readonly LegacyNativeMsgSpell[] added;
        private readonly ushort[] removed;

        internal IReadOnlyList<LegacyNativeMsgSpell> Added => added;
        internal IReadOnlyList<ushort> Removed => removed;

        internal PreparedNinjaSpells(LegacyNativeSpellStorage owner, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player, uint uid, NinjaSkillChoice[] choices, ushort[] managedSkills)
        {
            this.owner = owner; this.client = client; this.player = player; this.uid = uid;
            original = owner.ClientSpells;
            originalRows = CaptureNinjaSpellRows(original);
            var indexed = originalRows.ToDictionary(row => row.Key, row => row.Row);
            foreach (var id in choices.Select(choice => choice.Id).Concat(managedSkills).Distinct())
                CheckSpellLookup(original, indexed, id);
            RequireOriginal(); // Candidate callouts below are followed by the final fence.
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
                indexed.Add(row.ID, row);
                additions.Add(row);
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
            RequireCurrent();
        }

        internal void RequireCurrent()
        {
            RequireOriginal();
            if (changed) RequireNinjaSpellRows(candidate, candidateRows);
        }

        private void RequireOriginal()
        {
            owner.RequireNinjaSpellOwner(client, player, uid);
            if (!ReferenceEquals(owner.ClientSpells, original)) throw UnavailableNinjaSpells();
            RequireNinjaSpellRows(original, originalRows);
        }

        // All current-owner/graph checks precede the first related state store.
        internal void PublishPreparedMap()
        {
            if (changed) owner.ClientSpells = candidate;
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
