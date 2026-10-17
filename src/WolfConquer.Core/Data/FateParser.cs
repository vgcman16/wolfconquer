using System.Globalization;

namespace WolfConquer.Core.Data;

public static class FateParser
{
    private const int ExpectedFieldCount = 16;
    private const int NamedFieldCount = 4;

    public static FateExperience ParseRow(string sourceFile, int lineNumber, string row)
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

        return new FateExperience(
            ParseInt(fields[0], "fate id"),
            ParseInt(fields[1], "fate type"),
            ParseInt(fields[2], "fate level"),
            ParseLong(fields[3], "fate experience"),
            ParseLongRange(fields, NamedFieldCount, ExpectedFieldCount - NamedFieldCount - 1, "fate value"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static IReadOnlyList<long> ParseLongRange(IReadOnlyList<string> fields, int start, int count, string name)
    {
        var values = new long[count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ParseLong(fields[start + i], $"{name}[{i}]");
        }

        return values;
    }

    private static int ParseInt(string value, string name)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }

    private static long ParseLong(string value, string name)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }
}
