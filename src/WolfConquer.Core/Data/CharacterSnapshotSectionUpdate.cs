namespace WolfConquer.Core.Data;

internal readonly record struct CharacterSnapshotSectionUpdate(
    string SectionName,
    IReadOnlyDictionary<string, string> Values,
    bool CreateIfMissing,
    string MissingReason);

internal static class CharacterSnapshotSectionLineTransformer
{
    internal static bool TryApply(
        string[] lines,
        string sectionName,
        IReadOnlyDictionary<string, string> values,
        bool createIfMissing,
        out string[] updatedLines)
    {
        updatedLines = lines;
        var inTargetSection = false;
        var foundTargetSection = false;
        var insertIndex = lines.Length;
        var normalizedValues = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
        var remaining = new Dictionary<string, string>(normalizedValues, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < lines.Length; index++)
        {
            var rawLine = lines[index];
            var trimmed = rawLine.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                if (inTargetSection)
                {
                    insertIndex = index;
                }

                var currentSectionName = trimmed[1..^1].Trim();
                inTargetSection = string.Equals(
                    currentSectionName,
                    sectionName,
                    StringComparison.OrdinalIgnoreCase);
                foundTargetSection |= inTargetSection;
                if (inTargetSection)
                {
                    insertIndex = index + 1;
                }

                continue;
            }

            if (!inTargetSection)
            {
                continue;
            }

            insertIndex = index + 1;
            if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
            {
                continue;
            }

            var equalsIndex = rawLine.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            var key = rawLine[..equalsIndex].Trim();
            if (!normalizedValues.TryGetValue(key, out var value))
            {
                continue;
            }

            remaining.Remove(key);
            lines[index] = rawLine[..(equalsIndex + 1)] + value;
        }

        if (!foundTargetSection)
        {
            if (!createIfMissing)
            {
                return false;
            }

            var appended = new List<string>(lines.Length + remaining.Count + 2);
            appended.AddRange(lines);
            if (appended.Count > 0 && appended[^1].Length != 0)
            {
                appended.Add(string.Empty);
            }

            appended.Add("[" + sectionName + "]");
            appended.AddRange(remaining.Select(pair => pair.Key + "=" + pair.Value));
            updatedLines = appended.ToArray();
            return true;
        }

        if (remaining.Count == 0)
        {
            return true;
        }

        updatedLines = lines
            .Take(insertIndex)
            .Concat(remaining.Select(pair => pair.Key + "=" + pair.Value))
            .Concat(lines.Skip(insertIndex))
            .ToArray();
        return true;
    }
}
