using System.Globalization;

namespace WolfConquer.Core.Data;

public static class GuildWarParser
{
    private const string InfoSectionName = "Info";
    private const string PoleSectionName = "Pole";
    private const string CondutorsSectionName = "Condutors";
    private const string ConductorsSectionName = "Conductors";

    public static GuildWarData ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return Parse(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static GuildWarData Parse(string text, string sourceFile = "GuildWarInfo.ini")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var sectionBuilders = new List<SectionBuilder>();
        var entries = new List<GuildWarEntry>();
        var diagnostics = new List<WorldDataDiagnostic>();
        var seenKeys = new Dictionary<string, GuildWarEntry>(StringComparer.OrdinalIgnoreCase);
        SectionBuilder? currentSection = null;

        using var reader = new StringReader(text);
        var lineNumber = 0;
        while (reader.ReadLine() is { } rawLine)
        {
            lineNumber++;

            var trimmedLine = rawLine.Trim();
            if (trimmedLine.Length == 0 || trimmedLine.StartsWith(';') || trimmedLine.StartsWith('#'))
            {
                continue;
            }

            if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
            {
                var sectionName = trimmedLine[1..^1].Trim();
                if (sectionName.Length == 0)
                {
                    throw new FormatException($"{sourceFile} line {lineNumber} has an empty guild-war section name.");
                }

                currentSection = new SectionBuilder(sectionName, sourceFile, lineNumber, rawLine);
                sectionBuilders.Add(currentSection);
                continue;
            }

            if (currentSection is null)
            {
                throw new FormatException($"{sourceFile} line {lineNumber} has a guild-war key before any section.");
            }

            var equalsIndex = rawLine.IndexOf('=');
            if (equalsIndex <= 0)
            {
                throw new FormatException($"{sourceFile} line {lineNumber} is not a guild-war key/value row.");
            }

            var rawKey = rawLine[..equalsIndex];
            var rawValue = rawLine[(equalsIndex + 1)..];
            var key = rawKey.Trim();
            if (key.Length == 0)
            {
                throw new FormatException($"{sourceFile} line {lineNumber} has an empty guild-war key.");
            }

            var entry = new GuildWarEntry(
                currentSection.Name,
                key,
                rawValue.Trim(),
                rawKey,
                rawValue,
                sourceFile,
                lineNumber,
                rawLine);

            var duplicateKey = BuildEntryIndexKey(entry.SectionName, entry.Key);
            if (seenKeys.TryGetValue(duplicateKey, out var firstEntry))
            {
                diagnostics.Add(new WorldDataDiagnostic(
                    "DuplicateGuildWarKey",
                    $"{sourceFile} line {lineNumber} duplicates [{entry.SectionName}] {entry.Key}; first seen on line {firstEntry.LineNumber}."));
            }
            else
            {
                seenKeys[duplicateKey] = entry;
            }

            currentSection.Entries.Add(entry);
            entries.Add(entry);
        }

        var sections = sectionBuilders
            .Select(builder => new GuildWarSection(
                builder.Name,
                builder.SourceFile,
                builder.LineNumber,
                builder.RawLine,
                builder.Entries.ToArray()))
            .ToArray();

        var info = BuildInfo(GetSectionEntries(sections, InfoSectionName), sourceFile, diagnostics);
        var pole = BuildPole(GetSectionEntries(sections, PoleSectionName), sourceFile, diagnostics);
        var conductors = BuildConductors(sections);

        return new GuildWarData(
            sourceFile,
            sections,
            entries.ToArray(),
            diagnostics.ToArray(),
            info,
            pole,
            conductors);
    }

    private static GuildWarInfoData? BuildInfo(
        IReadOnlyList<GuildWarEntry> entries,
        string sourceFile,
        List<WorldDataDiagnostic> diagnostics)
    {
        if (entries.Count == 0)
        {
            return null;
        }

        var leaderTop = new Dictionary<int, int>();
        var deputiTop = new Dictionary<int, int>();
        foreach (var entry in entries)
        {
            if (TryParseRankingKey(entry.Key, "LeaderTop", out var leaderIndex))
            {
                var value = ParseInt(entry, sourceFile, diagnostics);
                if (value is not null)
                {
                    leaderTop[leaderIndex] = value.Value;
                }

                continue;
            }

            if (TryParseRankingKey(entry.Key, "DeputiTop", out var deputiIndex)
                || TryParseRankingKey(entry.Key, "DeputyTop", out deputiIndex))
            {
                var value = ParseInt(entry, sourceFile, diagnostics);
                if (value is not null)
                {
                    deputiTop[deputiIndex] = value.Value;
                }
            }
        }

        return new GuildWarInfoData(
            ParseInt(FindLastEntry(entries, "ID"), sourceFile, diagnostics),
            FindLastEntry(entries, "Name")?.Value,
            ParseInt(FindLastEntry(entries, "LeaderReward"), sourceFile, diagnostics),
            ParseInt(
                FindLastEntry(entries, "DeputiLeaderReward") ?? FindLastEntry(entries, "DeputyLeaderReward"),
                sourceFile,
                diagnostics),
            leaderTop,
            deputiTop);
    }

    private static GuildWarPoleData? BuildPole(
        IReadOnlyList<GuildWarEntry> entries,
        string sourceFile,
        List<WorldDataDiagnostic> diagnostics)
    {
        if (entries.Count == 0)
        {
            return null;
        }

        return new GuildWarPoleData(
            FindLastEntry(entries, "Name")?.Value,
            ParseLong(FindLastEntry(entries, "HitPoints"), sourceFile, diagnostics));
    }

    private static IReadOnlyList<GuildWarConductorRow> BuildConductors(IReadOnlyList<GuildWarSection> sections)
    {
        return sections
            .Where(section =>
                string.Equals(section.Name, CondutorsSectionName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(section.Name, ConductorsSectionName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(section => section.Entries)
            .Where(entry => entry.Key.StartsWith("GuildConductor", StringComparison.OrdinalIgnoreCase))
            .Select(entry => new GuildWarConductorRow(
                entry.Key,
                entry.Value.Split(',', StringSplitOptions.None),
                entry.SourceFile,
                entry.LineNumber,
                entry.RawLine,
                entry))
            .ToArray();
    }

    private static IReadOnlyList<GuildWarEntry> GetSectionEntries(
        IReadOnlyList<GuildWarSection> sections,
        string sectionName)
    {
        return sections
            .Where(section => string.Equals(section.Name, sectionName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(section => section.Entries)
            .ToArray();
    }

    private static GuildWarEntry? FindLastEntry(IReadOnlyList<GuildWarEntry> entries, string key)
    {
        for (var index = entries.Count - 1; index >= 0; index--)
        {
            if (string.Equals(entries[index].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return entries[index];
            }
        }

        return null;
    }

    private static int? ParseInt(
        GuildWarEntry? entry,
        string sourceFile,
        List<WorldDataDiagnostic> diagnostics)
    {
        if (entry is null)
        {
            return null;
        }

        if (int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        AddInvalidNumericDiagnostic(entry, sourceFile, diagnostics);
        return null;
    }

    private static long? ParseLong(
        GuildWarEntry? entry,
        string sourceFile,
        List<WorldDataDiagnostic> diagnostics)
    {
        if (entry is null)
        {
            return null;
        }

        if (long.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        AddInvalidNumericDiagnostic(entry, sourceFile, diagnostics);
        return null;
    }

    private static void AddInvalidNumericDiagnostic(
        GuildWarEntry entry,
        string sourceFile,
        List<WorldDataDiagnostic> diagnostics)
    {
        diagnostics.Add(new WorldDataDiagnostic(
            "InvalidGuildWarNumericValue",
            $"{sourceFile} line {entry.LineNumber} has invalid numeric value for [{entry.SectionName}] {entry.Key}: '{entry.Value}'."));
    }

    private static bool TryParseRankingKey(string key, string prefix, out int index)
    {
        index = 0;
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = key[prefix.Length..];
        return suffix.Length > 0
            && int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out index);
    }

    private static string BuildEntryIndexKey(string sectionName, string key)
    {
        return $"{sectionName}\u001f{key}";
    }

    private sealed class SectionBuilder
    {
        public SectionBuilder(string name, string sourceFile, int lineNumber, string rawLine)
        {
            Name = name;
            SourceFile = sourceFile;
            LineNumber = lineNumber;
            RawLine = rawLine;
        }

        public string Name { get; }

        public string SourceFile { get; }

        public int LineNumber { get; }

        public string RawLine { get; }

        public List<GuildWarEntry> Entries { get; } = [];
    }
}
