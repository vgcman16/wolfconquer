using System.Globalization;
using System.Text.RegularExpressions;

namespace WolfConquer.Core.Data;

public static partial class StageGoalParser
{
    public static StageGoalCatalog Parse(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var sections = ParseSections(content);
        var metadata = FindSection(sections, "StageGoal")
            ?? throw new KeyNotFoundException("StageGoal metadata section was not found.");
        var stageAmount = GetRequiredInt(metadata.Entries, "StageAmount");
        var stages = sections
            .Where(section => int.TryParse(section.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .Select(ParseStage)
            .OrderBy(stage => stage.StageId)
            .ToArray();
        var tasks = sections
            .Select(section => (Section: section, Match: TaskSectionRegex().Match(section.Name)))
            .Where(pair => pair.Match.Success)
            .Select(pair => ParseTask(pair.Section, pair.Match))
            .OrderBy(task => task.StageId)
            .ThenBy(task => task.GoalId)
            .ToArray();

        if (stageAmount != stages.Length)
        {
            throw new FormatException($"StageGoal declares {stageAmount} stages but has {stages.Length} stage sections.");
        }

        foreach (var stage in stages)
        {
            var actual = tasks.Count(task => task.StageId == stage.StageId);
            if (stage.GoalCount != actual)
            {
                throw new FormatException($"Stage {stage.StageId} declares {stage.GoalCount} goals but has {actual} task sections.");
            }
        }

        return new StageGoalCatalog(new StageGoalInfo(stageAmount), stages, tasks);
    }

    private static StageGoalStage ParseStage(StageGoalSection section)
    {
        var openLevelRaw = GetRequiredString(section.Entries, "OpenLev");
        return new StageGoalStage(
            ParseInt(section.Name, "stage id"),
            openLevelRaw,
            TryParseInt(openLevelRaw),
            GetRequiredString(section.Entries, "StageAni"),
            GetRequiredInt(section.Entries, "GoalNum"),
            ParseRewards(section.Entries),
            section.SourceLine,
            section.Entries.ToArray());
    }

    private static StageGoalTask ParseTask(StageGoalSection section, Match match)
    {
        var requireRaw = GetString(section.Entries, "Require");
        return new StageGoalTask(
            ParseInt(match.Groups["stage"].Value, "stage id"),
            ParseInt(match.Groups["goal"].Value, "stage goal id"),
            GetRequiredString(section.Entries, "Title"),
            GetRequiredString(section.Entries, "Intro"),
            GetRequiredInt(section.Entries, "Progress"),
            ParseRewards(section.Entries),
            GetRequiredInt(section.Entries, "TaskType"),
            requireRaw,
            TryParseInt(requireRaw),
            section.SourceLine,
            section.Entries.ToArray());
    }

    private static IReadOnlyList<StageGoalReward> ParseRewards(IReadOnlyList<StageGoalRawEntry> entries)
    {
        return entries
            .Select(entry => (Entry: entry, Match: PrizeKeyRegex().Match(entry.Key)))
            .Where(pair => pair.Match.Success)
            .Select(pair =>
            {
                var fields = AtAtTableParser.SplitRow(pair.Entry.Value);
                if (fields.Count != 3)
                {
                    throw new FormatException($"{pair.Entry.Key} has {fields.Count} fields; expected 3.");
                }

                return new StageGoalReward(
                    ParseInt(pair.Match.Groups["position"].Value, "stage goal reward position"),
                    ParseInt(fields[0], "stage goal reward item type id"),
                    ParseInt(fields[1], "stage goal reward amount"),
                    ParseInt(fields[2], "stage goal reward bound flag"),
                    pair.Entry.Key,
                    pair.Entry.Value,
                    pair.Entry.SourceLine,
                    fields);
            })
            .OrderBy(reward => reward.Position)
            .ToArray();
    }

    private static IReadOnlyList<StageGoalSection> ParseSections(string content)
    {
        var sections = new List<StageGoalSection>();
        StageGoalSection? current = null;
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
                current = new StageGoalSection(trimmed[1..^1], lineNumber, []);
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
            current.Entries.Add(new StageGoalRawEntry(key, value, lineNumber, line));
        }

        return sections;
    }

    private static StageGoalSection? FindSection(IReadOnlyList<StageGoalSection> sections, string name)
    {
        return sections.FirstOrDefault(section => StringComparer.OrdinalIgnoreCase.Equals(section.Name, name));
    }

    private static string? GetString(IReadOnlyList<StageGoalRawEntry> entries, string key)
    {
        return entries.LastOrDefault(entry => StringComparer.OrdinalIgnoreCase.Equals(entry.Key, key))?.Value;
    }

    private static string GetRequiredString(IReadOnlyList<StageGoalRawEntry> entries, string key)
    {
        return GetString(entries, key) ?? throw new KeyNotFoundException($"StageGoal entry '{key}' was not found.");
    }

    private static int GetRequiredInt(IReadOnlyList<StageGoalRawEntry> entries, string key)
    {
        return ParseInt(GetRequiredString(entries, key), key);
    }

    private static int? TryParseInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static int ParseInt(string value, string name)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }

    private sealed record StageGoalSection(string Name, int SourceLine, List<StageGoalRawEntry> Entries);

    [GeneratedRegex(@"^(?<stage>\d+)-(?<goal>\d+)$")]
    private static partial Regex TaskSectionRegex();

    [GeneratedRegex(@"^Prize(?<position>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PrizeKeyRegex();
}
