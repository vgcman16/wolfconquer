namespace WolfConquer.Core.Data;

public sealed class GuildWarData
{
    private readonly Dictionary<string, GuildWarSection> _sectionsByName;
    private readonly Dictionary<string, IReadOnlyList<GuildWarEntry>> _entriesBySectionAndKey;

    public GuildWarData(
        string sourceFile,
        IReadOnlyList<GuildWarSection> sections,
        IReadOnlyList<GuildWarEntry> entries,
        IReadOnlyList<WorldDataDiagnostic> diagnostics,
        GuildWarInfoData? info,
        GuildWarPoleData? pole,
        IReadOnlyList<GuildWarConductorRow> conductors)
    {
        SourceFile = sourceFile;
        Sections = sections;
        Entries = entries;
        Diagnostics = diagnostics;
        Info = info;
        Pole = pole;
        Conductors = conductors;

        _sectionsByName = new Dictionary<string, GuildWarSection>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in sections)
        {
            _sectionsByName.TryAdd(section.Name, section);
        }

        var entriesByKey = new Dictionary<string, List<GuildWarEntry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var indexKey = BuildEntryIndexKey(entry.SectionName, entry.Key);
            if (!entriesByKey.TryGetValue(indexKey, out var sectionEntries))
            {
                sectionEntries = [];
                entriesByKey[indexKey] = sectionEntries;
            }

            sectionEntries.Add(entry);
        }

        _entriesBySectionAndKey = entriesByKey.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<GuildWarEntry>)pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    public string SourceFile { get; }

    public IReadOnlyList<GuildWarSection> Sections { get; }

    public IReadOnlyList<GuildWarEntry> Entries { get; }

    public IReadOnlyList<WorldDataDiagnostic> Diagnostics { get; }

    public GuildWarInfoData? Info { get; }

    public GuildWarPoleData? Pole { get; }

    public IReadOnlyList<GuildWarConductorRow> Conductors { get; }

    public GuildWarSection? GetSection(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return _sectionsByName.GetValueOrDefault(name);
    }

    public GuildWarEntry? GetEntry(string sectionName, string key)
    {
        var entries = GetEntries(sectionName, key);
        return entries.Count == 0 ? null : entries[^1];
    }

    public IReadOnlyList<GuildWarEntry> GetEntries(string sectionName, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _entriesBySectionAndKey.GetValueOrDefault(BuildEntryIndexKey(sectionName, key))
            ?? Array.Empty<GuildWarEntry>();
    }

    private static string BuildEntryIndexKey(string sectionName, string key)
    {
        return $"{sectionName}\u001f{key}";
    }
}

public sealed record GuildWarSection(
    string Name,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<GuildWarEntry> Entries);

public sealed record GuildWarEntry(
    string SectionName,
    string Key,
    string Value,
    string RawKey,
    string RawValue,
    string SourceFile,
    int LineNumber,
    string RawLine);

public sealed record GuildWarInfoData(
    int? Id,
    string? Name,
    int? LeaderReward,
    int? DeputiLeaderReward,
    IReadOnlyDictionary<int, int> LeaderTop,
    IReadOnlyDictionary<int, int> DeputiTop)
{
    public int? DeputyLeaderReward => DeputiLeaderReward;

    public IReadOnlyDictionary<int, int> DeputyTop => DeputiTop;
}

public sealed record GuildWarPoleData(
    string? Name,
    long? HitPoints);

public sealed record GuildWarConductorRow(
    string Key,
    IReadOnlyList<string> Fields,
    string SourceFile,
    int LineNumber,
    string RawLine,
    GuildWarEntry Entry);
