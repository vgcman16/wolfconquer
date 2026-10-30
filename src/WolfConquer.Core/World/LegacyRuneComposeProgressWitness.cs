using System.Collections.ObjectModel;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Pins metadata only. The real admitted Rune evidence owns authorization and
// the Store/publication boundary; a detached table cannot issue either one.
internal sealed partial class LegacyRuneComposeProgressWitness
{
    private readonly WorldDataCatalog catalog;
    private readonly PlainRows<RuneLevelExperience> raw;
    private readonly Dictionary<(int RuneType, int Level), RuneLevelExperience> index;
    private readonly KeyValuePair<(int RuneType, int Level), RuneLevelExperience>[] indexed;
    private readonly Dictionary<(uint Family, byte Level), RuneLevelExperience> candidates = new();
    private RowWitness[]? used;
    internal IReadOnlyDictionary<(uint Family, byte Level), LegacyRuneComposeProgressRow> Lookup { get; }

    internal LegacyRuneComposeProgressWitness(WorldDataCatalog catalog)
    {
        this.catalog = catalog ?? throw Invalid();
        raw = new(catalog.RuneLevelExperience);
        if (raw.Values.Any(row => row is null)) throw Invalid();
        index = catalog.RuneLevelExperienceByRuneTypeAndLevel as Dictionary<(int RuneType, int Level), RuneLevelExperience> ?? throw Invalid();
        if (index.GetType() != typeof(Dictionary<(int RuneType, int Level), RuneLevelExperience>) ||
            !ReferenceEquals(index.Comparer, EqualityComparer<(int RuneType, int Level)>.Default)) throw Invalid();
        indexed = index.ToArray();
        if (indexed.Any(entry => entry.Value is null)) throw Invalid();
        var lookup = new Dictionary<(uint Family, byte Level), LegacyRuneComposeProgressRow>();
        foreach (var group in raw.Values.GroupBy(row => (row.RuneType, row.Level)))
        {
            // Future/unrelated bad rows cannot authorize a used curve, but do
            // not fence a valid operation merely by being present in the table.
            if (group.Key.RuneType is < 401 or > 410 || group.Key.Level is < 1 or > 99) continue;
            var rows = group.ToArray();
            if (rows.Length != 1 || rows[0].Experience is < 0 or > uint.MaxValue ||
                !index.TryGetValue(group.Key, out var indexedRow) || !ReferenceEquals(rows[0], indexedRow)) continue;
            var key = ((uint)group.Key.RuneType, (byte)group.Key.Level);
            candidates.Add(key, rows[0]); lookup.Add(key, new(key.Item1, key.Item2, (uint)rows[0].Experience));
        }
        Lookup = new ReadOnlyDictionary<(uint Family, byte Level), LegacyRuneComposeProgressRow>(lookup);
        if (!IsCurrent()) throw Invalid();
    }

    internal LegacyRuneComposeProgressRow[] CaptureUsed(IReadOnlySet<(uint Family, byte Level)> required)
    {
        // This is the pure planner's BCL set, not a caller-defined enumerator.
        if (required is not HashSet<(uint Family, byte Level)> keys || keys.GetType() != typeof(HashSet<(uint Family, byte Level)>) ||
            !ReferenceEquals(keys.Comparer, EqualityComparer<(uint Family, byte Level)>.Default) || keys.Count > 128 || !IsCurrent()) throw Invalid();
        var ordered = keys.OrderBy(key => key.Family).ThenBy(key => key.Level).ToArray();
        if (used is not null)
        {
            if (!ordered.SequenceEqual(used.Select(row => (row.Record.Family, row.Record.Level)))) throw Invalid();
            return used.Select(row => row.Record).ToArray();
        }
        var captured = new List<RowWitness>();
        foreach (var key in ordered)
        {
            if (!candidates.TryGetValue(key, out var row) || !Lookup.TryGetValue(key, out var record) ||
                !index.TryGetValue(((int)key.Family, (int)key.Level), out var indexedRow) || !ReferenceEquals(row, indexedRow) ||
                raw.Values.Count(value => value.RuneType == key.Family && value.Level == key.Level) != 1) throw Invalid();
            captured.Add(new RowWitness(row, record));
        }
        used = captured.ToArray();
        if (!IsCurrent()) throw Invalid();
        return used.Select(row => row.Record).ToArray();
    }

    internal bool IsCurrent()
    {
        try
        {
            if (!ReferenceEquals(catalog.RuneLevelExperience, raw.Source) ||
                !ReferenceEquals(catalog.RuneLevelExperienceByRuneTypeAndLevel, index) || !raw.IsCurrent() || index.Count != indexed.Length) return false;
            var iterator = index.GetEnumerator();
            foreach (var entry in indexed)
                if (!iterator.MoveNext() || iterator.Current.Key != entry.Key || !ReferenceEquals(iterator.Current.Value, entry.Value)) return false;
            return !iterator.MoveNext() && (used is null || used.All(row => row.IsCurrent()));
        }
        catch (InvalidOperationException) { return false; }
        catch (ArgumentOutOfRangeException) { return false; }
        catch (IndexOutOfRangeException) { return false; }
    }

    private static InvalidOperationException Invalid() => new("runeComposeRequiresCurrentProgressMetadata");
}
