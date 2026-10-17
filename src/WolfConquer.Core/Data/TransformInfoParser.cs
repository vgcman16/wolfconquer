using System.Globalization;

namespace WolfConquer.Core.Data;

public static class TransformInfoParser
{
    private const int ExpectedFieldCount = 5;

    public static TransformInfo ParseLine(string sourceFile, int lineNumber, string line)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(line);

        if (lineNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number must be positive.");
        }

        var rawLine = line.Trim();
        var fields = rawLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length != ExpectedFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected {ExpectedFieldCount}.");
        }

        return new TransformInfo(
            ParseInt(fields[0], "transform id"),
            ParseInt(fields[1], "transform level"),
            fields[2],
            ParseInt(fields[3], "transform lookface"),
            ParseInt(fields[4], "transform max life"),
            sourceFile,
            lineNumber,
            rawLine,
            fields);
    }

    private static int ParseInt(string value, string fieldName)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {fieldName}: '{value}'.");
    }
}
