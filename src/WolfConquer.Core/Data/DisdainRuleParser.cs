using System.Globalization;

namespace WolfConquer.Core.Data;

public static class DisdainRuleParser
{
    private const int ExpectedFieldCount = 21;
    private const int NamedFieldCount = 1;

    public static DisdainRule ParseRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = row.Split(',', StringSplitOptions.None);
        if (fields.Length != ExpectedFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected {ExpectedFieldCount}.");
        }

        return new DisdainRule(
            ParseInt(fields[0], "disdain rule id"),
            ParseIntRange(fields, NamedFieldCount, ExpectedFieldCount - NamedFieldCount, "disdain rule value"),
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
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }
}
