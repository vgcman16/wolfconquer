using System.Globalization;

namespace WolfConquer.Core.Data;

public static class JiangHuParser
{
    private const int AttributeFieldCount = 4;

    public static IReadOnlyList<JiangHuAttribute> ParseAttributesFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ParseAttributes(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<JiangHuAttribute> ParseAttributes(
        string text,
        string sourceFile = "JianghuAttributes.txt")
    {
        return ParseCountedRows(text, sourceFile, ParseAttributeRow);
    }

    public static JiangHuAttribute ParseAttributeRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);
        ValidateLineNumber(lineNumber);

        var fields = SplitWhitespace(row);
        if (fields.Count != AttributeFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected {AttributeFieldCount}.");
        }

        return new JiangHuAttribute(
            ParseInt(fields[0], "Jiang Hu attribute id"),
            ParseInt(fields[1], "Jiang Hu attribute type"),
            ParseInt(fields[2], "Jiang Hu attribute level"),
            ParseInt(fields[3], "Jiang Hu attribute value"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static IReadOnlyList<JiangHuCultivateStatus> ParseCultivateStatusesFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ParseCultivateStatuses(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<JiangHuCultivateStatus> ParseCultivateStatuses(
        string text,
        string sourceFile = "JingHuCultivateStatus.txt")
    {
        return ParseCountedRows(text, sourceFile, ParseCultivateStatusRow);
    }

    public static JiangHuCultivateStatus ParseCultivateStatusRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);
        ValidateLineNumber(lineNumber);

        var fields = SplitWhitespace(row);
        if (fields.Count < 2)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected id and declared value count.");
        }

        var declaredValueCount = ParseInt(fields[1], "Jiang Hu cultivate status value count");
        if (declaredValueCount < 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has negative Jiang Hu cultivate status value count.");
        }

        var expectedFieldCount = 2 + declaredValueCount;
        if (fields.Count != expectedFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count - 2} values; expected {declaredValueCount}.");
        }

        var values = new int[declaredValueCount];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = ParseInt(fields[index + 2], $"Jiang Hu cultivate status value {index}");
        }

        return new JiangHuCultivateStatus(
            ParseInt(fields[0], "Jiang Hu cultivate status id"),
            declaredValueCount,
            values,
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static IReadOnlyList<T> ParseCountedRows<T>(
        string text,
        string sourceFile,
        Func<string, int, string, T> parseRow)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var rows = new List<T>();
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

            rows.Add(parseRow(sourceFile, lineNumber, line));
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

    private static int ParseCountHeader(string sourceFile, int lineNumber, string line)
    {
        const string prefix = "Count=";
        var trimmed = line.Trim();
        if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must start with a Count header.");
        }

        var countText = trimmed[prefix.Length..].Trim();
        if (!int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has invalid Count value '{countText}'.");
        }

        if (count < 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has negative Count value.");
        }

        return count;
    }

    private static IReadOnlyList<string> SplitWhitespace(string row)
    {
        var fields = new List<string>();
        var index = 0;
        while (index < row.Length)
        {
            while (index < row.Length && char.IsWhiteSpace(row[index]))
            {
                index++;
            }

            var start = index;
            while (index < row.Length && !char.IsWhiteSpace(row[index]))
            {
                index++;
            }

            if (start < index)
            {
                fields.Add(row[start..index]);
            }
        }

        return fields;
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
