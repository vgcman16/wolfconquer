using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeServerDatabasePersistence
{
    private sealed partial class SpellFileMetadata
    {
        private sealed class LevelWitness
        {
            internal readonly Dictionary<ushort, LegacyNativeMagicType.Magic> Source;
            internal readonly Dictionary<ushort, LegacyNativeMagicType.Magic?> Rows = new();
            internal LevelWitness(Dictionary<ushort, LegacyNativeMagicType.Magic> source)
            {
                Source = source;
                foreach (var pair in source) if (!Rows.TryAdd(pair.Key, pair.Value)) throw InvalidSpellMetadata();
            }
        }
        private readonly record struct ItemWitness(uint Key, LegacyNativeItemRuneMetadata Row, uint Id, uint Rune, uint Class);

        internal void RequireCurrent()
        {
            if (!ReferenceEquals(client.Player, player) || player.Class != currentClass ||
                !ReferenceEquals(LegacyNativePoolMagic.Magic, magic) || magic.Count != roots.Count) throw InvalidSpellMetadata();
            foreach (var pair in magic)
                if (!roots.TryGetValue(pair.Key, out var expected) || !ReferenceEquals(pair.Value, expected)) throw InvalidSpellMetadata();
            foreach (var witness in levels.Values)
            {
                if (witness.Source.Count != witness.Rows.Count) throw InvalidSpellMetadata();
                foreach (var pair in witness.Source)
                    if (!witness.Rows.TryGetValue(pair.Key, out var expected) || !ReferenceEquals(pair.Value, expected)) throw InvalidSpellMetadata();
            }
            foreach (var flag in flags) if (flag.Row.isRuneSkill != flag.Rune) throw InvalidSpellMetadata();
            if (items is null) return;
            if (!ReferenceEquals(LegacyNativePoolItems.ItemsBase, items) || items.Count != itemRows!.Length) throw InvalidSpellMetadata();
            int ordinal = 0;
            foreach (var pair in items)
            {
                if (ordinal >= itemRows.Length || pair.Value is not { } row) throw InvalidSpellMetadata();
                var before = itemRows[ordinal++];
                if (pair.Key != before.Key || !ReferenceEquals(row, before.Row) || row.ID != before.Id ||
                    row.RuneSpellID != before.Rune || row.Class != before.Class) throw InvalidSpellMetadata();
            }
            if (ordinal != itemRows.Length) throw InvalidSpellMetadata();
        }
    }
}
