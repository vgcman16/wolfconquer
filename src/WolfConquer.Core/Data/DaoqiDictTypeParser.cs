using System.Globalization;

namespace WolfConquer.Core.Data;

public static class DaoqiDictTypeParser
{
    private const int ExpectedFieldCount = 16;
    private const int NamedFieldCount = 3;

    public static DaoqiDictType ParseRow(string sourceFile, int lineNumber, string row)
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

        return new DaoqiDictType(
            ParseInt(fields[0], "daoqi dict type id"),
            ParseInt(fields[1], "daoqi dict type type"),
            ParseInt(fields[2], "daoqi dict type level"),
            ParseIntRange(fields, NamedFieldCount, ExpectedFieldCount - NamedFieldCount - 1, "daoqi dict type value"),
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
