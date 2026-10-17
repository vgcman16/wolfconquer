using System.Globalization;

namespace WolfConquer.Core.Data;

public static class LevelExperienceParser
{
    private const int ExpectedFieldCount = 5;

    public static LevelExperience ParseLine(string line, int sourceLine)
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
            throw new FormatException($"Level experience row has {fields.Length} fields; expected {ExpectedFieldCount}.");
        }

        return new LevelExperience(
            ParseInt(fields[0], "level experience type"),
            ParseInt(fields[1], "level experience level"),
            ParseLong(fields[2], "level experience value"),
            ParseInt(fields[3], "level experience up level time"),
            ParseInt(fields[4], "level experience mentor up level time"),
            sourceLine,
            rawLine,
            fields);
    }

    private static int ParseInt(string value, string fieldName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid {fieldName}: '{value}'.");
        }

        return parsed;
    }

    private static long ParseLong(string value, string fieldName)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid {fieldName}: '{value}'.");
        }

        return parsed;
    }
}
