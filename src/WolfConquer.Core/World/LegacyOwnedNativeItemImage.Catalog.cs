using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOwnedNativeItemImage
{
    internal static CatalogEvidence ValidateItemIds(WorldDataCatalog? catalog, IEnumerable<uint> itemIds,
        ConquerProtocolProfile profile) => new(catalog, itemIds, profile);

    internal sealed class CatalogEvidence
    {
        private sealed class Row
        {
            internal readonly uint Id, Spell, Class;
            internal readonly ItemType Item;
            internal readonly PlainList<ItemType> Group;
            private readonly IReadOnlyList<string> fields;
            private readonly string[]? array;
            private readonly List<string>? list;
            private readonly int count;
            private readonly string classText, spellText;
            internal Row(uint id, IReadOnlyList<ItemType> group)
            {
                Id = id; Group = new(group);
                if (Group.Values.Length != 1 || Group.Values[0].Id != (int)id) throw Invalid("ambiguous/mismatched Rune catalog key");
                Item = Group.Values[0]; fields = Item.Fields ?? throw Invalid("missing Rune fields");
                array = fields as string[]; list = fields as List<string>;
                if (array is null && list is null) throw Invalid("Rune fields have no trusted plain backing");
                count = fields.Count;
                if (count <= ItemType.RuneSpellIdFieldIndex) throw Invalid("missing required Rune columns");
                classText = fields[ItemType.RequiredClassFieldIndex]; spellText = fields[ItemType.RuneSpellIdFieldIndex];
                if (!uint.TryParse(classText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var job) ||
                    !uint.TryParse(spellText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var spell))
                    throw Invalid("malformed required Rune columns");
                Class = job; Spell = spell;
            }
            internal void RequireCurrent()
            {
                Group.RequireCurrent();
                if (fields.Count != count || !string.Equals(fields[2], classText, StringComparison.Ordinal) ||
                    !string.Equals(fields[25], spellText, StringComparison.Ordinal)) throw Invalid("Rune catalog field changed");
            }
            internal bool IsCurrentPlain()
            {
                if (Item.Id != (int)Id || !ReferenceEquals(Item.Fields, fields) || !Group.IsCurrentPlain()) return false;
                int length = array is not null ? array.Length : list!.Count;
                return length == count && string.Equals(array is not null ? array[2] : list![2], classText, StringComparison.Ordinal) &&
                    string.Equals(array is not null ? array[25] : list![25], spellText, StringComparison.Ordinal);
            }
        }
        internal WorldDataCatalog? Catalog { get; }
        internal ConquerProtocolProfile Profile { get; }
        private readonly Dictionary<uint, Row> rows = new();
        private readonly PlainList<ItemType>? raw;
        private readonly Dictionary<int, IReadOnlyList<ItemType>>? index;
        internal CatalogEvidence(WorldDataCatalog? catalog, IEnumerable<uint> ids, ConquerProtocolProfile profile)
        {
            ArgumentNullException.ThrowIfNull(ids);
            if (profile is not (ConquerProtocolProfile.RootD11_7899 or ConquerProtocolProfile.VisibleDx8D13))
                throw new ArgumentOutOfRangeException(nameof(profile));
            Catalog = catalog; Profile = profile;
            var needed = new HashSet<uint>();
            foreach (uint id in ids)
            {
                if (id == 0) throw Invalid("zero package/item ID");
                if (LegacyNativeItemTypeRuneMetadata.IsRune(id)) needed.Add(id);
            }
            if (needed.Count != 0)
            {
                if (catalog is null) throw Invalid("required Rune catalog missing");
                raw = new(catalog.Items);
                index = catalog.ItemsById as Dictionary<int, IReadOnlyList<ItemType>>
                    ?? throw Invalid("catalog index has no actual plain dictionary");
                foreach (uint id in needed)
                {
                    if (!index.TryGetValue((int)id, out var group) || group is null) throw Invalid("missing required Rune catalog row");
                    var row = new Row(id, group);
                    int matches = 0;
                    foreach (var item in raw.Values) if (item.Id == (int)id)
                    { if (!ReferenceEquals(item, row.Item)) throw Invalid("raw/index Rune catalog disagreement"); matches++; }
                    if (matches != 1) throw Invalid("missing/duplicate raw Rune row");
                    rows.Add(id, row);
                }
            }
            RequireCurrent();
        }
        internal bool HasRequirements(IEnumerable<uint> ids)
        {
            var required = new HashSet<uint>();
            foreach (uint id in ids) if (LegacyNativeItemTypeRuneMetadata.IsRune(id)) required.Add(id);
            if (required.Count != rows.Count) return false;
            foreach (uint id in required) if (!rows.ContainsKey(id)) return false;
            return true;
        }
        internal uint RuneSpell(uint id) => rows.TryGetValue(id, out var row) ? row.Spell :
            throw Invalid("scoped constructor has no real Rune evidence");
        private uint RuneClass(uint id) => rows.TryGetValue(id, out var row) ? row.Class : 0;
        internal bool SameConstructor(uint id, CatalogEvidence other) =>
            Profile == other.Profile && (!LegacyNativeItemTypeRuneMetadata.IsRune(id) ||
                RuneSpell(id) == other.RuneSpell(id) && RuneClass(id) == other.RuneClass(id));
        internal void RequireCurrent()
        {
            if (raw is not null)
            {
                raw.RequireCurrent();
                foreach (var row in rows.Values)
                {
                    if (!index!.TryGetValue((int)row.Id, out var group) || !ReferenceEquals(group, row.Group.Source))
                        throw Invalid("Rune catalog index changed");
                    row.RequireCurrent();
                }
            }
            // No application callbacks follow this complete pure pass.
            if (!IsCurrentPlain()) throw Invalid("scoped catalog evidence changed");
        }
        internal bool IsCurrentPlain()
        {
            if (raw is null) return true; // Actual null catalog is valid with no Rune requirements.
            var catalog = Catalog;
            if (catalog is null || !ReferenceEquals(catalog.Items, raw.Source) ||
                !ReferenceEquals(catalog.ItemsById, index) || !raw.IsCurrentPlain()) return false;
            int found = 0;
            foreach (var entry in index!)
                if (entry.Key > 0 && rows.TryGetValue((uint)entry.Key, out var expected))
                { if (!ReferenceEquals(entry.Value, expected.Group.Source)) return false; found++; }
            if (found != rows.Count) return false;
            foreach (var row in rows.Values) if (!row.IsCurrentPlain()) return false;
            return true;
        }
    }
}
