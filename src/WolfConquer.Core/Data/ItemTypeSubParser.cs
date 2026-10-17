using System.Globalization;

namespace WolfConquer.Core.Data;

public static class ItemTypeSubParser
{
    private const string DefaultSourceFile = "ItemtypeSub.txt";

    public static IReadOnlyList<ItemTypeSub> ParseFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return Parse(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<ItemTypeSub> Parse(string text, string sourceFile = DefaultSourceFile)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var entries = new List<ItemTypeSub>();

        using var reader = new StringReader(text);
        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            entries.Add(ParseRow(sourceFile, lineNumber, line));
        }

        return entries.ToArray();
    }

    public static ItemTypeSub ParseRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        if (lineNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number must be positive.");
        }

        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count == 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} is empty.");
        }

        return new ItemTypeSub(
            ParseInt(fields[0], "item type sub item type id"),
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
