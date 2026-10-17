namespace WolfConquer.Core.Data;

public static class CollectionParser
{
    public static CollectionRewardTier ParseRewardTierRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(row);

        var fields = AtAtTableParser.SplitRow(row);
        if (fields.Count != 8)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Count} fields; expected 8.");
        }

        if (fields[^1].Length != 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} must end with a trailing @@ field.");
        }

        return new CollectionRewardTier(
            ParseInt(fields[0], "collection reward tier id"),
            ParseInt(fields[1], "collection id"),
            ParseInt(fields[2], "collection reward minimum"),
            ParseInt(fields[3], "collection reward maximum"),
            ParseInt(fields[4], "collection reward type"),
            ParseInt(fields[5], "collection reward value"),
            ParseInt(fields[6], "collection reward amount"),
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
