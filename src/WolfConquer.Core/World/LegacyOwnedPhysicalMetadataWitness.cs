using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Live dependency data only. A virtual query source is permitted here; the
// registered receive/death plan separately owns the actual current holders.
internal sealed partial class LegacyOwnedPhysicalMetadataWitness
{
    private readonly LegacyOrdinarySpellBinding binding;
    private readonly WorldDataCatalog? catalog;
    private readonly bool preserveRawAttributes;
    private readonly Dictionary<object, ICurrent> collections = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, Row> rows = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> capturedRaw = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, int> rawCounts = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, Dictionary<object, object[]>> rawIndices = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> actualIndices = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> actualRaw = new(ReferenceEqualityComparer.Instance);
    private readonly List<Input> inputs = [];
    private readonly Dictionary<MagicType, FieldAt> magicLevels = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(ushort Id, ushort Level), MagicType> derivedSpells = [];
    private readonly Dictionary<MagicType, NativeMagic> nativeMagic = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ushort, SpellClassification> spellClassifications = [];
    private readonly List<(ushort Id, ushort Level)> derivedOrder = [];
    private bool nativeQueryRequired;

    private LegacyOwnedPhysicalMetadataWitness(LegacyOrdinarySpellBinding binding, WorldDataCatalog? catalog, bool preserveRawAttributes = false)
    {
        this.binding = binding;
        this.catalog = catalog;
        this.preserveRawAttributes = preserveRawAttributes;
    }

    internal static LegacyOwnedPhysicalMetadataWitness Capture(LegacyOrdinarySpellBinding binding,
        WorldDataCatalog? catalog, WorldSession source)
    {
        var result = new LegacyOwnedPhysicalMetadataWitness(binding, catalog);
        result.Extend(source);
        return result;
    }

    internal void Extend(WorldSession source)
    {
        if (!IsCurrentPlain() || !ReferenceEquals(source.NativeGameClient, binding.Client) ||
            source.Character.Uid != binding.Uid || source.AccountId != binding.AccountId) throw Invalid();
        inputs.Add(new(source, preserveRawAttributes));
        LegacyPlayerStatusCalculator.CaptureOwnedPhysicalMetadata(this, source, catalog);
        LegacyMaxHitpointsCalculator.CaptureOwnedPhysicalMetadata(this, source, catalog);
        if (nativeQueryRequired) LegacyPlayerStatusCalculator.CaptureOwnedPhysicalNativeQueryMetadata(this, source, catalog);
        if (catalog is not null && source.Character.Class / 1000 is 2 or 4 or 7 or 13 or 14)
        {
            // Native rank update truncates after EACH source row; all membership,
            // source order, points and ties participate, including other actors.
            UseRows(catalog.CombatGearRanks);
            RequireCombatGearRankNumbers();
        }
        if (!IsCurrentPlain()) throw Invalid();
    }

    internal void RequireNativeQueryInputs(WorldSession source)
    {
        if (!IsCurrentPlain() || !ReferenceEquals(source.NativeGameClient, binding.Client) ||
            source.AccountId != binding.AccountId || source.Character.Uid != binding.Uid) throw Invalid();
        inputs.Add(new(source, preserveRawAttributes));
        nativeQueryRequired = true;
        LegacyPlayerStatusCalculator.CaptureOwnedPhysicalNativeQueryMetadata(this, source, catalog);
        if (!IsCurrentPlain()) throw Invalid();
    }

    internal bool IsCurrentPlain() => binding.Store.IsOwnedPhysicalMetadataCatalogCurrent(binding, catalog) &&
        actualIndices.All(IsActualIndex) && actualRaw.All(IsActualRaw) &&
        inputs.All(input => input.IsCurrentPlain()) && collections.Values.All(value => value.IsCurrentPlain()) &&
        rows.Values.All(row => row.IsCurrentPlain()) && magicLevels.All(pair => pair.Value.IsCurrentFor(pair.Key.Fields)) &&
        nativeMagic.Values.All(row => row.IsCurrentPlain()) &&
        spellClassifications.Values.All(proof => proof.IsCurrentPlain());

    internal void UseItem(uint id, bool required = false)
    {
        if (id == 0) return;
        if (id > int.MaxValue || catalog is null) { if (required) throw Invalid(); return; }
        var group = UseGroup(catalog.ItemsById, (int)id);
        if (required && (group is null || group.Count != 1)) throw Invalid();
        if (group is not null && group.Any(row => row.Fields.Count < 2 ||
            !int.TryParse(row.Fields[0], System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var rawId) || rawId != row.Id ||
            !string.Equals(row.Fields[1], row.Name, StringComparison.Ordinal))) throw Invalid();
    }

    internal void UseAdd(uint id, int level)
    {
        if (catalog is not null && id <= int.MaxValue)
            UseIndex(catalog.ItemAddBonusByItemTypeIdAndLevel, ((int)id, level));
    }

    internal void UseMagicLevel(int id, int level, bool required) =>
        UseMagicLevel(id, level, required, System.Globalization.CultureInfo.InvariantCulture);

    private void UseMagicLevel(int id, int level, bool required, System.Globalization.CultureInfo culture)
    {
        if (catalog is null) { if (required) throw Invalid(); return; }
        RequireActualIndex(catalog.MagicByTypeId);
        var index = UseMap(catalog.MagicByTypeId);
        var expected = RawGroup(catalog.MagicByTypeId, id);
        if (!index.TryGetValue(id, out var group))
        { if (required || expected.Length != 0) throw Invalid(); return; }
        UseList(group);
        if (group.Count != expected.Length) throw Invalid();
        MagicType? selected = null;
        for (int at = 0; at < group.Count; at++)
        {
            var row = group[at];
            if (!ReferenceEquals(row, expected[at]) || row.TypeId != id) throw Invalid();
            if (!magicLevels.ContainsKey(row)) magicLevels.Add(row, new FieldAt(row.Fields, 8));
            if (!int.TryParse(row.Fields[8], System.Globalization.NumberStyles.Integer,
                culture, out var current)) throw Invalid();
            if (current == level)
            { if (selected is not null) throw Invalid(); selected = row; }
        }
        if (selected is null)
        {
            if (required) throw Invalid();
            if (group.Count == 0) return;
            selected = group[0]; // Existing optional formula's first-row fallback.
        }
        UseRow(selected);
    }

    internal void RequireDerivedSpell(ushort id, ushort level)
    {
        if (id is not (20_000 or 21_930) || !IsCurrentPlain()) throw Invalid();
        var selected = RequireRawRuntimeSpell(id, level);
        if (derivedSpells.Keys.Any(key => key.Id == id && key.Level != level)) throw Invalid();
        if (!derivedSpells.ContainsKey((id, level))) derivedOrder.Add((id, level));
        derivedSpells[(id, level)] = selected;
        if (!IsCurrentPlain()) throw Invalid();
    }

    internal IReadOnlyList<MagicType> CaptureDerivedSpellRows()
    {
        if (!IsCurrentPlain() || derivedSpells.Count > 2) throw Invalid();
        var result = derivedOrder.Select(key => derivedSpells[key]).Select(row =>
            new MagicType(row.Id, row.TypeId, row.Name, Array.AsReadOnly(row.Fields.ToArray()))).ToArray();
        if (!IsCurrentPlain()) throw Invalid();
        return Array.AsReadOnly(result);
    }

    internal void RequireRuntimeSpell(LegacyMagicRuntimeSpell actualResolved)
    {
        if (actualResolved is null || !IsCurrentPlain()) throw Invalid();
        _ = RequireRawRuntimeSpell(actualResolved.Id, actualResolved.Level, out var retained);
        if (retained != actualResolved) throw Invalid();
        if (!IsCurrentPlain()) throw Invalid();
    }

    internal IReadOnlyList<T>? UseGroup<K, T>(IReadOnlyDictionary<K, IReadOnlyList<T>> source, K key)
        where K : notnull where T : class
    {
        RequireActualIndex(source);
        var actual = UseMap(source);
        if (!actual.TryGetValue(key, out var values))
        {
            if (RawGroup(source, key).Length != 0) throw Invalid();
            return null;
        }
        UseRows(values, indexedGroup: true);
        foreach (var row in values)
            if (!Equals(IndexKey(source, row), key)) throw Invalid();
        var expected = RawGroup(source, key);
        if (expected.Length != values.Count) throw Invalid();
        for (int index = 0; index < expected.Length; index++)
            if (!ReferenceEquals(expected[index], values[index])) throw Invalid();
        return values;
    }

    internal T? UseIndex<K, T>(IReadOnlyDictionary<K, T> source, K key) where K : notnull where T : class
    {
        RequireActualIndex(source);
        var actual = UseMap(source);
        if (!actual.TryGetValue(key, out var row))
        {
            if (RawGroup(source, key).Length != 0) throw Invalid();
            return null;
        }
        if (!Equals(IndexKey(source, row), key)) throw Invalid();
        if (RawGroup(source, key).Length != 1 || !ReferenceEquals(RawGroup(source, key)[0], row)) throw Invalid();
        UseRow(row);
        return row;
    }

    internal void UseAllIndex<K, T>(IReadOnlyDictionary<K, T> source) where K : notnull where T : class
    {
        RequireActualIndex(source);
        var actual = UseMap(source);
        var raw = RowsForIndex(source).ToArray();
        if (raw.Select(row => IndexKey(source, row)).Distinct().Count() != actual.Count) throw Invalid();
        foreach (var pair in actual)
        {
            if (!Equals(IndexKey(source, pair.Value), pair.Key)) throw Invalid();
            var group = RawGroup(source, pair.Key);
            if (group.Length != 1 || !ReferenceEquals(group[0], pair.Value)) throw Invalid();
            UseRow(pair.Value);
        }
    }

    internal void UseRows<T>(IReadOnlyList<T> source) where T : class => UseRows(source, indexedGroup: false);

    private void UseRows<T>(IReadOnlyList<T> source, bool indexedGroup) where T : class
    {
        if (!indexedGroup)
        {
            if (!IsActualRaw(source)) throw Invalid();
            actualRaw.Add(source);
        }
        UseList(source);
        foreach (var row in source) UseRow(row);
    }

    private void UseRow(object row)
    {
        if (rows.ContainsKey(row)) return;
        _ = RawRows(row);
        if (!rawCounts.TryGetValue(row, out var count) || count != 1) throw Invalid();
        rows.Add(row, new Row(row));
    }

    private void UseList<T>(IReadOnlyList<T> source)
    {
        if (!collections.ContainsKey(source)) collections.Add(source, new PlainList<T>(source));
    }

    private Dictionary<K, V> UseMap<K, V>(IReadOnlyDictionary<K, V> source) where K : notnull
    {
        if (!collections.TryGetValue(source, out var proof))
        {
            proof = new PlainMap<K, V>(source);
            collections.Add(source, proof);
        }
        return ((PlainMap<K, V>)proof).Actual;
    }

    private object[] RawGroup(object source, object key)
    {
        if (!rawIndices.TryGetValue(source, out var index))
        {
            var groups = new Dictionary<object, List<object>>();
            foreach (var row in RowsForIndex(source))
            {
                var rowKey = IndexKey(source, row);
                if (!groups.TryGetValue(rowKey, out var group)) groups.Add(rowKey, group = []);
                group.Add(row);
            }
            index = groups.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
            rawIndices.Add(source, index);
        }
        return index.TryGetValue(key, out var rows) ? rows : Array.Empty<object>();
    }

    private interface ICurrent { bool IsCurrentPlain(); }
    private sealed class Input
    {
        private readonly WorldSession source;
        private readonly LegacyNativeAttributeMapProof attributes, paid;
        private readonly SpellRows spells;
        private readonly LegacyItemSaveGraph items;
        private readonly List<ICurrent> gear = [];
        internal Input(WorldSession source, bool preserveRawAttributes = false)
        {
            this.source = source;
            attributes = preserveRawAttributes ? LegacyNativeAttributeMapProof.CaptureRaw(source.Character.Attributes) : new(source.Character.Attributes);
            paid = preserveRawAttributes ? LegacyNativeAttributeMapProof.CaptureRaw(source.Character.PaidNobility) : new(source.Character.PaidNobility);
            spells = new(source.PlayerSpells.Spells);
            if (!LegacyItemSaveGraph.TryCapture(source.PlayerItems, out var captured) || captured is null) throw Invalid();
            items = captured;
            if (source.CombatGear is { } combat)
            {
                gear.Add(new PlainList<CombatGearSnapshotItem>(combat.Items));
                gear.Add(new PlainList<uint>(combat.BlackJadeBag.BlackJades));
                foreach (var row in combat.Items) gear.Add(new GearRow(row));
            }
        }
        internal bool IsCurrentPlain() => attributes.IsCurrentPlain() && paid.IsCurrentPlain() &&
            spells.IsCurrentPlain() && items.IsCurrent(source.PlayerItems) && gear.All(row => row.IsCurrentPlain());
    }

    private sealed class GearRow : ICurrent
    {
        private readonly CombatGearSnapshotItem row, copy;
        private readonly PlainList<uint> jades;
        private readonly PlainList<IReadOnlyList<uint>> animas;
        private readonly PlainList<uint>[] animaRows;
        internal GearRow(CombatGearSnapshotItem row)
        {
            this.row = row; copy = row with { };
            jades = new(row.BlackJades); animas = new(row.Animas);
            animaRows = row.Animas.Select(value => new PlainList<uint>(value)).ToArray();
        }
        public bool IsCurrentPlain() => row == copy && jades.IsCurrentPlain() && animas.IsCurrentPlain() &&
            animaRows.All(value => value.IsCurrentPlain());
    }

    private sealed class SpellRows
    {
        private readonly LegacyNativeSpellStorage.CanonicalSessionSpellRows? canonical;
        private readonly PlayerSpellSnapshotEntry[]? saved;
        private readonly PlainList<PlayerSpellSnapshotEntry>? plain;
        internal SpellRows(IReadOnlyList<PlayerSpellSnapshotEntry> source)
        {
            if (source is LegacyNativeSpellStorage.CanonicalSessionSpellRows actual)
            { canonical = actual; saved = actual.CopyRows(); }
            else plain = new(source);
        }
        internal bool IsCurrentPlain() => canonical is not null ? canonical.MatchesRowsPlain(saved!) : plain!.IsCurrentPlain();
    }

    private static InvalidDataException Invalid() => new("ordinaryPhysicalMetadataChangedOrInvalid");
}
