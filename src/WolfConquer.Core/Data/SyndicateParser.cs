namespace WolfConquer.Core.Data;

public static class SyndicateParser
{
    public static SyndicateLevel ParseLevelRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count != 13)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected 13.");
        }

        if (fields[^1].Length != 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing @@ field.");
        }

        var parameters = new int[11];
        for (var i = 1; i < fields.Count - 1; i++)
        {
            parameters[i - 1] = ParseInt(fields[i], $"syndicate level parameter {i}");
        }

        return new SyndicateLevel(
            ParseInt(fields[0], "syndicate level"),
            parameters,
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static int ParseInt(string value, string name)
    {
        return int.TryParse(value, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }
}
