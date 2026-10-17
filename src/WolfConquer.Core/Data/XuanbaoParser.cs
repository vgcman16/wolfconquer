namespace WolfConquer.Core.Data;

public static class XuanbaoParser
{
    public static XuanbaoAdditionAttribute ParseAdditionAttributeRow(string sourceFile, int lineNumber, string row)
    {
        var fields = ParseAtAtRow(sourceFile, lineNumber, row, expectedFieldCount: 6);

        return new XuanbaoAdditionAttribute(
            ParseInt(fields[0], "xuanbao addition attribute id"),
            ParseInt(fields[1], "xuanbao addition attribute type"),
            ParseInt(fields[2], "xuanbao addition level"),
            ParseIntRange(fields, start: 3, count: 2, name: "xuanbao addition value"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static XuanbaoComposeAttributeLimit ParseComposeAttributeLimitRow(string sourceFile, int lineNumber, string row)
    {
        var fields = ParseAtAtRow(sourceFile, lineNumber, row, expectedFieldCount: 5);

        return new XuanbaoComposeAttributeLimit(
            ParseInt(fields[0], "xuanbao compose attribute limit id"),
            ParseIntRange(fields, start: 1, count: 3, name: "xuanbao compose attribute limit value"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static XuanbaoRandomAttribute ParseRandomAttributeRow(string sourceFile, int lineNumber, string row)
    {
        var fields = ParseAtAtRow(sourceFile, lineNumber, row, expectedFieldCount: 5);

        return new XuanbaoRandomAttribute(
            ParseInt(fields[0], "xuanbao item type id"),
            ParseInt(fields[1], "xuanbao random attribute type"),
            ParseInt(fields[2], "xuanbao random attribute minimum"),
            ParseInt(fields[3], "xuanbao random attribute maximum"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static IReadOnlyList<string> ParseAtAtRow(
        string sourceFile,
        int lineNumber,
        string row,
        int expectedFieldCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count != expectedFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected {expectedFieldCount}.");
        }

        if (fields[^1].Length != 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing @@ field.");
        }

        return fields;
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
