namespace WolfConquer.Core.Data;

public static class TutorParser
{
    public static TutorType ParseTypeRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = SplitCommaRow(row);
        if (fields.Count != 5)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected 5.");
        }

        return new TutorType(
            ParseInt(fields[0], "tutor type id"),
            ParseInt(fields[1], "tutor type min level"),
            ParseInt(fields[2], "tutor type max level"),
            ParseIntRange(fields, start: 3, count: 2, name: "tutor type value"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static TutorBattleLimitType ParseBattleLimitTypeRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = SplitCommaRow(row);
        if (fields.Count != 2)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected 2.");
        }

        return new TutorBattleLimitType(
            ParseInt(fields[0], "tutor battle limit type id"),
            ParseInt(fields[1], "tutor battle power limit"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static IReadOnlyList<string> SplitCommaRow(string row)
    {
        return row.Split(',').Select(field => field.Trim()).ToArray();
    }

    private static IReadOnlyList<int> ParseIntRange(IReadOnlyList<string> fields, int start, int count, string name)
    {
        var values = new int[count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ParseInt(fields[start + i], $"{name}[{i}]");
        }

        return values;
    }

    private static int ParseInt(string value, string name)
    {
        return int.TryParse(value, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }
}
