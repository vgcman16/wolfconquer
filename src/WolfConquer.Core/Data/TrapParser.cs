using System.Globalization;

namespace WolfConquer.Core.Data;

public static class TrapParser
{
    public static Trap ParseRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = row.Split(',', StringSplitOptions.None | StringSplitOptions.TrimEntries);
        if (fields.Length is not (4 or 5))
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected 4 or 5.");
        }

        return new Trap(
            ParseInt(fields[0], "trap map id"),
            ParseInt(fields[1], "trap x"),
            ParseInt(fields[2], "trap y"),
            ParseInt(fields[3], "trap type id"),
            fields.Length == 5 ? ParseInt(fields[4], "trap optional flag") : null,
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static int ParseInt(string value, string name)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid {name}: '{value}'.");
        }

        return parsed;
    }
}
