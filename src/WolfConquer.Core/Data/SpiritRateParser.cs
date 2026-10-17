using System.Globalization;

namespace WolfConquer.Core.Data;

public static class SpiritRateParser
{
    private const int ExpectedFieldCount = 9;
    private const int NamedFieldCount = 3;

    public static IReadOnlyList<SpiritRate> ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return Parse(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<SpiritRate> Parse(string text, string sourceFile = "spirit_rate.txt")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var entries = new List<SpiritRate>();
        using var reader = new StringReader(text);
        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (line.Trim().Length == 0)
            {
                continue;
            }

            entries.Add(ParseRow(sourceFile, lineNumber, line));
        }

        return entries;
    }

    public static SpiritRate ParseRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count != ExpectedFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected {ExpectedFieldCount}.");
        }

        if (fields[^1].Length != 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing @@ field.");
        }

        var parameters = new int[ExpectedFieldCount - NamedFieldCount - 1];
        for (var index = NamedFieldCount; index < fields.Count - 1; index++)
        {
            parameters[index - NamedFieldCount] = ParseInt(fields[index], $"spirit rate parameter {index - NamedFieldCount}");
        }

        return new SpiritRate(
            ParseInt(fields[0], "spirit rate id"),
            ParseInt(fields[1], "spirit rate type"),
            ParseInt(fields[2], "spirit rate item type id"),
            parameters,
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static int ParseInt(string value, string name)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }
}
