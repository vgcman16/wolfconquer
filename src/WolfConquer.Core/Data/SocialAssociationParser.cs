using System.Globalization;

namespace WolfConquer.Core.Data;

public static class SocialAssociationParser
{
    private const int FieldCount = 21;
    private const int ValuesCount = 14;

    public static IReadOnlyList<SocialAssociationRecord> ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return Parse(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<SocialAssociationRecord> Parse(
        string text,
        string sourceFile = "Associate.txt")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        int? expectedCount = null;
        var records = new List<SocialAssociationRecord>();
        using var reader = new StringReader(text);
        var lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (expectedCount is null)
            {
                expectedCount = ParseCountHeader(sourceFile, lineNumber, trimmed);
                continue;
            }

            records.Add(ParseRow(sourceFile, lineNumber, line));
        }

        if (expectedCount is null)
        {
            throw new FormatException($"{sourceFile} is missing Count header.");
        }

        if (records.Count != expectedCount.Value)
        {
            throw new FormatException($"{sourceFile} declares Count={expectedCount.Value} but contains {records.Count} rows.");
        }

        return records;
    }

    public static SocialAssociationRecord ParseRow(string sourceFile, int lineNumber, string row)
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
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing slash.");
        }

        var values = new int[ValuesCount];
        values[0] = ParseInt(sourceFile, lineNumber, fields[2], "association value 2");
        values[1] = ParseInt(sourceFile, lineNumber, fields[3], "association value 3");
        values[2] = ParseInt(sourceFile, lineNumber, fields[4], "association value 4");
        values[3] = ParseInt(sourceFile, lineNumber, fields[6], "association value 6");
        values[4] = ParseInt(sourceFile, lineNumber, fields[7], "association value 7");
        values[5] = ParseInt(sourceFile, lineNumber, fields[8], "association value 8");
        values[6] = ParseInt(sourceFile, lineNumber, fields[9], "association value 9");
        values[7] = ParseInt(sourceFile, lineNumber, fields[11], "association value 11");
        values[8] = ParseInt(sourceFile, lineNumber, fields[12], "association value 12");
        values[9] = ParseInt(sourceFile, lineNumber, fields[13], "association value 13");
        values[10] = ParseInt(sourceFile, lineNumber, fields[14], "association value 14");
        values[11] = ParseInt(sourceFile, lineNumber, fields[15], "association value 15");
        values[12] = ParseInt(sourceFile, lineNumber, fields[16], "association value 16");
        values[13] = ParseInt(sourceFile, lineNumber, fields[17], "association value 17");

        return new SocialAssociationRecord(
            ParseInt(sourceFile, lineNumber, fields[0], "association owner id"),
            ParseInt(sourceFile, lineNumber, fields[1], "association relation type"),
            ParseInt(sourceFile, lineNumber, fields[5], "association target id"),
            fields[10],
            values,
            fields[18],
            ParseInt(sourceFile, lineNumber, fields[19], "association tail value"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static int ParseCountHeader(string sourceFile, int lineNumber, string line)
    {
        const string prefix = "Count=";
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must start with a Count header.");
        }

        var countText = line[prefix.Length..].Trim();
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

    private static int ParseInt(string sourceFile, int lineNumber, string value, string fieldName)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"{sourceFile} line {lineNumber} has invalid {fieldName}: '{value}'.");
    }

    private static void ValidateLineNumber(int lineNumber)
    {
        if (lineNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number must be positive.");
        }
    }
}
