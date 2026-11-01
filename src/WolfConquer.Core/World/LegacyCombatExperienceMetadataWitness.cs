using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Closed DATA witness for the actual level/stat catalog indexes. It does not
// authorize a gain, account, combat event, file write or native publication.
internal sealed class LegacyCombatExperienceMetadataWitness
{
    private const int MaximumRows = 262_144;
    private readonly WorldDataCatalog catalog;
    private readonly IReadOnlyList<LevelExperience> levelSource;
    private readonly IReadOnlyList<ClassLevelStat> statSource;
    private readonly LevelExperience[] levels;
    private readonly ClassLevelStat[] stats;
    private readonly Dictionary<(int Type, int Level), LevelExperience> levelIndex;
    private readonly Dictionary<(string ClassName, int Level), ClassLevelStat> statIndex;
    private readonly KeyValuePair<(int Type, int Level), LevelExperience>[] levelEntries;
    private readonly KeyValuePair<(string ClassName, int Level), ClassLevelStat>[] statEntries;

    internal LegacyCombatExperienceMetadataWitness(WorldDataCatalog catalog)
    {
        this.catalog = catalog ?? throw Invalid();
        levelSource = catalog.LevelExperience; statSource = catalog.ClassLevelStats;
        levels = Copy(levelSource); stats = Copy(statSource);
        levelIndex = catalog.LevelExperienceByTypeAndLevel as Dictionary<(int, int), LevelExperience> ?? throw Invalid();
        statIndex = catalog.ClassLevelStatsByClassAndLevel as Dictionary<(string, int), ClassLevelStat> ?? throw Invalid();
        if (levelIndex.GetType() != typeof(Dictionary<(int, int), LevelExperience>) ||
            statIndex.GetType() != typeof(Dictionary<(string, int), ClassLevelStat>) ||
            !ReferenceEquals(levelIndex.Comparer, EqualityComparer<(int, int)>.Default) ||
            !ReferenceEquals(statIndex.Comparer, EqualityComparer<(string, int)>.Default) ||
            levelIndex.Count > MaximumRows || statIndex.Count > MaximumRows) throw Invalid();
        levelEntries = levelIndex.ToArray(); statEntries = statIndex.ToArray();
        var expectedLevels = new Dictionary<(int, int), LevelExperience>();
        var expectedStats = new Dictionary<(string, int), ClassLevelStat>();
        foreach (var row in levels) expectedLevels.TryAdd((row.Type, row.Level), row);
        foreach (var row in stats) expectedStats.TryAdd((row.ClassName, row.Level), row);
        if (!SameIndex(levelEntries, expectedLevels) || !SameIndex(statEntries, expectedStats) || !IsCurrentPlain()) throw Invalid();
    }

    internal bool IsCurrentPlain()
    {
        try
        {
            return ReferenceEquals(catalog.LevelExperience, levelSource) && ReferenceEquals(catalog.ClassLevelStats, statSource) &&
                ReferenceEquals(catalog.LevelExperienceByTypeAndLevel, levelIndex) &&
                ReferenceEquals(catalog.ClassLevelStatsByClassAndLevel, statIndex) &&
                SameRows(levelSource, levels) && SameRows(statSource, stats) &&
                SameIndex(levelEntries, levelIndex) && SameIndex(statEntries, statIndex);
        }
        catch (InvalidOperationException) { return false; }
    }

    private static T[] Copy<T>(IReadOnlyList<T> source) where T : class
    {
        if (source is null || source.GetType() != typeof(T[]) && source.GetType() != typeof(List<T>) ||
            source.Count > MaximumRows) throw Invalid();
        var result = source.ToArray();
        if (result.Any(row => row is null) || !SameRows(source, result)) throw Invalid();
        return result;
    }

    private static bool SameRows<T>(IReadOnlyList<T> source, T[] captured) where T : class
    {
        if (source.Count != captured.Length) return false;
        for (var i = 0; i < captured.Length; i++)
            if (!ReferenceEquals(source[i], captured[i])) return false;
        return true;
    }

    private static bool SameIndex<K, T>(KeyValuePair<K, T>[] captured, Dictionary<K, T> current)
        where K : notnull where T : class => current.Count == captured.Length &&
        captured.All(pair => current.TryGetValue(pair.Key, out var row) && ReferenceEquals(row, pair.Value));

    private static InvalidOperationException Invalid() => new("combatExperienceMetadataUnproven");
}
