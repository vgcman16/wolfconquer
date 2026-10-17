namespace WolfConquer.Core.Data;

public static class TaskRewardParser
{
    private const int TypeIniFieldCount = 32;

    public static TaskRewardTypeEntry ParseTypeRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count != 33)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected 33.");
        }

        return new TaskRewardTypeEntry(
            ParseInt(fields[0], "task reward id"),
            ParseIntRange(fields, start: 1, count: 13, name: "task reward parameters"),
            ParseIntRange(fields, start: 14, count: 8, name: "task reward values"),
            ParseIntRange(fields, start: 22, count: 8, name: "task reward weights"),
            ParseInt(fields[30], "task reward extra0"),
            ParseInt(fields[31], "task reward extra1"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static TaskRewardTypeIniEntry ParseTypeIniRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = row.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length != TypeIniFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected {TypeIniFieldCount}.");
        }

        return new TaskRewardTypeIniEntry(
            ParseInt(fields[0], "task reward ini id"),
            ParseIntRange(fields, start: 1, count: 13, name: "task reward ini parameters"),
            ParseIntRange(fields, start: 14, count: 8, name: "task reward ini values"),
            ParseIntRange(fields, start: 22, count: 8, name: "task reward ini weights"),
            ParseInt(fields[30], "task reward ini extra0"),
            ParseInt(fields[31], "task reward ini extra1"),
            sourceFile,
            lineNumber,
            row,
            fields);
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
