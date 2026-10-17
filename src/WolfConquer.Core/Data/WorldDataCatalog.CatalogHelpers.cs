namespace WolfConquer.Core.Data;

public sealed partial class WorldDataCatalog
{
    private static IReadOnlyDictionary<TKey, T> BuildUniqueIndex<T, TKey>(
        IReadOnlyList<T> values,
        Func<T, TKey> keySelector,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        var result = new Dictionary<TKey, T>(comparer);
        foreach (var value in values)
        {
            result.TryAdd(keySelector(value), value);
        }

        return result;
    }

    private static IReadOnlyDictionary<TKey, IReadOnlyList<T>> BuildGroupedIndex<T, TKey>(
        IReadOnlyList<T> values,
        Func<T, TKey> keySelector,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        return values
            .GroupBy(keySelector, comparer)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<T>)group.ToArray(),
                comparer);
    }

    private static void AddCountDiagnostics(
        SeedManifest manifest,
        List<WorldDataDiagnostic> diagnostics,
        string name,
        int actual)
    {
        if (!manifest.Counts.TryGetValue(name, out var expected))
        {
            diagnostics.Add(new WorldDataDiagnostic("MissingManifestCount", $"Manifest does not contain a count for '{name}'."));
            return;
        }

        if (expected != actual)
        {
            diagnostics.Add(new WorldDataDiagnostic(
                "ManifestCountMismatch",
                $"Manifest count for '{name}' is {expected}, but loaded {actual}."));
        }
    }

    private static void AddDuplicateDiagnostics<T, TKey>(
        List<WorldDataDiagnostic> diagnostics,
        IReadOnlyList<T> values,
        Func<T, TKey> keySelector,
        string code,
        string label,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        foreach (var duplicate in values.GroupBy(keySelector, comparer).Where(group => group.Count() > 1))
        {
            diagnostics.Add(new WorldDataDiagnostic(
                code,
                $"Duplicate {label} {duplicate.Key} appears {duplicate.Count()} times."));
        }
    }

    private static bool IsCompleteQuestNavigationAnchor(QuestNpcAnchor anchor)
    {
        return anchor.MapId is > 0 &&
            anchor.X.HasValue &&
            anchor.Y.HasValue &&
            !string.IsNullOrWhiteSpace(anchor.Name);
    }
}
