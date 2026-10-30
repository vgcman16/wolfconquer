using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Actual MagicEffect.ini ID00 classification, never an inferred Rune ID list.
internal sealed class LegacyRuneSpellClassification
{
    private readonly WorldDataCatalog catalog;
    private readonly MagicEffect[] rows;
    private readonly MagicEffect row;
    private readonly MagicEffectEntry[] entries;
    private readonly Dictionary<string, string> attributes;
    private readonly KeyValuePair<string, string>[] fields;
    internal bool IsRuneSkill { get; }

    private LegacyRuneSpellClassification(WorldDataCatalog catalog, MagicEffect[] rows, MagicEffect row,
        MagicEffectEntry[] entries, Dictionary<string, string> attributes, KeyValuePair<string, string>[] fields, bool rune)
    { this.catalog = catalog; this.rows = rows; this.row = row; this.entries = entries;
      this.attributes = attributes; this.fields = fields; IsRuneSkill = rune; }

    internal static bool TryCapture(WorldDataCatalog catalog, ushort spellId, out LegacyRuneSpellClassification? proof)
    {
        proof = null;
        if (catalog is null || spellId == 0 || CopyRows(catalog.MagicEffects) is not { } rows) return false;
        var section = checked((long)spellId * 100);
        var matching = rows.Where(value => value is not null && value.Id == section).ToArray();
        if (matching.Length != 1) return false;
        var row = matching[0];
        if (row.SourceSection != section.ToString(CultureInfo.InvariantCulture) || CopyRows(row.Entries) is not { } entries ||
            row.Attributes is not Dictionary<string, string> attributes) return false;
        var keys = entries.Where(entry => entry is not null && entry.Key.Equals("IsRuneSkill", StringComparison.OrdinalIgnoreCase)).ToArray();
        var fields = attributes.ToArray();
        var values = fields.Where(field => field.Key.Equals("IsRuneSkill", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (keys.Length != 1 || values.Length != 1 || keys[0].Value != values[0].Value ||
            !RawEntryMatches(keys[0]) || !byte.TryParse(keys[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value)) return false;
        var candidate = new LegacyRuneSpellClassification(catalog, rows, row, entries, attributes, fields, value != 0);
        if (!candidate.IsCurrent()) return false;
        proof = candidate; return true;
    }

    // Distinguish an unrelated absent classification from malformed explicit
    // evidence. The former keeps existing ordinary-spell behavior; the latter
    // cannot silently downgrade an explicitly classified spell to ordinary.
    internal static bool HasExplicitEntry(WorldDataCatalog catalog, ushort id) => catalog.MagicEffects.Any(row =>
        row is not null && row.Id == (long)id * 100 &&
        (row.Entries.Any(entry => entry is not null && entry.Key.Equals("IsRuneSkill", StringComparison.OrdinalIgnoreCase)) ||
         row.Attributes.Keys.Any(key => key.Equals("IsRuneSkill", StringComparison.OrdinalIgnoreCase))));

    internal bool IsCurrent()
    {
        if (!SameRows(catalog.MagicEffects, rows) || !SameRows(row.Entries, entries) || attributes.Count != fields.Length) return false;
        try
        {
            var iterator = attributes.GetEnumerator();
            foreach (var field in fields)
                if (!iterator.MoveNext() || iterator.Current.Key != field.Key || iterator.Current.Value != field.Value) return false;
            return !iterator.MoveNext();
        }
        catch (InvalidOperationException) { return false; }
    }

    private static bool RawEntryMatches(MagicEffectEntry entry)
    {
        var equals = entry.RawLine.IndexOf('=');
        return equals > 0 && entry.RawLine[..equals].Trim() == entry.Key && entry.RawLine[(equals + 1)..].Trim() == entry.Value;
    }

    private static T[]? CopyRows<T>(IReadOnlyList<T> rows) where T : class => rows switch
    { T[] array => array.ToArray(), List<T> list => list.ToArray(), _ => null };

    private static bool SameRows<T>(IReadOnlyList<T> source, T[] before) where T : class
    {
        if (source is T[] array)
        {
            if (array.Length != before.Length) return false;
            for (var index = 0; index < before.Length; index++) if (!ReferenceEquals(array[index], before[index])) return false;
            return true;
        }
        if (source is not List<T> list || list.Count != before.Length) return false;
        for (var index = 0; index < before.Length; index++) if (!ReferenceEquals(list[index], before[index])) return false;
        return true;
    }
}
