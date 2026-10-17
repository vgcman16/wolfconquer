using System.Globalization;

namespace WolfConquer.Core.Data;

public static class FurnitureParser
{
    private const int ExpectedFieldCount = 9;

    public static Furniture ParseRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        if (lineNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number must be positive.");
        }

        var fields = row.Split(',', StringSplitOptions.None | StringSplitOptions.TrimEntries);
        if (fields.Length != ExpectedFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected {ExpectedFieldCount}.");
        }

        return new Furniture(
            ParseInt(fields[0], "furniture id"),
            ParseInt(fields[1], "furniture kind"),
            ParseInt(fields[2], "furniture lookface"),
            ParseInt(fields[3], "furniture map id"),
            ParseInt(fields[4], "furniture x"),
            ParseInt(fields[5], "furniture y"),
            fields[6],
            ParseInt(fields[7], "furniture item type id"),
            ParseInt(fields[8], "furniture price"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static int ParseInt(string value, string fieldName)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {fieldName}: '{value}'.");
    }
}
