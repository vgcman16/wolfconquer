using System.Globalization;

namespace WolfConquer.Core.Data;

public static class ItemAddParser
{
    private const int ExpectedFieldCount = 10;
    private const int ValueCount = 8;

    public static ItemAddBonus ParseLine(string line, int sourceLine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);

        if (sourceLine <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceLine), "Source line must be positive.");
        }

        var rawLine = line.Trim();
        var fields = rawLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length != ExpectedFieldCount)
        {
            throw new FormatException($"ItemAdd row has {fields.Length} fields; expected {ExpectedFieldCount}.");
        }

        var values = new int[ValueCount];
        for (var index = 0; index < ValueCount; index++)
        {
            values[index] = ParseInt(fields[index + 2], $"ItemAdd value {index}");
        }

        var level = ParseInt(fields[1], "ItemAdd level");
        if (level is < 1 or > 12)
        {
            throw new FormatException($"ItemAdd level must be between 1 and 12: '{level}'.");
        }

        return new ItemAddBonus(
            ParseInt(fields[0], "ItemAdd item type id"),
            level,
            values,
            sourceLine,
            rawLine);
    }

    private static int ParseInt(string value, string fieldName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid {fieldName}: '{value}'.");
        }

        return parsed;
    }
}
