using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeServerDatabasePersistence
{
    private sealed partial class SpellFileMetadata
    {
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly uint currentClass;
        private readonly LegacyNativeMagicType magic;
        private readonly Dictionary<ushort, Dictionary<ushort, LegacyNativeMagicType.Magic>?> roots = new();
        private readonly Dictionary<ushort, LevelWitness> levels = new();
        private readonly Dictionary<(ushort Id, ushort Level), bool> permissions = new();
        private readonly List<(LegacyNativeMagicType.Magic Row, bool Rune)> flags = new();
        private LegacyNativeItemTypeRuneMetadata? items;
        private ItemWitness[]? itemRows;

        internal SpellFileMetadata(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player)
        {
            this.client = client; this.player = player; currentClass = player.Class;
            magic = LegacyNativePoolMagic.Magic ?? throw InvalidSpellMetadata();
            foreach (var pair in magic)
                if (!roots.TryAdd(pair.Key, pair.Value)) throw InvalidSpellMetadata();
        }

        internal LegacyNativeMsgSpell[] Filter(LegacyNativeMsgSpell[] records)
        {
            var admitted = new List<LegacyNativeMsgSpell>();
            foreach (var row in records)
            {
                var key = (row.ID, row.Level);
                if (!permissions.TryGetValue(key, out var allowed))
                { allowed = Allows(row); permissions.Add(key, allowed); }
                if (allowed) admitted.Add(row);
            }
            return admitted.ToArray();
        }

        private bool Allows(LegacyNativeMsgSpell spell)
        {
            bool found = magic.TryGetValue(spell.ID, out var definition);
            if (!roots.TryGetValue(spell.ID, out var captured))
            {
                if (found) throw InvalidSpellMetadata();
                return true; // Opaque saved membership, not casting permission.
            }
            if (!found || definition is null || !ReferenceEquals(definition, captured)) throw InvalidSpellMetadata();
            if (!levels.TryGetValue(spell.ID, out var witness))
            { witness = new(definition); levels.Add(spell.ID, witness); }
            bool levelFound = definition.TryGetValue(spell.Level, out var row);
            if (!levelFound || row is null || !witness.Rows.TryGetValue(spell.Level, out var expected) || !ReferenceEquals(row, expected))
                throw InvalidSpellMetadata();
            flags.Add((row, row.isRuneSkill));
            return !row.isRuneSkill || AllowsRune(spell);
        }

        private bool AllowsRune(LegacyNativeMsgSpell spell)
        {
            if (items is null)
            {
                items = LegacyNativePoolItems.ItemsBase ?? throw InvalidSpellMetadata();
                var captured = new List<ItemWitness>();
                foreach (var pair in items)
                {
                    var row = pair.Value ?? throw InvalidSpellMetadata();
                    captured.Add(new(pair.Key, row, row.ID, row.RuneSpellID, row.Class));
                }
                itemRows = captured.ToArray();
            }
            uint rune = (uint)spell.ID * 100 + spell.Level;
            ItemWitness? matched = null;
            foreach (var row in itemRows!) if (row.Rune == rune) { matched = row; break; }
            if (matched is not { } selected || selected.Key != selected.Id ||
                !items.TryGetValue(selected.Id, out var permission) || !ReferenceEquals(permission, selected.Row) ||
                permission.ID != selected.Id || permission.RuneSpellID != rune) throw InvalidSpellMetadata();
            return LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(permission, client);
        }

        private static InvalidOperationException InvalidSpellMetadata() =>
            new("Known spell/rune metadata is missing, conflicting or changed.");
    }
}
