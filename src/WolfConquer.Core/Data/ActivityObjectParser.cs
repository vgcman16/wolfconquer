using System.Globalization;

namespace WolfConquer.Core.Data;

public static class ActivityObjectParser
{
    private const int PokerTableFieldCount = 9;
    private const int RouletteTableFieldCount = 7;
    private const int NpcTimeFieldCount = 11;

    public static PokerTable ParsePokerTableRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);
        ThrowIfInvalidLineNumber(lineNumber);

        var fields = row.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length != PokerTableFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected {PokerTableFieldCount}.");
        }

        return new PokerTable(
            ParseInt(fields[0], "poker table id"),
            ParseIntRange(fields, 1, PokerTableFieldCount - 1, "poker table value"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static RouletteTable ParseRouletteTableRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);
        ThrowIfInvalidLineNumber(lineNumber);

        var fields = SplitCommaRow(row);
        if (fields.Length != RouletteTableFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected {RouletteTableFieldCount}.");
        }

        return new RouletteTable(
            ParseInt(fields[0], "roulette table id"),
            ParseIntRange(fields, 1, RouletteTableFieldCount - 1, "roulette table value"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static NpcTimeEntry ParseNpcTimeRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);
        ThrowIfInvalidLineNumber(lineNumber);

        var fields = SplitCommaRow(row);
        if (fields.Length != NpcTimeFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected {NpcTimeFieldCount}.");
        }

        return new NpcTimeEntry(
            ParseInt(fields[0], "npc time id"),
            fields[1],
            ParseIntRange(fields, 2, NpcTimeFieldCount - 2, "npc time value"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static string[] SplitCommaRow(string row)
    {
        return row.Split(',', StringSplitOptions.None);
    }

    private static IReadOnlyList<int> ParseIntRange(IReadOnlyList<string> fields, int start, int count, string name)
    {
        var values = new int[count];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = ParseInt(fields[start + index], $"{name}[{index}]");
        }

        return values;
    }

    private static int ParseInt(string value, string fieldName)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {fieldName}: '{value}'.");
    }

    private static void ThrowIfInvalidLineNumber(int lineNumber)
    {
        if (lineNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number must be positive.");
        }
    }
}
