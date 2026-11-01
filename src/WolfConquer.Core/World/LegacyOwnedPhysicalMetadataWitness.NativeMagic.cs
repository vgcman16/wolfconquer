using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOwnedPhysicalMetadataWitness
{
    // The raw resolver does not consult the native mutable-row cache. Retain
    // only the actual raw parse and classification dependencies it consumes.
    private MagicType RequireRawRuntimeSpell(ushort id, ushort level) => RequireRawRuntimeSpell(id, level, out _);

    private MagicType RequireRawRuntimeSpell(ushort id, ushort level, out LegacyMagicRuntimeSpell spell)
    {
        var selected = RequireSelectedMagicRow(id, level, CultureInfo.InvariantCulture);
        if (!LegacyMagicRuntime.TryResolveSpell(catalog!, id, level, out spell) ||
            spell.Id != id || spell.Level != level || spell.Name != selected.Name ||
            spell.IsRuneSkill != RequireSpellClassification(id).Value) throw Invalid();
        return selected;
    }

    // A real native-loaded caller supplies the exact row returned by its
    // resolver. Observing that row must never create a previously cold cache.
    internal void RequireNativeLoadedSpell(ushort requestedId, ushort level, LegacyNativeMagicRow actual)
    {
        if (actual is null || !IsCurrentPlain()) throw Invalid();
        ushort selectedId = requestedId == 6_009 ? (ushort)6_010 : requestedId;
        var selected = RequireSelectedMagicRow(selectedId, level, catalog!.NativeMagicParseCulture);
        _ = RequireSpellClassification(actual.Id);
        if (!ReferenceEquals(actual.Source, selected) || actual.Id != selectedId || actual.Level != level)
            throw Invalid();
        if (nativeMagic.TryGetValue(selected, out var prior))
        { if (!prior.IsFor(actual)) throw Invalid(); }
        else nativeMagic.Add(selected, new NativeMagic(catalog!, selected, actual));
        if (!IsCurrentPlain()) throw Invalid();
    }

    private MagicType RequireSelectedMagicRow(ushort id, ushort level, CultureInfo culture)
    {
        UseMagicLevel(id, level, required: true, culture);
        var selected = catalog!.MagicByTypeId[id].FirstOrDefault(row =>
            byte.TryParse(row.Fields[8], NumberStyles.Integer, culture, out var value) && value == level)
            ?? throw Invalid();
        if (!int.TryParse(selected.Fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawId) ||
            rawId != selected.Id || !ushort.TryParse(selected.Fields[1], NumberStyles.Integer,
                culture, out var type) || type != selected.TypeId || type != id ||
            selected.Fields.Count <= 3 || selected.Fields[3] != selected.Name) throw Invalid();
        return selected;
    }

    private SpellClassification RequireSpellClassification(ushort id)
    {
        if (!spellClassifications.TryGetValue(id, out var proof))
            spellClassifications.Add(id, proof = new SpellClassification(catalog!, id));
        if (!proof.IsCurrentPlain()) throw Invalid();
        return proof;
    }

    private sealed class SpellClassification : ICurrent
    {
        private readonly WorldDataCatalog catalog;
        private readonly IReadOnlyList<MagicEffect> source;
        private readonly PlainList<MagicEffect> root;
        private readonly EffectRow[] matching;
        private readonly LegacyRuneSpellClassification? classification;
        internal bool? Value { get; }
        internal SpellClassification(WorldDataCatalog catalog, ushort id)
        {
            this.catalog = catalog; source = catalog.MagicEffects; root = new(source);
            matching = source.Where(row => row.Id == (long)id * 100).Select(row => new EffectRow(row)).ToArray();
            if (LegacyRuneSpellClassification.TryCapture(catalog, id, out var proof))
            { classification = proof; Value = proof!.IsRuneSkill; }
            else if (LegacyRuneSpellClassification.HasExplicitEntry(catalog, id)) throw Invalid();
            // An actually absent IsRuneSkill declaration keeps the resolver's
            // nullable classification. No new MagicEffect row is required.
            if (!IsCurrentPlain()) throw Invalid();
        }
        public bool IsCurrentPlain() => ReferenceEquals(catalog.MagicEffects, source) && root.IsCurrentPlain() &&
            matching.All(row => row.IsCurrentPlain()) && (classification is null || classification.IsCurrent());
    }

    private sealed class EffectRow : ICurrent
    {
        private readonly MagicEffect row, copy;
        private readonly PlainList<MagicEffectEntry> entries;
        private readonly MagicEffectEntry[] saved;
        private readonly LegacyNativeAttributeMapProof attributes;
        internal EffectRow(MagicEffect row)
        {
            this.row = row; copy = row with { }; entries = new(row.Entries);
            saved = row.Entries.Select(entry => entry with { }).ToArray();
            attributes = new(row.Attributes);
        }
        public bool IsCurrentPlain() => row == copy && entries.IsCurrentPlain() && attributes.IsCurrentPlain() &&
            row.Entries.Count == saved.Length && row.Entries.Select((entry, index) => entry == saved[index]).All(value => value);
    }

    private sealed class NativeMagic
    {
        private readonly WorldDataCatalog catalog;
        private readonly MagicType source;
        private readonly LegacyNativeMagicRow native;
        private readonly int damageBits;
        private readonly ushort cooldown;
        private readonly ushort id;
        private readonly byte level;
        private readonly int gDamage;
        internal NativeMagic(WorldDataCatalog catalog, MagicType source, LegacyNativeMagicRow expected)
        {
            this.catalog = catalog; this.source = source;
            if (!catalog.TryReadOwnedPhysicalMagicRow(source, out var actual) || actual is null ||
                !ReferenceEquals(actual, expected) || !ReferenceEquals(actual.Source, source) ||
                !float.IsFinite(actual.Damage) || actual.Damage < 0)
                throw Invalid();
            native = actual; damageBits = BitConverter.SingleToInt32Bits(actual.Damage); cooldown = actual.CoolDown;
            id = actual.Id; level = actual.Level; gDamage = actual.GDamage;
        }
        internal bool IsFor(LegacyNativeMagicRow actual) => ReferenceEquals(actual, native) && IsCurrentPlain();
        internal bool IsCurrentPlain() => catalog.TryReadOwnedPhysicalMagicRow(source, out var actual) &&
            ReferenceEquals(actual, native) && ReferenceEquals(native.Source, source) &&
            native.Id == id && native.Level == level && native.GDamage == gDamage &&
            BitConverter.SingleToInt32Bits(native.Damage) == damageBits && native.CoolDown == cooldown;
    }
}
