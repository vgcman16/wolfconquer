using System.Globalization;

namespace WolfConquer.Core.Data;

public static class MeltingTypeParser
{
    private const int MinimumFieldCount = 4;
    private const int NamedFieldCount = 2;

    public static IReadOnlyList<MeltingTypeRule> ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return Parse(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<MeltingTypeRule> Parse(string text, string sourceFile = "MeltingType.ini")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var rows = new List<MeltingTypeRule>();
        using var reader = new StringReader(text);
        var lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            rows.Add(ParseRow(sourceFile, lineNumber, line));
        }

        return rows;
    }

    public static MeltingTypeRule ParseRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);
        ThrowIfInvalidLineNumber(lineNumber);

        var fields = row.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length < MinimumFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected at least {MinimumFieldCount}.");
        }

        return new MeltingTypeRule(
            ParseInt(sourceFile, lineNumber, fields[0], "melting type rule type"),
            ParseInt(sourceFile, lineNumber, fields[1], "melting type item type id"),
            ParseIntRange(sourceFile, lineNumber, fields, NamedFieldCount),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static IReadOnlyList<int> ParseIntRange(
        string sourceFile,
        int lineNumber,
        IReadOnlyList<string> fields,
        int start)
    {
        var values = new int[fields.Count - start];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = ParseInt(sourceFile, lineNumber, fields[start + index], $"melting type parameter {index}");
        }

        return values;
    }

    private static int ParseInt(string sourceFile, int lineNumber, string value, string fieldName)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"{sourceFile} line {lineNumber} has invalid {fieldName}: '{value}'.");
    }

    private static void ThrowIfInvalidLineNumber(int lineNumber)
    {
        if (lineNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number must be positive.");
        }
    }
}
