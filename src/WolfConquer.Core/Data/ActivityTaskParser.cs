using System.Globalization;

namespace WolfConquer.Core.Data;

public static class ActivityTaskParser
{
    public static ActivityTaskCatalog Parse(string content, string sourceFile = "ActivityTask.ini")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var sections = ParseSections(content);
        var metadataSection = FindSection(sections, "ActivityTask")
            ?? throw new KeyNotFoundException("ActivityTask metadata section was not found.");
        var declaredCount = GetRequiredInt(metadataSection.Entries, "Num");
        var taskSections = sections
            .Where(section => int.TryParse(section.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .ToArray();

        if (declaredCount != taskSections.Length)
        {
            throw new FormatException($"ActivityTask declares {declaredCount} tasks but has {taskSections.Length} numeric task sections.");
        }

        var metadata = new ActivityTaskMetadata(
            declaredCount,
            metadataSection.Name,
            metadataSection.SourceLine,
            metadataSection.Entries.ToArray());
        var tasks = taskSections.Select(ParseTask).ToArray();

        return new ActivityTaskCatalog(metadata, tasks, sourceFile);
    }

    private static ActivityTaskDefinition ParseTask(ActivityTaskSection section)
    {
        var openLevelRaw = GetString(section.Entries, "OpenLev");
        var closeLevelRaw = GetRequiredString(section.Entries, "CloseLev");
        var previewRaw = GetString(section.Entries, "PreView");

        return new ActivityTaskDefinition(
            ParseInt(section.Name, "activity task section id"),
            GetRequiredInt(section.Entries, "id"),
            GetRequiredString(section.Entries, "Title"),
            GetRequiredInt(section.Entries, "Type"),
            GetRequiredInt(section.Entries, "Progress"),
            GetRequiredInt(section.Entries, "ActiveValue"),
            closeLevelRaw,
            ParseInt(closeLevelRaw, "CloseLev"),
            GetRequiredString(section.Entries, "Tip"),
            openLevelRaw,
            ParseOptionalInt(openLevelRaw, "OpenLev"),
            previewRaw,
            ParseOptionalInt(previewRaw, "PreView"),
            GetString(section.Entries, "Intro"),
            GetString(section.Entries, "AniSection"),
            GetString(section.Entries, "Param"),
            section.Name,
            section.SourceLine,
            section.Entries.ToArray());
    }

    private static IReadOnlyList<ActivityTaskSection> ParseSections(string content)
    {
        var sections = new List<ActivityTaskSection>();
        ActivityTaskSection? current = null;
        var lineNumber = 0;
        foreach (var line in content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.Length > 2 && trimmed[0] == '[' && trimmed[^1] == ']')
            {
                current = new ActivityTaskSection(trimmed[1..^1].Trim(), lineNumber, []);
                if (current.Name.Length == 0)
                {
                    throw new FormatException($"ActivityTask line {lineNumber} has an empty section name.");
                }

                sections.Add(current);
                continue;
            }

            var equalsIndex = line.IndexOf('=');
            if (current is null)
            {
                throw new FormatException($"ActivityTask line {lineNumber} is not a valid key/value entry.");
            }

            if (equalsIndex <= 0)
            {
                continue;
            }

            var key = line[..equalsIndex].Trim();
            if (key.Length == 0)
            {
                throw new FormatException($"ActivityTask line {lineNumber} has an empty key.");
            }

            var value = line[(equalsIndex + 1)..].Trim();
            current.Entries.Add(new ActivityTaskRawEntry(key, value, lineNumber, line));
        }

        return sections;
    }

    private static ActivityTaskSection? FindSection(IReadOnlyList<ActivityTaskSection> sections, string name)
    {
        return sections.FirstOrDefault(section => StringComparer.OrdinalIgnoreCase.Equals(section.Name, name));
    }

    private static string? GetString(IReadOnlyList<ActivityTaskRawEntry> entries, string key)
    {
        return entries.LastOrDefault(entry => StringComparer.OrdinalIgnoreCase.Equals(entry.Key, key))?.Value;
    }

    private static string GetRequiredString(IReadOnlyList<ActivityTaskRawEntry> entries, string key)
    {
        return GetString(entries, key) ?? throw new KeyNotFoundException($"ActivityTask entry '{key}' was not found.");
    }

    private static int GetRequiredInt(IReadOnlyList<ActivityTaskRawEntry> entries, string key)
    {
        return ParseInt(GetRequiredString(entries, key), key);
    }

    private static int? ParseOptionalInt(string? value, string name)
    {
        return value is null ? null : ParseInt(value, name);
    }

    private static int ParseInt(string value, string name)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }

    private sealed record ActivityTaskSection(string Name, int SourceLine, List<ActivityTaskRawEntry> Entries);
}
