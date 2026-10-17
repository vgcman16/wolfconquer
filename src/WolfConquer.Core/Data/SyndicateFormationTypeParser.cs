using System.Globalization;

namespace WolfConquer.Core.Data;

public static class SyndicateFormationTypeParser
{
    private const int ExpectedFieldCount = 9;
    private const int NamedFieldCount = 3;

    public static SyndicateFormationType ParseRow(string sourceFile, int lineNumber, string row)
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

        var values = new long[ExpectedFieldCount - NamedFieldCount - 1];
        for (var index = NamedFieldCount; index < fields.Count - 1; index++)
        {
            values[index - NamedFieldCount] = ParseLong(fields[index], $"syndicate formation type value {index - NamedFieldCount}");
        }

        return new SyndicateFormationType(
            ParseInt(fields[0], "syndicate formation type id"),
            ParseInt(fields[1], "syndicate formation type type"),
            ParseInt(fields[2], "syndicate formation type level"),
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

    private static long ParseLong(string value, string name)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }
}
