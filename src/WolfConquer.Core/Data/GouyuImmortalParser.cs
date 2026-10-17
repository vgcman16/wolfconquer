using System.Globalization;

namespace WolfConquer.Core.Data;

public static class GouyuImmortalParser
{
    private const int ExpectedFieldCount = 17;
    private const int NamedFieldCount = 1;

    public static GouyuImmortal ParseRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count != ExpectedFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected {ExpectedFieldCount}.");
        }

        if (fields[^1].Length != 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing @@ field.");
        }

        var values = new int[ExpectedFieldCount - NamedFieldCount - 1];
        for (var index = NamedFieldCount; index < fields.Count - 1; index++)
        {
            values[index - NamedFieldCount] = ParseInt(fields[index], $"gouyu immortal value {index - NamedFieldCount}");
        }

        return new GouyuImmortal(
            ParseInt(fields[0], "gouyu immortal id"),
            values,
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static int ParseInt(string value, string name)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }
}
