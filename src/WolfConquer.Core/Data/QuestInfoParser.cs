using System.Globalization;
using System.Text.RegularExpressions;

namespace WolfConquer.Core.Data;

public static partial class QuestInfoParser
{
    public static QuestInfoCatalog Parse(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var sections = ParseSections(content);
        var metadata = new QuestInfoMetadata(
            GetSectionInt(sections, "ActionBase", "ActionBase"),
            GetSectionInt(sections, "Option", "RecommendInterval"),
            GetSectionInt(sections, "TotalMission", "TotalMission"));

        var taskTypes = ParseTaskTypes(sections);
        var missions = sections
            .Where(section => int.TryParse(section.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .Select(ParseMission)
            .OrderBy(mission => mission.SectionId)
            .ToArray();

        return new QuestInfoCatalog(metadata, taskTypes, missions);
    }

    private static IReadOnlyList<QuestSection> ParseSections(string content)
    {
        var sections = new List<QuestSection>();
        QuestSection? current = null;
        var lineNumber = 0;
        foreach (var line in content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.Length > 2 && trimmed[0] == '[' && trimmed[^1] == ']')
            {
                current = new QuestSection(trimmed[1..^1], lineNumber, []);
                sections.Add(current);
                continue;
            }

            var equalsIndex = line.IndexOf('=');
            if (current is null || equalsIndex <= 0)
            {
                continue;
            }

            var key = line[..equalsIndex].Trim();
            var value = line[(equalsIndex + 1)..].Trim();
            current.Entries.Add(new QuestRawEntry(key, value, lineNumber, line));
        }

        return sections;
    }

    private static IReadOnlyList<QuestTaskType> ParseTaskTypes(IReadOnlyList<QuestSection> sections)
    {
        var section = FindSection(sections, "TaskType");
        if (section is null)
        {
            return [];
        }

        var declaredCount = GetInt(section.Entries, "Num") ?? 0;
        var color = GetString(section.Entries, "TaskNameColor");
        var result = new List<QuestTaskType>();
        for (var position = 1; position <= declaredCount; position++)
        {
            var idKey = "TypeId" + position.ToString(CultureInfo.InvariantCulture);
            var nameKey = "TypeName" + position.ToString(CultureInfo.InvariantCulture);
            var idEntry = FindEntry(section.Entries, idKey);
            var nameEntry = FindEntry(section.Entries, nameKey);
            if (idEntry is null || nameEntry is null)
            {
                continue;
            }

            result.Add(new QuestTaskType(
                ParseInt(idEntry.Value, "quest task type id"),
                nameEntry.Value,
                color,
                position,
                idEntry.SourceLine,
                idEntry.Key,
                nameEntry.Key));
        }

        return result;
    }

    private static QuestMission ParseMission(QuestSection section)
    {
        var sectionId = ParseInt(section.Name, "quest mission section id");
        var prereqRaw = GetString(section.Entries, "Prequest");
        var prize = GetString(section.Entries, "Prize");

        return new QuestMission(
            sectionId,
            section.SourceLine,
            GetRequiredInt(section.Entries, "TypeId"),
            GetRequiredInt(section.Entries, "MissionId"),
            GetString(section.Entries, "Name"),
            GetInt(section.Entries, "Lv_min"),
            GetInt(section.Entries, "Lv_max"),
            GetInt(section.Entries, "Auto"),
            GetInt(section.Entries, "First"),
            prereqRaw,
            ParsePrerequisites(prereqRaw),
            GetInt(section.Entries, "Map"),
            GetString(section.Entries, "Profession"),
            GetInt(section.Entries, "Sex"),
            GetInt(section.Entries, "FinishTime"),
            GetString(section.Entries, "ActivityBeginTime"),
            GetString(section.Entries, "ActivityEndTime"),
            GetInt(section.Entries, "CompleteFlag"),
            prize,
            GetString(section.Entries, "IntentionDesp"),
            GetInt(section.Entries, "IntentAmount"),
            GetString(section.Entries, "Content"),
            ParseNpcAnchors(section.Entries, "BeginNpcId", "Begin"),
            ParseNpcAnchors(section.Entries, "FinishNpcId", "Finish"),
            ParseIntentions(section.Entries),
            ParsePrizeItems(prize),
            section.Entries.ToArray());
    }

    private static IReadOnlyList<int> ParsePrerequisites(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "0")
        {
            return [];
        }

        return raw
            .Split(['|', ',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => ParseInt(value, "quest prerequisite mission id"))
            .Where(value => value > 0)
            .ToArray();
    }

    private static IReadOnlyList<QuestNpcAnchor> ParseNpcAnchors(
        IReadOnlyList<QuestRawEntry> entries,
        string prefix,
        string kind)
    {
        return entries
            .Where(entry => TryGetVariant(entry.Key, prefix, out _))
            .Select(entry =>
            {
                TryGetVariant(entry.Key, prefix, out var variant);
                var fields = entry.Value.Split(',', 6, StringSplitOptions.None | StringSplitOptions.TrimEntries);
                return new QuestNpcAnchor(
                    kind,
                    entry.Key,
                    variant,
                    TryParseInt(fields, 0),
                    TryParseInt(fields, 1),
                    TryParseInt(fields, 2),
                    TryParseInt(fields, 3),
                    fields.Length > 4 && fields[4].Length > 0 ? fields[4] : null,
                    fields.Length > 5 && fields[5].Length > 0 ? fields[5] : null,
                    entry.Value,
                    entry.SourceLine,
                    fields);
            })
            .ToArray();
    }

    private static IReadOnlyList<QuestIntention> ParseIntentions(IReadOnlyList<QuestRawEntry> entries)
    {
        return entries
            .Select(entry => (Entry: entry, Match: IntentionKeyRegex().Match(entry.Key)))
            .Where(pair => pair.Match.Success)
            .Select(pair => new QuestIntention(
                ParseInt(pair.Match.Groups["position"].Value, "quest intention position"),
                pair.Match.Groups["variant"].Success
                    ? ParseInt(pair.Match.Groups["variant"].Value, "quest intention variant")
                    : null,
                pair.Entry.Key,
                pair.Entry.Value,
                pair.Entry.SourceLine))
            .ToArray();
    }

    private static IReadOnlyList<QuestPrizeItemRef> ParsePrizeItems(string? prize)
    {
        if (string.IsNullOrWhiteSpace(prize))
        {
            return [];
        }

        return PrizeItemRegex()
            .Matches(prize)
            .Select((match, index) => new QuestPrizeItemRef(
                ParseInt(match.Groups["itemTypeId"].Value, "quest prize item id"),
                match.Groups["arguments"].Value,
                index))
            .ToArray();
    }

    private static bool TryGetVariant(string key, string prefix, out int? variant)
    {
        variant = null;
        if (StringComparer.OrdinalIgnoreCase.Equals(key, prefix))
        {
            return true;
        }

        var variantPrefix = prefix + "_";
        if (!key.StartsWith(variantPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var raw = key[variantPrefix.Length..];
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        variant = parsed;
        return true;
    }

    private static QuestSection? FindSection(IReadOnlyList<QuestSection> sections, string name)
    {
        return sections.FirstOrDefault(section => StringComparer.OrdinalIgnoreCase.Equals(section.Name, name));
    }

    private static int? GetSectionInt(IReadOnlyList<QuestSection> sections, string sectionName, string key)
    {
        var section = FindSection(sections, sectionName);
        return section is null ? null : GetInt(section.Entries, key);
    }

    private static string? GetString(IReadOnlyList<QuestRawEntry> entries, string key)
    {
        return FindEntry(entries, key)?.Value;
    }

    private static int GetRequiredInt(IReadOnlyList<QuestRawEntry> entries, string key)
    {
        var entry = FindEntry(entries, key) ?? throw new FormatException($"Missing quest field: {key}.");
        return ParseInt(entry.Value, key);
    }

    private static int? GetInt(IReadOnlyList<QuestRawEntry> entries, string key)
    {
        var value = GetString(entries, key);
        return string.IsNullOrWhiteSpace(value) ? null : ParseInt(value, key);
    }

    private static QuestRawEntry? FindEntry(IReadOnlyList<QuestRawEntry> entries, string key)
    {
        return entries.FirstOrDefault(entry => StringComparer.OrdinalIgnoreCase.Equals(entry.Key, key));
    }

    private static int? TryParseInt(IReadOnlyList<string> fields, int index)
    {
        return fields.Count > index && int.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static int ParseInt(string value, string fieldName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid {fieldName}: '{value}'.");
        }

        return parsed;
    }

    [GeneratedRegex(@"^Intention(?<position>\d+)(?:_(?<variant>\d+))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IntentionKeyRegex();

    [GeneratedRegex(@"\[item\s+(?<itemTypeId>\d+)(?<arguments>[^\]]*)\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PrizeItemRegex();

    private sealed record QuestSection(string Name, int SourceLine, List<QuestRawEntry> Entries);
}
