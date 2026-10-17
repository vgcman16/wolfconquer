using System.Globalization;

namespace WolfConquer.Core.Data;

public static class ChristmasNpcParser
{
    private const int ExpectedFieldCount = 11;

    public static ChristmasNpc ParseRow(string sourceFile, int lineNumber, string row)
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

        return new ChristmasNpc(
            ParseInt(fields[0], "christmas npc id"),
            string.IsNullOrEmpty(fields[1]) ? null : fields[1],
            ParseInt(fields[2], "christmas npc kind"),
            ParseInt(fields[3], "christmas npc lookface"),
            ParseInt(fields[4], "christmas npc map id"),
            ParseInt(fields[5], "christmas npc x"),
            ParseInt(fields[6], "christmas npc y"),
            fields.Skip(7).Select((field, index) => ParseInt(field, $"christmas npc parameter {index}")).ToArray(),
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
