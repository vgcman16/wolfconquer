namespace WolfConquer.Core.Data;

public static class ClassLevelStatParser
{
    public static IReadOnlyList<ClassLevelStat> Parse(string content, string sourceFile = "Stats.ini")
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var rows = new List<ClassLevelStat>();
        using var reader = new StringReader(content);
        string? section = null;
        var lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 ||
                trimmed.StartsWith("//", StringComparison.Ordinal) ||
                trimmed.StartsWith(';') ||
                trimmed.StartsWith('#'))
            {
                continue;
            }

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                section = trimmed[1..^1].Trim();
                continue;
            }

            if (!string.Equals(section, "Stats", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException($"{sourceFile} line {lineNumber} has a stat row outside the [Stats] section.");
            }

            rows.Add(ParseRow(sourceFile, lineNumber, line));
        }

        return rows;
    }

    public static ClassLevelStat ParseRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var trimmed = row.Trim();
        var separatorIndex = trimmed.IndexOf('=');
        if (separatorIndex <= 0 || separatorIndex == trimmed.Length - 1)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must be in ClassName[level]=str,vit,agi,spi format.");
        }

        var key = trimmed[..separatorIndex].Trim();
        var rawValue = trimmed[(separatorIndex + 1)..].Trim();
        var (className, level) = ParseKey(sourceFile, lineNumber, key);
        var fields = rawValue.Split(',').Select(field => field.Trim()).ToArray();

        if (fields.Length != 4)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} stat fields; expected 4.");
        }

        return new ClassLevelStat(
            className,
            level,
            ParseInt(fields[0], "strength"),
            ParseInt(fields[2], "agility"),
            ParseInt(fields[1], "vitality"),
            ParseInt(fields[3], "spirit"),
            sourceFile,
            lineNumber,
            key,
            rawValue,
            row,
            fields);
    }

    private static (string ClassName, int Level) ParseKey(string sourceFile, int lineNumber, string key)
    {
        var openBracket = key.IndexOf('[');
        var closeBracket = key.IndexOf(']', openBracket + 1);
        if (openBracket <= 0 || closeBracket != key.Length - 1)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has invalid class stat key '{key}'.");
        }

        var className = key[..openBracket];
        if (className.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has invalid class name '{className}'.");
        }

        var levelText = key[(openBracket + 1)..closeBracket];
        return (className, ParseInt(levelText, "class level"));
    }

    private static int ParseInt(string value, string name)
    {
        return int.TryParse(value, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }
}
