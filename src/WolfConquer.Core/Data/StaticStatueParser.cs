using System.Globalization;

namespace WolfConquer.Core.Data;

public static class StaticStatueParser
{
    private const int FieldCount = 306;

    public static IReadOnlyList<StaticStatueRecord> ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return Parse(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<StaticStatueRecord> Parse(
        string text,
        string sourceFile = "StaticStatue.txt")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var rows = new List<StaticStatueRecord>();
        int? expectedCount = null;
        using var reader = new StringReader(text);
        var lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.Trim().Length == 0)
            {
                continue;
            }

            if (expectedCount is null)
            {
                expectedCount = ParseCountHeader(sourceFile, lineNumber, line);
                continue;
            }

            rows.Add(ParseRow(sourceFile, lineNumber, line));
        }

        if (expectedCount is null)
        {
            throw new FormatException($"{sourceFile} is missing Count header.");
        }

        if (rows.Count != expectedCount.Value)
        {
            throw new FormatException($"{sourceFile} declares Count={expectedCount.Value} but contains {rows.Count} rows.");
        }

        return rows;
    }

    public static StaticStatueRecord ParseRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentNullException.ThrowIfNull(row);
        ValidateLineNumber(lineNumber);

        var fields = row.Split('/', StringSplitOptions.None);
        if (fields.Length != FieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected {FieldCount}.");
        }

        if (fields[^1].Length != 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing / field.");
        }

        var values = new int[FieldCount - 1];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = ParseInt(fields[index], $"static statue field {index + 1}");
        }

        return new StaticStatueRecord(
            values[0],
            values,
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static int ParseCountHeader(string sourceFile, int lineNumber, string line)
    {
        const string prefix = "Count=";
        var trimmed = line.Trim();
        if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must start with a Count header.");
        }

        var countText = trimmed[prefix.Length..].Trim();
        if (!int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has invalid Count value '{countText}'.");
        }

        if (count < 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has negative Count value.");
        }

        return count;
    }

    private static int ParseInt(string value, string name)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }

    private static void ValidateLineNumber(int lineNumber)
    {
        if (lineNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number must be positive.");
        }
    }
}
