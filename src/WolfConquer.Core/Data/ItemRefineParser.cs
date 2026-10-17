namespace WolfConquer.Core.Data;

public static class ItemRefineParser
{
    public static ItemRefineEffect ParseEffectRow(string sourceFile, int lineNumber, string row)
    {
        var fields = ParseAtAtRow(sourceFile, lineNumber, row, expectedFieldCount: 22);

        return new ItemRefineEffect(
            ParseInt(fields[0], "item refine effect id"),
            ParseIntRange(fields, start: 1, count: 20, name: "item refine effect value"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static ItemRefineEffectEx ParseEffectExRow(string sourceFile, int lineNumber, string row)
    {
        var fields = ParseAtAtRow(sourceFile, lineNumber, row, expectedFieldCount: 6);

        return new ItemRefineEffectEx(
            ParseInt(fields[0], "item refine effect ex id"),
            ParseInt(fields[1], "item refine effect ex item type id"),
            ParseIntRange(fields, start: 2, count: 3, name: "item refine effect ex value"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static ItemRefineUpgrade ParseUpgradeRow(string sourceFile, int lineNumber, string row)
    {
        var fields = ParseAtAtRow(sourceFile, lineNumber, row, expectedFieldCount: 4);

        return new ItemRefineUpgrade(
            ParseInt(fields[0], "item refine upgrade id"),
            ParseInt(fields[1], "item refine upgrade level"),
            ParseInt(fields[2], "item refine upgrade value"),
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
